using Core.Content;

namespace Core.Loc;

/// <summary>
/// Flat key → Ukrainian text table built from content: a <see cref="StringDef"/> is keyed by its
/// ID, every other <see cref="LocalizedText"/> by <c>&lt;id&gt;.&lt;yaml field&gt;</c>
/// (e.g. <c>stage.qi.1.name</c>), a nested one by its path (<c>evt.x.nodes.start.options.0.text</c>).
/// The same keys will name rows of the /loc tables.
/// </summary>
public static class LocTable
{
    public static string ContentKey(string id, string yamlField) => $"{id}.{yamlField}";

    public static SortedDictionary<string, string> FromContent(ContentDb db)
    {
        var table = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var def in db.All<ContentDef>())
        {
            if (def is StringDef str)
            {
                table.Add(str.Id, str.Text.Uk);
                continue;
            }

            ContentWalker.Walk(def, ContentPath.Root(def.Id), (path, _, value) =>
            {
                if (value is LocalizedText text)
                    table.Add(path.Yaml, text.Uk);
            });
        }
        return table;
    }
}
