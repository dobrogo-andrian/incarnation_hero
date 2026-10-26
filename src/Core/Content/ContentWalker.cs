using System.Collections;
using System.Reflection;
using YamlDotNet.Serialization.NamingConventions;

namespace Core.Content;

/// <summary>
/// Where a value sits inside a content object, in two spellings: <see cref="Code"/> for error
/// messages (<c>'item.f'.Folio.Subject</c>) and <see cref="Yaml"/> for localization keys
/// (<c>evt.x.nodes.start.options.0.text</c>).
/// </summary>
public sealed record ContentPath(string Code, string Yaml)
{
    public static ContentPath Root(string id) => new($"'{id}'", id);

    public ContentPath Field(string propertyName) =>
        new($"{Code}.{propertyName}", $"{Yaml}.{UnderscoredNamingConvention.Instance.Apply(propertyName)}");

    /// <summary>A list index or dictionary key.</summary>
    public ContentPath Key(string key) => new($"{Code}.{key}", $"{Yaml}.{key}");
}

/// <summary>
/// Visits every property of a content object and of the blocks nested in it: single blocks, lists
/// of blocks and dictionaries of blocks (an event's nodes and options). The validator and the
/// localization table both walk content this way. Order is fixed: properties by name, lists by
/// index, dictionaries by key.
/// </summary>
public static class ContentWalker
{
    /// <param name="property">Called for each property with its path and value.</param>
    /// <param name="block">Called for each nested block before its properties are visited.</param>
    public static void Walk(object obj, ContentPath path, Action<ContentPath, PropertyInfo, object?> property, Action<ContentPath, object>? block = null)
    {
        foreach (var info in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            object? value = info.GetValue(obj);
            var at = path.Field(info.Name);
            property(at, info, value);
            if (info.GetCustomAttribute<ContentRefAttribute>() is not null)
                continue;

            foreach (var (childPath, child) in Children(at, value))
            {
                block?.Invoke(childPath, child);
                Walk(child, childPath, property, block);
            }
        }
    }

    private static IEnumerable<(ContentPath Path, object Block)> Children(ContentPath at, object? value) => value switch
    {
        null or string => [],
        IDictionary map => map.Keys.Cast<string>().Order(StringComparer.Ordinal)
            .Where(k => IsNestedBlock(map[k])).Select(k => (at.Key(k), map[k]!)),
        IList list => list.Cast<object?>().Select((item, i) => (Item: item, Index: i))
            .Where(x => IsNestedBlock(x.Item)).Select(x => (at.Key(x.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)), x.Item!)),
        _ when IsNestedBlock(value) => [(at, value)],
        _ => [],
    };

    /// <summary>A content class used inside a content object, such as <see cref="PillDef"/>.</summary>
    private static bool IsNestedBlock(object? value) =>
        value?.GetType() is { IsClass: true } type && type.Namespace == typeof(ContentDef).Namespace
        && !typeof(ContentDef).IsAssignableFrom(type) && type != typeof(LocalizedText);
}
