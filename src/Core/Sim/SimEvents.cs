namespace Core.Sim;

/// <summary>A typed fact emitted by Core after executing a command. The UI reacts to these.</summary>
public abstract record SimEvent;

public sealed record WeekPassed(int Week, int AgeWeeks) : SimEvent;
