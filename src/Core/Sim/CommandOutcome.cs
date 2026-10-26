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
    public static readonly LocKey UnknownDirection = new("str.reject.unknown_direction");
    public static readonly LocKey PlaceNotHere = new("str.reject.place_not_here");
    public static readonly LocKey NoCultivationBuff = new("str.reject.no_cultivation_buff");
    public static readonly LocKey StageScaleFull = new("str.reject.stage_scale_full");
    public static readonly LocKey NoSuchItem = new("str.reject.no_such_item");
    public static readonly LocKey NotAPill = new("str.reject.not_a_pill");
    public static readonly LocKey ToxinsTooHigh = new("str.reject.toxins_too_high");
    public static readonly LocKey NotSoldHere = new("str.reject.not_sold_here");
    public static readonly LocKey InvalidAmount = new("str.reject.invalid_amount");
    public static readonly LocKey NotEnoughStones = new("str.reject.not_enough_stones");
    public static readonly LocKey TooDangerousHere = new("str.reject.too_dangerous_here");
    public static readonly LocKey QiPoolFull = new("str.reject.qi_pool_full");
    public static readonly LocKey PlaceFull = new("str.reject.place_full");
    public static readonly LocKey NothingToStudy = new("str.reject.nothing_to_study");
    public static readonly LocKey StudyCapReached = new("str.reject.study_cap_reached");
    public static readonly LocKey NoNextStage = new("str.reject.no_next_stage");
    public static readonly LocKey StageScaleNotFull = new("str.reject.stage_scale_not_full");
    public static readonly LocKey DeathRiskNotAccepted = new("str.reject.death_risk_not_accepted");
    public static readonly LocKey NotAMethod = new("str.reject.not_a_method");
    public static readonly LocKey MethodNotStudied = new("str.reject.method_not_studied");
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
