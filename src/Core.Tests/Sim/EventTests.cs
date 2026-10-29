using Core.Combat;
using Core.Content;
using Core.Loc;
using Core.Rng;
using Core.Save;
using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

public sealed class EventTests
{
    private static readonly ContentDb Content = TestContent.WithExtra("""
        - id: enemy.test.weak
          name: { uk: "Слабкий" }
          ai: ai.beast_aggressive
          stats: { stat.might: 0.1 }
          weapon_damage: 0.1
        - id: enemy.test.wall
          name: { uk: "Непробивний" }
          ai: ai.beast_aggressive
          stats: { stat.might: 100000 }
          weapon_damage: 0.1
        - id: grp.test.weak
          name: { uk: "Слабкі" }
          members: [ { enemy: enemy.test.weak } ]
        - id: grp.test.strong
          name: { uk: "Сильні" }
          members: [ { enemy: enemy.test.wall } ]
        - id: evt.test.trial
          contexts: [rest]
          weight: 1
          nodes:
            start:
              text: { uk: "Випробування" }
              options:
                - text: { uk: "Дорого" }
                  conditions: [ { stones: { min: 1000 } } ]
                  effects: [ { stones: -1000 } ]
                - text: { uk: "Обмін" }
                  effects:
                    - stones: -10
                    - item: { id: item.pill.qi_gathering, amount: 1 }
                    - reputation: { id: sect.iron_palm, amount: 7 }
                    - reveal: place.bamboo_grove.hidden_spring
                  next: done
                - text: { uk: "Чекати" }
                  effects: [ { time: 3 } ]
                - text: { uk: "Слабкі" }
                  effects: [ { start_combat: { group: grp.test.weak, win: won, loss: lost } } ]
                - text: { uk: "Сильні" }
                  effects: [ { start_combat: { group: grp.test.strong, win: won, loss: lost } } ]
                - text: { uk: "Удача" }
                  outcomes:
                    - weight: 0
                      weight_per_stat: { stat.perception: 1 }
                      success: true
                      next: won
                    - weight: 1
                      next: lost
            done: { text: { uk: "Готово" } }
            won: { text: { uk: "Перемога" } }
            lost: { text: { uk: "Поразка" } }
        - id: evt.test.once
          contexts: [rest]
          weight: 1
          once_per_run: true
          nodes: { start: { text: { uk: "Раз" } } }
        - id: evt.test.cooldown
          contexts: [rest]
          weight: 1
          cooldown_weeks: 10
          conditions: [ { zone: [zone.bamboo_grove] } ]
          nodes: { start: { text: { uk: "Знову" } } }
        """);

    private const int Expensive = 0, Trade = 1, Wait = 2, Weak = 3, Strong = 4, Luck = 5;

    private static Simulation Open(string eventId = "evt.test.trial", ulong seed = 1)
    {
        var sim = Simulation.NewGame(Content, seed);
        EventSystem.Start(sim.State, Content.Get<EventDef>(eventId), []);
        return sim;
    }

    private static SimContext Ctx(Simulation sim) => new(sim.Content, RngStreams.FromSeed(sim.State.Seed));

    [Fact]
    public void Choosing_AppliesEffectsInOrder_ThenGoesToTheNextNode()
    {
        var sim = Open();

        var outcome = sim.Execute(new ChooseEventOption(Trade));

        var player = sim.State.Player;
        Assert.Equal(20, player.Stones);
        Assert.Equal(3, ItemSystem.Count(player.Items, "item.pill.qi_gathering"));
        Assert.Equal(7, EventSystem.ReputationOf(player, "sect.iron_palm"));
        Assert.Contains("place.bamboo_grove.hidden_spring", sim.State.World.Revealed);
        Assert.Equal(
            [new StonesChanged(-10), new ItemsChanged("item.pill.qi_gathering", 1), new ReputationChanged("sect.iron_palm", 7), new Revealed("place.bamboo_grove.hidden_spring")],
            outcome.Events);
        Assert.Equal(("done", GameMode.EventDialog), (sim.State.Event!.NodeId, sim.State.Mode));
    }

    [Fact]
    public void NodeWithoutOptions_ClosesWithContinue()
    {
        var sim = Open();
        sim.Execute(new ChooseEventOption(Trade));

        var choices = SimText.EventChoices(sim.View.Event!);
        Assert.Equal((0, new LocKey("str.dialog.continue"), true), Assert.Single(choices));
        Assert.Equal(RejectReasons.InvalidOption, sim.Execute(new ChooseEventOption(1)).RejectReason);

        var outcome = sim.Execute(new ChooseEventOption(0));

        Assert.Equal([new EventEnded("evt.test.trial")], outcome.Events);
        Assert.Equal(GameMode.Overworld, sim.State.Mode);
        Assert.Null(sim.State.Event);
    }

    [Fact]
    public void UnavailableOption_IsShownWithItsRequirement_AndRefused()
    {
        var sim = Open();

        var option = sim.View.Event!.Options[Expensive];

        Assert.False(option.Available);
        Assert.Equal(1000, Assert.Single(option.Unmet).Stones!.Min);
        Assert.Equal(
            "Дорого (потрібно: духовні камені від 1000)",
            Localizer.FromContent(Content).Resolve(SimText.EventChoices(sim.View.Event!)[Expensive].Text));
        Assert.Equal(RejectReasons.OptionUnavailable, sim.Execute(new ChooseEventOption(Expensive)).RejectReason);
        Assert.Equal(RejectReasons.InvalidOption, sim.Execute(new ChooseEventOption(99)).RejectReason);
        Assert.Equal(RejectReasons.WrongMode, sim.Execute(TestContent.Wait(1)).RejectReason);
    }

    [Fact]
    public void TimeEffect_PassesWorldWeeks_WithoutActivityProgress()
    {
        var sim = Open();
        sim.State.Activity = new ActiveActivity { Activity = new WaitActivity(), WeeksLeft = 5 };
        int age = sim.State.Player.AgeWeeks;

        var outcome = sim.Execute(new ChooseEventOption(Wait));

        Assert.Equal(3, sim.State.Week);
        Assert.Equal(age + 3, sim.State.Player.AgeWeeks);
        Assert.Equal(5, sim.State.Activity!.WeeksLeft);
        Assert.Equal(3, outcome.Events.OfType<WeekPassed>().Count());
        Assert.Equal(GameMode.Overworld, sim.State.Mode);
    }

    [Fact]
    public void Combat_GoesToWinOrLoss()
    {
        var won = Open();
        var wonEvents = won.Execute(new ChooseEventOption(Weak)).Events;
        Assert.Equal("won", won.State.Event!.NodeId);
        Assert.Equal(CombatEnd.Won, wonEvents.OfType<CombatResolved>().Single().End);

        var lost = Open();
        var lostEvents = lost.Execute(new ChooseEventOption(Strong)).Events;
        Assert.Equal("lost", lost.State.Event!.NodeId);
        Assert.Equal(CombatEnd.Fled, lostEvents.OfType<CombatResolved>().Single().End);
    }

    [Fact]
    public void OutcomeWeights_GrowWithStats()
    {
        var unlucky = Open();
        Assert.Equal(0, unlucky.View.Event!.Options[Luck].Success!.Low);
        unlucky.Execute(new ChooseEventOption(Luck));
        Assert.Equal("lost", unlucky.State.Event!.NodeId);

        var sharp = Open();
        sharp.State.Player.Stats.First(s => s.StatId == "stat.perception").Base = 1_000_000;
        Assert.Equal(new ObservedRange(0.95, 1), sharp.View.Event!.Options[Luck].Success);
        sharp.Execute(new ChooseEventOption(Luck));
        Assert.Equal("won", sharp.State.Event!.NodeId);
    }

    [Theory]
    [InlineData(0.62, 0)]
    [InlineData(0.62, 10)]
    [InlineData(0.05, 0)]
    [InlineData(0.98, 5)]
    public void ObservedChance_ContainsTheTruth_AndNarrowsWithPerception(double truth, double perception)
    {
        var sim = Simulation.NewGame(TestContent.Repo, 1);
        var ctx = Ctx(sim);
        sim.State.Player.Stats.First(s => s.StatId == "stat.perception").Base = perception;
        var config = ctx.Events;
        double width = config.ChanceRangeWidth - config.ChanceRangeNarrowPerPerception * perception;

        for (ulong key = 0; key < 50; key++)
        {
            var range = Observation.Chance(truth, sim.State.Player, ctx, StableHash.Of(key));
            Assert.InRange(truth, range.Low, range.High);
            Assert.True(range.High - range.Low <= width + 2 * config.ChanceRangeStep + 1e-9);
        }
    }

    [Fact]
    public void ObservedChance_IsExactWithEnoughPerception()
    {
        var sim = Simulation.NewGame(TestContent.Repo, 1);
        sim.State.Player.Stats.First(s => s.StatId == "stat.perception").Base = 20;

        Assert.Equal(new ObservedRange(0.6, 0.6), Observation.Chance(0.6, sim.State.Player, Ctx(sim), StableHash.Of(7)));
    }

    [Fact]
    public void Eligible_RespectsOncePerRun_CooldownAndConditions()
    {
        var sim = Simulation.NewGame(Content, 1);
        var ctx = Ctx(sim);
        string[] TestEvents() => EventSystem.Eligible(sim.State, ctx, EventContexts.Rest).Select(e => e.Id).Where(id => id.StartsWith("evt.test.")).ToArray();

        Assert.Equal(["evt.test.once", "evt.test.trial"], TestEvents());
        sim.State.Player.ZoneId = "zone.bamboo_grove";
        Assert.Equal(["evt.test.cooldown", "evt.test.once", "evt.test.trial"], TestEvents());

        EventSystem.Start(sim.State, Content.Get<EventDef>("evt.test.once"), []);
        EventSystem.Start(sim.State, Content.Get<EventDef>("evt.test.cooldown"), []);
        Assert.Equal(["evt.test.trial"], TestEvents());

        sim.State.Week += 10;
        Assert.Equal(["evt.test.cooldown", "evt.test.trial"], TestEvents());
    }

    [Theory]
    [InlineData("zone.hub", "wait", EventContexts.Settlement)]
    [InlineData("zone.hub", "meditate", EventContexts.Settlement)]
    [InlineData("zone.bamboo_grove", "wait", EventContexts.Rest)]
    [InlineData("zone.bamboo_grove", "meditate", EventContexts.Meditation)]
    [InlineData("zone.bamboo_grove", "travel", EventContexts.Travel)]
    [InlineData("zone.bamboo_grove", "breakthrough", null)]
    public void Context_FollowsActivityAndZone(string zone, string activity, string? expected)
    {
        var sim = Simulation.NewGame(TestContent.Repo, 1);
        sim.State.Player.ZoneId = zone;
        sim.State.Activity = new ActiveActivity
        {
            Activity = activity switch
            {
                "wait" => new WaitActivity(),
                "meditate" => new MeditateActivity("meditation.purification", "place.hub.quiet_courtyard"),
                "travel" => new TravelActivity([new TravelLeg("zone.hub", 1)]),
                _ => new BreakthroughActivity("qi", "place.hub.quiet_courtyard", false),
            },
        };

        Assert.Equal(expected, EventSystem.ContextOf(sim.State, TestContent.Repo));
    }

    [Fact]
    public void WeeklyRoll_OpensAnEvent_ThatEndsTheActivityAndStopsTimeUntilItCloses()
    {
        var sim = Simulation.NewGame(TestContent.Repo, 5);
        Assert.True(Script.Run(sim, new Travel("zone.bamboo_grove")));

        var rest = TestContent.Wait(null);
        var outcome = sim.Execute(rest);
        while (sim.State.Event is null && sim.State.Mode == GameMode.Overworld)
            outcome = sim.Execute(rest);

        var started = outcome.Events.OfType<EventStarted>().Last();
        Assert.Contains(EventContexts.Rest, TestContent.Repo.Get<EventDef>(started.EventId).Contexts);
        Assert.Equal(GameMode.EventDialog, sim.State.Mode);
        Assert.Equal(new ActivityInterrupted(new WaitActivity()), outcome.Events[^1]);
        Assert.Null(sim.State.Activity);

        while (sim.State.Event is not null)
            sim.Execute(TestContent.Proceed(sim.View, rest));
        Assert.Equal(GameMode.Overworld, sim.State.Mode);
        Assert.Null(sim.State.Activity);
    }

    [Fact]
    public void HiddenZone_IsOffTheMap_UntilRevealed()
    {
        var sim = Simulation.NewGame(TestContent.Repo, 1);
        bool Known(string zone) => WorldSystem.IsKnown(sim.State.World, sim.Content, zone);
        Assert.Equal(RejectReasons.UnknownZone, sim.Execute(new Travel("zone.ancient_tomb")).RejectReason);
        Assert.Null(sim.Map.FindRoute("zone.hub", "zone.ancient_tomb", Known));

        WorldSystem.Reveal(sim.State.World, "zone.ancient_tomb");

        Assert.Equal("zone.old_battlefield", sim.Map.FindRoute("zone.hub", "zone.ancient_tomb", Known)![^2].ZoneId);
        Assert.True(sim.Execute(new Travel("zone.ancient_tomb")).Accepted);
    }

    [Fact]
    public void HiddenPlace_IsUnseenAndUnusable_UntilRevealed()
    {
        var sim = Simulation.NewGame(TestContent.Repo, 1);
        sim.State.Player.ZoneId = "zone.bamboo_grove";
        var spring = new MeditateActivity("meditation.purification", "place.bamboo_grove.hidden_spring");

        Assert.DoesNotContain(sim.View.Places, p => p.PlaceId == spring.PlaceId);
        Assert.Equal(RejectReasons.PlaceNotHere, sim.Execute(new StartActivity(spring, 1)).RejectReason);

        WorldSystem.Reveal(sim.State.World, spring.PlaceId);

        Assert.Contains(sim.View.Places, p => p.PlaceId == spring.PlaceId);
        Assert.True(sim.Execute(new StartActivity(spring, 1)).Accepted);
    }

    [Fact]
    public void OpenDialog_SurvivesSaveLoad()
    {
        var sim = Open();
        sim.Execute(new ChooseEventOption(Trade));
        byte[] saved = SaveSerializer.Encode(sim.State);

        var resumed = Simulation.Resume(Content, SaveSerializer.Decode(saved).State!);

        Assert.Equal(saved, SaveSerializer.Encode(resumed.State));
        Assert.Equal("done", resumed.View.Event!.NodeId);
        Assert.True(resumed.Execute(new ChooseEventOption(0)).Accepted);
        Assert.Equal(GameMode.Overworld, resumed.State.Mode);
    }

    [Fact]
    public void NestedEventTexts_HaveLocalizationKeys()
    {
        var table = LocTable.FromContent(TestContent.Repo);

        Assert.Equal("Битися", table["evt.travel.road_bandits.nodes.start.options.1.text"]);
        Assert.StartsWith("Розбійники", table["evt.travel.road_bandits.nodes.paid.text"]);
    }
}
