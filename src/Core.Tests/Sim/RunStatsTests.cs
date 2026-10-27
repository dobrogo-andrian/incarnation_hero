using Core.Combat;
using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

/// <summary>The run's tallies for its summary (D47).</summary>
public sealed class RunStatsTests
{
    [Fact]
    public void Weeks_AreCountedByActivity_InKeyOrder()
    {
        var sim = Simulation.NewGame(TestContent.Calm, 1);

        sim.Execute(TestContent.Wait(3));
        sim.Execute(new StartActivity(new MeditateActivity("meditation.purification", "place.hub.quiet_courtyard"), 2));

        Assert.Equal(
            [("meditation.purification", 2), ("wait", 3)],
            sim.View.RunStats.ActivityWeeks.Select(t => (t.Key, t.Weeks)));
    }

    [Fact]
    public void EveryWeekOfARun_IsCounted_EventWeeksIncluded()
    {
        var sim = Simulation.NewGame(TestContent.Repo, 7);

        Assert.True(Script.Run(sim, TestContent.Wait(null)));

        var weeks = sim.View.RunStats.ActivityWeeks;
        Assert.Equal(GameMode.RunEnd, sim.View.Mode);
        Assert.Equal(sim.View.Week, weeks.Sum(t => t.Weeks));
        Assert.Contains(weeks, t => t.Key == RunStatsSystem.EventKey);
    }

    [Fact]
    public void Commands_AreTalliedFromTheirEvents()
    {
        var sim = Simulation.NewGame(TestContent.Calm, 1);

        sim.Execute(new UsePill("item.pill.qi_gathering"));
        sim.Execute(new Buy("item.pill.qi_gathering"));
        sim.Execute(new Buy("item.pill.qi_gathering"));
        var third = sim.Execute(new Buy("item.pill.qi_gathering"));   // 30 stones cover two; a rejected purchase counts nothing
        var stats = sim.View.RunStats;

        Assert.False(third.Accepted);
        Assert.Equal((1, 24, 0), (stats.PillsTaken, stats.StonesSpent, stats.StonesEarned));
    }

    [Fact]
    public void Breakthroughs_AreRecordedAtTheAgeOfTheirWeek_AndCombatsByEnd()
    {
        var state = new GameState { Player = new PlayerState { AgeWeeks = 100 } };

        RunStatsSystem.Record(state,
        [
            new WeekPassed(1, 99),
            new BreakthroughSucceeded("qi", "stage.qi.2", "rank.common", 64),
            new WeekPassed(2, 100),
            new BreakthroughFailed("qi", "failure_zone.light"),
            new CombatResolved("group.a", CombatEnd.Won, 3, 1, 1),
            new CombatResolved("group.a", CombatEnd.Fled, 3, 1, 1),
            new StonesChanged(7),
        ]);

        var stats = state.Stats;
        Assert.Equal(
            [(99, "rank.common", (string?)null), (100, null, "failure_zone.light")],
            stats.Breakthroughs.Select(b => (b.AgeWeeks, b.RankId, b.FailureZoneId)));
        Assert.Equal((1, 1, 0, 7), (stats.CombatsWon, stats.CombatsFled, stats.CombatsDied, stats.StonesEarned));
    }
}
