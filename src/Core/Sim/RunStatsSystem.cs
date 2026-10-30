using Core.Combat;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Keeps the run's tallies for its summary (D47): every week by what it was spent on, counted as it
/// passes, and everything else read from the SimEvents of each accepted command. The rules never read them.
/// </summary>
public static class RunStatsSystem
{
    /// <summary>What a week of <paramref name="activity"/> is counted as: meditation by its direction, training by its job, ground or arena, the rest by kind.</summary>
    public static string ActivityKey(Activity activity) => activity switch
    {
        MeditateActivity m => m.DirectionId,
        WorkActivity w => w.JobId,
        HuntActivity h => h.HuntId,
        ArenaActivity a => a.ArenaId,
        WaitActivity => "wait",
        InquireActivity => "inquire",
        TravelActivity => "travel",
        BreakthroughActivity => "breakthrough",
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, null),
    };

    /// <summary>Weeks that pass with no activity are spent inside an event (its <c>time</c> effect).</summary>
    public const string EventKey = "event";

    public static void AddWeek(RunStats stats, Activity? activity)
    {
        string key = activity is null ? EventKey : ActivityKey(activity);
        int index = stats.ActivityWeeks.FindIndex(t => t.Key == key);
        if (index >= 0)
        {
            stats.ActivityWeeks[index].Weeks++;
            return;
        }
        int at = stats.ActivityWeeks.FindIndex(t => string.CompareOrdinal(t.Key, key) > 0);
        stats.ActivityWeeks.Insert(at < 0 ? stats.ActivityWeeks.Count : at, new WeekTally { Key = key, Weeks = 1 });
    }

    /// <summary>Counts what <paramref name="events"/> tell; ages come from the weeks passed among them.</summary>
    public static void Record(GameState state, IReadOnlyList<SimEvent> events)
    {
        var stats = state.Stats;
        int age = state.Player.AgeWeeks - events.OfType<WeekPassed>().Count();
        foreach (var e in events)
        {
            switch (e)
            {
                case WeekPassed passed:
                    age = passed.AgeWeeks;
                    break;
                case BreakthroughSucceeded b:
                    stats.Breakthroughs.Add(new BreakthroughRecord { AgeWeeks = age, RankId = b.RankId });
                    break;
                case BreakthroughFailed b:
                    stats.Breakthroughs.Add(new BreakthroughRecord { AgeWeeks = age, FailureZoneId = b.FailureZoneId });
                    break;
                case CombatResolved { End: CombatEnd.Won }:
                    stats.CombatsWon++;
                    break;
                case CombatResolved { End: CombatEnd.Fled or CombatEnd.Yielded }:
                    stats.CombatsFled++;
                    break;
                case CombatResolved { End: CombatEnd.Beaten }:
                    stats.CombatsBeaten++;
                    break;
                case CombatResolved:
                    stats.CombatsDied++;
                    break;
                case PillTaken:
                    stats.PillsTaken++;
                    break;
                case StonesChanged { Amount: > 0 } s:
                    stats.StonesEarned += s.Amount;
                    break;
                case StonesChanged s:
                    stats.StonesSpent -= s.Amount;
                    break;
                case EventStarted:
                    stats.Dialogs++;
                    break;
                case RumorHeard:
                    stats.RumorsHeard++;
                    break;
            }
        }
    }
}
