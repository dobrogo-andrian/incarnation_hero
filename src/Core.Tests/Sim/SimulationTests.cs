using Core.Rng;
using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

public class SimulationTests
{
    private static readonly NewGameSettings Settings = new(StartAgeWeeks: 12 * 52);

    [Fact]
    public void NewGame_StartsAtWeekZeroWithQiTrack()
    {
        var sim = Simulation.NewGame(1, Settings);

        Assert.Equal(0, sim.State.Week);
        Assert.Equal(12 * 52, sim.State.Player.AgeWeeks);
        Assert.Equal(GameMode.Overworld, sim.State.Mode);
        Assert.Equal(RngStreams.Count, sim.State.RngStates.Length);
        var track = Assert.Single(sim.State.Player.Tracks);
        Assert.Equal("qi", track.VectorId);
        Assert.Null(track.LastBreakthroughQuality);
    }

    [Fact]
    public void WaitWeeks_AdvancesClockAndEmitsOneEventPerWeek()
    {
        var sim = Simulation.NewGame(1, Settings);

        var outcome = sim.Execute(new WaitWeeks(3));

        Assert.True(outcome.Accepted);
        Assert.Equal(3, sim.State.Week);
        Assert.Equal(12 * 52 + 3, sim.State.Player.AgeWeeks);
        Assert.Equal(
            [new WeekPassed(1, 625), new WeekPassed(2, 626), new WeekPassed(3, 627)],
            outcome.Events);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WaitWeeks_RejectsNonPositive(int weeks)
    {
        var sim = Simulation.NewGame(1, Settings);

        var outcome = sim.Execute(new WaitWeeks(weeks));

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
        var sim = Simulation.NewGame(1, Settings);
        sim.State.Mode = mode;

        var outcome = sim.Execute(new WaitWeeks(1));

        Assert.False(outcome.Accepted);
        Assert.Equal(RejectReasons.WrongMode, outcome.RejectReason);
        Assert.Equal(0, sim.State.Week);
    }

    [Fact]
    public void Resume_ContinuesRngStreamsFromSavedState()
    {
        var original = Simulation.NewGame(77, Settings);
        original.Execute(new WaitWeeks(5));

        var resumed = Simulation.Resume(original.State);

        Assert.Equal(original.State.RngStates, resumed.State.RngStates);
        Assert.Equal(original.View, resumed.View, new PlayerViewComparer());
    }

    [Fact]
    public void PlayerView_ReflectsState()
    {
        var sim = Simulation.NewGame(1, Settings);
        sim.Execute(new WaitWeeks(2));

        var view = sim.View;

        Assert.Equal(2, view.Week);
        Assert.Equal(12 * 52 + 2, view.AgeWeeks);
        Assert.Equal(GameMode.Overworld, view.Mode);
        Assert.Equal(new TrackView("qi", 0, 0), Assert.Single(view.Tracks));
    }

    private sealed class PlayerViewComparer : IEqualityComparer<PlayerView>
    {
        public bool Equals(PlayerView? x, PlayerView? y) =>
            x is not null && y is not null
            && x.Week == y.Week && x.AgeWeeks == y.AgeWeeks && x.Mode == y.Mode
            && x.Tracks.SequenceEqual(y.Tracks);

        public int GetHashCode(PlayerView obj) => obj.Week;
    }
}
