using Core.Character;
using Core.Content;
using Core.Rng;
using Core.State;
using Core.World;

namespace Core.Sim;

/// <summary>
/// Entry point for the UI, tests and Sim.Cli: owns the state and its live RNG streams,
/// validates commands and dispatches them to systems.
/// </summary>
public sealed class Simulation
{
    public const string QiVectorId = "qi";

    private readonly SimContext _ctx;

    private Simulation(ContentDb content, GameState state, RngStreams rng)
    {
        State = state;
        _ctx = new SimContext(content, rng);
    }

    public ContentDb Content => _ctx.Content;

    public GameState State { get; }

    public CharacterRules Rules => _ctx.Rules;

    public WorldMap Map => _ctx.Map;

    public PlayerView View => PlayerView.From(State, _ctx);

    public static Simulation NewGame(ContentDb content, ulong seed)
    {
        var config = content.Get<RunConfigDef>(RunConfigDef.RunId);
        var rng = RngStreams.FromSeed(seed);
        var state = new GameState
        {
            Seed = seed,
            RngStates = rng.GetStates(),
            Player = new PlayerState
            {
                AgeWeeks = config.StartAgeYears * config.WeeksPerYear,
                LifespanWeeks = config.StartLifespanYears * config.WeeksPerYear,
                ZoneId = config.StartZone,
                Tracks = [new CultivationTrack { VectorId = QiVectorId }],
            },
        };
        var sim = new Simulation(content, state, rng);
        sim.Rules.InitPlayer(state.Player);
        EconomySystem.InitPlayer(state.Player, sim._ctx.Economy);
        WorldSystem.InitWorld(state.World, content);
        return sim;
    }

    /// <summary>Resumes from a loaded state; RNG streams continue exactly where they were saved.</summary>
    public static Simulation Resume(ContentDb content, GameState state) =>
        new(content, state, RngStreams.FromStates(state.RngStates));

    public CommandOutcome Execute(Command command)
    {
        if (command.Mode != State.Mode)
            return CommandOutcome.Reject(RejectReasons.WrongMode);

        var outcome = command switch
        {
            StartActivity c => ExecuteStart(c),
            Travel c => ExecuteTravel(c),
            AttemptBreakthrough c => ExecuteBreakthrough(c),
            SetActiveMethod c => CultivationSystem.SetActiveMethod(State.Player, _ctx, c.MethodId),
            ContinueActivity => ExecuteContinue(),
            CancelActivity => ExecuteCancel(),
            SetInterruptStops c => ExecuteSetInterruptStops(c),
            ChooseEventOption c => EventSystem.Choose(State, _ctx, c.Index),
            UsePill c => ItemSystem.UsePill(State.Player, _ctx, c.ItemId),
            Buy c => EconomySystem.Buy(State.Player, _ctx, c.ItemId),
            RestoreQiWithStones c => EconomySystem.RestoreQi(State.Player, _ctx, c.Stones),
            RefillPlaceWithStones c => EconomySystem.RefillPlace(State, _ctx, c.PlaceId, c.Stones),
            _ => throw new NotSupportedException($"No handler for {command.GetType().Name}."),
        };

        State.RngStates = _ctx.Rng.GetStates();
        return outcome;
    }

    private CommandOutcome ExecuteStart(StartActivity command)
    {
        if (ActivitySystem.Validate(State, _ctx, command.Activity) is { } reason)
            return CommandOutcome.Reject(reason);
        if (command.Weeks <= 0)
            return CommandOutcome.Reject(RejectReasons.InvalidWeeks);

        return Start(command.Activity, command.Weeks);
    }

    private CommandOutcome ExecuteTravel(Travel command)
    {
        if (!Content.TryGet<ZoneDef>(command.ZoneId, out _) || !WorldSystem.IsKnown(State.World, Content, command.ZoneId))
            return CommandOutcome.Reject(RejectReasons.UnknownZone);
        if (command.ZoneId == State.Player.ZoneId)
            return CommandOutcome.Reject(RejectReasons.AlreadyThere);
        if (Map.FindRoute(State.Player.ZoneId, command.ZoneId, zone => WorldSystem.IsKnown(State.World, Content, zone)) is not { } legs)
            return CommandOutcome.Reject(RejectReasons.NoRoute);

        var travel = new TravelActivity(legs);
        return Start(travel, travel.TotalWeeks);
    }

    private CommandOutcome ExecuteBreakthrough(AttemptBreakthrough command)
    {
        var attempt = new BreakthroughActivity(command.VectorId, command.PlaceId, command.AcceptDeathRisk);
        if (BreakthroughSystem.Validate(State, _ctx, attempt) is { } reason)
            return CommandOutcome.Reject(reason);

        return Start(attempt, BreakthroughSystem.Weeks(State.Player, Content, command.VectorId));
    }

    private CommandOutcome ExecuteContinue() =>
        State.Activity is null ? CommandOutcome.Reject(RejectReasons.NoActivity) : RunActivity();

    private CommandOutcome ExecuteCancel()
    {
        if (State.Activity is not { } active)
            return CommandOutcome.Reject(RejectReasons.NoActivity);

        State.Activity = null;
        return CommandOutcome.Accept([new ActivityCancelled(active.Activity)]);
    }

    private CommandOutcome ExecuteSetInterruptStops(SetInterruptStops command)
    {
        State.StoppingInterrupts = command.Categories.Distinct().Order().ToList();
        return CommandOutcome.Accept([]);
    }

    private CommandOutcome Start(Activity activity, int? weeks)
    {
        State.Activity = new ActiveActivity { Activity = activity, WeeksLeft = weeks };
        return RunActivity();
    }

    private CommandOutcome RunActivity()
    {
        var events = new List<SimEvent>();
        TimeSystem.RunActivity(State, _ctx, events);
        return CommandOutcome.Accept(events);
    }
}
