namespace Core.Content;

/// <summary>
/// A meditation direction (<c>meditation.purification</c>, §4.2). Each week it adds
/// <see cref="Rate"/> × place density × efficiency to <see cref="Target"/>: a stat's base value,
/// a resource's current amount, or a cultivation vector's stage scale, which is assimilation and
/// spends cultivation buffs (§4.3). Comprehension adds its own handling with folios.
/// </summary>
public sealed class MeditationDirectionDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    public double Rate { get; set; }

    [ContentRef(typeof(ContentDef))]
    public string Target { get; set; } = "";

    /// <summary>
    /// A resource the gain drains first, <see cref="PurgeRatio"/> units per unit of gain; only what is
    /// left over reaches <see cref="Target"/>. Purification flushes toxins this way (§4.2).
    /// </summary>
    [ContentRef(typeof(ResourceDef))]
    public string? Purges { get; set; }

    public double PurgeRatio { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (db.Contains(Target) && !db.TryGet<StatDef>(Target, out _) && !db.TryGet<ResourceDef>(Target, out _)
            && !db.TryGet<CultivationVectorDef>(Target, out _))
            yield return $"target '{Target}' is not a stat, a resource or a cultivation vector.";
        if (Rate <= 0)
            yield return "needs a positive rate.";
        if (Purges is not null && PurgeRatio <= 0)
            yield return "needs a positive purge_ratio to purge.";
    }
}
