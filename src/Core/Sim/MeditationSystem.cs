using Core.Content;
using Core.Loc;
using Core.State;

namespace Core.Sim;

/// <summary>
/// One week of meditation (§4.2, D30): draw the place's density from its reserve, then add
/// rate × density × efficiency to the direction's target, or to the studied knowledge. Efficiency
/// is full while the reserve covers the draw and falls to reserve / draw, but never below the
/// configured floor. Side gains (Perception) use the same density × efficiency.
/// </summary>
public static class MeditationSystem
{
    /// <summary>Why <paramref name="meditation"/> cannot start now, or null if it can.</summary>
    public static LocKey? Validate(GameState state, SimContext ctx, MeditateActivity meditation)
    {
        if (!ctx.Content.TryGet<MeditationDirectionDef>(meditation.DirectionId, out var direction))
            return RejectReasons.UnknownDirection;
        if (!WorldSystem.IsPlaceHere(state, ctx.Content, meditation.PlaceId))
            return RejectReasons.PlaceNotHere;
        if (direction.Studies)
            return KnowledgeSystem.CannotStudy(state.Player, ctx, meditation.SubjectId);
        if (meditation.SubjectId is not null)
            return RejectReasons.InvalidActivity;
        if (ctx.Content.TryGet<InjuryDef>(direction.Target!, out _))
            return InjurySystem.Value(state.Player, direction.Target!) > 0 ? null : RejectReasons.NothingToHeal;
        return ctx.Content.TryGet<CultivationVectorDef>(direction.Target!, out _)
            ? CultivationSystem.CannotAssimilate(state.Player, ctx, direction.Target!)
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

        double strength = placeDef.Density * efficiency;
        double gain = direction.Rate * strength;
        if (direction.Purges is { } purged)
            gain -= ResourceSystem.Drain(state.Player, purged, gain * direction.PurgeRatio) / direction.PurgeRatio;
        if (gain > 0 && direction.Studies)
            KnowledgeSystem.Study(state.Player, ctx, meditation.SubjectId!, gain, events);
        else if (gain > 0)
            Apply(state.Player, ctx, direction.Target!, gain, events);
        foreach (var (statId, rate) in direction.SideGains.OrderBy(g => g.Key, StringComparer.Ordinal))
            StatSystem.Gain(state.Player, ctx, statId, rate * strength, events);

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
        if (ctx.Content.TryGet<InjuryDef>(target, out _))
        {
            InjurySystem.Heal(player, ctx, target, gain, events);
            return;
        }

        StatSystem.Gain(player, ctx, target, gain, events);
    }
}
