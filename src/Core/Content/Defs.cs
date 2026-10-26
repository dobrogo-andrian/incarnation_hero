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
/// Marks a <c>string</c> or <c>List&lt;string&gt;</c> property as holding IDs of <see cref="Target"/>
/// objects. <see cref="ContentValidator"/> checks every marked reference, so a new content type
/// only declares its references here.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ContentRefAttribute(Type target) : Attribute
{
    public Type Target { get; } = target;
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
public sealed class RunConfigDef : ContentDef
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
}
