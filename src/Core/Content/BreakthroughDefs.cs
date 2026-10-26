namespace Core.Content;

/// <summary>
/// How breakthroughs are judged (<c>config.breakthrough</c>, §4.5, D35). Exactly one per content set.
/// Quality = base + purity (up to the method's purity cap) − toxins − injuries − age − delay since
/// the scale filled, clamped to [0, ceiling]; the rank is the highest whose minimum it reaches.
/// </summary>
public sealed class BreakthroughConfigDef : ContentDef, IValidatedDef
{
    public const string BreakthroughId = "config.breakthrough";

    /// <summary>Quality ranks from worst to best; per-rank tables of stages follow this order.</summary>
    [ContentRef(typeof(QualityRankDef))]
    public List<string> Ranks { get; set; } = [];

    public double BaseQuality { get; set; }

    /// <summary>Quality per point of effective purity, counted only up to the purity cap.</summary>
    public double QualityPerPurity { get; set; }

    /// <summary>Quality lost per point of toxins left in the body (§4.4).</summary>
    public double QualityPerToxin { get; set; }

    /// <summary>Quality lost per year of age above <see cref="AgePenaltyFromYears"/> (§3).</summary>
    public double QualityPerAgeYear { get; set; }

    public int AgePenaltyFromYears { get; set; }

    /// <summary>Quality lost per year between the scale filling and the breakthrough (§4.5).</summary>
    public double QualityPerDelayYear { get; set; }

    /// <summary>Quality ceiling and purity cap without an active cultivation method.</summary>
    public double NoMethodQualityCeiling { get; set; }

    public double NoMethodPurityCap { get; set; }

    /// <summary>Stats whose base grows by the stage's <see cref="StageBreakthroughDef.StatGain"/> on success.</summary>
    [ContentRef(typeof(StatDef))]
    public List<string> GainStats { get; set; } = [];

    /// <summary>
    /// Bands of the foundation deficit (a share of the threshold), from the mildest. Within a band
    /// the failure chance grows linearly; the last band must reach a deficit of 1.
    /// </summary>
    [ContentRef(typeof(FailureZoneDef))]
    public List<string> FailureZones { get; set; } = [];

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (Ranks.Count == 0)
            yield return "needs at least one quality rank.";
        if (Ranks.All(id => db.TryGet<QualityRankDef>(id, out _)))
        {
            var mins = Ranks.Select(id => db.Get<QualityRankDef>(id).MinQuality).ToList();
            if (mins.Count > 0 && (mins[0] != 0 || mins.Zip(mins.Skip(1)).Any(p => p.Second <= p.First)))
                yield return "ranks need min_quality starting at 0 and growing.";
        }
        if (!FailureZones.All(id => db.TryGet<FailureZoneDef>(id, out _)))
            yield break;

        var zones = FailureZones.Select(db.Get<FailureZoneDef>).ToList();
        if (zones.Count == 0 || zones[^1].UpTo != 1 || zones[0].Fatal)
            yield return "failure_zones must start with a non-fatal zone and end with up_to: 1.";
        double previous = 0;
        foreach (var zone in zones)
        {
            if (zone.UpTo <= previous)
                yield return "failure_zones need growing up_to.";
            previous = zone.UpTo;
        }
    }
}

/// <summary>A breakthrough quality rank (<c>rank.good</c>); reached when quality is at least <see cref="MinQuality"/>.</summary>
public sealed class QualityRankDef : ContentDef
{
    public LocalizedText Name { get; set; } = new();

    public double MinQuality { get; set; }
}

/// <summary>
/// A band of foundation deficit and what failing in it costs (§4.5): a share of the stage scale,
/// toxins, injuries (the internal injury of §4.5) and a share of base purity. A <see cref="Fatal"/> band kills instead, and only an attempt
/// that accepted the risk may enter it.
/// </summary>
public sealed class FailureZoneDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Upper end of the band as a share of the threshold; the band starts where the previous one ends.</summary>
    public double UpTo { get; set; }

    /// <summary>Failure chance at the start and at the end of the band.</summary>
    public double ChanceFrom { get; set; }

    public double ChanceTo { get; set; }

    public double ScaleLoss { get; set; }

    public double Toxins { get; set; }

    public double PurityLoss { get; set; }

    [ContentRef(typeof(InjuryDef))]
    public Dictionary<string, double> Injuries { get; set; } = [];

    public bool Fatal { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (ChanceFrom is < 0 or > 1 || ChanceTo is < 0 or > 1)
            yield return "chances must be within [0, 1].";
        if (ScaleLoss is < 0 or > 1 || PurityLoss is < 0 or > 1 || Toxins < 0 || Injuries.Values.Any(i => i <= 0))
            yield return "scale_loss and purity_loss must be within [0, 1], toxins non-negative, injuries positive.";
    }
}

/// <summary>
/// The breakthrough out of a stage into the next one (§4.5): the foundation it needs, the weeks of
/// seclusion it takes, and per quality rank the years of life and the stat gain it gives.
/// </summary>
public sealed class StageBreakthroughDef
{
    public double Threshold { get; set; }

    public int Weeks { get; set; }

    public List<double> LifespanYears { get; set; } = [];

    public List<double> StatGain { get; set; } = [];
}

/// <summary>
/// What a cultivation method gives as the active one (§6, D35): the aspect profile (A2), and a
/// quality ceiling and purity cap that grow with the method's level.
/// </summary>
public sealed class MethodDef
{
    [ContentRef(typeof(AspectProfileDef))]
    public string AspectProfile { get; set; } = "";

    public double QualityCeiling { get; set; }

    public double QualityCeilingPerLevel { get; set; }

    public double PurityCap { get; set; }

    public double PurityCapPerLevel { get; set; }
}
