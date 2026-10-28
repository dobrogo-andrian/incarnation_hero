using Core.Rng;
using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

/// <summary>
/// Repo ranked body stats (D48): a fresh character has Might 10 at rank 1 (pool 12, breakthrough 2),
/// rank 2 holds 8 more and needs 3; overflow fades 5% of itself a week. Perception is a plain stat.
/// Purity (D49) starts at 0 on rank 1 (pool 20); rank 2 (base 20, pool 30) collapses at deficit 6,
/// which heals 5% a week; toxins erode 0.004 a point a week, internal injuries 0.004, body ones 0.001.
/// </summary>
public sealed class StatSystemTests
{
    private const string Might = "stat.might";
    private const string Purity = "stat.purity";

    private static Simulation NewGame() => Simulation.NewGame(TestContent.Calm, 1);

    private static SimContext Context(Simulation sim) => new(sim.Content, RngStreams.FromStates(sim.State.RngStates));

    private static StatValue Stat(Simulation sim, string id) => sim.State.Player.Stats.Single(s => s.StatId == id);

    private static List<SimEvent> Change(Simulation sim, string statId, double amount)
    {
        var events = new List<SimEvent>();
        StatSystem.Change(sim.State.Player, Context(sim), statId, amount, events);
        return events;
    }

    [Fact]
    public void Points_FillTheScaleFirst_ThenGatherAsOverflowThatDoesNotRaiseTheStat()
    {
        var sim = NewGame();

        var events = Change(sim, Might, 3);

        var might = Stat(sim, Might);
        Assert.Equal((12.0, 1.0, 0), (might.Base, might.Overflow, might.Rank));
        Assert.Empty(events);
        Assert.Equal(12, sim.Rules.Value(sim.State.Player, Might));
    }

    [Fact]
    public void EnoughOverflow_BreaksThroughToAnEmptyNextScale_AndBurnsTheRest()
    {
        var sim = NewGame();

        var events = Change(sim, Might, 7);

        var might = Stat(sim, Might);
        Assert.Equal((12.0, 0.0, 1), (might.Base, might.Overflow, might.Rank));
        Assert.Equal(new StatRankUp(Might, "stat_rank.body.2"), Assert.Single(events));
        var standing = sim.View.Stats.Single(s => s.Stat.Id == Might).Standing!;
        Assert.Equal((12.0, 8.0, 0.0, 3.0), (standing.RankBase, standing.Pool, standing.Filled, standing.Breakthrough!.Value));
    }

    [Fact]
    public void Overflow_GathersAcrossGains_UntilItBreaksThrough()
    {
        var sim = NewGame();
        Change(sim, Might, 3);

        var events = Change(sim, Might, 1);

        Assert.Equal(1, Stat(sim, Might).Rank);
        Assert.IsType<StatRankUp>(Assert.Single(events));
    }

    [Fact]
    public void Overflow_FadesByItsShareEachWeek_SoMoreOfItGoesFaster()
    {
        var small = NewGame();
        var large = NewGame();
        Change(small, Might, 2.5);
        Change(large, Might, 3.5);

        StatSystem.AdvanceWeek(small.State.Player, Context(small), []);
        StatSystem.AdvanceWeek(large.State.Player, Context(large), []);

        Assert.Equal(0.475, Stat(small, Might).Overflow);
        Assert.Equal(1.425, Stat(large, Might).Overflow);
        Assert.Equal(12, Stat(large, Might).Base);
    }

    [Fact]
    public void Weeks_WearOverflowAway_UntilNothingIsLeft()
    {
        var sim = NewGame();
        sim.Execute(new SetInterruptStops([]));
        Change(sim, Might, 3);

        sim.Execute(TestContent.Wait(200));

        Assert.Equal(0, Stat(sim, Might).Overflow);
        Assert.Equal(12, Stat(sim, Might).Base);
    }

    [Fact]
    public void Losses_TakeOverflowFirst_AndNeverDropBelowTheRankBase()
    {
        var sim = NewGame();
        Change(sim, Might, 7);
        Change(sim, Might, 5);

        Change(sim, Might, -100);

        var might = Stat(sim, Might);
        Assert.Equal((12.0, 0.0, 1), (might.Base, might.Overflow, might.Rank));
    }

    [Fact]
    public void PlainStat_JustMoves_AndStopsAtZero()
    {
        var sim = NewGame();

        Change(sim, "stat.perception", 3);
        Change(sim, "stat.perception", -5);

        Assert.Equal(0, Stat(sim, "stat.perception").Base);
        Assert.Null(sim.View.Stats.Single(s => s.Stat.Id == "stat.perception").Standing);
    }

    [Fact]
    public void PurityLosses_BelowTheRankBase_AreDeficit_ThatGainsMendFirst()
    {
        var sim = NewGame();
        TestContent.SetStat(sim, Purity, 22);

        Change(sim, Purity, -5);
        var purity = Stat(sim, Purity);
        Assert.Equal((20.0, 3.0, 1), (purity.Base, purity.Deficit, purity.Rank));
        Assert.Equal(20, sim.Rules.Value(sim.State.Player, Purity));

        Change(sim, Purity, 4);
        Assert.Equal((21.0, 0.0), (purity.Base, purity.Deficit));
    }

    [Fact]
    public void EnoughDeficit_CollapsesToTheRankBelow_WithItsScaleFull()
    {
        var sim = NewGame();
        TestContent.SetStat(sim, Purity, 22);

        var events = Change(sim, Purity, -8);

        var purity = Stat(sim, Purity);
        Assert.Equal((20.0, 0.0, 0), (purity.Base, purity.Deficit, purity.Rank));
        Assert.Equal(new StatRankDown(Purity, "stat_rank.purity.1"), Assert.Single(events));
        Change(sim, Purity, -3);
        Assert.Equal((17.0, 0.0), (purity.Base, purity.Deficit));
    }

    [Fact]
    public void FirstRank_HasNowhereToFall_SoLossesStopAtZero()
    {
        var sim = NewGame();

        Change(sim, Purity, -3);

        Assert.Equal((0.0, 0.0), (Stat(sim, Purity).Base, Stat(sim, Purity).Deficit));
    }

    [Fact]
    public void ToxinsAndInjuries_WearPurityAwayEachWeek_AndDeficitHeals()
    {
        var sim = NewGame();
        TestContent.SetStat(sim, Purity, 21);
        var player = sim.State.Player;
        player.Resources.First(r => r.ResourceId == "resource.toxins").Current = 10;
        InjurySystem.Inflict(player, Context(sim), "injury.internal", 50, []);

        StatSystem.AdvanceWeek(player, Context(sim), []);

        // 10 × 0.004 + 50 × 0.004 = 0.24 off 21.
        Assert.Equal(20.76, Stat(sim, Purity).Base);

        Stat(sim, Purity).Base = 20;
        Stat(sim, Purity).Deficit = 2;
        player.Resources.First(r => r.ResourceId == "resource.toxins").Current = 0;
        InjurySystem.Heal(player, Context(sim), "injury.internal", 50, []);
        StatSystem.AdvanceWeek(player, Context(sim), []);
        Assert.Equal(1.9, Stat(sim, Purity).Deficit);
    }

    [Fact]
    public void PurifyingPill_GivesPurityWithoutToxins_LessWithEachRepeat()
    {
        var sim = NewGame();
        sim.State.Player.Items.Add(new ItemCount { ItemId = "item.pill.marrow_washing", Count = 2 });

        var first = sim.Execute(new UsePill("item.pill.marrow_washing"));
        sim.Execute(new UsePill("item.pill.marrow_washing"));

        Assert.Contains(new StatChanged(Purity, 6), first.Events);
        Assert.Equal(10.8, Stat(sim, Purity).Base);
        Assert.Equal(0, sim.State.Player.Resources.First(r => r.ResourceId == "resource.toxins").Current);
    }
}
