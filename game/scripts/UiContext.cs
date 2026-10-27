using Core.Content;
using Core.Loc;
using Core.Sim;

/// <summary>
/// What every part of the screen needs: content for names and map points, the localizer, and the
/// one way to act — sending a command to Core.
/// </summary>
public sealed record UiContext(ContentDb Db, Localizer Loc, Action<Command> Send)
{
    public string Text(LocKey key) => Loc.Resolve(key);

    public string Text(string key) => Loc.Resolve(new LocKey(key));

    public string Lines(IEnumerable<LocKey> keys) => string.Join("\n", keys.Select(Loc.Resolve));
}

/// <summary>Something the player picked on the map; the point panel shows it.</summary>
public abstract record MapTarget;

public sealed record ZoneTarget(string ZoneId) : MapTarget;

public sealed record PlaceTarget(string PlaceId) : MapTarget;

public sealed record ShopTarget(string ShopId) : MapTarget;

public sealed record InquireTarget : MapTarget;
