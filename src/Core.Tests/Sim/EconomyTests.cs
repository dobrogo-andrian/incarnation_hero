using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

/// <summary>Repo economy: 30 starting stones, the hub apothecary; the qi pool is refilled by pills, not stones (D18).</summary>
public sealed class EconomyTests
{
    private static Simulation NewGame() => Simulation.NewGame(TestContent.Repo, 1);

    private static ResourceValue QiPool(Simulation sim) =>
        sim.State.Player.Resources.Single(r => r.ResourceId == "resource.qi_pool");

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
    public void QiRestoringPill_RefillsThePool_EvenInADangerousZone_AndNotPastItsMaximum()
    {
        var sim = NewGame();
        sim.State.Player.ZoneId = "zone.spirit_peak";   // danger 3: stones were never burnt here, pills work anywhere
        sim.State.Player.Items.Add(new ItemCount { ItemId = "item.pill.qi_restoring", Count = 3 });
        QiPool(sim).Current = 30;

        Assert.True(sim.Execute(new UsePill("item.pill.qi_restoring")).Accepted);
        Assert.Equal(70, QiPool(sim).Current);
        Assert.True(sim.Execute(new UsePill("item.pill.qi_restoring")).Accepted);
        Assert.Equal(100, QiPool(sim).Current);   // 40 more would pass the maximum of 100
        Assert.Equal(4, sim.State.Player.Resources.Single(r => r.ResourceId == "resource.toxins").Current);

        Assert.Equal(RejectReasons.NothingToRestore, sim.Execute(new UsePill("item.pill.qi_restoring")).RejectReason);
        Assert.Equal(1, ItemSystem.Count(sim.State.Player.Items, "item.pill.qi_restoring"));
    }
}
