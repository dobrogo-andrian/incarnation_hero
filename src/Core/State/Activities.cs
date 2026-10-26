using System.Text.Json.Serialization;

namespace Core.State;

/// <summary>
/// What the player spends weeks on (tech 2.3). Each kind is a record registered here for saving
/// and handled in <c>ActivitySystem</c>; its parameters (direction, destination…) are its fields.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(WaitActivity), "wait")]
public abstract record Activity;

/// <summary>Let time pass doing nothing in particular.</summary>
public sealed record WaitActivity : Activity;

public sealed class ActiveActivity
{
    public Activity Activity { get; set; } = null!;

    /// <summary>Weeks still to go; null runs until an interrupt or the end of the run.</summary>
    public int? WeeksLeft { get; set; }
}
