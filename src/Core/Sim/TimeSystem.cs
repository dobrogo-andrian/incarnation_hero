using Core.State;

namespace Core.Sim;

/// <summary>Runs the current activity week by week (tech 2.3).</summary>
public static class TimeSystem
{
    /// <summary>
    /// Advances weeks until the activity ends, an interrupt of a stopping category fires, an event
    /// opens or the run ends. An interrupt or event ends the activity for good (D44): the player
    /// decides anew what to do. Seclusion runs to its resolution, since no event comes during it
    /// and stopping would throw away its weeks. Non-stopping interrupts are still reported in
    /// <paramref name="events"/>.
    /// </summary>
    public static void RunActivity(GameState state, SimContext ctx, List<SimEvent> events)
    {
        while (state.Activity is { } active && state.Mode == GameMode.Overworld)
        {
            int weekStart = events.Count;
            AdvanceWeek(state, ctx, events);

            bool stopped = state.Mode == GameMode.EventDialog
                || (active.Activity is not BreakthroughActivity && StopsAt(state, events, weekStart));
            if (stopped && state.Activity is not null)
            {
                state.Activity = null;
                events.Add(new ActivityInterrupted(active.Activity));
            }
        }
    }

    private static bool StopsAt(GameState state, List<SimEvent> events, int weekStart)
    {
        for (int i = weekStart; i < events.Count; i++)
        {
            if (events[i] is InterruptEvent interrupt && state.StoppingInterrupts.Contains(interrupt.Category))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Weeks the world lives through while the player is busy with an event (D38): no activity
    /// progress and no event rolls. Stops early if the run ends.
    /// </summary>
    public static void PassWeeks(GameState state, SimContext ctx, int weeks, List<SimEvent> events)
    {
        for (int i = 0; i < weeks; i++)
        {
            if (!AdvanceWorld(state, ctx, events))
                return;
        }
    }

    /// <summary>
    /// One week in the fixed order: world systems, player activity progress, rumors heard, then a
    /// rumor met in its zone or else the event roll, interrupt collection. Systems not built yet
    /// are simply absent.
    /// </summary>
    private static void AdvanceWeek(GameState state, SimContext ctx, List<SimEvent> events)
    {
        if (!AdvanceWorld(state, ctx, events))
            return;

        // The week's context is what the player did during it, before a journey's arrival or an activity's end.
        string? context = EventSystem.ContextOf(state, ctx.Content);
        var activity = state.Activity!.Activity;
        ActivitySystem.AdvanceWeek(state, ctx, events);
        if (state.Mode == GameMode.RunEnd)
            return;
        RumorSystem.RollWeek(state, ctx, RumorSystem.OriginOf(context, activity, ctx), events);
        if (!RumorSystem.TryMeet(state, ctx, activity, events))
            EventSystem.RollWeek(state, ctx, context, events);
    }

    /// <summary>The week's world phase: age, natural death, modifiers, buffs, injuries, resources, places, rumors. False if the run ended.</summary>
    private static bool AdvanceWorld(GameState state, SimContext ctx, List<SimEvent> events)
    {
        state.Week++;
        state.Player.AgeWeeks++;
        events.Add(new WeekPassed(state.Week, state.Player.AgeWeeks));

        LifeSystem.AdvanceWeek(state, ctx.Config, ctx.Rng, events);
        if (state.Mode == GameMode.RunEnd)
            return false;
        ModifierSystem.AdvanceWeek(state);
        CultivationSystem.AdvanceWeek(state, ctx, events);
        InjurySystem.AdvanceWeek(state, ctx, events);
        ResourceSystem.AdvanceWeek(state, ctx);
        WorldSystem.AdvanceWeek(state, ctx.Content);
        RumorSystem.AdvanceWeek(state, ctx);
        return true;
    }
}
