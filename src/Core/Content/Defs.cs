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

/// <summary>A cultivation vector (<c>vector.qi</c>) and its ordered stages.</summary>
public sealed class CultivationVectorDef : ContentDef
{
    public LocalizedText Name { get; set; } = new();

    [ContentRef(typeof(CultivationStageDef))]
    public List<string> Stages { get; set; } = [];
}

/// <summary>One stage of a cultivation vector (<c>stage.qi.1</c>).</summary>
public sealed class CultivationStageDef : ContentDef
{
    public LocalizedText Name { get; set; } = new();
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

    /// <summary>Where a run starts: the hub settlement (§9).</summary>
    [ContentRef(typeof(ZoneDef))]
    public string StartZone { get; set; } = "";

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (!db.Contains(StartZone))
            yield break;

        var reachable = new World.WorldMap(db).ReachableFrom(StartZone);
        var cutOff = db.All<ZoneDef>().Select(z => z.Id).Where(id => !reachable.Contains(id)).ToList();
        if (cutOff.Count > 0)
            yield return $"zones unreachable from '{StartZone}': {string.Join(", ", cutOff)}.";
    }
}
