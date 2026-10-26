using Core.State;

namespace Core.Sim;

/// <summary>Runs the current activity week by week (tech 2.3).</summary>
public static class TimeSystem
{
    /// <summary>
    /// Advances weeks until the activity ends, an interrupt of a stopping category fires or the
    /// run ends. Non-stopping interrupts are still reported in <paramref name="events"/>.
    /// </summary>
    public static void RunActivity(GameState state, SimContext ctx, List<SimEvent> events)
    {
        while (true)
        {
            int weekStart = events.Count;
            AdvanceWeek(state, ctx, events);

            if (state.Mode == GameMode.RunEnd || state.Activity is null)
                return;
            for (int i = weekStart; i < events.Count; i++)
            {
                if (events[i] is InterruptEvent interrupt && state.StoppingInterrupts.Contains(interrupt.Category))
                    return;
            }
        }
    }

    /// <summary>
    /// One week in the fixed order: world systems, player activity progress, event roll,
    /// interrupt collection. Systems not built yet are simply absent.
    /// </summary>
    private static void AdvanceWeek(GameState state, SimContext ctx, List<SimEvent> events)
    {
        state.Week++;
        state.Player.AgeWeeks++;
        events.Add(new WeekPassed(state.Week, state.Player.AgeWeeks));

        LifeSystem.AdvanceWeek(state, ctx.Config, ctx.Rng, events);
        if (state.Mode == GameMode.RunEnd)
            return;
        ModifierSystem.AdvanceWeek(state);
        CultivationSystem.AdvanceWeek(state, ctx, events);
        ResourceSystem.AdvanceWeek(state, ctx);
        WorldSystem.AdvanceWeek(state, ctx.Content);

        ActivitySystem.AdvanceWeek(state, ctx, events);
    }
}
