using Core.State;

namespace Core.Sim;

/// <summary>A typed fact emitted by Core after executing a command. The UI reacts to these.</summary>
public abstract record SimEvent;

public sealed record WeekPassed(int Week, int AgeWeeks) : SimEvent;

public sealed record ActivityFinished(Activity Activity) : SimEvent;

public sealed record ActivityCancelled(Activity Activity) : SimEvent;

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
