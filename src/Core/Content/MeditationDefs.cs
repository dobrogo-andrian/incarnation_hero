namespace Core.Content;

/// <summary>
/// A meditation direction (<c>meditation.purification</c>, §4.2). Each week it adds
/// <see cref="Rate"/> × place density × efficiency to <see cref="Target"/>: a stat's base value or
/// a resource's current amount. Directions with other effects (comprehension, assimilation) will
/// add their own handling when they arrive.
/// </summary>
public sealed class MeditationDirectionDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    public double Rate { get; set; }

    [ContentRef(typeof(ContentDef))]
    public string Target { get; set; } = "";

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (db.Contains(Target) && !db.TryGet<StatDef>(Target, out _) && !db.TryGet<ResourceDef>(Target, out _))
            yield return $"target '{Target}' is neither a stat nor a resource.";
        if (Rate <= 0)
            yield return "needs a positive rate.";
    }
}
