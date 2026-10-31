using Core.Combat;
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
    InjuryHealed,
    RumorHeard,
}

/// <summary>Where a fight of the run comes from: what it goes on to, and whether it may be auto-resolved unasked (D67).</summary>
public enum FightKind
{
    Event,
    Hunt,
    Arena,
}

public enum RunEndCause
{
    OldAge,
    BreakthroughDeath,
    CombatDeath,
}

/// <summary>
/// The whole true state of a run. Plain serializable data: no logic, no references to content
/// objects (only string IDs), no Godot types.
/// </summary>
public sealed class GameState
{
    public const int CurrentSchemaVersion = 19;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ulong Seed { get; set; }

    /// <summary>RNG stream states indexed by <see cref="RngStream"/> value.</summary>
    public RngState[] RngStates { get; set; } = [];

    /// <summary>Weeks elapsed since the run started.</summary>
    public int Week { get; set; }

    public GameMode Mode { get; set; } = GameMode.Overworld;
    public PlayerState Player { get; set; } = new();
    public WorldState World { get; set; } = new();

    /// <summary>The activity being run by the current command; an interrupt or event ends it (D45), so between commands it is empty.</summary>
    public ActiveActivity? Activity { get; set; }

    /// <summary>The event dialog in progress (<see cref="GameMode.EventDialog"/>).</summary>
    public ActiveEvent? Event { get; set; }

    /// <summary>The fight offered or fought by hand (<see cref="GameMode.Combat"/>); an event's dialog waits in <see cref="Event"/> meanwhile.</summary>
    public ActiveFight? Fight { get; set; }

    /// <summary>Every event shown this run, in event ID order: for once-per-run, cooldowns and the repeat penalty (D38).</summary>
    public List<EventRecord> EventHistory { get; set; } = [];

    /// <summary>Rumors being followed, in the order they were heard (D41).</summary>
    public List<Rumor> Rumors { get; set; } = [];

    /// <summary>When each rumor template was last heard, in rumor ID order: for its cooldown.</summary>
    public List<RumorRecord> RumorHistory { get; set; } = [];

    /// <summary>Interrupt categories that stop a running activity. Others are reported and time goes on.</summary>
    public List<InterruptCategory> StoppingInterrupts { get; set; } = [.. Enum.GetValues<InterruptCategory>()];

    /// <summary>Kinds of fight auto-resolved without asking (D67); none by default, and an event's fight always asks.</summary>
    public List<FightKind> AutoFights { get; set; } = [];

    /// <summary>Set when the run is over (<see cref="GameMode.RunEnd"/>).</summary>
    public RunEndCause? EndCause { get; set; }

    /// <summary>Tallies for the run's summary (D47); the rules never read them.</summary>
    public RunStats Stats { get; set; } = new();
}

/// <summary>What the run-end summary shows (D47), kept as the run goes.</summary>
public sealed class RunStats
{
    /// <summary>Weeks spent on each activity, by <c>RunStatsSystem.ActivityKey</c>, in key order.</summary>
    public List<WeekTally> ActivityWeeks { get; set; } = [];

    /// <summary>Every breakthrough attempt in order.</summary>
    public List<BreakthroughRecord> Breakthroughs { get; set; } = [];

    public int CombatsWon { get; set; }

    public int CombatsFled { get; set; }

    /// <summary>Fights lost and survived at the winners' mercy (D62).</summary>
    public int CombatsBeaten { get; set; }

    public int CombatsDied { get; set; }

    public int PillsTaken { get; set; }

    public int StonesEarned { get; set; }

    public int StonesSpent { get; set; }

    /// <summary>Event and rumor dialogs opened.</summary>
    public int Dialogs { get; set; }

    public int RumorsHeard { get; set; }
}

public sealed class WeekTally
{
    public string Key { get; set; } = "";

    public int Weeks { get; set; }
}

/// <summary>A breakthrough attempt: the rank reached, or the failure zone it fell into.</summary>
public sealed class BreakthroughRecord
{
    public int AgeWeeks { get; set; }

    public string? RankId { get; set; }

    public string? FailureZoneId { get; set; }
}

/// <summary>
/// What <c>CharacterRules</c> computes values from: the player, or an enemy's snapshot in a fight,
/// so both follow the same stat → aspect → resource chain.
/// </summary>
public interface ICharacter
{
    List<StatValue> Stats { get; }
    string AspectProfileId { get; }
    List<Modifier> Modifiers { get; }
}

public sealed class PlayerState : ICharacter
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

    /// <summary>The cultivation method breakthroughs go through (§6); null without one.</summary>
    public string? ActiveMethodId { get; set; }

    /// <summary>General injury scales above zero, in injury ID order (§8).</summary>
    public List<InjuryLevel> Injuries { get; set; } = [];

    /// <summary>Quality ceiling a breakthrough pill adds to the next breakthrough; spent by the attempt (§4.5).</summary>
    public double BreakthroughCeilingBonus { get; set; }

    /// <summary>Reputation with sects other than zero, in sect ID order (D39).</summary>
    public List<Reputation> Reputation { get; set; } = [];
}

public sealed class Reputation
{
    public string SectId { get; set; } = "";
    public int Value { get; set; }
}

/// <summary>An event dialog in progress: which event, at which node, since which week.</summary>
public sealed class ActiveEvent
{
    public string EventId { get; set; } = "";
    public string NodeId { get; set; } = "";

    /// <summary>Week it started; with the seed it fixes how the chances of its options are misjudged.</summary>
    public int StartedWeek { get; set; }
}

/// <summary>
/// A fight of the run waiting for the player (D67): offered, then fought by hand or auto-resolved.
/// The battle itself is not saved: its seeds and the player's commands replay it exactly (tech 4.1).
/// </summary>
public sealed class ActiveFight
{
    public string GroupId { get; set; } = "";
    public FightKind Kind { get; set; }

    /// <summary>Subseeds of the <c>combat</c> and <c>ai</c> streams, drawn when the fight was offered.</summary>
    public ulong CombatSeed { get; set; }

    public ulong AiSeed { get; set; }

    /// <summary>Whether the player took the fight by hand; until then it is only offered.</summary>
    public bool ByHand { get; set; }

    /// <summary>The player's commands so far, in order.</summary>
    public List<BattleCommand> Commands { get; set; } = [];

    /// <summary>An event's nodes after a win and after any other end.</summary>
    public string? WinNode { get; set; }

    public string? LossNode { get; set; }

    /// <summary>An arena bout's prize for a win.</summary>
    public int Prize { get; set; }
}

public sealed class EventRecord
{
    public string EventId { get; set; } = "";
    public int Count { get; set; }
    public int LastWeek { get; set; }
}

/// <summary>
/// A rumor heard and not yet met (D41). The player sees its claim, source and window, never
/// <see cref="TrueNode"/> or <see cref="Taken"/>.
/// </summary>
public sealed class Rumor
{
    public string RumorId { get; set; } = "";
    public string SourceId { get; set; } = "";
    public int HeardWeek { get; set; }

    /// <summary>Last week in which arriving still finds it.</summary>
    public int LastWeek { get; set; }

    /// <summary>The node of the rumor's dialog that is really there.</summary>
    public string TrueNode { get; set; } = "";

    /// <summary>Rivals got there first; arriving finds only their traces.</summary>
    public bool Taken { get; set; }
}

public sealed class RumorRecord
{
    public string RumorId { get; set; } = "";
    public int LastHeardWeek { get; set; }
}

/// <summary>How far one piece of knowledge has been comprehended (§6).</summary>
public sealed class KnowledgeLevel
{
    public string KnowledgeId { get; set; } = "";
    public int Level { get; set; }

    /// <summary>Comprehension points towards the next level.</summary>
    public double Progress { get; set; }
}

/// <summary>How injured the player is on one scale (§8); scales at zero have no entry.</summary>
public sealed class InjuryLevel
{
    public string InjuryId { get; set; } = "";
    public double Value { get; set; }
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

    /// <summary>Hidden zones and places the player has found, in ID order (D40).</summary>
    public List<string> Revealed { get; set; } = [];
}

public sealed class PlaceState
{
    public string PlaceId { get; set; } = "";
    public double Reserve { get; set; }
}

public sealed class StatValue
{
    public string StatId { get; set; } = "";

    /// <summary>The stat's value before modifiers; for a ranked stat, its rank's base plus the filled part of the scale.</summary>
    public double Base { get; set; }

    /// <summary>Ranked stats only (D48): the rank index, the points past the full scale and those lost below the rank's base (D49).</summary>
    public int Rank { get; set; }

    public double Overflow { get; set; }

    public double Deficit { get; set; }
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

    /// <summary>Index into the vector's stage list: 0 is the mortal start, before the first breakthrough.</summary>
    public int StageIndex { get; set; }
    public double StageProgress { get; set; }

    /// <summary>Age at which the scale last became full; delaying the breakthrough after it costs quality (§4.5).</summary>
    public int? ScaleFullSinceAgeWeeks { get; set; }

    /// <summary>Quality rank index (into <c>config.breakthrough</c> ranks) of the last breakthrough; null until the first one.</summary>
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
