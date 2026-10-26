using Core.Content;
using Core.State;
using Core.World;

namespace Core.Tests.World;

public sealed class WorldMapTests
{
    /// <summary>
    /// Two equal 2-week routes from a to d (a–b–d and a–c–d, 1 week per route), a slower direct
    /// a–d route of 3 weeks, and an isolated zone e.
    /// </summary>
    private static readonly WorldMap Map = new(Load("""
        - { id: zone.a, name: { uk: "А" } }
        - { id: zone.b, name: { uk: "Б" } }
        - { id: zone.c, name: { uk: "В" } }
        - { id: zone.d, name: { uk: "Г" } }
        - { id: zone.e, name: { uk: "Ґ" } }
        - { id: route.ab, a: zone.a, b: zone.b, weeks: 1 }
        - { id: route.bd, a: zone.b, b: zone.d, weeks: 1 }
        - { id: route.ac, a: zone.a, b: zone.c, weeks: 1 }
        - { id: route.cd, a: zone.c, b: zone.d, weeks: 1 }
        - { id: route.ad, a: zone.a, b: zone.d, weeks: 3 }
        """));

    private static ContentDb Load(string yaml)
    {
        var loaded = ContentLoader.Load([new ContentFile("map.yaml", yaml)]);
        Assert.True(loaded.Ok, string.Join("\n", loaded.Errors));
        return loaded.Db;
    }

    [Fact]
    public void FindRoute_TakesTheFastestRoute_SettlingTiesByZoneId()
    {
        var legs = Map.FindRoute("zone.a", "zone.d");

        Assert.Equal([new TravelLeg("zone.b", 1), new TravelLeg("zone.d", 1)], legs);
    }

    [Fact]
    public void FindRoute_WorksInBothDirections()
    {
        Assert.Equal([new TravelLeg("zone.b", 1), new TravelLeg("zone.a", 1)], Map.FindRoute("zone.d", "zone.a"));
    }

    [Fact]
    public void FindRoute_ToUnreachableZone_IsNull()
    {
        Assert.Null(Map.FindRoute("zone.a", "zone.e"));
    }

    [Fact]
    public void ReachableFrom_ListsTheConnectedZones()
    {
        Assert.Equal(["zone.a", "zone.b", "zone.c", "zone.d"], Map.ReachableFrom("zone.a").Order(StringComparer.Ordinal));
        Assert.Equal(["zone.e"], Map.ReachableFrom("zone.e"));
    }
}
