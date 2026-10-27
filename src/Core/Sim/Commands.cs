using Core.State;

namespace Core.Sim;

/// <summary>A typed request from the UI or a bot. Each command is valid in exactly one mode.</summary>
public abstract record Command
{
    public abstract GameMode Mode { get; }
}

public abstract record OverworldCommand : Command
{
    public override GameMode Mode => GameMode.Overworld;
}

public abstract record EventDialogCommand : Command
{
    public override GameMode Mode => GameMode.EventDialog;
}

/// <summary>
/// Chooses option <paramref name="Index"/> of the current event node; at a node without options,
/// index 0 is the implicit "continue" that closes the dialog (D38).
/// </summary>
public sealed record ChooseEventOption(int Index) : EventDialogCommand;

/// <summary>
/// Starts <paramref name="Activity"/> (replacing any current one) and runs weeks until it ends,
/// a stopping interrupt fires or the run ends. Null <paramref name="Weeks"/> means no limit.
/// </summary>
public sealed record StartActivity(Activity Activity, int? Weeks) : OverworldCommand;

/// <summary>Travels to <paramref name="ZoneId"/> by the fastest route (replacing any current activity).</summary>
public sealed record Travel(string ZoneId) : OverworldCommand;

/// <summary>Takes one carried pill (§4.3). Instant: no time passes.</summary>
public sealed record UsePill(string ItemId) : OverworldCommand;

/// <summary>Buys one item in a shop of the current zone (§13.3).</summary>
public sealed record Buy(string ItemId) : OverworldCommand;

/// <summary>Burns up to <paramref name="Stones"/> spirit stones to refill the qi pool (D18).</summary>
public sealed record RestoreQiWithStones(int Stones) : OverworldCommand;

/// <summary>Burns up to <paramref name="Stones"/> spirit stones to refill a meditation place's reserve (D19).</summary>
public sealed record RefillPlaceWithStones(string PlaceId, int Stones) : OverworldCommand;

/// <summary>
/// Goes into seclusion at <paramref name="PlaceId"/> to break through the vector's stage (§4.5),
/// replacing any current activity. When the forecast is in the fatal zone, it starts only with
/// <paramref name="AcceptDeathRisk"/>.
/// </summary>
public sealed record AttemptBreakthrough(string VectorId, string PlaceId, bool AcceptDeathRisk = false) : OverworldCommand;

/// <summary>Makes a studied cultivation method the active one, or clears it with null (§6). Instant.</summary>
public sealed record SetActiveMethod(string? MethodId) : OverworldCommand;

/// <summary>Chooses which interrupt categories stop a running activity.</summary>
public sealed record SetInterruptStops(IReadOnlyList<InterruptCategory> Categories) : OverworldCommand;
