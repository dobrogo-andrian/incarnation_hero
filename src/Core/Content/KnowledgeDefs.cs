namespace Core.Content;

/// <summary>
/// Something comprehension raises the level of (§6): a technique (<c>tech.spear.piercing_dragon</c>),
/// an element (<c>element.fire</c>) or a cultivation method (<c>method.azure_breath</c>). For now
/// only the level matters; combat and breakthroughs give techniques and methods their effects later.
/// </summary>
public sealed class KnowledgeDef : ContentDef
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Category tags for soul resonance (<c>weapon:spear</c>, <c>element:fire</c>).</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Elements can be sensed without a folio, up to <see cref="RunConfigDef.ElementBasicMaxLevel"/> (§6).</summary>
    public bool IsElement => Id.StartsWith("element.", StringComparison.Ordinal);
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
