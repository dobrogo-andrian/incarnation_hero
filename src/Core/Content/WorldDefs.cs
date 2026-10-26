namespace Core.Content;

/// <summary>A zone of the region (<c>zone.hub</c>, §9). Danger grows away from the hub.</summary>
public sealed class ZoneDef : ContentDef
{
    public LocalizedText Name { get; set; } = new();

    public int Danger { get; set; }
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

    /// <summary>Qi density: how productive meditation here is while the reserve lasts.</summary>
    public double Density { get; set; }

    public double Capacity { get; set; }

    public double RegenPerWeek { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (Density <= 0 || Capacity <= 0)
            yield return "needs a positive density and capacity.";
        if (RegenPerWeek < 0)
            yield return "cannot have negative regeneration.";
    }
}
