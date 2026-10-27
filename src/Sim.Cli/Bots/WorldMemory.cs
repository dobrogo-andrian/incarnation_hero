using Core.Content;
using Core.Sim;

namespace Sim.Cli.Bots;

/// <summary>
/// What a player would remember of the world: which hidden zones and places were revealed, and the
/// reserve each meditation place had when last seen. Unseen places are assumed full.
/// </summary>
public sealed class WorldMemory(ContentDb content)
{
    private readonly HashSet<string> _revealed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (double Reserve, int Week)> _reserves = new(StringComparer.Ordinal);

    public ContentDb Content { get; } = content;

    public void See(PlayerView view)
    {
        foreach (var place in view.Places)
            _reserves[place.PlaceId] = (place.Reserve, view.Week);
    }

    public void Observe(IReadOnlyList<SimEvent> events)
    {
        foreach (var e in events)
        {
            if (e is Revealed revealed)
                _revealed.Add(revealed.Id);
        }
    }

    public bool IsKnown(ZoneDef zone) => !zone.Hidden || _revealed.Contains(zone.Id);

    public bool IsKnown(PlaceDef place) => !place.Hidden || _revealed.Contains(place.Id);

    /// <summary>Known zones in ID order.</summary>
    public IEnumerable<ZoneDef> KnownZones() => Content.All<ZoneDef>().Where(IsKnown);

    /// <summary>The place's reserve now, from the last sighting plus regeneration since.</summary>
    public double ExpectedReserve(PlaceDef place, int week) =>
        _reserves.TryGetValue(place.Id, out var seen)
            ? Math.Min(place.Capacity, seen.Reserve + place.RegenPerWeek * (week - seen.Week))
            : place.Capacity;
}
