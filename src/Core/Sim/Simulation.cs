using Core.Character;
using Core.Combat;
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

    /// <summary>The battle of <see cref="GameState.Fight"/>, replayed once after a load and then kept live.</summary>
    private (ActiveFight Fight, BattleSession Session)? _battle;

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

    /// <summary>
    /// The battle of the fight offered or fought by hand (D67): the field and every fighter's true
    /// numbers, which the battle screen shows as the sandbox does once the fight is taken by hand. It
    /// lives on as the same object to the fight's end, so the screen can show how it ended. Null with no fight.
    /// </summary>
    public Battle? Battle => State.Fight is not null ? Session().Battle : null;

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
            SetInterruptStops c => ExecuteSetInterruptStops(c),
            SetAutoFights c => ExecuteSetAutoFights(c),
            FightByHand => CombatSystem.ByHand(State, _ctx, Session()),
            FightAction c => CombatSystem.Act(State, _ctx, Session(), c.Command),
            AutoResolveFight => CombatSystem.AutoResolve(State, _ctx, Session()),
            ChooseEventOption c => EventSystem.Choose(State, _ctx, c.Index),
            UsePill c => ItemSystem.UsePill(State.Player, _ctx, c.ItemId),
            Buy c => EconomySystem.Buy(State.Player, _ctx, c.ItemId),
            _ => throw new NotSupportedException($"No handler for {command.GetType().Name}."),
        };

        if (outcome.Accepted)
            RunStatsSystem.Record(State, outcome.Events);
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
        if (WorldSystem.RouteTo(State, _ctx, command.ZoneId) is not { } legs)
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

    private CommandOutcome ExecuteSetInterruptStops(SetInterruptStops command)
    {
        State.StoppingInterrupts = command.Categories.Distinct().Order().ToList();
        return CommandOutcome.Accept([]);
    }

    private CommandOutcome ExecuteSetAutoFights(SetAutoFights command)
    {
        if (command.Kinds.Contains(FightKind.Event))
            return CommandOutcome.Reject(RejectReasons.EventFightsAsk);
        State.AutoFights = command.Kinds.Distinct().Order().ToList();
        return CommandOutcome.Accept([]);
    }

    private BattleSession Session()
    {
        var fight = State.Fight!;
        if (_battle is not { } battle || battle.Fight != fight)
            _battle = battle = (fight, CombatSystem.Session(State, _ctx, fight));
        return battle.Session;
    }

    private CommandOutcome Start(Activity activity, int? weeks)
    {
        State.Activity = new ActiveActivity { Activity = activity, WeeksLeft = weeks };
        var events = new List<SimEvent>();
        TimeSystem.RunActivity(State, _ctx, events);
        return CommandOutcome.Accept(events);
    }
}
