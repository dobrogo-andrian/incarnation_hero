namespace Core.Content;

/// <summary>One content source file: a path relative to the content root and its text.</summary>
public sealed record ContentFile(string Path, string Text);

/// <summary>Where a content object was defined, for error messages.</summary>
public sealed record ContentSource(string File, int Line);

/// <summary>A load or validation problem. <see cref="Line"/> is 1-based, 0 when unknown.</summary>
public sealed record ContentError(string File, int Line, string Message)
{
    public override string ToString() => Line > 0 ? $"{File}:{Line}: {Message}" : $"{File}: {Message}";
}

/// <summary>
/// All loaded content, keyed by ID. Immutable after loading; enumeration is always in ordinal
/// ID order so that nothing in the simulation depends on file or hash order.
/// </summary>
public sealed class ContentDb
{
    private readonly SortedDictionary<string, ContentDef> _defs;
    private readonly SortedDictionary<string, ContentSource> _sources;

    internal ContentDb(SortedDictionary<string, ContentDef> defs, SortedDictionary<string, ContentSource> sources)
    {
        _defs = defs;
        _sources = sources;
    }

    public int Count => _defs.Count;

    public bool Contains(string id) => _defs.ContainsKey(id);

    public T Get<T>(string id) where T : ContentDef =>
        TryGet<T>(id, out var def) ? def : throw new KeyNotFoundException($"No {typeof(T).Name} with id '{id}'.");

    public bool TryGet<T>(string id, out T def) where T : ContentDef
    {
        if (_defs.TryGetValue(id, out var found) && found is T typed)
        {
            def = typed;
            return true;
        }
        def = null!;
        return false;
    }

    /// <summary>All objects of type <typeparamref name="T"/>, in ordinal ID order.</summary>
    public IEnumerable<T> All<T>() where T : ContentDef => _defs.Values.OfType<T>();

    public ContentSource? SourceOf(string id) => _sources.GetValueOrDefault(id);
}
