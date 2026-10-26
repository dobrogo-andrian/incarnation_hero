using Core.Content;
using Core.State;

namespace Core.World;

/// <summary>
/// The zone graph built from <see cref="RouteDef"/>s (tech 4.2: a graph of nodes, not a tile map).
/// Neighbours are kept in ordinal ID order so route finding is deterministic.
/// </summary>
public sealed class WorldMap
{
    /// <summary>Lookup only; each neighbour list is sorted by zone ID.</summary>
    private readonly Dictionary<string, List<(string Zone, int Weeks)>> _neighbours = new(StringComparer.Ordinal);

    public WorldMap(ContentDb db)
    {
        foreach (var route in db.All<RouteDef>())
        {
            Neighbours(route.A).Add((route.B, route.Weeks));
            Neighbours(route.B).Add((route.A, route.Weeks));
        }
        foreach (var list in _neighbours.Values)
            list.Sort((x, y) => string.CompareOrdinal(x.Zone, y.Zone));
    }

    public IReadOnlySet<string> ReachableFrom(string zone)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { zone };
        var queue = new Queue<string>([zone]);
        while (queue.TryDequeue(out var current))
        {
            foreach (var (next, _) in NeighboursOf(current))
            {
                if (seen.Add(next))
                    queue.Enqueue(next);
            }
        }
        return seen;
    }

    /// <summary>
    /// The fastest way from <paramref name="from"/> to <paramref name="to"/> as legs (zone entered,
    /// weeks to reach it), or null when there is none. Equal routes are settled by zone ID order.
    /// Only zones <paramref name="passable"/> accepts are entered (unknown zones are not, D40).
    /// </summary>
    public IReadOnlyList<TravelLeg>? FindRoute(string from, string to, Func<string, bool>? passable = null)
    {
        var best = new Dictionary<string, (int Weeks, string? Previous, int LegWeeks)>(StringComparer.Ordinal) { [from] = (0, null, 0) };
        var open = new SortedSet<(int Weeks, string Zone)>(Comparer<(int Weeks, string Zone)>.Create((x, y) =>
            x.Weeks != y.Weeks ? x.Weeks.CompareTo(y.Weeks) : string.CompareOrdinal(x.Zone, y.Zone))) { (0, from) };

        while (open.Count > 0)
        {
            var (weeks, zone) = open.Min;
            open.Remove(open.Min);
            if (zone == to)
                return Legs(best, to);

            foreach (var (next, legWeeks) in NeighboursOf(zone))
            {
                if (passable is not null && !passable(next))
                    continue;
                int total = weeks + legWeeks;
                if (best.TryGetValue(next, out var known) && known.Weeks <= total)
                    continue;
                if (best.ContainsKey(next))
                    open.Remove((known.Weeks, next));
                best[next] = (total, zone, legWeeks);
                open.Add((total, next));
            }
        }
        return null;
    }

    private static List<TravelLeg> Legs(Dictionary<string, (int Weeks, string? Previous, int LegWeeks)> best, string to)
    {
        var legs = new List<TravelLeg>();
        for (string zone = to; best[zone].Previous is { } previous; zone = previous)
            legs.Add(new TravelLeg(zone, best[zone].LegWeeks));
        legs.Reverse();
        return legs;
    }

    private IEnumerable<(string Zone, int Weeks)> NeighboursOf(string zone) =>
        _neighbours.TryGetValue(zone, out var list) ? list : [];

    private List<(string Zone, int Weeks)> Neighbours(string zone)
    {
        if (!_neighbours.TryGetValue(zone, out var list))
            _neighbours[zone] = list = [];
        return list;
    }
}
