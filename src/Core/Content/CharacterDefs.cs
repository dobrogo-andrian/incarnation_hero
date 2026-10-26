namespace Core.Content;

/// <summary>A character stat (<c>stat.might</c>, §2.1). Body stats split into aspects; Perception has none.</summary>
public sealed class StatDef : ContentDef
{
    public LocalizedText Name { get; set; } = new();

    public double StartValue { get; set; }

    [ContentRef(typeof(AspectDef))]
    public List<string> Aspects { get; set; } = [];
}

/// <summary>One of a body stat's aspects (<c>aspect.might.vitality</c>, §2.1).</summary>
public sealed class AspectDef : ContentDef
{
    public LocalizedText Name { get; set; } = new();
}

/// <summary>
/// How each body stat divides between its aspects (§2.1, A2). Zero-sum: a stat's aspect shares add
/// up to 1, and an aspect is worth stat × aspect count × share, so an even split gives each the stat.
/// </summary>
public sealed class AspectProfileDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    [ContentRef(typeof(AspectDef))]
    public Dictionary<string, double> Shares { get; set; } = [];

    public IEnumerable<string> Validate(ContentDb db)
    {
        foreach (var stat in db.All<StatDef>().Where(s => s.Aspects.Count > 0))
        {
            var missing = stat.Aspects.Where(a => !Shares.ContainsKey(a)).ToList();
            if (missing.Count > 0)
            {
                yield return $"has no share for {string.Join(", ", missing)}.";
                continue;
            }

            double sum = stat.Aspects.Sum(a => Shares[a]);
            if (Math.Abs(sum - 1) > 1e-9)
                yield return FormattableString.Invariant($"shares of '{stat.Id}' aspects add up to {sum}, not 1.");
        }
    }
}

/// <summary>
/// A character resource (<c>resource.health</c>, §2.2). Its maximum is
/// <see cref="Base"/> + <see cref="Coefficient"/> × the effective value of <see cref="Source"/>.
/// </summary>
public sealed class ResourceDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    public double Base { get; set; }

    public double Coefficient { get; set; }

    /// <summary>How much returns on its own each week, up to the maximum (§13.4 for qi).</summary>
    public double RegenPerWeek { get; set; }

    /// <summary>A stat or aspect the maximum grows with; null for a flat maximum.</summary>
    [ContentRef(typeof(ContentDef))]
    public string? Source { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (Source is not null && db.Contains(Source) && !db.TryGet<StatDef>(Source, out _) && !db.TryGet<AspectDef>(Source, out _))
            yield return $"source '{Source}' is neither a stat nor an aspect.";
    }
}
