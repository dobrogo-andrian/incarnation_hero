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

/// <summary>A cultivation vector (<c>vector.qi</c>) and its ordered stages.</summary>
public sealed class CultivationVectorDef : ContentDef
{
    public LocalizedText Name { get; set; } = new();
    public List<string> Stages { get; set; } = [];
}

/// <summary>One stage of a cultivation vector (<c>stage.qi.1</c>).</summary>
public sealed class CultivationStageDef : ContentDef
{
    public LocalizedText Name { get; set; } = new();
}
