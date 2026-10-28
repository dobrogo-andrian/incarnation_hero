using Core.Content;
using Core.State;

namespace Core.Sim;

/// <summary>Carried items and taking pills (§4.3–4.4).</summary>
public static class ItemSystem
{
    public static int Count(List<ItemCount> counts, string itemId) =>
        counts.Find(c => c.ItemId == itemId)?.Count ?? 0;

    /// <summary>Changes the count of <paramref name="itemId"/> by <paramref name="delta"/>; keeps the list in ID order without empty entries.</summary>
    public static void Change(List<ItemCount> counts, string itemId, int delta)
    {
        if (counts.Find(c => c.ItemId == itemId) is not { } entry)
        {
            entry = new ItemCount { ItemId = itemId };
            counts.Add(entry);
            counts.Sort((a, b) => string.CompareOrdinal(a.ItemId, b.ItemId));
        }
        entry.Count += delta;
        if (entry.Count <= 0)
            counts.Remove(entry);
    }

    /// <summary>Energy the next pill of this type gives: every earlier one of the same type shrinks it by the repeat factor.</summary>
    public static double NextPillEnergy(PlayerState player, string itemId, PillDef pill) =>
        StateNumbers.Round(pill.Energy * Math.Pow(pill.RepeatFactor, Count(player.PillsTaken, itemId)));

    /// <summary>
    /// Takes a carried pill: its energy joins the pill-energy buff, a breakthrough pill raises the next
    /// breakthrough's ceiling (the strongest one counts), a healing pill heals injuries (§8), and its
    /// toxins build up; a purifying pill gives stat points, shrinking with repeats like energy (D49).
    /// Refused when the toxins would go over their maximum, the tolerance (§4.4), or when a pill that only heals has nothing to heal.
    /// </summary>
    public static CommandOutcome UsePill(PlayerState player, SimContext ctx, string itemId)
    {
        if (Count(player.Items, itemId) == 0)
            return CommandOutcome.Reject(RejectReasons.NoSuchItem);
        if (ctx.Content.Get<ItemDef>(itemId).Pill is not { } pill)
            return CommandOutcome.Reject(RejectReasons.NotAPill);
        if (pill.Energy + pill.CeilingBonus <= 0 && pill.Stats.Count == 0 && pill.Heals.Keys.All(id => InjurySystem.Value(player, id) == 0))
            return CommandOutcome.Reject(RejectReasons.NothingToHeal);

        string toxinsId = ctx.Config.ToxinsResource;
        double toxins = player.Resources.First(r => r.ResourceId == toxinsId).Current;
        if (toxins + pill.Toxicity > ctx.Rules.Value(player, toxinsId))
            return CommandOutcome.Reject(RejectReasons.ToxinsTooHigh);

        double energy = NextPillEnergy(player, itemId, pill);
        Change(player.Items, itemId, -1);
        Change(player.PillsTaken, itemId, 1);
        if (energy > 0)
            CultivationSystem.AddPillEnergy(player, energy);
        player.BreakthroughCeilingBonus = Math.Max(player.BreakthroughCeilingBonus, pill.CeilingBonus);
        ResourceSystem.Restore(player, ctx, toxinsId, pill.Toxicity);
        var events = new List<SimEvent> { new PillTaken(itemId, energy) };
        foreach (var (injuryId, amount) in pill.Heals.OrderBy(h => h.Key, StringComparer.Ordinal))
            InjurySystem.Heal(player, ctx, injuryId, amount, events);
        double strength = Math.Pow(pill.RepeatFactor, Count(player.PillsTaken, itemId) - 1);
        foreach (var (statId, amount) in pill.Stats.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            var rankChanges = new List<SimEvent>();
            events.Add(new StatChanged(statId, StatSystem.Change(player, ctx, statId, amount * strength, rankChanges)));
            events.AddRange(rankChanges);
        }
        return CommandOutcome.Accept(events);
    }
}
