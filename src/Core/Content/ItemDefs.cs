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
        if (Pill is not null && (Pill.Energy < 0 || Pill.CeilingBonus < 0 || Pill.Heals.Values.Any(h => h <= 0)
            || Pill.Energy + Pill.CeilingBonus <= 0 && Pill.Heals.Count == 0 || Pill.Toxicity < 0 || Pill.RepeatFactor is <= 0 or > 1))
            yield return "pill needs energy, a ceiling_bonus or heals (none negative), non-negative toxicity and a repeat_factor in (0, 1].";
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

    public double Toxicity { get; set; }

    public double RepeatFactor { get; set; }
}

/// <summary>A shop in a zone (<c>shop.hub.apothecary</c>, §13.3) selling items at fixed prices in spirit stones.</summary>
public sealed class ShopDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    [ContentRef(typeof(ZoneDef))]
    public string Zone { get; set; } = "";

    [ContentRef(typeof(ItemDef))]
    public Dictionary<string, int> Prices { get; set; } = [];

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

    /// <summary>The resource stones refill: the qi pool (D18).</summary>
    [ContentRef(typeof(ResourceDef))]
    public string StoneRestores { get; set; } = "";

    /// <summary>Points of <see cref="StoneRestores"/> one stone refills.</summary>
    public double QiPerStone { get; set; }

    /// <summary>Highest zone danger where stones may restore the qi pool; dangerous zones forbid it (§13.4).</summary>
    public int StoneRestoreMaxDanger { get; set; }

    /// <summary>Meditation place reserve one stone refills, up to the place's capacity (D19).</summary>
    public double PlaceReservePerStone { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (QiPerStone <= 0 || PlaceReservePerStone <= 0)
            yield return "stone rates must be positive.";
        if (StartStones < 0 || StartItems.Values.Any(c => c <= 0))
            yield return "starting stones must be non-negative and item counts positive.";
    }
}
