using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

public sealed class MeditationTests
{
    /// <summary>A hub place with round numbers: draws 2 a week, holds 5, never refills; and a shelter in the grove.</summary>
    private static readonly Core.Content.ContentDb Content = TestContent.Quiet(TestContent.WithExtra("""
        - id: place.hub.test
          name: { uk: "Тест" }
          zone: zone.hub
          density: 2
          capacity: 5
          regen_per_week: 0

        - id: place.bamboo_grove.test_shelter
          name: { uk: "Тестове укриття" }
          zone: zone.bamboo_grove
          density: 0.5
          capacity: 50
          regen_per_week: 0
          shelter: true
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
    public void PlaceQiLow_FiresOnceWhenTheReserveStopsCoveringAWeek_AndEndsMeditationByDefault()
    {
        var sim = Simulation.NewGame(Content, 1);

        var outcome = sim.Execute(Meditate("meditation.purification", 10));

        Assert.Contains(new PlaceQiLow("place.hub.test"), outcome.Events);
        Assert.IsType<ActivityInterrupted>(outcome.Events[^1]);
        Assert.Equal(2, sim.State.Week);
        Assert.Null(sim.State.Activity);

        var again = sim.Execute(Meditate("meditation.purification", 8));
        Assert.DoesNotContain(again.Events, e => e is PlaceQiLow);
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
        sim.State.Player.Resources.Single(r => r.ResourceId == "resource.qi_pool").Current -= 1;

        sim.Execute(Meditate("meditation.recovery", 2));

        var pool = sim.State.Player.Resources.Single(r => r.ResourceId == "resource.qi_pool");
        Assert.Equal(sim.Rules.Value(sim.State.Player, "resource.qi_pool"), pool.Current);
    }

    [Fact]
    public void Recovery_IsRejected_AndNotOffered_WhenTheBodyAndQiAreWhole()
    {
        var sim = NewGame();

        Assert.Equal(RejectReasons.NothingToRecover, sim.Execute(Meditate("meditation.recovery", 1)).RejectReason);
        Assert.Null(MeditationSystem.Recovery(sim.View, sim.Content));
    }

    [Fact]
    public void Recovery_RestsTheBody_HealingInjuriesAsRestDid()
    {
        var resting = NewGame();
        var meditating = NewGame();
        foreach (var sim in new[] { resting, meditating })
            InjurySystem.Inflict(sim.State.Player, new SimContext(sim.Content, Core.Rng.RngStreams.FromStates(sim.State.RngStates)), "injury.body", 10, []);

        resting.Execute(TestContent.Wait(1));
        meditating.Execute(Meditate("meditation.recovery", 1));

        Assert.Equal(resting.State.Player.Injuries.Single().Value, meditating.State.Player.Injuries.Single().Value);
        Assert.True(meditating.State.Player.Injuries.Single().Value < 10);
    }

    [Fact]
    public void OpenGroundOfADangerousZone_AllowsOnlyRecovery_AShelterAllowsEverything()
    {
        var sim = NewGame();
        sim.State.Player.ZoneId = "zone.bamboo_grove";
        sim.State.Player.Resources.Single(r => r.ResourceId == "resource.qi_pool").Current = 50;

        Assert.Equal(RejectReasons.TooExposed, sim.Execute(Meditate("meditation.purification", 1, "place.bamboo_grove.clearing")).RejectReason);
        Assert.Equal("place.bamboo_grove.test_shelter", MeditationSystem.BestPlace(sim.View, sim.Content, "meditation.purification"));
        Assert.Equal("place.bamboo_grove.clearing", MeditationSystem.BestPlace(sim.View, sim.Content, "meditation.recovery"));
        Assert.True(sim.Execute(Meditate("meditation.purification", 1, "place.bamboo_grove.test_shelter")).Accepted);
        Assert.True(sim.Execute(Meditate("meditation.recovery", 1, "place.bamboo_grove.clearing")).Accepted);
    }

    [Fact]
    public void RecoveryInTheOpenOfADangerousZone_CanBeAmbushed_ByTheZonesAmbushers_ButNotInAShelter()
    {
        var content = TestContent.Quiet(TestContent.WithExtra("""
            id: place.bamboo_grove.test_shelter
            name: { uk: "Тестове укриття" }
            zone: zone.bamboo_grove
            density: 0.5
            capacity: 50
            regen_per_week: 0
            shelter: true
            """));
        content.Get<Core.Content.RunConfigDef>(Core.Content.RunConfigDef.RunId).AmbushChancePerDanger = 1;
        Simulation InTheGrove()
        {
            var sim = Simulation.NewGame(content, 1);
            sim.Execute(new SetInterruptStops([]));
            sim.State.Player.ZoneId = "zone.bamboo_grove";
            sim.State.Player.Resources.Single(r => r.ResourceId == "resource.qi_pool").Current = 50;
            return sim;
        }

        var open = InTheGrove();
        var outcome = open.Execute(Meditate("meditation.recovery", 3, "place.bamboo_grove.clearing"));
        var offered = Assert.Single(outcome.Events.OfType<FightOffered>());
        Assert.Contains(offered.GroupId, content.Get<Core.Content.ZoneDef>("zone.bamboo_grove").Ambushers.Keys);
        Assert.Equal(GameMode.Combat, open.State.Mode);

        var sheltered = InTheGrove();
        sheltered.Execute(Meditate("meditation.recovery", 3, "place.bamboo_grove.test_shelter"));
        Assert.Equal(GameMode.Overworld, sheltered.State.Mode);
    }

    [Fact]
    public void Contemplation_GrowsPerceptionTenTimesFasterThanItsSideGain()
    {
        var contemplating = NewGame();
        var purifying = NewGame();

        contemplating.Execute(Meditate("meditation.contemplation", 1));
        purifying.Execute(Meditate("meditation.purification", 1));

        double Perception(Simulation s) => s.State.Player.Stats.Single(st => st.StatId == "stat.perception").Base;
        Assert.Equal(Perception(purifying) * 10, Perception(contemplating), 9);
    }

    [Fact]
    public void Recovery_WithoutALimit_EndsOnItsOwn_OnceHealthAndQiAreFull()
    {
        var sim = NewGame();
        sim.State.Player.Resources.Single(r => r.ResourceId == "resource.qi_pool").Current = 80;
        sim.State.Player.Resources.Single(r => r.ResourceId == "resource.health").Current = 75;

        var outcome = sim.Execute(new StartActivity(new MeditateActivity("meditation.recovery", "place.hub.test"), null));

        Assert.IsType<ActivityFinished>(outcome.Events[^1]);
        Assert.Null(sim.State.Activity);
        Assert.All(sim.State.Player.Resources.Where(r => r.ResourceId is "resource.qi_pool" or "resource.health"),
            r => Assert.Equal(sim.Rules.Value(sim.State.Player, r.ResourceId), r.Current));
        Assert.Equal(3, sim.State.Week);   // health 75 → 100 at 10 a week; qi is full sooner
    }

    [Fact]
    public void RecoveryHere_PicksThePlaceThatGivesTheMost_AsItsReserveRunsDry()
    {
        var sim = NewGame();
        sim.State.Player.Resources.Single(r => r.ResourceId == "resource.qi_pool").Current = 50;

        Assert.Equal(new MeditateActivity(TestContent.Config.RecoveryMeditation, "place.hub.test"), MeditationSystem.Recovery(sim.View, sim.Content));

        sim.State.World.Places.Single(p => p.PlaceId == "place.hub.test").Reserve = 0;   // 2 × floor 0.2 < 1 × 1
        Assert.Equal(new MeditateActivity(TestContent.Config.RecoveryMeditation, "place.hub.quiet_courtyard"), MeditationSystem.Recovery(sim.View, sim.Content));
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
