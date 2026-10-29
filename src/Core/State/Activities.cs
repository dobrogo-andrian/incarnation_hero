using System.Text.Json.Serialization;

namespace Core.State;

/// <summary>
/// What the player spends weeks on (tech 2.3). Each kind is a record registered here for saving
/// and handled in <c>ActivitySystem</c>; its parameters (direction, destination…) are its fields.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(WaitActivity), "wait")]
[JsonDerivedType(typeof(TravelActivity), "travel")]
[JsonDerivedType(typeof(MeditateActivity), "meditate")]
[JsonDerivedType(typeof(BreakthroughActivity), "breakthrough")]
[JsonDerivedType(typeof(InquireActivity), "inquire")]
[JsonDerivedType(typeof(WorkActivity), "work")]
[JsonDerivedType(typeof(HuntActivity), "hunt")]
[JsonDerivedType(typeof(ArenaActivity), "arena")]
public abstract record Activity;

/// <summary>Let time pass doing nothing in particular.</summary>
public sealed record WaitActivity : Activity;

/// <summary>Asking around a settlement for rumors: likelier and from better sources than just being there (D41).</summary>
public sealed record InquireActivity : Activity;

/// <summary>Hired work at a job of the current settlement (§5, §13.2).</summary>
public sealed record WorkActivity(string JobId) : Activity;

/// <summary>Hunting at a hunting ground of the current zone (§5).</summary>
public sealed record HuntActivity(string HuntId) : Activity;

/// <summary>Weekly bouts at an arena of the current zone against its rank of opponents number <paramref name="Bout"/> (§5).</summary>
public sealed record ArenaActivity(string ArenaId, int Bout) : Activity;

/// <summary>
/// Meditation in one direction at a meditation place of the current zone (§4.2). Comprehension
/// also names what it studies: a carried folio's item ID, or an element to sense without one (§6).
/// </summary>
public sealed record MeditateActivity(string DirectionId, string PlaceId, string? SubjectId = null) : Activity;

/// <summary>
/// Seclusion at a meditation place for a breakthrough of the vector's stage (§4.5); it lasts the
/// stage's breakthrough weeks and resolves in the last one. Only an attempt that accepted the risk
/// of death can die of it.
/// </summary>
public sealed record BreakthroughActivity(string VectorId, string PlaceId, bool AcceptDeathRisk) : Activity;

/// <summary>
/// Moving along a route found by Core; its length is the sum of the legs, not chosen by the player.
/// The player is in a leg's zone once all weeks up to and including that leg have passed.
/// </summary>
public sealed record TravelActivity(IReadOnlyList<TravelLeg> Legs) : Activity
{
    [JsonIgnore]
    public string Destination => Legs[^1].ZoneId;

    [JsonIgnore]
    public int TotalWeeks => Legs.Sum(l => l.Weeks);
}

/// <summary>One step of a journey: the zone reached and the weeks it takes to get there.</summary>
public sealed record TravelLeg(string ZoneId, int Weeks);

public sealed class ActiveActivity
{
    public Activity Activity { get; set; } = null!;

    /// <summary>Weeks still to go; null runs until an interrupt or the end of the run.</summary>
    public int? WeeksLeft { get; set; }
}
