using Core.Content;
using Core.Loc;
using Core.Rng;
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
        if (!Allowed(direction, ctx.Content.Get<PlaceDef>(meditation.PlaceId), ctx.Content))
            return RejectReasons.TooExposed;
        if (direction.Rests && !NeedsRest(
                state.Player.Resources.Select(r => (r.ResourceId, r.Current, ctx.Rules.Value(state.Player, r.ResourceId))),
                state.Player.Injuries.Count > 0, direction.Target, ctx.Content))
            return RejectReasons.NothingToRecover;
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
        double efficiency = Efficiency(draw, place.Reserve, ctx.Config);
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
        if (direction.Rests)
            InjurySystem.Rest(state.Player, ctx, events);
        if (Exposed(placeDef, ctx.Content))
            RollAmbush(state, ctx, events);

        if (covered && place.Reserve < draw)
            events.Add(new PlaceQiLow(meditation.PlaceId));
    }

    /// <summary>
    /// The known place of the zone where <paramref name="directionId"/> gives the most this week, its
    /// density times efficiency, among those it is allowed at (D72, D73); null if there is none.
    /// </summary>
    public static string? BestPlace(PlayerView view, ContentDb db, string directionId)
    {
        var config = db.Get<RunConfigDef>(RunConfigDef.RunId);
        var direction = db.Get<MeditationDirectionDef>(directionId);
        return view.Places
            .Where(p => Allowed(direction, db.Get<PlaceDef>(p.PlaceId), db))
            .OrderByDescending(p => p.Density * Efficiency(p.Density, p.Reserve, config))
            .ThenBy(p => p.PlaceId, StringComparer.Ordinal)
            .FirstOrDefault()?.PlaceId;
    }

    /// <summary>
    /// The recovery meditation at the zone's best place while the body needs it (D72, D73); null once
    /// health, qi and the rest are full and nothing is injured, when "meditate" offers a direction instead.
    /// </summary>
    public static MeditateActivity? Recovery(PlayerView view, ContentDb db)
    {
        string recovery = db.Get<RunConfigDef>(RunConfigDef.RunId).RecoveryMeditation;
        if (!NeedsRest(view.Resources.Select(r => (r.ResourceId, r.Current, r.Max.Value)), view.Injuries.Count > 0,
                db.Get<MeditationDirectionDef>(recovery).Target, db))
            return null;
        return BestPlace(view, db, recovery) is { } place ? new MeditateActivity(recovery, place) : null;
    }

    /// <summary>
    /// Whether a resting meditation has work left: an injury, or <paramref name="target"/> or any resource
    /// that returns on its own (health, body energy…) below its maximum.
    /// </summary>
    private static bool NeedsRest(IEnumerable<(string Id, double Current, double Max)> resources, bool injured, string? target, ContentDb db) =>
        injured || resources.Any(r => (r.Id == target || db.Get<ResourceDef>(r.Id).RegenPerWeek > 0) && r.Current < r.Max);

    /// <summary>Open ground in a dangerous zone (D73): ambushes, and only directions meant for the open.</summary>
    public static bool Exposed(PlaceDef place, ContentDb db) => !place.Shelter && db.Get<ZoneDef>(place.Zone).Danger > 0;

    private static bool Allowed(MeditationDirectionDef direction, PlaceDef place, ContentDb db) => direction.InTheOpen || !Exposed(place, db);

    /// <summary>Maybe one of the zone's ambushers falls on the meditating player, more likely the more dangerous the zone (D73).</summary>
    private static void RollAmbush(GameState state, SimContext ctx, List<SimEvent> events)
    {
        var zone = ctx.Content.Get<ZoneDef>(state.Player.ZoneId);
        var rng = ctx.Rng[RngStream.Events];
        if (!rng.Chance(zone.Danger * ctx.Config.AmbushChancePerDanger))
            return;
        var group = rng.PickWeighted(zone.Ambushers.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => (a.Key, a.Value)).ToList());
        CombatSystem.Offer(state, ctx, CombatSystem.NewFight(ctx, group, FightKind.Hunt), events);
    }

    /// <summary>Full while the reserve covers the week's draw, else reserve / draw, but never below the floor (D30).</summary>
    private static double Efficiency(double draw, double reserve, RunConfigDef config) =>
        reserve >= draw ? 1 : Math.Max(config.MeditationDepletedFloor, reserve / draw);

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
