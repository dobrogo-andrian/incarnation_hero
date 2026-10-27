using Core.Content;
using Core.Rng;
using Core.Save;
using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

public sealed class RumorTests
{
    private const string Extra = """
        - id: source.test.truthful
          name: { uk: "Правдиве" }
          reliability: 1
        - id: source.test.liar
          name: { uk: "Брехливе" }
          reliability: 0
        - id: rumor.test.grove
          name: { uk: "Гай" }
          text: { uk: "У гаю щось є" }
          zone: zone.bamboo_grove
          window_weeks: { min: 5, max: 5 }
          appeal: 0
          weight: 1
          truth: real
          falsehoods: { fake: 1 }
          nodes:
            real: { text: { uk: "Правда" } }
            fake: { text: { uk: "Неправда" } }
            taken: { text: { uk: "Забрали" } }
        - id: rumor.test.river
          name: { uk: "Долина" }
          text: { uk: "У долині щось є" }
          zone: zone.river_valley
          window_weeks: { min: 50, max: 50 }
          appeal: 100
          weight: 1
          truth: real
          nodes:
            real: { text: { uk: "Правда" } }
            taken: { text: { uk: "Забрали" } }
        - id: evt.test.tip
          contexts: [rest]
          weight: 1
          nodes:
            start:
              text: { uk: "Підказка" }
              options:
                - text: { uk: "Правдиве" }
                  effects: [ { add_rumor: { id: rumor.test.grove, source: source.test.truthful } } ]
                - text: { uk: "Брехливе" }
                  effects: [ { add_rumor: { id: rumor.test.grove, source: source.test.liar } } ]
        """;

    /// <summary>No weekly event or rumor rolls, so only what a test does happens.</summary>
    private static readonly ContentDb Content = TestContent.Quiet(TestContent.WithExtra(Extra));

    private static Simulation NewGame(ContentDb? content = null) => Simulation.NewGame(content ?? Content, 1);

    private static SimContext Ctx(Simulation sim) => new(sim.Content, RngStreams.FromSeed(sim.State.Seed));

    private static void Hear(Simulation sim, string rumorId, string source = "source.test.truthful") =>
        RumorSystem.Hear(sim.State, Ctx(sim), sim.Content.Get<RumorDef>(rumorId), source, []);

    [Theory]
    [InlineData(0, "real")]
    [InlineData(1, "fake")]
    public void AddRumor_HearsItForItsWindow_AndTheSourceDecidesWhatIsThere(int option, string node)
    {
        var sim = NewGame();
        sim.Execute(TestContent.Wait(3));
        EventSystem.Start(sim.State, Content.Get<EventDef>("evt.test.tip"), []);

        var outcome = sim.Execute(new ChooseEventOption(option));

        Assert.Contains(new RumorHeard("rumor.test.grove"), outcome.Events);
        var rumor = Assert.Single(sim.State.Rumors);
        Assert.Equal((3, 8, node, false), (rumor.HeardWeek, rumor.LastWeek, rumor.TrueNode, rumor.Taken));
        var view = Assert.Single(sim.View.Rumors);
        Assert.Equal(("zone.bamboo_grove", 5), (view.ZoneId, view.WeeksLeft));
    }

    [Fact]
    public void HearingAFollowedRumor_AgainChangesNothing()
    {
        var sim = NewGame();
        Hear(sim, "rumor.test.grove");
        var events = new List<SimEvent>();

        RumorSystem.Hear(sim.State, Ctx(sim), Content.Get<RumorDef>("rumor.test.grove"), "source.test.liar", events);

        Assert.Empty(events);
        Assert.Equal("real", Assert.Single(sim.State.Rumors).TrueNode);
    }

    [Fact]
    public void ArrivingAtTheDestination_OpensWhatIsReallyThere_AndEndsTheRumor()
    {
        var sim = NewGame();
        Hear(sim, "rumor.test.grove", "source.test.liar");

        var outcome = sim.Execute(new Travel("zone.bamboo_grove"));

        Assert.Contains(new EventStarted("rumor.test.grove"), outcome.Events);
        Assert.Equal(("rumor.test.grove", "fake"), (sim.State.Event!.EventId, sim.State.Event.NodeId));
        Assert.Empty(sim.State.Rumors);
        Assert.Equal("rumor.test.grove.nodes.fake.text", SimText.EventText(sim.View.Event!, sim.View).Key);

        sim.Execute(new ChooseEventOption(0));
        Assert.Equal(GameMode.Overworld, sim.State.Mode);
    }

    [Fact]
    public void PassingThroughTheZone_DoesNotMeetIt()
    {
        var sim = NewGame();
        Hear(sim, "rumor.test.river");

        // The fastest way to the old battlefield leads through the river valley.
        var outcome = sim.Execute(new Travel("zone.old_battlefield"));

        Assert.Contains(new ZoneEntered("zone.river_valley"), outcome.Events);
        Assert.DoesNotContain(outcome.Events, e => e is EventStarted);
        Assert.Equal(("zone.old_battlefield", GameMode.Overworld), (sim.State.Player.ZoneId, sim.State.Mode));
        Assert.Single(sim.State.Rumors);
    }

    [Fact]
    public void StayingInTheZone_MeetsItAfterAWeek()
    {
        var sim = NewGame();
        sim.State.Player.ZoneId = "zone.bamboo_grove";
        Hear(sim, "rumor.test.grove");

        sim.Execute(TestContent.Wait(10));

        Assert.Equal((1, "real"), (sim.State.Week, sim.State.Event!.NodeId));
        Assert.Null(sim.State.Activity);
    }

    [Fact]
    public void AfterItsWindow_ItIsGoneSilently()
    {
        var sim = NewGame();
        Hear(sim, "rumor.test.grove");

        var waited = sim.Execute(TestContent.Wait(5));
        Assert.Single(sim.State.Rumors);
        var later = sim.Execute(TestContent.Wait(1));

        Assert.Empty(sim.State.Rumors);
        Assert.DoesNotContain(waited.Events.Concat(later.Events), e => e is RumorHeard or EventStarted);
        sim.Execute(new Travel("zone.bamboo_grove"));
        Assert.Equal(GameMode.Overworld, sim.State.Mode);
    }

    [Fact]
    public void RivalsTakeIt_UnseenUntilArrival()
    {
        var sim = NewGame();
        Hear(sim, "rumor.test.river");

        // Appeal 100 at 0.01 per point: certain within a week.
        var outcome = sim.Execute(new Travel("zone.river_valley"));

        Assert.DoesNotContain(outcome.Events, e => e is InterruptEvent);
        Assert.Equal("taken", sim.State.Event!.NodeId);
    }

    [Fact]
    public void Inquiry_OnlyInASettlement()
    {
        var sim = NewGame();
        sim.State.Player.ZoneId = "zone.bamboo_grove";

        Assert.Equal(RejectReasons.NotInSettlement, sim.Execute(new StartActivity(new InquireActivity(), 5)).RejectReason);
    }

    [Fact]
    public void Inquiry_BringsARumorFromItsSources_AndTheNewRumorStopsTime()
    {
        var sim = Simulation.NewGame(TestContent.WithExtra(Extra), 1);
        var inquiry = sim.Content.Get<RumorsConfigDef>(RumorsConfigDef.RumorsId).Inquiry;

        var inquire = new StartActivity(new InquireActivity(), null);
        var outcome = sim.Execute(inquire);
        while (sim.State.Rumors.Count == 0)
            outcome = sim.Execute(TestContent.Proceed(sim.View, inquire));

        var rumor = Assert.Single(sim.State.Rumors);
        Assert.Contains(new RumorHeard(rumor.RumorId), outcome.Events);
        Assert.Contains(rumor.SourceId, inquiry.Sources.Keys);
        Assert.Equal(new ActivityInterrupted(new InquireActivity()), outcome.Events[^1]);
        Assert.Null(sim.State.Activity);
    }

    [Fact]
    public void Settlement_BringsNoMore_WhileTheLimitIsFollowed()
    {
        var content = TestContent.WithExtra(Extra);
        var config = content.Get<RumorsConfigDef>(RumorsConfigDef.RumorsId);
        config.Inquiry.WeekChance = 1;
        config.MaxActive = 1;
        var sim = Simulation.NewGame(WithInquiryOnly(content), 1);
        Hear(sim, "rumor.test.grove");

        var outcome = sim.Execute(new StartActivity(new InquireActivity(), 4));

        Assert.DoesNotContain(outcome.Events, e => e is RumorHeard);
        Assert.Single(sim.State.Rumors);
    }

    [Fact]
    public void Reliability_IsSeenAsARange_ThatPerceptionNarrows()
    {
        var sim = NewGame();
        Hear(sim, "rumor.bamboo_grove.spirit_herb", "source.travelling_merchant");
        var blurred = Assert.Single(sim.View.Rumors).Reliability;
        sim.State.Player.Stats.First(s => s.StatId == "stat.perception").Base = 20;
        var sharp = Assert.Single(sim.View.Rumors).Reliability;

        Assert.InRange(0.6, blurred.Low, blurred.High);
        Assert.True(blurred.High - blurred.Low >= 0.35);
        Assert.Equal(new ObservedRange(0.6, 0.6), sharp);
    }

    [Fact]
    public void FollowedRumors_SurviveSaveAndLoad()
    {
        var sim = NewGame();
        Hear(sim, "rumor.test.grove");
        sim.Execute(new StartActivity(new InquireActivity(), 3));

        var loaded = SaveSerializer.Decode(SaveSerializer.Encode(sim.State)).State!;

        Assert.Equal(SaveSerializer.HashState(sim.State), SaveSerializer.HashState(loaded));
        Assert.Equal("rumor.test.grove", Assert.Single(loaded.RumorHistory).RumorId);
    }

    /// <summary>Weekly events off, rumor rolls as configured.</summary>
    private static ContentDb WithInquiryOnly(ContentDb content)
    {
        var events = content.Get<EventsConfigDef>(EventsConfigDef.EventsId);
        foreach (string context in events.WeekChance.Keys.ToList())
            events.WeekChance[context] = 0;
        return content;
    }
}
