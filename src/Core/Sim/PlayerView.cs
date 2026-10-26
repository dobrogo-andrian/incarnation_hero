using Core.State;

namespace Core.Sim;

/// <summary>
/// Everything the UI may read. Built from the true state, but only exposes what the player knows.
/// </summary>
public sealed record PlayerView(
    int Week,
    int AgeWeeks,
    int LifespanWeeks,
    GameMode Mode,
    ActivityView? Activity,
    RunEndCause? EndCause,
    IReadOnlyList<TrackView> Tracks)
{
    public static PlayerView From(GameState state) => new(
        state.Week,
        state.Player.AgeWeeks,
        state.Player.LifespanWeeks,
        state.Mode,
        state.Activity is { } active ? new ActivityView(active.Activity, active.WeeksLeft) : null,
        state.EndCause,
        state.Player.Tracks.Select(t => new TrackView(t.VectorId, t.StageIndex, t.StageProgress)).ToList());
}

public sealed record ActivityView(Activity Activity, int? WeeksLeft);

public sealed record TrackView(string VectorId, int StageIndex, int StageProgress);
