using Core.Rng;

namespace Core.State;

public enum GameMode
{
    Overworld,
    EventDialog,
    Combat,
    RunEnd,
}

/// <summary>
/// The whole true state of a run. Plain serializable data: no logic, no references to content
/// objects (only string IDs), no Godot types.
/// </summary>
public sealed class GameState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ulong Seed { get; set; }

    /// <summary>RNG stream states indexed by <see cref="RngStream"/> value.</summary>
    public RngState[] RngStates { get; set; } = [];

    /// <summary>Weeks elapsed since the run started.</summary>
    public int Week { get; set; }

    public GameMode Mode { get; set; } = GameMode.Overworld;
    public PlayerState Player { get; set; } = new();
}

public sealed class PlayerState
{
    public int AgeWeeks { get; set; }
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
