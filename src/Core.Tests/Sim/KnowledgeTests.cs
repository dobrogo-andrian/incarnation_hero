using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

/// <summary>
/// Repo knowledge: levels cost 20, 40, 80…; elements are sensed to level 1 without a folio.
/// The test place never runs dry: comprehension gains rate 5 × density 2 = 10 points a week.
/// </summary>
public sealed class KnowledgeTests
{
    private static readonly Core.Content.ContentDb Content = TestContent.WithExtra("""
        id: place.hub.test
        name: { uk: "Тест" }
        zone: zone.hub
        density: 2
        capacity: 1000
        regen_per_week: 0
        """);

    private const string FireFolio = "item.folio.fire";

    private static Simulation NewGame()
    {
        var sim = Simulation.NewGame(Content, 1);
        sim.Execute(new SetInterruptStops([]));
        return sim;
    }

    private static StartActivity Study(string? subject, int? weeks) =>
        new(new MeditateActivity("meditation.comprehension", "place.hub.test", subject), weeks);

    private static KnowledgeLevel? Knowledge(Simulation sim, string id) =>
        sim.State.Player.Knowledge.Find(k => k.KnowledgeId == id);

    private static double Perception(Simulation sim) => sim.State.Player.Stats.Single(s => s.StatId == "stat.perception").Base;

    [Fact]
    public void ElementSensing_WithoutAFolio_StopsAtTheBasicLevel_WithOneInterrupt()
    {
        var sim = NewGame();

        var outcome = sim.Execute(Study("element.fire", 5));

        Assert.Equal(1, Knowledge(sim, "element.fire")!.Level);
        Assert.Equal(0, Knowledge(sim, "element.fire")!.Progress);
        Assert.Single(outcome.Events, e => e is StudyCapReached);
        Assert.Contains(new KnowledgeLevelUp("element.fire", 1), outcome.Events);
        Assert.Equal(RejectReasons.StudyCapReached, sim.Execute(Study("element.fire", 1)).RejectReason);
    }

    [Fact]
    public void Folio_GoesPastTheBasicLevel_AtItsOwnSpeed()
    {
        var sim = NewGame();
        sim.Execute(Study("element.fire", 2));   // level 1 from basic sensing
        ItemSystem.Change(sim.State.Player.Items, FireFolio, 1);

        sim.Execute(Study(FireFolio, 3));   // 3 × 10 × 1.2 = 36 of the 40 for level 2

        Assert.Equal(1, Knowledge(sim, "element.fire")!.Level);
        Assert.Equal(36, Knowledge(sim, "element.fire")!.Progress, 9);

        sim.Execute(Study(FireFolio, 1));
        Assert.Equal(2, Knowledge(sim, "element.fire")!.Level);
        Assert.Equal(8, Knowledge(sim, "element.fire")!.Progress, 9);
    }

    [Fact]
    public void Study_NeedsACarriedFolioOrAnElement()
    {
        var sim = NewGame();

        Assert.Equal(RejectReasons.NothingToStudy, sim.Execute(Study(null, 1)).RejectReason);
        Assert.Equal(RejectReasons.NothingToStudy, sim.Execute(Study(FireFolio, 1)).RejectReason);
        Assert.Equal(RejectReasons.NothingToStudy, sim.Execute(Study("tech.spear.piercing_dragon", 1)).RejectReason);
        Assert.Equal(RejectReasons.NothingToStudy, sim.Execute(Study("item.pill.qi_gathering", 1)).RejectReason);
        Assert.Equal(0, sim.State.Week);
    }

    [Fact]
    public void OtherDirections_TakeNoSubject()
    {
        var sim = NewGame();

        var purify = new StartActivity(new MeditateActivity("meditation.purification", "place.hub.test", "element.fire"), 1);

        Assert.Equal(RejectReasons.InvalidActivity, sim.Execute(purify).RejectReason);
    }

    [Fact]
    public void Perception_GrowsFromComprehensionMoreThanFromOtherMeditation_AndDiminishes()
    {
        var studying = NewGame();
        var purifying = NewGame();

        studying.Execute(Study("element.fire", 1));
        purifying.Execute(new StartActivity(new MeditateActivity("meditation.purification", "place.hub.test"), 1));

        Assert.Equal(0.05 * 2, Perception(studying), 9);
        Assert.Equal(0.01 * 2, Perception(purifying), 9);

        studying.State.Player.Stats.Single(s => s.StatId == "stat.perception").Base = 10;   // scale 10: half the gain
        studying.Execute(Study("element.water", 1));
        Assert.Equal(10 + 0.05 * 2 / 2, Perception(studying), 9);
    }

    [Fact]
    public void Foundation_WeighsKnowledgeLevelsAndPurity()
    {
        var sim = NewGame();
        sim.Execute(Study("element.fire", 2));
        sim.Execute(Study("element.water", 2));
        sim.State.Player.Stats.Single(s => s.StatId == "stat.purity").Base = 3;

        Assert.Equal(new Foundation(20, 3, 0), sim.View.Foundation);
        Assert.Equal(23, sim.View.Foundation.Total);
    }
}
