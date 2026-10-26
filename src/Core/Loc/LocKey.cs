namespace Core.Loc;

/// <summary>
/// What Core returns instead of finished text: a key plus named parameters. The presentation
/// layer turns it into text with <see cref="Localizer"/>. Keys are either a <c>str.*</c> string
/// ID or <c>&lt;content id&gt;.&lt;field&gt;</c> (see <see cref="LocTable"/>).
/// </summary>
public sealed record LocKey(string Key, IReadOnlyList<LocArg> Args)
{
    private static readonly IReadOnlyList<LocArg> NoArgs = [];

    public LocKey(string key) : this(key, NoArgs) { }

    public static LocKey Of(string key, params LocArg[] args) => new(key, args);
}

/// <summary>
/// One named parameter, substituted for <c>{Name}</c> in the text. <see cref="Value"/> is a
/// number, a plain string, another <see cref="LocKey"/> that is resolved first, or a list of
/// <see cref="LocKey"/>s resolved and joined with commas.
/// </summary>
public sealed record LocArg(string Name, object Value);
