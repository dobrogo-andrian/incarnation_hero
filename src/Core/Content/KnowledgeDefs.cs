namespace Core.Content;

/// <summary>
/// Something comprehension raises the level of (§6): a technique (<c>tech.spear.piercing_dragon</c>),
/// an element (<c>element.fire</c>) or a cultivation method (<c>method.azure_breath</c>). A method
/// also says what it gives as the active one (<see cref="Method"/>); a technique's level adds to combat damage.
/// </summary>
public sealed class KnowledgeDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Category tags for soul resonance (<c>weapon:spear</c>, <c>element:fire</c>).</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Elements can be sensed without a folio, up to <see cref="RunConfigDef.ElementBasicMaxLevel"/> (§6).</summary>
    public bool IsElement => Id.StartsWith("element.", StringComparison.Ordinal);

    public bool IsMethod => Id.StartsWith("method.", StringComparison.Ordinal);

    /// <summary>Its level adds to combat damage (D42).</summary>
    public bool IsTechnique => Id.StartsWith("tech.", StringComparison.Ordinal);

    /// <summary>Required on a cultivation method, forbidden elsewhere.</summary>
    public MethodDef? Method { get; set; }

    /// <summary>What the technique does on the hex field (D56); only techniques have it.</summary>
    public TechniqueCombatDef? Combat { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if ((Method is null) == IsMethod)
            yield return IsMethod ? "a cultivation method needs a method block." : "only a cultivation method has a method block.";
        if (Combat is not null && !IsTechnique)
            yield return "only a technique has a combat block.";
        if (Combat is { } c && (!TechniqueCombatDef.Shapes.Contains(c.Shape) || c.Range < 1 || c.Damage <= 0 || c.Qi < 0 || c.Energy < 0))
            yield return $"combat needs a shape ({string.Join(", ", TechniqueCombatDef.Shapes)}), a range of at least 1, positive damage and qi of zero or more.";
        if (Method is { } m && (m.QualityCeiling <= 0 || m.QualityCeilingPerLevel < 0 || m.PurityCap < 0 || m.PurityCapPerLevel < 0))
            yield return "method needs a positive quality_ceiling and non-negative per-level growth and purity cap.";
    }
}

/// <summary>
/// A technique on the hex field (D56): one of a fixed set of shapes, each a branch in C#
/// (<c>Battle</c>), with its numbers here. Damage = weapon × (1 + level × technique bonus) ×
/// <see cref="Damage"/> × qi density. It costs combat qi, the same qi that holds the shield, and body
/// energy, which pays for its own movement too (D55).
/// </summary>
public sealed class TechniqueCombatDef
{
    public const string Line = "line";
    public const string Arc = "arc";
    public const string Charge = "charge";
    public const string Retreat = "retreat";

    /// <summary>
    /// <c>line</c>: every enemy on <see cref="Range"/> hexes straight out from an adjacent hex;
    /// <c>arc</c>: the adjacent hex and its two neighbours beside the fighter;
    /// <c>charge</c>: a dash up to <see cref="Range"/> free hexes along a straight line to strike the enemy at its end;
    /// <c>retreat</c>: strike an adjacent enemy, then leap up to <see cref="Range"/> hexes straight away from it.
    /// Charge and retreat move without drawing free attacks (D54).
    /// </summary>
    public static readonly IReadOnlyList<string> Shapes = [Line, Arc, Charge, Retreat];

    public string Shape { get; set; } = "";
    public int Range { get; set; } = 1;

    /// <summary>Multiplier on the technique's damage.</summary>
    public double Damage { get; set; } = 1;

    /// <summary>Combat qi it costs.</summary>
    public double Qi { get; set; }

    /// <summary>Body energy it costs.</summary>
    public double Energy { get; set; }
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
