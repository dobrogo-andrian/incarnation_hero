using System.Globalization;
using System.Text.RegularExpressions;
using Core.Content;

namespace Core.Loc;

/// <summary>
/// Resolves <see cref="LocKey"/>s to text for the UI and Sim.Cli. Only Ukrainian for now; a
/// missing key resolves to the key itself (tech 3.5). Plural forms are not handled: texts are
/// phrased to avoid number agreement (A9).
/// </summary>
public sealed partial class Localizer(IReadOnlyDictionary<string, string> table)
{
    public static Localizer FromContent(ContentDb db) => new(LocTable.FromContent(db));

    public bool Has(string key) => table.ContainsKey(key);

    public string Resolve(LocKey key)
    {
        if (!table.TryGetValue(key.Key, out string? text))
            return key.Key;

        return Placeholder().Replace(text, match =>
        {
            string name = match.Groups[1].Value;
            var arg = key.Args.FirstOrDefault(a => a.Name == name);
            return arg is null ? match.Value : Format(arg.Value);
        });
    }

    private string Format(object value) => value switch
    {
        LocKey nested => Resolve(nested),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    [GeneratedRegex(@"\{([a-z0-9_]+)\}")]
    private static partial Regex Placeholder();
}
