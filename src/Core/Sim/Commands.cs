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

/// <summary>
/// Starts <paramref name="Activity"/> (replacing any current one) and runs weeks until it ends,
/// a stopping interrupt fires or the run ends. Null <paramref name="Weeks"/> means no limit.
/// </summary>
public sealed record StartActivity(Activity Activity, int? Weeks) : OverworldCommand;

/// <summary>Runs the interrupted activity further.</summary>
public sealed record ContinueActivity : OverworldCommand;

public sealed record CancelActivity : OverworldCommand;

/// <summary>Travels to <paramref name="ZoneId"/> by the fastest route (replacing any current activity).</summary>
public sealed record Travel(string ZoneId) : OverworldCommand;

/// <summary>Chooses which interrupt categories stop a running activity.</summary>
public sealed record SetInterruptStops(IReadOnlyList<InterruptCategory> Categories) : OverworldCommand;
