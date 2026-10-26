using Core.Content;
using Core.State;

namespace Core.Sim;

/// <summary>
/// One week of meditation (§4.2, D30): draw the place's density from its reserve, then add
/// rate × density × efficiency to the direction's target. Efficiency is full while the reserve
/// covers the draw and falls to reserve / draw, but never below the configured floor.
/// </summary>
public static class MeditationSystem
{
    public static void AdvanceWeek(GameState state, SimContext ctx, MeditateActivity meditation, List<SimEvent> events)
    {
        var direction = ctx.Content.Get<MeditationDirectionDef>(meditation.DirectionId);
        var placeDef = ctx.Content.Get<PlaceDef>(meditation.PlaceId);
        var place = state.World.Places.First(p => p.PlaceId == meditation.PlaceId);

        double draw = placeDef.Density;
        bool covered = place.Reserve >= draw;
        double efficiency = covered ? 1 : Math.Max(ctx.Config.MeditationDepletedFloor, place.Reserve / draw);
        place.Reserve = StateNumbers.Round(Math.Max(0, place.Reserve - draw));

        Apply(state.Player, ctx, direction.Target, direction.Rate * placeDef.Density * efficiency);

        if (covered && place.Reserve < draw)
            events.Add(new PlaceQiLow(meditation.PlaceId));
    }

    private static void Apply(PlayerState player, SimContext ctx, string target, double gain)
    {
        if (ctx.Content.TryGet<ResourceDef>(target, out _))
        {
            ResourceSystem.Restore(player, ctx, target, gain);
            return;
        }

        var stat = player.Stats.First(s => s.StatId == target);
        stat.Base = StateNumbers.Round(stat.Base + gain);
    }
}
