using Core.State;

namespace Core.Sim;

/// <summary>Weekly progress of the player's activity. Each activity kind adds its effect here.</summary>
public static class ActivitySystem
{
    public static void AdvanceWeek(GameState state, List<SimEvent> events)
    {
        var active = state.Activity!;
        switch (active.Activity)
        {
            case WaitActivity:
                break;
            default:
                throw new NotSupportedException($"No progress handler for {active.Activity.GetType().Name}.");
        }

        if (active.WeeksLeft is int left)
        {
            active.WeeksLeft = left - 1;
            if (active.WeeksLeft == 0)
            {
                state.Activity = null;
                events.Add(new ActivityFinished(active.Activity));
            }
        }
    }
}
