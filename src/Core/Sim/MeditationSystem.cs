using Core.Content;
using Core.Loc;
using Core.State;

namespace Core.Sim;

/// <summary>
/// One week of meditation (§4.2, D30): draw the place's density from its reserve, then add
/// rate × density × efficiency to the direction's target. Efficiency is full while the reserve
/// covers the draw and falls to reserve / draw, but never below the configured floor.
/// </summary>
public static class MeditationSystem
{
    /// <summary>Why <paramref name="meditation"/> cannot start now, or null if it can.</summary>
    public static LocKey? Validate(GameState state, SimContext ctx, MeditateActivity meditation)
    {
        if (!ctx.Content.TryGet<MeditationDirectionDef>(meditation.DirectionId, out var direction))
            return RejectReasons.UnknownDirection;
        if (!ctx.Content.TryGet<PlaceDef>(meditation.PlaceId, out var place) || place.Zone != state.Player.ZoneId)
            return RejectReasons.PlaceNotHere;
        return ctx.Content.TryGet<CultivationVectorDef>(direction.Target, out _)
            ? CultivationSystem.CannotAssimilate(state.Player, ctx, direction.Target)
            : null;
    }

    public static void AdvanceWeek(GameState state, SimContext ctx, MeditateActivity meditation, List<SimEvent> events)
    {
        var direction = ctx.Content.Get<MeditationDirectionDef>(meditation.DirectionId);
        var placeDef = ctx.Content.Get<PlaceDef>(meditation.PlaceId);
        var place = state.World.Places.First(p => p.PlaceId == meditation.PlaceId);

        double draw = placeDef.Density;
        bool covered = place.Reserve >= draw;
        double efficiency = covered ? 1 : Math.Max(ctx.Config.MeditationDepletedFloor, place.Reserve / draw);
        place.Reserve = StateNumbers.Round(Math.Max(0, place.Reserve - draw));

        Apply(state.Player, ctx, direction.Target, direction.Rate * placeDef.Density * efficiency, events);

        if (covered && place.Reserve < draw)
            events.Add(new PlaceQiLow(meditation.PlaceId));
    }

    private static void Apply(PlayerState player, SimContext ctx, string target, double gain, List<SimEvent> events)
    {
        if (ctx.Content.TryGet<CultivationVectorDef>(target, out _))
        {
            CultivationSystem.Assimilate(player, ctx, target, gain, events);
            return;
        }
        if (ctx.Content.TryGet<ResourceDef>(target, out _))
        {
            ResourceSystem.Restore(player, ctx, target, gain);
            return;
        }

        var stat = player.Stats.First(s => s.StatId == target);
        stat.Base = StateNumbers.Round(stat.Base + gain);
    }
}
