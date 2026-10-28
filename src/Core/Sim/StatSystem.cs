using Core.Character;
using Core.Content;
using Core.State;

namespace Core.Sim;

/// <summary>
/// The one way a stat's base changes (§2.1, D48, D49). A plain stat just moves. A ranked stat has a
/// position on its rank's scale: above the full scale it is overflow, which breaks through to the
/// next rank when there is enough; below the rank's base it is a deficit (for stats that take it),
/// which collapses the stat to the rank below when there is enough. Both fade back to the scale
/// week by week.
/// </summary>
public static class StatSystem
{
    /// <summary>Adds <paramref name="amount"/> points (negative takes them away) and returns how many went in.</summary>
    public static double Change(PlayerState player, SimContext ctx, string statId, double amount, List<SimEvent> events)
    {
        var value = player.Stats.First(s => s.StatId == statId);
        if (ctx.Content.Get<StatDef>(statId).Progress is not { } progressId)
        {
            double before = value.Base;
            value.Base = StateNumbers.Round(Math.Max(0, value.Base + amount));
            return StateNumbers.Round(value.Base - before);
        }

        var progress = ctx.Content.Get<StatProgressDef>(progressId);
        var s = StatProgress.Of(value, progress, ctx.Content);
        double position = value.Base + value.Overflow - value.Deficit;
        double floor = s.Collapse is null ? s.RankBase : double.NegativeInfinity;   // no deficit where there is nowhere to fall
        double after = Math.Max(floor, position + amount);
        value.Base = StateNumbers.Round(Math.Clamp(after, s.RankBase, s.Top));
        value.Overflow = StateNumbers.Round(Math.Max(0, after - s.Top));
        value.Deficit = StateNumbers.Round(Math.Max(0, s.RankBase - after));

        if (s.Breakthrough is { } needed && value.Overflow >= needed)
        {
            value.Rank++;
            value.Overflow = 0;
            events.Add(new StatRankUp(statId, progress.Ranks[value.Rank]));
        }
        else if (s.Collapse is { } limit && value.Deficit >= limit)
        {
            value.Rank--;
            value.Deficit = 0;
            events.Add(new StatRankDown(statId, progress.Ranks[value.Rank]));
        }
        return StateNumbers.Round(after - position);
    }

    /// <summary>
    /// World phase: erosion takes its points (toxins and injuries wearing purity away), then overflow
    /// and deficit fade by their shares; a crumb too small to keep is dropped.
    /// </summary>
    public static void AdvanceWeek(PlayerState player, SimContext ctx, List<SimEvent> events)
    {
        foreach (var value in player.Stats)
        {
            var def = ctx.Content.Get<StatDef>(value.StatId);
            double erosion = def.Erosion.OrderBy(e => e.Key, StringComparer.Ordinal).Sum(e => e.Value * Amount(player, ctx, e.Key));
            if (erosion > 0)
                Change(player, ctx, value.StatId, -erosion, events);
            if (def.Progress is not { } progressId)
                continue;
            var progress = ctx.Content.Get<StatProgressDef>(progressId);
            value.Overflow = Fade(value.Overflow, progress.OverflowDecayPerWeek);
            value.Deficit = Fade(value.Deficit, progress.DeficitHealPerWeek ?? 0);
        }
    }

    private static double Amount(PlayerState player, SimContext ctx, string sourceId) =>
        ctx.Content.TryGet<InjuryDef>(sourceId, out _)
            ? InjurySystem.Value(player, sourceId)
            : player.Resources.First(r => r.ResourceId == sourceId).Current;

    private static double Fade(double amount, double share)
    {
        double left = amount * (1 - share);
        return left < MinRemainder ? 0 : StateNumbers.Round(left);
    }

    private const double MinRemainder = 0.01;
}
