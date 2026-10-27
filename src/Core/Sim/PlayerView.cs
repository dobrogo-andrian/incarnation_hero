using Core.Character;
using Core.Content;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Everything the UI may read. Built from the true state, but only exposes what the player knows.
/// </summary>
public sealed record PlayerView(
    int Week,
    int AgeWeeks,
    int LifespanWeeks,
    GameMode Mode,
    ActivityView? Activity,
    RunEndCause? EndCause,
    ZoneView Zone,
    IReadOnlyList<PlaceView> Places,
    IReadOnlyList<RegionZoneView> Region,
    IReadOnlyList<StatView> Stats,
    IReadOnlyList<ResourceView> Resources,
    IReadOnlyList<InjuryView> Injuries,
    IReadOnlyList<TrackView> Tracks,
    IReadOnlyList<BuffView> Buffs,
    int Stones,
    IReadOnlyList<ItemView> Items,
    IReadOnlyList<ShopOfferView> ShopOffers,
    Foundation Foundation,
    IReadOnlyList<KnowledgeView> Knowledge,
    string? ActiveMethodId,
    IReadOnlyList<ReputationView> Reputation,
    IReadOnlyList<RumorView> Rumors,
    EventView? Event)
{
    public static PlayerView From(GameState state, SimContext ctx)
    {
        var player = state.Player;
        var rules = ctx.Rules;
        var content = ctx.Content;
        return new(
            state.Week,
            player.AgeWeeks,
            player.LifespanWeeks,
            state.Mode,
            state.Activity is { } active ? new ActivityView(active.Activity, active.WeeksLeft) : null,
            state.EndCause,
            new ZoneView(player.ZoneId, content.Get<ZoneDef>(player.ZoneId).Danger),
            state.World.Places
                .Select(p => (State: p, Def: content.Get<PlaceDef>(p.PlaceId)))
                .Where(p => WorldSystem.IsPlaceHere(state, content, p.Def.Id))
                .Select(p => new PlaceView(p.State.PlaceId, p.Def.Density, p.State.Reserve, p.Def.Capacity))
                .ToList(),
            content.All<ZoneDef>()
                .Where(z => WorldSystem.IsKnown(state.World, content, z.Id))
                .Select(z => new RegionZoneView(z.Id, z.Danger, z.Id == player.ZoneId ? null : WorldSystem.RouteTo(state, ctx, z.Id)?.Sum(l => l.Weeks)))
                .ToList(),
            rules.Stats
                .Select(s => new StatView(rules.Explain(player, s.Id), s.Aspects.Select(a => rules.Explain(player, a)).ToList()))
                .ToList(),
            player.Resources
                .Select(r => new ResourceView(r.ResourceId, r.Current, rules.Explain(player, r.ResourceId)))
                .ToList(),
            player.Injuries.Select(i => new InjuryView(i.InjuryId, i.Value, content.Get<InjuryDef>(i.InjuryId).Max)).ToList(),
            player.Tracks.Select(t => TrackView.From(state, t, rules, content)).ToList(),
            player.Buffs.Select(b => new BuffView(b.Kind, b.Points)).ToList(),
            player.Stones,
            player.Items
                .Select(i => new ItemView(i.ItemId, i.Count, content.Get<ItemDef>(i.ItemId).Pill is { } pill ? ItemSystem.NextPillEnergy(player, i.ItemId, pill) : null))
                .ToList(),
            EconomySystem.ShopsHere(player, content)
                .SelectMany(s => s.Prices.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new ShopOfferView(s.Id, p.Key, p.Value)))
                .ToList(),
            CultivationSystem.FoundationOf(player, rules, content),
            player.Knowledge.Select(k => new KnowledgeView(k.KnowledgeId, k.Level)).ToList(),
            player.ActiveMethodId,
            content.All<SectDef>().Select(s => new ReputationView(s.Id, EventSystem.ReputationOf(player, s.Id))).ToList(),
            RumorSystem.View(state, ctx),
            EventSystem.View(state, ctx));
    }
}

public sealed record ActivityView(Activity Activity, int? WeeksLeft);

public sealed record ZoneView(string ZoneId, int Danger);

/// <summary>A zone the player knows, for the region map; <see cref="TravelWeeks"/> is null for the zone the player is in or one with no known route.</summary>
public sealed record RegionZoneView(string ZoneId, int Danger, int? TravelWeeks);

/// <summary>A meditation place in the player's zone; being there, the player sees its reserve.</summary>
public sealed record PlaceView(string PlaceId, double Density, double Reserve, double Capacity);

/// <summary>A stat and, for a body stat, its aspects; each with its breakdown for tooltips.</summary>
public sealed record StatView(StatBreakdown Stat, IReadOnlyList<StatBreakdown> Aspects);

/// <summary>A resource's current amount and its computed maximum.</summary>
public sealed record ResourceView(string ResourceId, double Current, StatBreakdown Max);

/// <summary>An injury scale above zero; what it weakens shows up as its modifiers in the breakdowns.</summary>
public sealed record InjuryView(string InjuryId, double Value, double Max);

/// <summary>
/// A cultivation vector's stage, how full its breakthrough scale is and the rank of the last
/// breakthrough. With a full scale and a next stage, <see cref="Forecast"/> is the breakthrough as
/// it will resolve at the end of the seclusion (the one in progress, or one started now).
/// </summary>
public sealed record TrackView(
    string VectorId,
    int StageIndex,
    double StageProgress,
    double ProgressRequired,
    int? LastBreakthroughQuality,
    BreakthroughEstimate? Forecast)
{
    public static TrackView From(GameState state, CultivationTrack track, CharacterRules rules, ContentDb content)
    {
        var stage = CultivationSystem.Stage(track, content);
        BreakthroughEstimate? forecast = null;
        if (stage.Breakthrough is { } breakthrough && track.StageProgress >= stage.ProgressRequired)
        {
            int weeksAhead = state.Activity is { Activity: BreakthroughActivity attempt, WeeksLeft: int left } && attempt.VectorId == track.VectorId
                ? left
                : breakthrough.Weeks;
            forecast = BreakthroughSystem.Estimate(state.Player, track, content, rules, weeksAhead);
        }
        return new(track.VectorId, track.StageIndex, track.StageProgress, stage.ProgressRequired, track.LastBreakthroughQuality, forecast);
    }
}

public sealed record BuffView(BuffKind Kind, double Points);

/// <summary>A carried item; for a pill, the energy the next one of its type would give.</summary>
public sealed record ItemView(string ItemId, int Count, double? NextPillEnergy);

public sealed record KnowledgeView(string KnowledgeId, int Level);

public sealed record ReputationView(string SectId, int Value);

/// <summary>An item for sale in a shop of the player's zone.</summary>
public sealed record ShopOfferView(string ShopId, string ItemId, int Price);
