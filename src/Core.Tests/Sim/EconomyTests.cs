using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

/// <summary>Repo economy: 30 starting stones, 10 qi or 5 place reserve per stone, the hub apothecary.</summary>
public sealed class EconomyTests
{
    private const string Courtyard = "place.hub.quiet_courtyard";

    private static Simulation NewGame() => Simulation.NewGame(TestContent.Repo, 1);

    private static ResourceValue QiPool(Simulation sim) =>
        sim.State.Player.Resources.Single(r => r.ResourceId == "resource.qi_pool");

    private static PlaceState Place(Simulation sim, string id) => sim.State.World.Places.Single(p => p.PlaceId == id);

    [Fact]
    public void Buy_PaysTheShopPrice_AndRejectsWhatIsNotSoldOrUnaffordable()
    {
        var sim = NewGame();

        var outcome = sim.Execute(new Buy("item.pill.spirit_dew"));
        Assert.True(outcome.Accepted);
        Assert.Equal([new StonesChanged(-30), new ItemsChanged("item.pill.spirit_dew", 1)], outcome.Events);
        Assert.Equal(0, sim.State.Player.Stones);
        Assert.Equal(1, ItemSystem.Count(sim.State.Player.Items, "item.pill.spirit_dew"));

        Assert.Equal(RejectReasons.NotEnoughStones, sim.Execute(new Buy("item.pill.qi_gathering")).RejectReason);
        Assert.Equal(RejectReasons.NotSoldHere, sim.Execute(new Buy("item.nothing")).RejectReason);
    }

    [Fact]
    public void Buy_OutsideAZoneWithAShop_IsRejected()
    {
        var sim = NewGame();
        sim.State.Player.ZoneId = "zone.spirit_peak";

        Assert.Equal(RejectReasons.NotSoldHere, sim.Execute(new Buy("item.pill.qi_gathering")).RejectReason);
    }

    [Fact]
    public void RestoreQi_SpendsOnlyTheStonesNeeded()
    {
        var sim = NewGame();
        QiPool(sim).Current = 75;

        Assert.Equal([new StonesChanged(-3)], sim.Execute(new RestoreQiWithStones(10)).Events);

        Assert.Equal(100, QiPool(sim).Current);
        Assert.Equal(27, sim.State.Player.Stones);   // 25 missing: 3 stones
        Assert.Equal(RejectReasons.QiPoolFull, sim.Execute(new RestoreQiWithStones(1)).RejectReason);
    }

    [Fact]
    public void RestoreQi_IsForbiddenInDangerousZones()
    {
        var sim = NewGame();
        QiPool(sim).Current = 50;
        sim.State.Player.ZoneId = "zone.spirit_peak";   // danger 3

        Assert.Equal(RejectReasons.TooDangerousHere, sim.Execute(new RestoreQiWithStones(1)).RejectReason);
        Assert.Equal(30, sim.State.Player.Stones);
    }

    [Fact]
    public void StoneCommands_RejectBadAmounts()
    {
        var sim = NewGame();
        QiPool(sim).Current = 0;

        Assert.Equal(RejectReasons.InvalidAmount, sim.Execute(new RestoreQiWithStones(0)).RejectReason);
        Assert.Equal(RejectReasons.NotEnoughStones, sim.Execute(new RestoreQiWithStones(31)).RejectReason);
        Assert.Equal(RejectReasons.InvalidAmount, sim.Execute(new RefillPlaceWithStones(Courtyard, -1)).RejectReason);
    }

    [Fact]
    public void RefillPlace_StopsAtCapacity_AndOnlyHere()
    {
        var sim = NewGame();
        Place(sim, Courtyard).Reserve = 1;

        Assert.Equal([new StonesChanged(-2)], sim.Execute(new RefillPlaceWithStones(Courtyard, 5)).Events);

        Assert.Equal(8, Place(sim, Courtyard).Reserve);   // capacity 8: 7 missing takes 2 stones
        Assert.Equal(28, sim.State.Player.Stones);
        Assert.Equal(RejectReasons.PlaceFull, sim.Execute(new RefillPlaceWithStones(Courtyard, 1)).RejectReason);
        Assert.Equal(RejectReasons.PlaceNotHere, sim.Execute(new RefillPlaceWithStones("place.spirit_peak.vein", 1)).RejectReason);
    }
}
