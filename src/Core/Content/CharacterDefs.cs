namespace Core.Content;

/// <summary>A character stat (<c>stat.might</c>, §2.1). Body stats split into aspects; Perception has none.</summary>
public sealed class StatDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    public double StartValue { get; set; }

    /// <summary>
    /// When set, every gain shrinks as the stat grows: gain × 1 / (1 + value / scale). Perception
    /// grows this way (§2.1).
    /// </summary>
    public double? DiminishScale { get; set; }

    [ContentRef(typeof(AspectDef))]
    public List<string> Aspects { get; set; } = [];

    /// <summary>Ranks with a scale and an overflow to break through (§2.1, D48); null for a plain number.</summary>
    [ContentRef(typeof(StatProgressDef))]
    public string? Progress { get; set; }

    /// <summary>
    /// What wears the stat away each week (D49): a resource's current amount or an injury's value →
    /// points lost per unit. Purity erodes this way under toxins and injuries.
    /// </summary>
    [ContentRef(typeof(ContentDef))]
    public Dictionary<string, double> Erosion { get; set; } = [];

    public IEnumerable<string> Validate(ContentDb db)
    {
        foreach (var (source, perUnit) in Erosion.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (db.Contains(source) && !db.TryGet<ResourceDef>(source, out _) && !db.TryGet<InjuryDef>(source, out _))
                yield return $"erosion source '{source}' is neither a resource nor an injury.";
            if (perUnit <= 0)
                yield return $"erosion by '{source}' must be positive.";
        }
        if (Progress is not null && db.TryGet<StatProgressDef>(Progress, out var progress) && progress.Ranks.All(r => db.TryGet<StatRankDef>(r, out _))
            && StartValue > progress.Ranks.Sum(r => db.Get<StatRankDef>(r).Pool))
            yield return $"start_value is above the top of the last rank of '{Progress}'.";
    }
}

/// <summary>
/// How a ranked stat grows (<c>stat_progress.body</c>, §2.1, D48). Within a rank, points fill its
/// pool and raise the stat; points past the full pool are overflow, which fades each week by
/// <see cref="OverflowDecayPerWeek"/> of itself. Enough overflow breaks through to the next rank:
/// its scale starts empty from where the last one ended, and the overflow is gone.
/// With <see cref="DeficitHealPerWeek"/> the scale mirrors downwards (D49): losses past the rank's
/// base are a deficit, which heals the same way; enough deficit collapses the stat to the rank below,
/// whose scale is then full, and the deficit is gone. Without it a rank's base is a floor.
/// Without <see cref="OverflowDecayPerWeek"/> there is neither overflow nor breakthroughs (D50): the
/// stat just moves along the scales, and its rank is whichever scale it stands on.
/// </summary>
public sealed class StatProgressDef : ContentDef, IValidatedDef
{
    /// <summary>Ranks from the lowest; a rank's base is the sum of the pools below it.</summary>
    [ContentRef(typeof(StatRankDef))]
    public List<string> Ranks { get; set; } = [];

    /// <summary>Share of the overflow lost each week, so the more there is, the faster it goes; null for no overflow.</summary>
    public double? OverflowDecayPerWeek { get; set; }

    /// <summary>Share of the deficit healed each week; null when losses stop at the rank's base.</summary>
    public double? DeficitHealPerWeek { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (Ranks.Count == 0)
            yield return "needs at least one rank.";
        if (OverflowDecayPerWeek is < 0 or > 1 || DeficitHealPerWeek is < 0 or > 1)
            yield return "overflow_decay_per_week and deficit_heal_per_week must be within [0, 1].";
        if (Ranks.Count == 0 || !Ranks.All(r => db.TryGet<StatRankDef>(r, out _)))
            yield break;
        var ranks = Ranks.Select(db.Get<StatRankDef>).ToList();
        if (ranks.Any(r => r.Pool <= 0))
            yield return "every rank needs a positive pool.";
        if (OverflowDecayPerWeek is null)
        {
            if (DeficitHealPerWeek is not null || ranks.Any(r => r.Breakthrough is not null || r.Collapse is not null))
                yield return "without overflow_decay_per_week ranks just follow the scale: no breakthrough, collapse or deficit_heal_per_week.";
            yield break;
        }
        if (ranks.SkipLast(1).Any(r => r.Breakthrough is not > 0))
            yield return "every rank but the last needs a positive breakthrough.";
        if (ranks[^1].Breakthrough is not null)
            yield return "the last rank has nowhere to break through to.";
        if (ranks[0].Collapse is not null)
            yield return "the first rank has nowhere to collapse to.";
        if (DeficitHealPerWeek is null && ranks.Any(r => r.Collapse is not null))
            yield return "collapse needs deficit_heal_per_week.";
        if (DeficitHealPerWeek is not null && ranks.Skip(1).Any(r => r.Collapse is not > 0))
            yield return "with a deficit, every rank but the first needs a positive collapse.";
    }
}

/// <summary>One rank of a ranked stat (<c>stat_rank.body.1</c>).</summary>
public sealed class StatRankDef : ContentDef
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Points the rank's scale holds.</summary>
    public double Pool { get; set; }

    /// <summary>Overflow past the full pool that breaks through to the next rank; null for the last rank.</summary>
    public double? Breakthrough { get; set; }

    /// <summary>Deficit past the rank's base that collapses it to the rank below (D49); null for the first rank.</summary>
    public double? Collapse { get; set; }
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

    /// <summary>Starts a run at zero instead of full: toxins build up towards their maximum, the tolerance.</summary>
    public bool StartEmpty { get; set; }

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
