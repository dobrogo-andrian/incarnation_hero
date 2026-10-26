using Core.Character;
using Core.Content;
using Core.Rng;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Entry point for the UI, tests and Sim.Cli: owns the state and its live RNG streams,
/// validates commands and dispatches them to systems.
/// </summary>
public sealed class Simulation
{
    public const string QiVectorId = "qi";

    private readonly RngStreams _rng;
    private readonly RunConfigDef _config;

    private Simulation(ContentDb content, GameState state, RngStreams rng)
    {
        Content = content;
        State = state;
        _rng = rng;
        _config = content.Get<RunConfigDef>(RunConfigDef.RunId);
        Rules = new CharacterRules(content);
    }

    public ContentDb Content { get; }

    public GameState State { get; }

    public CharacterRules Rules { get; }

    public PlayerView View => PlayerView.From(State, Rules);

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
                Tracks = [new CultivationTrack { VectorId = QiVectorId }],
            },
        };
        var sim = new Simulation(content, state, rng);
        sim.Rules.InitPlayer(state.Player);
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
            ContinueActivity => ExecuteContinue(),
            CancelActivity => ExecuteCancel(),
            SetInterruptStops c => ExecuteSetInterruptStops(c),
            _ => throw new NotSupportedException($"No handler for {command.GetType().Name}."),
        };

        State.RngStates = _rng.GetStates();
        return outcome;
    }

    private CommandOutcome ExecuteStart(StartActivity command)
    {
        if (command.Weeks <= 0)
            return CommandOutcome.Reject(RejectReasons.InvalidWeeks);

        State.Activity = new ActiveActivity { Activity = command.Activity, WeeksLeft = command.Weeks };
        return RunActivity();
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

    private CommandOutcome RunActivity()
    {
        var events = new List<SimEvent>();
        TimeSystem.RunActivity(State, _config, _rng, events);
        return CommandOutcome.Accept(events);
    }
}
