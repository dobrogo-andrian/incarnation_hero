using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

public sealed class MeditationTests
{
    /// <summary>A hub place with round numbers: draws 2 a week, holds 5, never refills.</summary>
    private static readonly Core.Content.ContentDb Content = TestContent.Quiet(TestContent.WithExtra("""
        id: place.hub.test
        name: { uk: "Тест" }
        zone: zone.hub
        density: 2
        capacity: 5
        regen_per_week: 0
        """));

    private static Simulation NewGame()
    {
        var sim = Simulation.NewGame(Content, 1);
        sim.Execute(new SetInterruptStops([]));
        return sim;
    }

    private static StartActivity Meditate(string direction, int weeks, string place = "place.hub.test") =>
        new(new MeditateActivity(direction, place), weeks);

    private static double Purity(Simulation sim) => sim.State.Player.Stats.Single(s => s.StatId == "stat.purity").Base;

    private static double Reserve(Simulation sim, string place = "place.hub.test") =>
        sim.State.World.Places.Single(p => p.PlaceId == place).Reserve;

    [Fact]
    public void Purification_GainsRateTimesDensityWhileTheReserveLasts_ThenFallsToTheFloor()
    {
        var sim = NewGame();
        double rate = 0.1, floor = TestContent.Config.MeditationDepletedFloor;

        sim.Execute(Meditate("meditation.purification", 2));
        Assert.Equal(2 * rate * 2, Purity(sim), 9);
        Assert.Equal(1, Reserve(sim));

        sim.Execute(Meditate("meditation.purification", 1));   // reserve 1 of 2 needed: half efficiency
        Assert.Equal((2 + 0.5) * rate * 2, Purity(sim), 9);
        Assert.Equal(0, Reserve(sim));

        sim.Execute(Meditate("meditation.purification", 1));   // empty: the floor
        Assert.Equal((2 + 0.5 + floor) * rate * 2, Purity(sim), 9);
    }

    [Fact]
    public void PlaceQiLow_FiresOnceWhenTheReserveStopsCoveringAWeek_AndStopsTimeByDefault()
    {
        var sim = Simulation.NewGame(Content, 1);

        var outcome = sim.Execute(Meditate("meditation.purification", 10));

        Assert.Equal(new PlaceQiLow("place.hub.test"), outcome.Events[^1]);
        Assert.Equal(2, sim.State.Week);
        Assert.Equal(8, sim.State.Activity!.WeeksLeft);

        var rest = sim.Execute(new ContinueActivity());
        Assert.DoesNotContain(rest.Events, e => e is PlaceQiLow);
    }

    [Fact]
    public void Purity_WidensTheQiPool()
    {
        var sim = NewGame();
        double before = sim.Rules.Value(sim.State.Player, "resource.qi_pool");

        sim.Execute(Meditate("meditation.purification", 2));

        Assert.Equal(before + Purity(sim) * 2, sim.Rules.Value(sim.State.Player, "resource.qi_pool"), 9);
    }

    [Fact]
    public void Recovery_RefillsTheQiPoolFasterThanNaturalRegen()
    {
        var waiting = NewGame();
        var meditating = NewGame();
        waiting.State.Player.Resources.Single(r => r.ResourceId == "resource.qi_pool").Current = 50;
        meditating.State.Player.Resources.Single(r => r.ResourceId == "resource.qi_pool").Current = 50;

        waiting.Execute(TestContent.Wait(1));
        meditating.Execute(Meditate("meditation.recovery", 1));

        double Pool(Simulation s) => s.State.Player.Resources.Single(r => r.ResourceId == "resource.qi_pool").Current;
        Assert.Equal(52, Pool(waiting));        // natural regen 2
        Assert.Equal(52 + 5 * 2, Pool(meditating)); // plus rate 5 × density 2
    }

    [Fact]
    public void ResourceNeverExceedsItsMaximum()
    {
        var sim = NewGame();

        sim.Execute(Meditate("meditation.recovery", 2));

        var pool = sim.State.Player.Resources.Single(r => r.ResourceId == "resource.qi_pool");
        Assert.Equal(sim.Rules.Value(sim.State.Player, "resource.qi_pool"), pool.Current);
    }

    [Fact]
    public void Meditation_IsRejectedForUnknownDirectionOrPlaceElsewhere()
    {
        var sim = NewGame();

        Assert.Equal(RejectReasons.UnknownDirection, sim.Execute(Meditate("meditation.nothing", 1)).RejectReason);
        Assert.Equal(RejectReasons.PlaceNotHere, sim.Execute(Meditate("meditation.purification", 1, "place.spirit_peak.vein")).RejectReason);
        Assert.Equal(RejectReasons.PlaceNotHere, sim.Execute(Meditate("meditation.purification", 1, "place.nowhere")).RejectReason);
        Assert.Equal(0, sim.State.Week);
    }
}
