using Core.Rng;
using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

public class SimulationTests
{
    private static readonly int WeeksPerYear = TestContent.Config.WeeksPerYear;

    private static Simulation NewGame(ulong seed = 1) => Simulation.NewGame(TestContent.Repo, seed);

    /// <summary>A new game moved to <paramref name="weeksBefore"/> weeks before the 5-year life-end warning.</summary>
    private static Simulation NearFiveYearWarning(int weeksBefore)
    {
        var sim = NewGame();
        var player = sim.State.Player;
        player.AgeWeeks = player.LifespanWeeks - 5 * WeeksPerYear - weeksBefore;
        return sim;
    }

    [Fact]
    public void NewGame_StartsFromRunConfig()
    {
        var sim = NewGame();

        Assert.Equal(0, sim.State.Week);
        Assert.Equal(12 * 52, sim.State.Player.AgeWeeks);
        Assert.Equal(60 * 52, sim.State.Player.LifespanWeeks);
        Assert.Equal(GameMode.Overworld, sim.State.Mode);
        Assert.Null(sim.State.Activity);
        Assert.Equal(Enum.GetValues<InterruptCategory>(), sim.State.StoppingInterrupts);
        Assert.Equal(RngStreams.Count, sim.State.RngStates.Length);
        var track = Assert.Single(sim.State.Player.Tracks);
        Assert.Equal("qi", track.VectorId);
        Assert.Null(track.LastBreakthroughQuality);
    }

    [Fact]
    public void StartActivity_RunsToCompletion()
    {
        var sim = NewGame();

        var outcome = sim.Execute(TestContent.Wait(3));

        Assert.True(outcome.Accepted);
        Assert.Equal(3, sim.State.Week);
        Assert.Equal(12 * 52 + 3, sim.State.Player.AgeWeeks);
        Assert.Null(sim.State.Activity);
        Assert.Equal(
            [new WeekPassed(1, 625), new WeekPassed(2, 626), new WeekPassed(3, 627), new ActivityFinished(new WaitActivity())],
            outcome.Events);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StartActivity_RejectsNonPositiveWeeks(int weeks)
    {
        var sim = NewGame();

        var outcome = sim.Execute(TestContent.Wait(weeks));

        Assert.False(outcome.Accepted);
        Assert.Equal(RejectReasons.InvalidWeeks, outcome.RejectReason);
        Assert.Empty(outcome.Events);
        Assert.Equal(0, sim.State.Week);
    }

    [Theory]
    [InlineData(GameMode.EventDialog)]
    [InlineData(GameMode.Combat)]
    [InlineData(GameMode.RunEnd)]
    public void OverworldCommand_IsRejectedInOtherModes(GameMode mode)
    {
        var sim = NewGame();
        sim.State.Mode = mode;

        var outcome = sim.Execute(TestContent.Wait(1));

        Assert.False(outcome.Accepted);
        Assert.Equal(RejectReasons.WrongMode, outcome.RejectReason);
        Assert.Equal(0, sim.State.Week);
    }

    [Fact]
    public void StoppingInterrupt_LeavesActivityWithRemainder_AndContinueFinishesIt()
    {
        var sim = NearFiveYearWarning(weeksBefore: 2);

        var first = sim.Execute(TestContent.Wait(10));

        Assert.Equal(new LifeEndApproaching(5), first.Events[^1]);
        Assert.Equal(2, sim.State.Week);
        Assert.Equal(8, sim.State.Activity!.WeeksLeft);

        var second = sim.Execute(new ContinueActivity());

        Assert.Equal(new ActivityFinished(new WaitActivity()), second.Events[^1]);
        Assert.Equal(10, sim.State.Week);
        Assert.Null(sim.State.Activity);
    }

    [Fact]
    public void NonStoppingInterrupt_IsReportedAndTimeGoesOn()
    {
        var sim = NearFiveYearWarning(weeksBefore: 2);
        sim.Execute(new SetInterruptStops([]));

        var outcome = sim.Execute(TestContent.Wait(10));

        Assert.Contains(new LifeEndApproaching(5), outcome.Events);
        Assert.Equal(10, sim.State.Week);
        Assert.Null(sim.State.Activity);
    }

    [Fact]
    public void SetInterruptStops_StoresSortedDistinctCategories()
    {
        var sim = NewGame();

        sim.Execute(new SetInterruptStops([InterruptCategory.LifeEndApproaching, InterruptCategory.LifeEndApproaching]));

        Assert.Equal([InterruptCategory.LifeEndApproaching], sim.State.StoppingInterrupts);
    }

    [Fact]
    public void ContinueAndCancel_WithoutActivity_AreRejected()
    {
        var sim = NewGame();

        Assert.Equal(RejectReasons.NoActivity, sim.Execute(new ContinueActivity()).RejectReason);
        Assert.Equal(RejectReasons.NoActivity, sim.Execute(new CancelActivity()).RejectReason);
    }

    [Fact]
    public void Cancel_DropsInterruptedActivity()
    {
        var sim = NearFiveYearWarning(weeksBefore: 1);
        sim.Execute(TestContent.Wait(10));

        var outcome = sim.Execute(new CancelActivity());

        Assert.Equal([new ActivityCancelled(new WaitActivity())], outcome.Events);
        Assert.Null(sim.State.Activity);
        Assert.Equal(1, sim.State.Week);
    }

    [Fact]
    public void OpenEndedWait_EndsInNaturalDeathInsideTheWindow()
    {
        var sim = NewGame(seed: 3);

        var outcome = sim.Execute(TestContent.Wait(null));
        while (sim.State.Mode != GameMode.RunEnd)
            outcome = sim.Execute(new ContinueActivity());

        Assert.Equal(new RunEnded(RunEndCause.OldAge), outcome.Events[^1]);
        Assert.Equal(RunEndCause.OldAge, sim.State.EndCause);
        Assert.Null(sim.State.Activity);
        var player = sim.State.Player;
        Assert.True(player.AgeWeeks > player.LifespanWeeks - TestContent.Config.NaturalDeathWindowYears * WeeksPerYear);
        Assert.Equal(RejectReasons.WrongMode, sim.Execute(TestContent.Wait(1)).RejectReason);
    }

    [Fact]
    public void PlayerView_ReflectsState()
    {
        var sim = NearFiveYearWarning(weeksBefore: 1);
        sim.Execute(TestContent.Wait(4));

        var view = sim.View;

        Assert.Equal(1, view.Week);
        Assert.Equal(sim.State.Player.AgeWeeks, view.AgeWeeks);
        Assert.Equal(60 * 52, view.LifespanWeeks);
        Assert.Equal(GameMode.Overworld, view.Mode);
        Assert.Equal(new ActivityView(new WaitActivity(), 3), view.Activity);
        Assert.Null(view.EndCause);
        Assert.Equal(new TrackView("qi", 0, 0), Assert.Single(view.Tracks));
    }
}
