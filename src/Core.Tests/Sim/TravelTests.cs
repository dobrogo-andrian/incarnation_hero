using Core.Content;
using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

/// <summary>Travel and meditation places on the repository map (content/world/region.yaml).</summary>
public sealed class TravelTests
{
    private static Simulation NewGame() => Simulation.NewGame(TestContent.Repo, 1);

    [Fact]
    public void NewGame_StartsInTheHubWithFullPlaces()
    {
        var sim = NewGame();

        Assert.Equal("zone.hub", sim.State.Player.ZoneId);
        Assert.Equal(TestContent.Repo.All<PlaceDef>().Count(), sim.State.World.Places.Count);
        Assert.All(sim.State.World.Places, p => Assert.Equal(TestContent.Repo.Get<PlaceDef>(p.PlaceId).Capacity, p.Reserve));
    }

    [Fact]
    public void Travel_FollowsTheFastestRoute_EnteringEachZone()
    {
        var sim = NewGame();

        var outcome = sim.Execute(new Travel("zone.spirit_peak"));

        Assert.True(outcome.Accepted);
        Assert.Equal(6, sim.State.Week);
        Assert.Equal("zone.spirit_peak", sim.State.Player.ZoneId);
        Assert.Null(sim.State.Activity);
        Assert.Equal(
            ["zone.bamboo_grove", "zone.misty_hills", "zone.spirit_peak"],
            outcome.Events.OfType<ZoneEntered>().Select(e => e.ZoneId));
        Assert.IsType<TravelActivity>(Assert.IsType<ActivityFinished>(outcome.Events[^1]).Activity);
    }

    [Fact]
    public void InterruptedTravel_LeavesPlayerInTheLastZoneReached_AndContinues()
    {
        var sim = NewGame();
        var player = sim.State.Player;
        player.AgeWeeks = player.LifespanWeeks - 5 * 52 - 2;   // the 5-year warning fires in week 2

        sim.Execute(new Travel("zone.spirit_peak"));

        Assert.Equal("zone.bamboo_grove", player.ZoneId);
        Assert.Equal(4, sim.State.Activity!.WeeksLeft);

        sim.Execute(new ContinueActivity());

        Assert.Equal("zone.spirit_peak", player.ZoneId);
        Assert.Equal(6, sim.State.Week);
    }

    [Fact]
    public void CancelledTravel_KeepsPlayerWhereTheyGot()
    {
        var sim = NewGame();
        var player = sim.State.Player;
        player.AgeWeeks = player.LifespanWeeks - 5 * 52 - 4;   // stops in week 4, between misty hills and the peak

        sim.Execute(new Travel("zone.spirit_peak"));
        sim.Execute(new CancelActivity());

        Assert.Equal("zone.misty_hills", player.ZoneId);
        Assert.Null(sim.State.Activity);
    }

    [Fact]
    public void Travel_IsRejectedForUnknownOrCurrentZone()
    {
        var sim = NewGame();

        Assert.Equal(RejectReasons.UnknownZone, sim.Execute(new Travel("zone.nowhere")).RejectReason);
        Assert.Equal(RejectReasons.UnknownZone, sim.Execute(new Travel("place.hub.quiet_courtyard")).RejectReason);
        Assert.Equal(RejectReasons.AlreadyThere, sim.Execute(new Travel("zone.hub")).RejectReason);
        Assert.Equal(0, sim.State.Week);
    }

    [Fact]
    public void TravelActivity_CannotBeStartedWithAPlayerChosenLength()
    {
        var sim = NewGame();
        var travel = new TravelActivity([new TravelLeg("zone.spirit_peak", 1)]);

        Assert.Equal(RejectReasons.InvalidActivity, sim.Execute(new StartActivity(travel, 1)).RejectReason);
        Assert.Equal("zone.hub", sim.State.Player.ZoneId);
    }

    [Fact]
    public void PlaceReserve_RefillsLinearlyUpToCapacity()
    {
        var sim = NewGame();
        var courtyard = sim.State.World.Places.Single(p => p.PlaceId == "place.hub.quiet_courtyard");
        courtyard.Reserve = 7.5;   // capacity 8, regen 0.25 per week

        sim.Execute(TestContent.Wait(1));
        Assert.Equal(7.75, courtyard.Reserve);

        sim.Execute(TestContent.Wait(2));
        Assert.Equal(8, courtyard.Reserve);
    }

    [Fact]
    public void PlayerView_ShowsOnlyThePlacesOfTheCurrentZone()
    {
        var sim = NewGame();
        sim.Execute(new Travel("zone.misty_hills"));

        var view = sim.View;

        Assert.Equal(new ZoneView("zone.misty_hills", 2), view.Zone);
        Assert.Equal([new PlaceView("place.misty_hills.cave", 2, 60, 60)], view.Places);
    }
}
