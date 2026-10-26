namespace Core.Sim;

/// <summary>Localization key with no parameters yet; parameters arrive with the loc layer.</summary>
public sealed record LocKey(string Key);

public static class RejectReasons
{
    public static readonly LocKey WrongMode = new("reject.wrong_mode");
    public static readonly LocKey InvalidWeeks = new("reject.invalid_weeks");
}

/// <summary>Either the command was executed and produced events, or it was rejected with a reason.</summary>
public sealed class CommandOutcome
{
    private static readonly IReadOnlyList<SimEvent> NoEvents = [];

    private CommandOutcome(LocKey? rejectReason, IReadOnlyList<SimEvent> events)
    {
        RejectReason = rejectReason;
        Events = events;
    }

    public bool Accepted => RejectReason is null;
    public LocKey? RejectReason { get; }
    public IReadOnlyList<SimEvent> Events { get; }

    public static CommandOutcome Accept(IReadOnlyList<SimEvent> events) => new(null, events);
    public static CommandOutcome Reject(LocKey reason) => new(reason, NoEvents);
}
