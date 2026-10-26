using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Core.Content;

public sealed record ContentLoadResult(ContentDb Db, IReadOnlyList<ContentError> Errors)
{
    public bool Ok => Errors.Count == 0;
}

/// <summary>
/// Parses YAML content into a <see cref="ContentDb"/>. A file holds either one mapping or a list
/// of mappings; each mapping is one object whose ID prefix picks its type. Core never touches
/// the disk here: callers pass file texts, so Godot can feed <c>res://</c> files the same way.
/// All errors are collected instead of stopping at the first one.
/// </summary>
public static class ContentLoader
{
    /// <summary>First ID segment → content type. Extend when a new kind of content appears.</summary>
    private static readonly Dictionary<string, Type> TypesByKind = new(StringComparer.Ordinal)
    {
        ["vector"] = typeof(CultivationVectorDef),
        ["stage"] = typeof(CultivationStageDef),
        ["str"] = typeof(StringDef),
        ["config"] = typeof(RunConfigDef),
        ["stat"] = typeof(StatDef),
        ["aspect"] = typeof(AspectDef),
        ["aspect_profile"] = typeof(AspectProfileDef),
        ["resource"] = typeof(ResourceDef),
        ["zone"] = typeof(ZoneDef),
        ["route"] = typeof(RouteDef),
        ["place"] = typeof(PlaceDef),
        ["meditation"] = typeof(MeditationDirectionDef),
        ["item"] = typeof(ItemDef),
        ["shop"] = typeof(ShopDef),
    };

    /// <summary>Objects whose type is not their kind's: one-off configs. Checked before <see cref="TypesByKind"/>.</summary>
    private static readonly Dictionary<string, Type> TypesById = new(StringComparer.Ordinal)
    {
        [EconomyConfigDef.EconomyId] = typeof(EconomyConfigDef),
    };

    private static readonly IDeserializer Strict = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer IdProbe = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private sealed class IdOnly
    {
        public string? Id { get; set; }
    }

    public static ContentLoadResult Load(IEnumerable<ContentFile> files)
    {
        var defs = new SortedDictionary<string, ContentDef>(StringComparer.Ordinal);
        var sources = new SortedDictionary<string, ContentSource>(StringComparer.Ordinal);
        var errors = new List<ContentError>();

        foreach (var file in files.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            try
            {
                LoadFile(file, defs, sources, errors);
            }
            catch (YamlException e)
            {
                errors.Add(new ContentError(file.Path, (int)e.Start.Line, e.Message));
            }
        }

        return new ContentLoadResult(new ContentDb(defs, sources), errors);
    }

    private static void LoadFile(
        ContentFile file,
        SortedDictionary<string, ContentDef> defs,
        SortedDictionary<string, ContentSource> sources,
        List<ContentError> errors)
    {
        var parser = new Parser(new StringReader(file.Text));
        parser.Consume<StreamStart>();
        while (parser.TryConsume<DocumentStart>(out _))
        {
            if (parser.TryConsume<SequenceStart>(out _))
            {
                while (!parser.TryConsume<SequenceEnd>(out _))
                    LoadObject(file, parser, defs, sources, errors);
            }
            else
            {
                LoadObject(file, parser, defs, sources, errors);
            }
            parser.Consume<DocumentEnd>();
        }
        parser.Consume<StreamEnd>();
    }

    private static void LoadObject(
        ContentFile file,
        IParser parser,
        SortedDictionary<string, ContentDef> defs,
        SortedDictionary<string, ContentSource> sources,
        List<ContentError> errors)
    {
        var start = parser.Current!;
        int line = (int)start.Start.Line;
        if (start is not MappingStart)
        {
            errors.Add(new ContentError(file.Path, line, "Expected a mapping (one content object)."));
            parser.SkipThisAndNestedEvents();
            return;
        }

        var events = BufferNode(parser);

        string? id = IdProbe.Deserialize<IdOnly>(new ReplayParser(events))?.Id;
        if (string.IsNullOrEmpty(id))
        {
            errors.Add(new ContentError(file.Path, line, "Object has no 'id'."));
            return;
        }

        string kind = id.Split('.', 2)[0];
        if (!TypesById.TryGetValue(id, out var type) && !TypesByKind.TryGetValue(kind, out type))
        {
            errors.Add(new ContentError(file.Path, line, $"Unknown content kind '{kind}' in id '{id}'."));
            return;
        }

        if (sources.TryGetValue(id, out var first))
        {
            errors.Add(new ContentError(file.Path, line, $"Duplicate id '{id}', first defined at {first.File}:{first.Line}."));
            return;
        }

        try
        {
            var def = (ContentDef)Strict.Deserialize(new ReplayParser(events), type)!;
            defs.Add(id, def);
            sources.Add(id, new ContentSource(file.Path, line));
        }
        catch (YamlException e)
        {
            string message = e.InnerException is { } inner ? $"{e.Message} {inner.Message}" : e.Message;
            errors.Add(new ContentError(file.Path, (int)e.Start.Line, $"'{id}': {message}"));
        }
    }

    /// <summary>Consumes the current node with all nested events so it can be deserialized twice.</summary>
    private static List<ParsingEvent> BufferNode(IParser parser)
    {
        var events = new List<ParsingEvent>();
        int depth = 0;
        do
        {
            var current = parser.Current!;
            events.Add(current);
            depth += current.NestingIncrease;
            parser.MoveNext();
        }
        while (depth > 0);
        return events;
    }

    private sealed class ReplayParser(List<ParsingEvent> events) : IParser
    {
        private int _index = -1;

        public ParsingEvent? Current => _index >= 0 && _index < events.Count ? events[_index] : null;

        public bool MoveNext() => ++_index < events.Count;
    }
}
