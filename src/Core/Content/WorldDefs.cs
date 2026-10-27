namespace Core.Content;

/// <summary>A zone of the region (<c>zone.hub</c>, §9). Danger grows away from the hub.</summary>
public sealed class ZoneDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    public int Danger { get; set; }

    /// <summary>A settlement: any activity here rolls settlement events (D38).</summary>
    public bool Settlement { get; set; }

    /// <summary>Unknown until an event or rumor reveals it; routes do not lead through it before (D40).</summary>
    public bool Hidden { get; set; }

    /// <summary>Where the zone sits on the region map (D44).</summary>
    public MapPoint? Map { get; set; }

    /// <summary>A settlement's own map screen (D44); its places and shops carry their own points.</summary>
    public LocalMapDef? LocalMap { get; set; }

    /// <summary>
    /// Meditation needs a place (D30), so every zone offers at least one. Every zone is drawn on the
    /// region map, and a settlement also on its local map, with every place and shop on it.
    /// </summary>
    public IEnumerable<string> Validate(ContentDb db)
    {
        if (!db.All<PlaceDef>().Any(p => p.Zone == Id))
            yield return "has no meditation place.";
        if (Map is null)
            yield return "needs a point on the region map (map).";
        if (!Settlement)
            yield break;
        if (LocalMap is not { Inquire: not null, Exit: not null })
            yield return "is a settlement, so it needs a local_map with inquire and exit points.";
        var unplaced = db.All<PlaceDef>().Where(p => p.Zone == Id && p.Map is null).Select(p => p.Id)
            .Concat(db.All<ShopDef>().Where(s => s.Zone == Id && s.Map is null).Select(s => s.Id));
        foreach (string id in unplaced)
            yield return $"is a settlement, so '{id}' needs a point on its local map (map).";
    }
}

/// <summary>A point on a static map screen (D44), in percent of the map's width and height.</summary>
public sealed class MapPoint : IValidatedDef
{
    public double X { get; set; }

    public double Y { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (X is < 0 or > 100 || Y is < 0 or > 100)
            yield return "map point must lie within 0..100.";
    }
}

/// <summary>A settlement's local map (D44): the points that are actions rather than places or shops.</summary>
public sealed class LocalMapDef
{
    /// <summary>Where to ask around for rumors (D41).</summary>
    public MapPoint? Inquire { get; set; }

    /// <summary>The way out to the region map.</summary>
    public MapPoint? Exit { get; set; }
}

/// <summary>A two-way passage between zones that takes <see cref="Weeks"/> to cross (§9).</summary>
public sealed class RouteDef : ContentDef, IValidatedDef
{
    [ContentRef(typeof(ZoneDef))]
    public string A { get; set; } = "";

    [ContentRef(typeof(ZoneDef))]
    public string B { get; set; } = "";

    public int Weeks { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (A == B)
            yield return "connects a zone to itself.";
        if (Weeks < 1)
            yield return "must take at least one week.";
    }
}

/// <summary>
/// A meditation place in a zone (§4.2, §9). Its qi reserve is drained by meditation and refills by
/// <see cref="RegenPerWeek"/> up to <see cref="Capacity"/>; spirit stones never push it higher (D19).
/// </summary>
public sealed class PlaceDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    [ContentRef(typeof(ZoneDef))]
    public string Zone { get; set; } = "";

    /// <summary>
    /// Qi density: how productive meditation here is while the reserve lasts. Each week of
    /// meditation also draws this much from the reserve (D30).
    /// </summary>
    public double Density { get; set; }

    public double Capacity { get; set; }

    public double RegenPerWeek { get; set; }

    /// <summary>Unknown, so unusable, until revealed (D40).</summary>
    public bool Hidden { get; set; }

    /// <summary>Its point on a settlement's local map (D44); wild zones have no local map.</summary>
    public MapPoint? Map { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (Density <= 0 || Capacity <= 0)
            yield return "needs a positive density and capacity.";
        if (RegenPerWeek < 0)
            yield return "cannot have negative regeneration.";
    }
}
