using Core.Content;
using Core.Rng;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Rumors (§10, tech 4.3, D41): heard in a settlement or from events, followed for a window of
/// weeks while rivals may take them, and met as a dialog on arriving in their zone.
/// </summary>
public static class RumorSystem
{
    /// <summary>World phase: rumors past their window are dropped, then rivals may take the rest (D68).</summary>
    public static void AdvanceWeek(GameState state, SimContext ctx)
    {
        state.Rumors.RemoveAll(r => state.Week > r.LastWeek);
        var rng = ctx.Rng[RngStream.Rumors];
        foreach (var rumor in state.Rumors.Where(r => !r.Taken))
        {
            if (rng.Chance(ctx.Content.Get<RumorDef>(rumor.RumorId).Appeal * ctx.Rumors.InterceptPerAppeal))
                rumor.Taken = true;
        }
    }

    /// <summary>How the week's rumors come, given its event context and the activity it was spent on; null brings none.</summary>
    public static RumorOriginDef? OriginOf(string? context, Activity activity, SimContext ctx) => context switch
    {
        EventContexts.Settlement => activity is AdventureActivity ? ctx.Rumors.Adventure : ctx.Rumors.Settlement,
        _ => null,
    };

    /// <summary>Rumor phase of a week: maybe the player hears one of the rumors <paramref name="origin"/> can bring.</summary>
    public static void RollWeek(GameState state, SimContext ctx, RumorOriginDef? origin, List<SimEvent> events)
    {
        if (origin is null || state.Rumors.Count >= ctx.Rumors.MaxActive)
            return;
        var rng = ctx.Rng[RngStream.Rumors];
        if (!rng.Chance(origin.WeekChance))
            return;

        var candidates = ctx.Content.All<RumorDef>().Where(r => CanBring(state, ctx, r)).Select(r => (r, r.Weight)).ToList();
        if (candidates.Count == 0)
            return;
        var def = rng.PickWeighted(candidates);
        string source = rng.PickWeighted(origin.Sources.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => (s.Key, s.Value)).ToList());
        Hear(state, ctx, def, source, events);
    }

    /// <summary>
    /// The player hears <paramref name="def"/> from <paramref name="sourceId"/>: its window is rolled,
    /// and what is really there is the truth with the source's reliability. Nothing if it is already followed.
    /// </summary>
    public static void Hear(GameState state, SimContext ctx, RumorDef def, string sourceId, List<SimEvent> events)
    {
        if (state.Rumors.Any(r => r.RumorId == def.Id))
            return;
        var rng = ctx.Rng[RngStream.Rumors];
        int window = rng.NextInt((int)def.WindowWeeks.Min!.Value, (int)def.WindowWeeks.Max!.Value + 1);
        double reliability = ctx.Content.Get<RumorSourceDef>(sourceId).Reliability;
        string node = def.Falsehoods.Count == 0 || rng.Chance(reliability)
            ? def.Truth
            : rng.PickWeighted(def.Falsehoods.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => (f.Key, f.Value)).ToList());

        state.Rumors.Add(new Rumor { RumorId = def.Id, SourceId = sourceId, HeardWeek = state.Week, LastWeek = state.Week + window, TrueNode = node });
        if (state.RumorHistory.Find(r => r.RumorId == def.Id) is not { } record)
        {
            record = new RumorRecord { RumorId = def.Id };
            state.RumorHistory.Add(record);
            state.RumorHistory.Sort((a, b) => string.CompareOrdinal(a.RumorId, b.RumorId));
        }
        record.LastHeardWeek = state.Week;
        events.Add(new RumorHeard(def.Id));
    }

    /// <summary>
    /// After a week spent in a rumor's zone (arriving there, or staying, but not passing through or
    /// in seclusion), opens its dialog at what is really there, or at its traces if rivals took it.
    /// The rumor is then over. True if a dialog opened.
    /// </summary>
    public static bool TryMeet(GameState state, SimContext ctx, Activity weekActivity, List<SimEvent> events)
    {
        bool stayed = weekActivity switch
        {
            TravelActivity => state.Activity is null,
            BreakthroughActivity => false,
            _ => true,
        };
        if (!stayed || state.Mode != GameMode.Overworld)
            return false;
        int index = state.Rumors.FindIndex(r => ctx.Content.Get<RumorDef>(r.RumorId).Zone == state.Player.ZoneId);
        if (index < 0)
            return false;

        var rumor = state.Rumors[index];
        state.Rumors.RemoveAt(index);
        EventSystem.Open(state, rumor.RumorId, rumor.Taken ? RumorDef.TakenNode : rumor.TrueNode, events);
        return true;
    }

    /// <summary>Rumors as the player knows them: the claim, the source and a range around its reliability.</summary>
    public static IReadOnlyList<RumorView> View(GameState state, SimContext ctx) =>
        state.Rumors.Select(r => new RumorView(
            r.RumorId,
            r.SourceId,
            ctx.Content.Get<RumorDef>(r.RumorId).Zone,
            r.LastWeek - state.Week,
            Observation.Chance(ctx.Content.Get<RumorSourceDef>(r.SourceId).Reliability, state.Player, ctx, StableHash.Of(state.Seed, r.RumorId, r.HeardWeek))))
        .ToList();

    /// <summary>A settlement can bring a rumor that is not followed, is off cooldown, leads to a known zone and whose conditions hold.</summary>
    private static bool CanBring(GameState state, SimContext ctx, RumorDef def) =>
        !state.Rumors.Any(r => r.RumorId == def.Id)
        && !(state.RumorHistory.Find(r => r.RumorId == def.Id) is { } record && state.Week - record.LastHeardWeek < def.CooldownWeeks)
        && WorldSystem.IsKnown(state.World, ctx.Content, def.Zone)
        && def.Conditions.All(c => EventSystem.Holds(c, state, ctx));
}

/// <summary>A rumor being followed: where, how many weeks are left, and how far its source can be trusted.</summary>
public sealed record RumorView(string RumorId, string SourceId, string ZoneId, int WeeksLeft, ObservedRange Reliability);
