using Core.Loc;

namespace Core.Sim;

public static class RejectReasons
{
    public static readonly LocKey WrongMode = new("str.reject.wrong_mode");
    public static readonly LocKey InvalidWeeks = new("str.reject.invalid_weeks");
    public static readonly LocKey BattleOver = new("str.reject.battle_over");
    public static readonly LocKey InvalidTarget = new("str.reject.invalid_target");
    public static readonly LocKey OutOfReach = new("str.reject.out_of_reach");
    public static readonly LocKey NotEnoughEnergy = new("str.reject.not_enough_energy");
    public static readonly LocKey Unreachable = new("str.reject.unreachable");
    public static readonly LocKey NotOnEdge = new("str.reject.not_on_edge");
    public static readonly LocKey EnemyAdjacent = new("str.reject.enemy_adjacent");
    public static readonly LocKey CannotYield = new("str.reject.cannot_yield");
    public static readonly LocKey FightUnderway = new("str.reject.fight_underway");
    public static readonly LocKey FightNotTaken = new("str.reject.fight_not_taken");
    public static readonly LocKey EventFightsAsk = new("str.reject.event_fights_ask");
    public static readonly LocKey UnknownTechnique = new("str.reject.unknown_technique");
    public static readonly LocKey NotEnoughQi = new("str.reject.not_enough_qi");
    public static readonly LocKey NoRoom = new("str.reject.no_room");
    public static readonly LocKey InvalidActivity = new("str.reject.invalid_activity");
    public static readonly LocKey UnknownZone = new("str.reject.unknown_zone");
    public static readonly LocKey AlreadyThere = new("str.reject.already_there");
    public static readonly LocKey NoRoute = new("str.reject.no_route");
    public static readonly LocKey NotInSettlement = new("str.reject.not_in_settlement");
    public static readonly LocKey UnknownDirection = new("str.reject.unknown_direction");
    public static readonly LocKey PlaceNotHere = new("str.reject.place_not_here");
    public static readonly LocKey NotHere = new("str.reject.not_here");
    public static readonly LocKey TooWounded = new("str.reject.too_wounded");
    public static readonly LocKey NoCultivationBuff = new("str.reject.no_cultivation_buff");
    public static readonly LocKey StageScaleFull = new("str.reject.stage_scale_full");
    public static readonly LocKey NoSuchItem = new("str.reject.no_such_item");
    public static readonly LocKey NotAPill = new("str.reject.not_a_pill");
    public static readonly LocKey ToxinsTooHigh = new("str.reject.toxins_too_high");
    public static readonly LocKey NotSoldHere = new("str.reject.not_sold_here");
    public static readonly LocKey NotEnoughStones = new("str.reject.not_enough_stones");
    public static readonly LocKey NothingToRestore = new("str.reject.nothing_to_restore");
    public static readonly LocKey NothingToRecover = new("str.reject.nothing_to_recover");
    public static readonly LocKey TooExposed = new("str.reject.too_exposed");
    public static readonly LocKey NothingToStudy = new("str.reject.nothing_to_study");
    public static readonly LocKey StudyCapReached = new("str.reject.study_cap_reached");
    public static readonly LocKey NoNextStage = new("str.reject.no_next_stage");
    public static readonly LocKey StageScaleNotFull = new("str.reject.stage_scale_not_full");
    public static readonly LocKey DeathRiskNotAccepted = new("str.reject.death_risk_not_accepted");
    public static readonly LocKey NotAMethod = new("str.reject.not_a_method");
    public static readonly LocKey MethodNotStudied = new("str.reject.method_not_studied");
    public static readonly LocKey NothingToHeal = new("str.reject.nothing_to_heal");
    public static readonly LocKey InvalidOption = new("str.reject.invalid_option");
    public static readonly LocKey OptionUnavailable = new("str.reject.option_unavailable");
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
