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
    PlaceQiLow,
    BuffExhausted,
    StageScaleFull,
    StudyCapReached,
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
    public const int CurrentSchemaVersion = 8;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ulong Seed { get; set; }

    /// <summary>RNG stream states indexed by <see cref="RngStream"/> value.</summary>
    public RngState[] RngStates { get; set; } = [];

    /// <summary>Weeks elapsed since the run started.</summary>
    public int Week { get; set; }

    public GameMode Mode { get; set; } = GameMode.Overworld;
    public PlayerState Player { get; set; } = new();
    public WorldState World { get; set; } = new();

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

    /// <summary>The zone the player is in; while travelling, the last zone reached.</summary>
    public string ZoneId { get; set; } = "";

    /// <summary>Base stat values, before modifiers; one per stat in <c>config.run</c>, in its order.</summary>
    public List<StatValue> Stats { get; set; } = [];

    /// <summary>Current resource amounts; maximums are computed from stats and modifiers.</summary>
    public List<ResourceValue> Resources { get; set; } = [];

    public string AspectProfileId { get; set; } = "";

    /// <summary>Every active modifier on the player, in the order they were applied.</summary>
    public List<Modifier> Modifiers { get; set; } = [];

    public List<CultivationTrack> Tracks { get; set; } = [];

    /// <summary>Active cultivation buffs, at most one per kind, in <see cref="BuffKind"/> order.</summary>
    public List<CultivationBuff> Buffs { get; set; } = [];

    /// <summary>Spirit stones: the only currency, and fuel for cultivation (§13.1).</summary>
    public int Stones { get; set; }

    /// <summary>Carried items, in item ID order.</summary>
    public List<ItemCount> Items { get; set; } = [];

    /// <summary>How many pills of each type were taken this run; each repeat gives less energy. In item ID order.</summary>
    public List<ItemCount> PillsTaken { get; set; } = [];

    /// <summary>Techniques, elements and methods studied at least a little, in knowledge ID order.</summary>
    public List<KnowledgeLevel> Knowledge { get; set; } = [];
}

/// <summary>How far one piece of knowledge has been comprehended (§6).</summary>
public sealed class KnowledgeLevel
{
    public string KnowledgeId { get; set; } = "";
    public int Level { get; set; }

    /// <summary>Comprehension points towards the next level.</summary>
    public double Progress { get; set; }
}

/// <summary>A number of items of one type.</summary>
public sealed class ItemCount
{
    public string ItemId { get; set; } = "";
    public int Count { get; set; }
}

/// <summary>The changing part of the region; its fixed shape (zones, routes, places) is content.</summary>
public sealed class WorldState
{
    /// <summary>One entry per meditation place, in place ID order.</summary>
    public List<PlaceState> Places { get; set; } = [];
}

public sealed class PlaceState
{
    public string PlaceId { get; set; } = "";
    public double Reserve { get; set; }
}

public sealed class StatValue
{
    public string StatId { get; set; } = "";
    public double Base { get; set; }
}

public sealed class ResourceValue
{
    public string ResourceId { get; set; } = "";
    public double Current { get; set; }
}

public enum ModifierOp
{
    Add,
    Mul,
    Cap,
    Override,
}

/// <summary>
/// One change to a computed value (tech 2.5): injuries, buffs, elements, methods and items all use
/// this. <see cref="TargetId"/> is a stat, aspect or resource ID; for a resource it changes the maximum.
/// </summary>
public sealed class Modifier
{
    public string TargetId { get; set; } = "";
    public ModifierOp Op { get; set; }

    /// <summary>Amount for Add, Cap and Override; a fraction for Mul (0.1 is +10%).</summary>
    public double Value { get; set; }

    /// <summary>What applied it (an injury, a buff…), shown in "where does this number come from".</summary>
    public string SourceId { get; set; } = "";

    /// <summary>Weeks until it expires; null lasts until removed.</summary>
    public int? WeeksLeft { get; set; }
}

/// <summary>
/// One cultivation vector. The MVP only has <c>qi</c>; body/will reuse the same shape later.
/// </summary>
public sealed class CultivationTrack
{
    public string VectorId { get; set; } = "";

    /// <summary>Index into the vector's stage list: 0 is the first stage.</summary>
    public int StageIndex { get; set; }
    public double StageProgress { get; set; }

    /// <summary>Quality rank of the last breakthrough; null until the first one.</summary>
    public int? LastBreakthroughQuality { get; set; }
}

/// <summary>
/// Sources of cultivation points that assimilation turns into stage progress (§4.3). Declaration
/// order is spending order: combat insight first, because it fades (D31).
/// </summary>
public enum BuffKind
{
    CombatInsight,
    PillEnergy,
}

public sealed class CultivationBuff
{
    public BuffKind Kind { get; set; }

    /// <summary>Cultivation points left; the buff is gone when they run out.</summary>
    public double Points { get; set; }
}
