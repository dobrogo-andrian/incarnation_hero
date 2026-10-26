using Core.State;

namespace Core.Sim;

/// <summary>Weekly progress of the player's activity. Each activity kind adds its effect here.</summary>
public static class ActivitySystem
{
    /// <summary>
    /// Whether the player chooses how long <paramref name="activity"/> lasts. Travel takes as long as
    /// its route, so it is started with the Travel command instead of StartActivity.
    /// </summary>
    public static bool HasPlayerChosenLength(Activity activity) => activity is not TravelActivity;

    public static void AdvanceWeek(GameState state, List<SimEvent> events)
    {
        var active = state.Activity!;
        if (active.WeeksLeft is int left)
            active.WeeksLeft = left - 1;

        switch (active.Activity)
        {
            case WaitActivity:
                break;
            case TravelActivity travel:
                EnterReachedZone(state, travel, travel.TotalWeeks - active.WeeksLeft!.Value, events);
                break;
            default:
                throw new NotSupportedException($"No progress handler for {active.Activity.GetType().Name}.");
        }

        if (active.WeeksLeft == 0)
        {
            state.Activity = null;
            events.Add(new ActivityFinished(active.Activity));
        }
    }

    private static void EnterReachedZone(GameState state, TravelActivity travel, int weeksTravelled, List<SimEvent> events)
    {
        int arrival = 0;
        foreach (var leg in travel.Legs)
        {
            arrival += leg.Weeks;
            if (arrival == weeksTravelled)
            {
                state.Player.ZoneId = leg.ZoneId;
                events.Add(new ZoneEntered(leg.ZoneId));
                return;
            }
        }
    }
}
