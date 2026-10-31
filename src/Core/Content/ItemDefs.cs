namespace Core.Content;

/// <summary>Something the player can carry (<c>item.pill.qi_gathering</c>): a pill or a folio.</summary>
public sealed class ItemDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    public PillDef? Pill { get; set; }

    public FolioDef? Folio { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if ((Pill is null) == (Folio is null))
            yield return "must be exactly one of: pill, folio.";
        if (Pill is not null && (Pill.Energy < 0 || Pill.CeilingBonus < 0 || Pill.Heals.Values.Any(h => h <= 0) || Pill.Stats.Values.Any(s => s <= 0) || Pill.Restores.Values.Any(r => r <= 0)
            || Pill.Energy + Pill.CeilingBonus <= 0 && Pill.Heals.Count == 0 && Pill.Stats.Count == 0 && Pill.Restores.Count == 0 || Pill.Toxicity < 0 || Pill.RepeatFactor is <= 0 or > 1))
            yield return "pill needs energy, a ceiling_bonus, heals, stats or restores (none negative), non-negative toxicity and a repeat_factor in (0, 1].";
        if (Folio is not null && (Folio.MaxLevel <= 0 || Folio.Speed <= 0))
            yield return "folio needs a positive max_level and speed.";
    }
}

/// <summary>
/// A pill's effect (§4.3–4.4): <see cref="Energy"/> pill-energy points, shrinking by
/// <see cref="RepeatFactor"/> for every earlier pill of the same type, and the same
/// <see cref="Toxicity"/> every time. A breakthrough pill raises the quality ceiling of the next
/// breakthrough by <see cref="CeilingBonus"/> instead (§4.5); several do not add up. A healing pill
/// takes <see cref="Heals"/> points off injury scales at once (§8).
/// </summary>
public sealed class PillDef
{
    public double Energy { get; set; }

    public double CeilingBonus { get; set; }

    [ContentRef(typeof(InjuryDef))]
    public Dictionary<string, double> Heals { get; set; } = [];

    /// <summary>Stat points the pill gives (D49), shrinking by the repeat factor like its energy.</summary>
    [ContentRef(typeof(StatDef))]
    public Dictionary<string, double> Stats { get; set; } = [];

    /// <summary>Resource points the pill refills at once, never past the maximum: the qi pool (D18).</summary>
    [ContentRef(typeof(ResourceDef))]
    public Dictionary<string, double> Restores { get; set; } = [];

    public double Toxicity { get; set; }

    public double RepeatFactor { get; set; }
}

/// <summary>A shop in a zone (<c>shop.hub.apothecary</c>, §13.3) selling items at fixed prices in spirit stones.</summary>
public sealed class ShopDef : ContentDef, IValidatedDef, IDescribedDef
{
    public LocalizedText Name { get; set; } = new();

    [ContentRef(typeof(ZoneDef))]
    public string Zone { get; set; } = "";

    [ContentRef(typeof(ItemDef))]
    public Dictionary<string, int> Prices { get; set; } = [];

    /// <summary>Its point on the settlement's local map (D44).</summary>
    public MapPoint? Map { get; set; }

    /// <summary>What the player sees on entering it (D44).</summary>
    public LocalizedText? Description { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (Prices.Values.Any(p => p <= 0))
            yield return "prices must be positive.";
    }
}

/// <summary>Spirit stone rates and the starting purse (<c>config.economy</c>, §13, tech 4.7). Exactly one per content set.</summary>
public sealed class EconomyConfigDef : ContentDef, IValidatedDef
{
    public const string EconomyId = "config.economy";

    public int StartStones { get; set; }

    [ContentRef(typeof(ItemDef))]
    public Dictionary<string, int> StartItems { get; set; } = [];

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (StartStones < 0 || StartItems.Values.Any(c => c <= 0))
            yield return "starting stones must be non-negative and item counts positive.";
    }
}
