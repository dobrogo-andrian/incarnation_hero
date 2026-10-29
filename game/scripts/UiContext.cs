using Core.Content;
using Core.Loc;
using Core.Sim;

/// <summary>
/// What every part of the screen needs: content for names and map points, the localizer, the
/// one way to act — sending a command to Core — and the player's last picks in the pickers.
/// </summary>
public sealed record UiContext(ContentDb Db, Localizer Loc, Action<Command> Send)
{
    public Choices Choices { get; } = new();

    public UiSettings Settings { get; } = UiSettings.Load();

    public string Text(LocKey key) => Loc.Resolve(key);

    public string Text(string key) => Loc.Resolve(new LocKey(key));

    public string Lines(IEnumerable<LocKey> keys) => string.Join("\n", keys.Select(Loc.Resolve));

    public string Name(string contentId) => Loc.Resolve(SimText.Name(contentId));
}

/// <summary>
/// The player's picks, kept while panels and tabs are rebuilt from each new PlayerView. Only
/// presentation state: Core never sees it, and a pick that is no longer offered falls back to the first option.
/// </summary>
public sealed class Choices
{
    public string? Direction { get; set; }

    public string? Subject { get; set; }

    public int Weeks { get; set; } = 4;

    public bool UntilDone { get; set; }

    public int Stones { get; set; } = 5;

    public string? BreakthroughPlace { get; set; }

    public bool AcceptDeathRisk { get; set; }

    /// <summary>The picked rank of arena opponents, by its index in the arena's bouts.</summary>
    public int Bout { get; set; }

    /// <summary>The picked activity length: null runs until the activity ends or is interrupted.</summary>
    public int? ActivityWeeks => UntilDone ? null : Weeks;
}

/// <summary>Something the player picked on the map; the point panel shows it.</summary>
public abstract record MapTarget;

public sealed record ZoneTarget(string ZoneId) : MapTarget;

public sealed record PlaceTarget(string PlaceId) : MapTarget;

public sealed record ShopTarget(string ShopId) : MapTarget;

public sealed record InquireTarget : MapTarget;

public sealed record HuntTarget(string HuntId) : MapTarget;

public sealed record ArenaTarget(string ArenaId) : MapTarget;

/// <summary>The settlement's job board, with every job of the zone.</summary>
public sealed record JobsTarget : MapTarget;
