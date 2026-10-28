using Core.Combat;
using Core.State;

namespace Core.Sim;

/// <summary>A typed fact emitted by Core after executing a command. The UI reacts to these.</summary>
public abstract record SimEvent;

public sealed record WeekPassed(int Week, int AgeWeeks) : SimEvent;

public sealed record ActivityFinished(Activity Activity) : SimEvent;

/// <summary>An event or a stopping interrupt ended the activity before it was done (D44); nothing resumes it.</summary>
public sealed record ActivityInterrupted(Activity Activity) : SimEvent;

/// <summary>The player reached a zone, on the way or at the destination.</summary>
public sealed record ZoneEntered(string ZoneId) : SimEvent;

/// <summary>A pill was taken and gave <paramref name="Energy"/> points of pill energy.</summary>
public sealed record PillTaken(string ItemId, double Energy) : SimEvent;

/// <summary>The run is over; the mode is now <see cref="GameMode.RunEnd"/>.</summary>
public sealed record RunEnded(RunEndCause Cause) : SimEvent;

/// <summary>A signal that may stop a running activity, depending on the player's choice of categories.</summary>
public abstract record InterruptEvent : SimEvent
{
    public abstract InterruptCategory Category { get; }
}

public sealed record LifeEndApproaching(int YearsLeft) : InterruptEvent
{
    public override InterruptCategory Category => InterruptCategory.LifeEndApproaching;
}

/// <summary>A cultivation buff ran out: its points were spent or combat insight faded away.</summary>
public sealed record BuffExhausted(BuffKind Kind) : InterruptEvent
{
    public override InterruptCategory Category => InterruptCategory.BuffExhausted;
}

/// <summary>The vector's stage scale is full; a breakthrough can be attempted (§4.5).</summary>
public sealed record StageScaleFull(string VectorId) : InterruptEvent
{
    public override InterruptCategory Category => InterruptCategory.StageScaleFull;
}

/// <summary>Comprehension reached a new level of a technique, element or method.</summary>
public sealed record KnowledgeLevelUp(string KnowledgeId, int Level) : SimEvent;

/// <summary>Study reached the ceiling of its folio (or of basic element sensing); a better source is needed.</summary>
public sealed record StudyCapReached(string KnowledgeId) : InterruptEvent
{
    public override InterruptCategory Category => InterruptCategory.StudyCapReached;
}

/// <summary>The vector broke through into <paramref name="StageId"/> with a quality rank, gaining years of life (§4.5).</summary>
public sealed record BreakthroughSucceeded(string VectorId, string StageId, string RankId, double LifespanYears) : SimEvent;

/// <summary>A breakthrough failed in a survivable zone of the deficit and its price was paid (§4.5).</summary>
public sealed record BreakthroughFailed(string VectorId, string FailureZoneId) : SimEvent;

/// <summary>The meditation place's reserve can no longer cover a full week of meditation.</summary>
public sealed record PlaceQiLow(string PlaceId) : InterruptEvent
{
    public override InterruptCategory Category => InterruptCategory.PlaceQiLow;
}

/// <summary>An injury scale rose by <paramref name="Amount"/> (§8).</summary>
public sealed record InjuryReceived(string InjuryId, double Amount) : SimEvent;

/// <summary>An injury scale healed down to zero.</summary>
public sealed record InjuryHealed(string InjuryId) : InterruptEvent
{
    public override InterruptCategory Category => InterruptCategory.InjuryHealed;
}

/// <summary>An event dialog opened (§10); the mode is now <see cref="GameMode.EventDialog"/>.</summary>
public sealed record EventStarted(string EventId) : SimEvent;

/// <summary>The event dialog closed; the interrupted activity can be continued.</summary>
public sealed record EventEnded(string EventId) : SimEvent;

/// <summary>A stat gained or lost <paramref name="Amount"/> points (for a ranked stat, overflow included).</summary>
public sealed record StatChanged(string StatId, double Amount) : SimEvent;

/// <summary>A ranked stat broke through to <paramref name="RankId"/> (D48).</summary>
public sealed record StatRankUp(string StatId, string RankId) : SimEvent;

/// <summary>A ranked stat's deficit collapsed it to <paramref name="RankId"/> (D49).</summary>
public sealed record StatRankDown(string StatId, string RankId) : SimEvent;

/// <summary>A ranked stat without breakthroughs crossed onto the scale of <paramref name="RankId"/>, up or down (D50).</summary>
public sealed record StatRankChanged(string StatId, string RankId) : SimEvent;

/// <summary>A resource's current amount changed by <paramref name="Amount"/>.</summary>
public sealed record ResourceChanged(string ResourceId, double Amount) : SimEvent;

/// <summary>Items were gained (positive) or lost (negative).</summary>
public sealed record ItemsChanged(string ItemId, int Count) : SimEvent;

public sealed record StonesChanged(int Amount) : SimEvent;

/// <summary>A cultivation buff was gained or topped up by <paramref name="Points"/>.</summary>
public sealed record BuffGained(BuffKind Kind, double Points) : SimEvent;

public sealed record ReputationChanged(string SectId, int Amount) : SimEvent;

/// <summary>A hidden zone or meditation place became known (D40).</summary>
public sealed record Revealed(string Id) : SimEvent;

/// <summary>The player heard a rumor (D41); it is now among the rumors being followed.</summary>
public sealed record RumorHeard(string RumorId) : InterruptEvent
{
    public override InterruptCategory Category => InterruptCategory.RumorHeard;
}

/// <summary>A fight is over (D42): how it ended, after how many rounds, and what it cost in health and qi (shield and pool).</summary>
public sealed record CombatResolved(string GroupId, CombatEnd End, int Rounds, double HealthLost, double QiSpent) : SimEvent;
