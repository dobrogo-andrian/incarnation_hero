namespace Core.Content;

/// <summary>
/// Base of every content object. The first segment of <see cref="Id"/> (before the first dot)
/// selects the C# type; see <see cref="ContentLoader"/>.
/// </summary>
public abstract class ContentDef
{
    public string Id { get; set; } = "";
}

/// <summary>
/// Player-facing text written inline in YAML. Only Ukrainian lives here; other languages come
/// from the /loc tables (tech 3.5).
/// </summary>
public sealed class LocalizedText
{
    public string Uk { get; set; } = "";
}

/// <summary>
/// Marks a <c>string</c>, <c>List&lt;string&gt;</c> or string-keyed dictionary property as holding
/// IDs of <see cref="Target"/> objects (for a dictionary, its keys). <see cref="ContentValidator"/>
/// checks every marked reference, so a new content type only declares its references here.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ContentRefAttribute(Type target) : Attribute
{
    public Type Target { get; } = target;
}

/// <summary>
/// A content type with rules beyond references and texts. <see cref="ContentValidator"/> calls
/// <see cref="Validate"/> after the generic checks, so the rules live next to the data they guard.
/// </summary>
public interface IValidatedDef
{
    /// <summary>Problems with this object, as messages; empty when it is valid.</summary>
    IEnumerable<string> Validate(ContentDb db);
}

/// <summary>
/// A piece of system or UI text that belongs to no content object (<c>str.reject.wrong_mode</c>).
/// Its ID is its localization key.
/// </summary>
public sealed class StringDef : ContentDef
{
    public LocalizedText Text { get; set; } = new();
}

/// <summary>
/// A cultivation vector (<c>vector.qi</c>) and its ordered stages. Every stage but the last has a
/// breakthrough into the next one.
/// </summary>
public sealed class CultivationVectorDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    [ContentRef(typeof(CultivationStageDef))]
    public List<string> Stages { get; set; } = [];

    public IEnumerable<string> Validate(ContentDb db)
    {
        // A missing stage is already reported as a broken reference; which stage is last is unclear then.
        if (!Stages.All(id => db.TryGet<CultivationStageDef>(id, out _)))
            yield break;
        for (int i = 0; i < Stages.Count; i++)
        {
            var stage = db.Get<CultivationStageDef>(Stages[i]);
            bool last = i == Stages.Count - 1;
            if ((stage.Breakthrough is null) != last)
                yield return $"stage '{Stages[i]}' {(last ? "is the last and must not have" : "needs")} a breakthrough.";
        }
    }
}

/// <summary>One stage of a cultivation vector (<c>stage.qi.1</c>); index 0 is the mortal start.</summary>
public sealed class CultivationStageDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Stage progress that fills the breakthrough scale (§4.1).</summary>
    public double ProgressRequired { get; set; }

    /// <summary>The breakthrough out of this stage; null for the last stage.</summary>
    public StageBreakthroughDef? Breakthrough { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (ProgressRequired <= 0)
            yield return "needs a positive progress_required.";
        if (Breakthrough is not { } b)
            yield break;
        if (b.Threshold <= 0 || b.Weeks <= 0)
            yield return "breakthrough needs a positive threshold and weeks.";
        if (db.TryGet<BreakthroughConfigDef>(BreakthroughConfigDef.BreakthroughId, out var config)
            && (b.LifespanYears.Count != config.Ranks.Count || b.StatGain.Count != config.Ranks.Count))
            yield return "breakthrough lifespan_years and stat_gain need one entry per quality rank.";
    }
}

/// <summary>Run-wide balance parameters (<c>config.run</c>). Exactly one per content set.</summary>
public sealed class RunConfigDef : ContentDef, IValidatedDef
{
    public const string RunId = "config.run";

    /// <summary>Age at the start of a run, §3.</summary>
    public int StartAgeYears { get; set; }

    /// <summary>Weeks in a year, D16.</summary>
    public int WeeksPerYear { get; set; }

    /// <summary>Expected lifespan at the start of a run, §3.</summary>
    public int StartLifespanYears { get; set; }

    /// <summary>
    /// How long before the expected lifespan natural death becomes possible (D21). The mean age of
    /// death equals the expected lifespan; a wider window spreads deaths further around it.
    /// </summary>
    public int NaturalDeathWindowYears { get; set; }

    /// <summary>Years before the expected lifespan at which the player is warned (D24).</summary>
    public List<int> LifeEndWarningYears { get; set; } = [];

    /// <summary>The character's stats, in display order (§2.1).</summary>
    [ContentRef(typeof(StatDef))]
    public List<string> Stats { get; set; } = [];

    /// <summary>The character's resources, in display order (§2.2).</summary>
    [ContentRef(typeof(ResourceDef))]
    public List<string> Resources { get; set; } = [];

    /// <summary>Aspect profile at the start of a run, until a cultivation method sets its own (A2).</summary>
    [ContentRef(typeof(AspectProfileDef))]
    public string StartAspectProfile { get; set; } = "";

    /// <summary>
    /// Lowest meditation efficiency once a place's reserve cannot cover a week's draw (§4.2, D30):
    /// below the draw, efficiency is reserve / draw, but never under this.
    /// </summary>
    public double MeditationDepletedFloor { get; set; }

    /// <summary>The meditation the main screen's "meditate" starts at the zone's best place (D72).</summary>
    [ContentRef(typeof(MeditationDirectionDef))]
    public string RecoveryMeditation { get; set; } = "";

    /// <summary>Share of combat insight points that fades each week, travel included (§4.3).</summary>
    public double CombatInsightDecay { get; set; }

    /// <summary>Combat insight below this many points is gone.</summary>
    public double CombatInsightMinPoints { get; set; }

    /// <summary>The resource pills fill with toxins; its maximum is the tolerance that limits pills (§4.4).</summary>
    [ContentRef(typeof(ResourceDef))]
    public string ToxinsResource { get; set; } = "";

    /// <summary>
    /// Comprehension points to go from each level to the next: the first entry reaches level 1. Its
    /// length is the highest level anything can reach.
    /// </summary>
    public List<double> KnowledgeLevelPoints { get; set; } = [];

    /// <summary>How far an element can be sensed without its folio (§6).</summary>
    public int ElementBasicMaxLevel { get; set; }

    /// <summary>Foundation = Σ knowledge levels × this + purity × <see cref="FoundationPerPurity"/> (§4.1).</summary>
    public double FoundationPerKnowledgeLevel { get; set; }

    public double FoundationPerPurity { get; set; }

    /// <summary>The purity stat counted in the foundation.</summary>
    [ContentRef(typeof(StatDef))]
    public string PurityStat { get; set; } = "";

    /// <summary>Where a run starts: the hub settlement (§9).</summary>
    [ContentRef(typeof(ZoneDef))]
    public string StartZone { get; set; } = "";

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (KnowledgeLevelPoints.Count == 0 || KnowledgeLevelPoints.Any(p => p <= 0))
            yield return "knowledge_level_points needs at least one positive entry.";
        if (!db.Contains(StartZone))
            yield break;

        var reachable = new World.WorldMap(db).ReachableFrom(StartZone);
        var cutOff = db.All<ZoneDef>().Select(z => z.Id).Where(id => !reachable.Contains(id)).ToList();
        if (cutOff.Count > 0)
            yield return $"zones unreachable from '{StartZone}': {string.Join(", ", cutOff)}.";
    }
}
