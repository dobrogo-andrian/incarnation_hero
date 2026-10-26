namespace Core.Content;

/// <summary>
/// Something comprehension raises the level of (§6): a technique (<c>tech.spear.piercing_dragon</c>),
/// an element (<c>element.fire</c>) or a cultivation method (<c>method.azure_breath</c>). A method
/// also says what it gives as the active one (<see cref="Method"/>); techniques get their effects with combat.
/// </summary>
public sealed class KnowledgeDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Category tags for soul resonance (<c>weapon:spear</c>, <c>element:fire</c>).</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Elements can be sensed without a folio, up to <see cref="RunConfigDef.ElementBasicMaxLevel"/> (§6).</summary>
    public bool IsElement => Id.StartsWith("element.", StringComparison.Ordinal);

    public bool IsMethod => Id.StartsWith("method.", StringComparison.Ordinal);

    /// <summary>Required on a cultivation method, forbidden elsewhere.</summary>
    public MethodDef? Method { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if ((Method is null) == IsMethod)
            yield return IsMethod ? "a cultivation method needs a method block." : "only a cultivation method has a method block.";
        if (Method is { } m && (m.QualityCeiling <= 0 || m.QualityCeilingPerLevel < 0 || m.PurityCap < 0 || m.PurityCapPerLevel < 0))
            yield return "method needs a positive quality_ceiling and non-negative per-level growth and purity cap.";
    }
}

/// <summary>
/// A folio (§6): makes <see cref="Subject"/> studiable up to <see cref="MaxLevel"/>, at
/// <see cref="Speed"/> times the usual pace. Studying does not use it up.
/// </summary>
public sealed class FolioDef
{
    [ContentRef(typeof(KnowledgeDef))]
    public string Subject { get; set; } = "";

    public int MaxLevel { get; set; }

    public double Speed { get; set; } = 1;
}
