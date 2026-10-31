namespace Sim.Cli.Runs;

/// <summary>
/// One JSONL line of <c>sim run --bot</c> (tech 6.1, D43): how one run went. Written in snake_case;
/// dictionary keys are content IDs or activity keys, sorted.
/// </summary>
public sealed record RunSummary(
    ulong Seed,
    string Bot,
    double? Aggression,
    string EndCause,
    int DeathAgeWeeks,
    double DeathAgeYears,
    int FinalStage,
    int Weeks,
    IReadOnlyList<BreakthroughRecord> Breakthroughs,
    IReadOnlyDictionary<string, int> ActivityWeeks,
    int HealingWeeks,
    IReadOnlyDictionary<string, int> Events,
    IReadOnlyDictionary<string, int> Rumors,
    CombatSummary Combat,
    StonesSummary Stones,
    PillsSummary Pills,
    InjurySummary Injuries);

/// <summary>
/// A breakthrough attempt with the forecast it started under, which is the foundation it resolved
/// by. <see cref="Result"/> is success, failed or death.
/// </summary>
public sealed record BreakthroughRecord(
    int Week,
    double AgeYears,
    int FromStage,
    string Result,
    string? Rank,
    string? FailureZone,
    double Threshold,
    double Foundation,
    double Purity,
    double Knowledge,
    double Deficit,
    double FailureChance,
    double Toxins);

public sealed record CombatSummary(
    int Fights,
    int Won,
    int Fled,
    int Beaten,
    int Died,
    double Insight,
    IReadOnlyDictionary<string, CombatGroupSummary> ByGroup);

public sealed record CombatGroupSummary(int Won, int Fled, int Beaten, int Died);

/// <summary>Stones earned by source (an event, <c>loot:grp.*</c>) and spent by sink (an event, shop).</summary>
public sealed record StonesSummary(
    int Start,
    int End,
    IReadOnlyDictionary<string, int> Earned,
    IReadOnlyDictionary<string, int> Spent);

public sealed record PillsSummary(
    IReadOnlyDictionary<string, int> Bought,
    IReadOnlyDictionary<string, int> Taken,
    double PeakToxins);

/// <summary>
/// The death-spiral metric (tech 4.6): the week an injury scale first reached the heavy share of
/// its maximum, and how long cultivation then stood still (to the next stage progress, or to the
/// end of the run when there was none).
/// </summary>
public sealed record InjurySummary(
    IReadOnlyDictionary<string, double> Peak,
    int? FirstHeavyWeek,
    int? WeeksWithoutProgressAfterHeavy,
    bool? ProgressedAfterHeavy);
