using Core.Rng;
using Core.State;

namespace Core.Sim;

public sealed record NewGameSettings(int StartAgeWeeks);

/// <summary>
/// Entry point for the UI, tests and Sim.Cli: owns the state and its live RNG streams,
/// validates commands and dispatches them to systems.
/// </summary>
public sealed class Simulation
{
    public const string QiVectorId = "qi";

    private readonly RngStreams _rng;

    private Simulation(GameState state, RngStreams rng)
    {
        State = state;
        _rng = rng;
    }

    public GameState State { get; }

    public PlayerView View => PlayerView.From(State);

    public static Simulation NewGame(ulong seed, NewGameSettings settings)
    {
        var rng = RngStreams.FromSeed(seed);
        var state = new GameState
        {
            Seed = seed,
            RngStates = rng.GetStates(),
            Player = new PlayerState
            {
                AgeWeeks = settings.StartAgeWeeks,
                Tracks = [new CultivationTrack { VectorId = QiVectorId }],
            },
        };
        return new Simulation(state, rng);
    }

    /// <summary>Resumes from a loaded state; RNG streams continue exactly where they were saved.</summary>
    public static Simulation Resume(GameState state) => new(state, RngStreams.FromStates(state.RngStates));

    public CommandOutcome Execute(Command command)
    {
        if (command.Mode != State.Mode)
            return CommandOutcome.Reject(RejectReasons.WrongMode);

        var outcome = command switch
        {
            WaitWeeks c => ExecuteWait(c),
            _ => throw new NotSupportedException($"No handler for {command.GetType().Name}."),
        };

        State.RngStates = _rng.GetStates();
        return outcome;
    }

    private CommandOutcome ExecuteWait(WaitWeeks command)
    {
        if (command.Weeks <= 0)
            return CommandOutcome.Reject(RejectReasons.InvalidWeeks);

        var events = new List<SimEvent>();
        for (int i = 0; i < command.Weeks; i++)
            TimeSystem.AdvanceWeek(State, _rng, events);
        return CommandOutcome.Accept(events);
    }
}
