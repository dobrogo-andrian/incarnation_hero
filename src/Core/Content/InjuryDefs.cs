namespace Core.Content;

/// <summary>
/// A scale of general injuries (<c>injury.body</c>, §8, tech 4.6, D37). Every point weakens the
/// character through <see cref="Effects"/>, takes breakthrough quality and foundation away, and the
/// scale heals by itself each week, faster while resting.
/// </summary>
public sealed class InjuryDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>The scale never goes above this.</summary>
    public double Max { get; set; }

    /// <summary>Points healed each week, whatever the player does.</summary>
    public double HealPerWeek { get; set; }

    /// <summary>Points healed each week of rest on top of <see cref="HealPerWeek"/>.</summary>
    public double RestHealPerWeek { get; set; }

    /// <summary>
    /// Stats, aspects or resources (for a resource, its maximum) changed per point of injury, as a
    /// Mul modifier: -0.005 takes 0.5% away per point.
    /// </summary>
    [ContentRef(typeof(ContentDef))]
    public Dictionary<string, double> Effects { get; set; } = [];

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (Max <= 0 || HealPerWeek < 0 || RestHealPerWeek < 0)
            yield return "needs a positive max and non-negative healing.";
        foreach (var (target, perPoint) in Effects.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (db.Contains(target) && !db.TryGet<StatDef>(target, out _) && !db.TryGet<AspectDef>(target, out _) && !db.TryGet<ResourceDef>(target, out _))
                yield return $"effect target '{target}' is not a stat, an aspect or a resource.";
            if (1 + perPoint * Max <= 0)
                yield return $"effect on '{target}' would take away everything at max.";
        }
    }
}
