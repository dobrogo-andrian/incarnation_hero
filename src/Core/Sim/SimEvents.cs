using Core.State;

namespace Core.Sim;

/// <summary>A typed fact emitted by Core after executing a command. The UI reacts to these.</summary>
public abstract record SimEvent;

public sealed record WeekPassed(int Week, int AgeWeeks) : SimEvent;

public sealed record ActivityFinished(Activity Activity) : SimEvent;

public sealed record ActivityCancelled(Activity Activity) : SimEvent;

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
