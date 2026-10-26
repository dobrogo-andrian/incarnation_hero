using System.Globalization;
using System.Text;

namespace Core.Rng;

/// <summary>
/// A hash that is the same on every machine and every run (unlike <c>string.GetHashCode</c>), for
/// values the view derives from the state, such as how an observed range is shifted.
/// </summary>
public static class StableHash
{
    /// <summary>FNV-1a over the UTF-8 parts joined by '|', then mixed by SplitMix64.</summary>
    public static ulong Of(params object[] parts)
    {
        ulong hash = 14695981039346656037UL;
        string text = string.Join('|', parts.Select(p => Convert.ToString(p, CultureInfo.InvariantCulture)));
        foreach (byte b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }
        return new SplitMix64(hash).Next();
    }
}
