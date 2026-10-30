using Core.Content;
using Core.Loc;
using Core.Rng;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Training activities (§5): hired work, hunting and arena bouts. Every week trains the body by the
/// def's gains; work pays a wage, hunting may meet prey (a real fight), the arena takes a fee for a
/// bout that ends in yielding rather than death and pays a prize for a win. Hunting and the arena
/// need fighting health, so a spent body ends them through the same check as their start (D44).
/// </summary>
public static class TrainingSystem
{
    /// <summary>Why <paramref name="activity"/> cannot start now, or null if it can.</summary>
    public static LocKey? Validate(GameState state, SimContext ctx, Activity activity)
    {
        var player = state.Player;
        switch (activity)
        {
            case WorkActivity w:
                return Here<JobDef>(player, ctx, w.JobId) is null ? RejectReasons.NotHere : null;
            case HuntActivity h:
                return Here<HuntDef>(player, ctx, h.HuntId) is null ? RejectReasons.NotHere
                    : !CombatSystem.FitToFight(player, ctx) ? RejectReasons.TooWounded
                    : null;
            case ArenaActivity a:
                if (Here<ArenaDef>(player, ctx, a.ArenaId) is not { } arena)
                    return RejectReasons.NotHere;
                if (a.Bout < 0 || a.Bout >= arena.Bouts.Count)
                    return RejectReasons.InvalidActivity;
                return player.Stones < arena.Bouts[a.Bout].Fee ? RejectReasons.NotEnoughStones
                    : !CombatSystem.FitToFight(player, ctx) ? RejectReasons.TooWounded
                    : null;
            default:
                throw new ArgumentOutOfRangeException(nameof(activity), activity, null);
        }
    }

    public static void AdvanceWeek(GameState state, SimContext ctx, Activity activity, List<SimEvent> events)
    {
        var player = state.Player;
        switch (activity)
        {
            case WorkActivity w:
                var job = ctx.Content.Get<JobDef>(w.JobId);
                Train(player, ctx, job, events);
                Pay(player, job.Pay, routine: true, events);
                break;
            case HuntActivity h:
                var hunt = ctx.Content.Get<HuntDef>(h.HuntId);
                Train(player, ctx, hunt, events);
                var rng = ctx.Rng[RngStream.Events];
                if (rng.Chance(hunt.EncounterChance))
                {
                    var prey = rng.PickWeighted(hunt.Groups.OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => (g.Key, g.Value)).ToList());
                    CombatSystem.Offer(state, ctx, CombatSystem.NewFight(ctx, prey, FightKind.Hunt), events);
                }
                break;
            case ArenaActivity a:
                var arena = ctx.Content.Get<ArenaDef>(a.ArenaId);
                var bout = arena.Bouts[a.Bout];
                Train(player, ctx, arena, events);
                Pay(player, -bout.Fee, routine: true, events);
                CombatSystem.Offer(state, ctx, CombatSystem.NewFight(ctx, bout.Group, FightKind.Arena, prize: bout.Prize), events);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(activity), activity, null);
        }
    }

    /// <summary>The def of <paramref name="id"/> if it is one of <typeparamref name="T"/> in the player's zone.</summary>
    private static T? Here<T>(PlayerState player, SimContext ctx, string id) where T : TrainingDef =>
        ctx.Content.TryGet<T>(id, out var def) && def.Zone == player.ZoneId ? def : null;

    private static void Train(PlayerState player, SimContext ctx, TrainingDef def, List<SimEvent> events)
    {
        foreach (var (statId, gain) in def.Gains.OrderBy(g => g.Key, StringComparer.Ordinal))
            StatSystem.Gain(player, ctx, statId, gain, events);
    }

    private static void Pay(PlayerState player, int stones, bool routine, List<SimEvent> events)
    {
        if (stones == 0)
            return;
        player.Stones += stones;
        events.Add(new StonesChanged(stones, routine));
    }
}
