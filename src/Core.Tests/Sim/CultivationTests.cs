using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

public sealed class CultivationTests
{
    /// <summary>A hub place that never runs dry: assimilation turns rate 5 × density 2 = 10 points a week.</summary>
    private static readonly Core.Content.ContentDb Content = TestContent.Quiet(TestContent.WithExtra("""
        id: place.hub.test
        name: { uk: "Тест" }
        zone: zone.hub
        density: 2
        capacity: 1000
        regen_per_week: 0
        """));

    private static readonly StartActivity Assimilate = new(new MeditateActivity("meditation.assimilation", "place.hub.test"), null);

    private static Simulation NewGame(bool stopOnInterrupts = false)
    {
        var sim = Simulation.NewGame(Content, 1);
        if (!stopOnInterrupts)
            sim.Execute(new SetInterruptStops([]));
        return sim;
    }

    private static CultivationTrack Qi(Simulation sim) => sim.State.Player.Tracks.Single();

    private static double? Points(Simulation sim, BuffKind kind) =>
        sim.State.Player.Buffs.Find(b => b.Kind == kind)?.Points;

    [Fact]
    public void Assimilation_WithoutABuff_IsRejected()
    {
        var sim = NewGame();

        Assert.Equal(RejectReasons.NoCultivationBuff, sim.Execute(Assimilate).RejectReason);
        Assert.Equal(0, sim.State.Week);
    }

    [Fact]
    public void Assimilation_TurnsBuffPointsIntoStageProgress()
    {
        var sim = NewGame();
        CultivationSystem.AddPillEnergy(sim.State.Player, 25);

        sim.Execute(Assimilate with { Weeks = 2 });

        Assert.Equal(20, Qi(sim).StageProgress);
        Assert.Equal(5, Points(sim, BuffKind.PillEnergy));
    }

    [Fact]
    public void Assimilation_SpendsFadingCombatInsightBeforePillEnergy()
    {
        var sim = NewGame();
        CultivationSystem.AddPillEnergy(sim.State.Player, 30);
        CultivationSystem.GrantCombatInsight(sim.State.Player, 15);

        var outcome = sim.Execute(Assimilate with { Weeks = 2 });

        // Week 1: insight fades to 13.5, then 10 of it is spent. Week 2: 3.5 fades to 3.15,
        // which runs out, and the other 6.85 comes from the pill.
        Assert.Equal(20, Qi(sim).StageProgress);
        Assert.Null(Points(sim, BuffKind.CombatInsight));
        Assert.Equal(23.15, Points(sim, BuffKind.PillEnergy));
        Assert.Single(outcome.Events, e => e is BuffExhausted(BuffKind.CombatInsight));
    }

    [Fact]
    public void CombatInsight_FadesEveryWeekWithoutAssimilation_AndStopsTimeWhenGone()
    {
        var sim = NewGame(stopOnInterrupts: true);
        CultivationSystem.GrantCombatInsight(sim.State.Player, 2);

        var outcome = sim.Execute(TestContent.Wait(20));

        // 2 × 0.9^7 ≈ 0.96 is under the minimum of 1.
        Assert.Equal(new BuffExhausted(BuffKind.CombatInsight), outcome.Events[^1]);
        Assert.Equal(7, sim.State.Week);
        Assert.Empty(sim.State.Player.Buffs);
    }

    [Fact]
    public void FullScale_EndsAssimilation_KeepsTheRestOfTheBuff_AndRejectsMore()
    {
        var sim = NewGame(stopOnInterrupts: true);
        Qi(sim).StageProgress = 95;
        CultivationSystem.AddPillEnergy(sim.State.Player, 50);

        var outcome = sim.Execute(Assimilate);

        Assert.Contains(new StageScaleFull("qi"), outcome.Events);
        Assert.IsType<ActivityFinished>(outcome.Events[^1]);
        Assert.Null(sim.State.Activity);
        Assert.Equal(100, Qi(sim).StageProgress);
        Assert.Equal(45, Points(sim, BuffKind.PillEnergy));
        Assert.Equal(RejectReasons.StageScaleFull, sim.Execute(Assimilate).RejectReason);
    }

    [Fact]
    public void NewCombatInsight_KeepsTheStrongerOne_WhilePillEnergyAddsUp()
    {
        var player = NewGame().State.Player;

        CultivationSystem.GrantCombatInsight(player, 20);
        CultivationSystem.GrantCombatInsight(player, 12);
        CultivationSystem.AddPillEnergy(player, 10);
        CultivationSystem.AddPillEnergy(player, 10);

        Assert.Equal([BuffKind.CombatInsight, BuffKind.PillEnergy], player.Buffs.Select(b => b.Kind));
        Assert.Equal([20.0, 20.0], player.Buffs.Select(b => b.Points));
    }

    [Fact]
    public void Buffs_AreShownInThePlayerView()
    {
        var sim = NewGame();
        CultivationSystem.AddPillEnergy(sim.State.Player, 7);

        Assert.Equal([new BuffView(BuffKind.PillEnergy, 7)], sim.View.Buffs);
    }
}
