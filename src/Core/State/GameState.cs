using Core.Rng;

namespace Core.State;

public enum GameMode
{
    Overworld,
    EventDialog,
    Combat,
    RunEnd,
}

/// <summary>Kinds of interrupt signals (tech 2.3). The player chooses which of them stop time.</summary>
public enum InterruptCategory
{
    LifeEndApproaching,
}

public enum RunEndCause
{
    OldAge,
}

/// <summary>
/// The whole true state of a run. Plain serializable data: no logic, no references to content
/// objects (only string IDs), no Godot types.
/// </summary>
public sealed class GameState
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ulong Seed { get; set; }

    /// <summary>RNG stream states indexed by <see cref="RngStream"/> value.</summary>
    public RngState[] RngStates { get; set; } = [];

    /// <summary>Weeks elapsed since the run started.</summary>
    public int Week { get; set; }

    public GameMode Mode { get; set; } = GameMode.Overworld;
    public PlayerState Player { get; set; } = new();

    /// <summary>The activity in progress; it stays here when an interrupt stops time.</summary>
    public ActiveActivity? Activity { get; set; }

    /// <summary>Interrupt categories that stop a running activity. Others are reported and time goes on.</summary>
    public List<InterruptCategory> StoppingInterrupts { get; set; } = [.. Enum.GetValues<InterruptCategory>()];

    /// <summary>Set when the run is over (<see cref="GameMode.RunEnd"/>).</summary>
    public RunEndCause? EndCause { get; set; }
}

public sealed class PlayerState
{
    public int AgeWeeks { get; set; }

    /// <summary>Expected lifespan; natural death becomes possible a few years before it (§3).</summary>
    public int LifespanWeeks { get; set; }

    public List<CultivationTrack> Tracks { get; set; } = [];
}

/// <summary>
/// One cultivation vector. The MVP only has <c>qi</c>; body/will reuse the same shape later.
/// </summary>
public sealed class CultivationTrack
{
    public string VectorId { get; set; } = "";

    /// <summary>Index into the vector's stage list: 0 is the first stage.</summary>
    public int StageIndex { get; set; }
    public int StageProgress { get; set; }

    /// <summary>Quality rank of the last breakthrough; null until the first one.</summary>
    public int? LastBreakthroughQuality { get; set; }
}
