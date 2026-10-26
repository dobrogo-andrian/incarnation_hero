using Core.Loc;

namespace Core.Sim;

public static class RejectReasons
{
    public static readonly LocKey WrongMode = new("str.reject.wrong_mode");
    public static readonly LocKey InvalidWeeks = new("str.reject.invalid_weeks");
    public static readonly LocKey NoActivity = new("str.reject.no_activity");
    public static readonly LocKey InvalidActivity = new("str.reject.invalid_activity");
    public static readonly LocKey UnknownZone = new("str.reject.unknown_zone");
    public static readonly LocKey AlreadyThere = new("str.reject.already_there");
    public static readonly LocKey NoRoute = new("str.reject.no_route");
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
