using Core.Rng;
using Core.State;

namespace Core.Sim;

/// <summary>Advances the world one week at a time.</summary>
public static class TimeSystem
{
    /// <summary>
    /// Runs one week in the fixed order: world systems, player activity progress, event roll,
    /// interrupt collection. Stage 0 has none of these yet, only the clock.
    /// </summary>
    public static void AdvanceWeek(GameState state, RngStreams rng, List<SimEvent> events)
    {
        state.Week++;
        state.Player.AgeWeeks++;
        events.Add(new WeekPassed(state.Week, state.Player.AgeWeeks));
    }
}
