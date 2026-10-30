namespace Core.Content;

/// <summary>
/// A rumor template (§10, tech 4.3, D41): an opportunity in a zone for a limited window of weeks.
/// The player sees its claim (<see cref="Text"/>); what is really there is one of its nodes, picked
/// when it is heard: <see cref="Truth"/> with the source's reliability, otherwise a weighted
/// falsehood. Arriving in the zone in time opens that node as a dialog, or <see cref="TakenNode"/>
/// if rivals got there first.
/// </summary>
public sealed class RumorDef : ContentDef, IDialogDef, IValidatedDef
{
    public const string TakenNode = "taken";

    /// <summary>Short title in the list of rumors.</summary>
    public LocalizedText Name { get; set; } = new();

    /// <summary>The claim: what the source says is there, with a hint of reward and risk.</summary>
    public LocalizedText Text { get; set; } = new();

    [ContentRef(typeof(ZoneDef))]
    public string Zone { get; set; } = "";

    /// <summary>Weeks it stays open after it is heard, rolled between min and max.</summary>
    public NumberRange WindowWeeks { get; set; } = new();

    /// <summary>How attractive the reward is to rivals: the weekly interception chance grows with it.</summary>
    public double Appeal { get; set; }

    /// <summary>Relative chance among the rumors a settlement can bring.</summary>
    public double Weight { get; set; }

    /// <summary>Weeks after it was heard before the settlement can bring it again.</summary>
    public int CooldownWeeks { get; set; }

    /// <summary>All must hold for the settlement to bring it; <c>add_rumor</c> ignores them.</summary>
    public List<EventConditionDef> Conditions { get; set; } = [];

    /// <summary>The node where the claim turns out true.</summary>
    public string Truth { get; set; } = "";

    /// <summary>Node → weight of what is there instead when the claim is false (exaggerated, another reward, a trap).</summary>
    public Dictionary<string, double> Falsehoods { get; set; } = [];

    public Dictionary<string, EventNodeDef> Nodes { get; set; } = [];

    /// <summary>Nodes the dialog can open at, in a fixed order.</summary>
    public IReadOnlyList<string> Entries() =>
        [Truth, .. Falsehoods.Keys.Order(StringComparer.Ordinal), TakenNode];

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (WindowWeeks is not { Min: double min, Max: double max } || min < 1 || max < min || min != Math.Floor(min) || max != Math.Floor(max))
            yield return "window_weeks needs whole min and max, 1 <= min <= max.";
        if (Appeal < 0 || Weight <= 0 || CooldownWeeks < 0)
            yield return "needs an appeal of zero or more, a positive weight and a cooldown of zero or more.";
        if (Truth == "" || Truth == TakenNode || Falsehoods.ContainsKey(Truth) || Falsehoods.ContainsKey(TakenNode))
            yield return $"truth and falsehoods must be different nodes, none of them '{TakenNode}'.";
        if (Falsehoods.Values.Any(w => w <= 0))
            yield return "falsehood weights must be positive.";
        foreach (string message in DialogNodes.Problems(Nodes, Entries()))
            yield return message;
    }
}

/// <summary>Where a rumor comes from (<c>source.*</c>): how often what it says is true.</summary>
public sealed class RumorSourceDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Chance from 0 to 1 that the claim is the truth.</summary>
    public double Reliability { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (Reliability is < 0 or > 1)
            yield return "reliability must be from 0 to 1.";
    }
}

/// <summary>Rumors (<c>config.rumors</c>, D41).</summary>
public sealed class RumorsConfigDef : ContentDef, IValidatedDef
{
    public const string RumorsId = "config.rumors";

    /// <summary>Any week spent in a settlement.</summary>
    public RumorOriginDef Settlement { get; set; } = new();

    /// <summary>A week of asking around in a settlement, instead of <see cref="Settlement"/>.</summary>
    public RumorOriginDef Inquiry { get; set; } = new();

    /// <summary>A settlement brings no new rumor while this many are being followed.</summary>
    public int MaxActive { get; set; }

    /// <summary>Weekly chance that rivals take a rumor, per point of its appeal (D68).</summary>
    public double InterceptPerAppeal { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (MaxActive < 1 || InterceptPerAppeal < 0)
            yield return "needs max_active of at least 1 and intercept_per_appeal of zero or more.";
    }
}

/// <summary>How a kind of week brings rumors: its chance, and which sources they come from.</summary>
public sealed class RumorOriginDef : IValidatedDef
{
    public double WeekChance { get; set; }

    /// <summary>Source → weight.</summary>
    [ContentRef(typeof(RumorSourceDef))]
    public Dictionary<string, double> Sources { get; set; } = [];

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (WeekChance is < 0 or > 1 || Sources.Count == 0 || Sources.Values.Any(w => w <= 0))
            yield return "needs week_chance from 0 to 1 and sources with positive weights.";
    }
}
