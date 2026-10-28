using Core.Content;
using Core.State;

namespace Core.Sim;

/// <summary>
/// General injuries (§8, tech 4.6, D37): scales that rise from blows and failed breakthroughs and
/// heal with time, rest, meditation and pills. Each scale keeps its modifiers in step with its
/// value, so a lower health ceiling and the like come from the one modifier system.
/// </summary>
public static class InjurySystem
{
    public static double Value(PlayerState player, string injuryId) =>
        player.Injuries.Find(i => i.InjuryId == injuryId)?.Value ?? 0;

    /// <summary>Adds <paramref name="amount"/> to a scale, up to its maximum.</summary>
    public static void Inflict(PlayerState player, SimContext ctx, string injuryId, double amount, List<SimEvent> events)
    {
        var def = ctx.Content.Get<InjuryDef>(injuryId);
        double before = Value(player, injuryId);
        double after = StateNumbers.Round(Math.Min(def.Max, before + amount));
        if (after <= before)
            return;

        Set(player, ctx, def, after);
        events.Add(new InjuryReceived(injuryId, StateNumbers.Round(after - before)));
    }

    /// <summary>Takes up to <paramref name="amount"/> off a scale; a scale that reaches zero is healed and reported.</summary>
    public static void Heal(PlayerState player, SimContext ctx, string injuryId, double amount, List<SimEvent> events)
    {
        double before = Value(player, injuryId);
        if (before <= 0 || amount <= 0)
            return;

        double after = StateNumbers.Round(Math.Max(0, before - amount));
        Set(player, ctx, ctx.Content.Get<InjuryDef>(injuryId), after);
        if (after == 0)
            events.Add(new InjuryHealed(injuryId));
    }

    /// <summary>World phase: every scale heals a little whatever the player does.</summary>
    public static void AdvanceWeek(GameState state, SimContext ctx, List<SimEvent> events)
    {
        foreach (var injury in state.Player.Injuries.ToList())
            Heal(state.Player, ctx, injury.InjuryId, ctx.Content.Get<InjuryDef>(injury.InjuryId).HealPerWeek, events);
    }

    /// <summary>A week of rest heals every scale by its rest rate on top of the natural one.</summary>
    public static void Rest(PlayerState player, SimContext ctx, List<SimEvent> events)
    {
        foreach (var injury in player.Injuries.ToList())
            Heal(player, ctx, injury.InjuryId, ctx.Content.Get<InjuryDef>(injury.InjuryId).RestHealPerWeek, events);
    }

    /// <summary>Stores the new value (no entry at zero), replaces the scale's modifiers and keeps resources under their new maximums.</summary>
    private static void Set(PlayerState player, SimContext ctx, InjuryDef def, double value)
    {
        player.Injuries.RemoveAll(i => i.InjuryId == def.Id);
        ModifierSystem.RemoveSource(player, def.Id);
        if (value > 0)
        {
            player.Injuries.Add(new InjuryLevel { InjuryId = def.Id, Value = value });
            player.Injuries.Sort((a, b) => string.CompareOrdinal(a.InjuryId, b.InjuryId));
            foreach (var (target, perPoint) in def.Effects.OrderBy(e => e.Key, StringComparer.Ordinal))
                ModifierSystem.Apply(player, new Modifier { TargetId = target, Op = ModifierOp.Mul, Value = StateNumbers.Round(perPoint * value), SourceId = def.Id });
        }
        ResourceSystem.ClampToMax(player, ctx);
    }
}
