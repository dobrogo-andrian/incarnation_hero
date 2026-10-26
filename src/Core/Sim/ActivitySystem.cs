using Core.Loc;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Checks and weekly progress of the player's activity. A new activity kind adds a case to
/// <see cref="Validate"/> and to <see cref="AdvanceWeek"/>.
/// </summary>
public static class ActivitySystem
{
    /// <summary>Why <paramref name="activity"/> cannot be started with StartActivity now, or null if it can.</summary>
    public static LocKey? Validate(GameState state, SimContext ctx, Activity activity) => activity switch
    {
        WaitActivity => null,
        MeditateActivity m => MeditationSystem.Validate(state, ctx, m),
        // Travel and breakthroughs last as long as their route or stage, so they have their own commands.
        _ => RejectReasons.InvalidActivity,
    };

    public static void AdvanceWeek(GameState state, SimContext ctx, List<SimEvent> events)
    {
        var active = state.Activity!;
        if (active.WeeksLeft is int left)
            active.WeeksLeft = left - 1;

        switch (active.Activity)
        {
            case WaitActivity:
                InjurySystem.Rest(state.Player, ctx, events);
                break;
            case MeditateActivity meditation:
                MeditationSystem.AdvanceWeek(state, ctx, meditation, events);
                break;
            case TravelActivity travel:
                EnterReachedZone(state, travel, travel.TotalWeeks - active.WeeksLeft!.Value, events);
                break;
            case BreakthroughActivity attempt:
                if (active.WeeksLeft == 0)
                    BreakthroughSystem.Resolve(state, ctx, attempt, events);
                break;
            default:
                throw new NotSupportedException($"No progress handler for {active.Activity.GetType().Name}.");
        }

        if (active.WeeksLeft == 0 && state.Mode != GameMode.RunEnd)
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
