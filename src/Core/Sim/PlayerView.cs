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
    IReadOnlyList<StatView> Stats,
    IReadOnlyList<ResourceView> Resources,
    IReadOnlyList<TrackView> Tracks,
    IReadOnlyList<BuffView> Buffs,
    int Stones,
    IReadOnlyList<ItemView> Items,
    IReadOnlyList<ShopOfferView> ShopOffers,
    Foundation Foundation,
    IReadOnlyList<KnowledgeView> Knowledge)
{
    public static PlayerView From(GameState state, CharacterRules rules, ContentDb content)
    {
        var player = state.Player;
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
                .Where(p => p.Def.Zone == player.ZoneId)
                .Select(p => new PlaceView(p.State.PlaceId, p.Def.Density, p.State.Reserve, p.Def.Capacity))
                .ToList(),
            rules.Stats
                .Select(s => new StatView(rules.Explain(player, s.Id), s.Aspects.Select(a => rules.Explain(player, a)).ToList()))
                .ToList(),
            player.Resources
                .Select(r => new ResourceView(r.ResourceId, r.Current, rules.Explain(player, r.ResourceId)))
                .ToList(),
            player.Tracks
                .Select(t => new TrackView(t.VectorId, t.StageIndex, t.StageProgress, CultivationSystem.Stage(t, content).ProgressRequired))
                .ToList(),
            player.Buffs.Select(b => new BuffView(b.Kind, b.Points)).ToList(),
            player.Stones,
            player.Items
                .Select(i => new ItemView(i.ItemId, i.Count, content.Get<ItemDef>(i.ItemId).Pill is { } pill ? ItemSystem.NextPillEnergy(player, i.ItemId, pill) : null))
                .ToList(),
            EconomySystem.ShopsHere(player, content)
                .SelectMany(s => s.Prices.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new ShopOfferView(s.Id, p.Key, p.Value)))
                .ToList(),
            CultivationSystem.FoundationOf(player, rules, content.Get<RunConfigDef>(RunConfigDef.RunId)),
            player.Knowledge.Select(k => new KnowledgeView(k.KnowledgeId, k.Level)).ToList());
    }
}

public sealed record ActivityView(Activity Activity, int? WeeksLeft);

public sealed record ZoneView(string ZoneId, int Danger);

/// <summary>A meditation place in the player's zone; being there, the player sees its reserve.</summary>
public sealed record PlaceView(string PlaceId, double Density, double Reserve, double Capacity);

/// <summary>A stat and, for a body stat, its aspects; each with its breakdown for tooltips.</summary>
public sealed record StatView(StatBreakdown Stat, IReadOnlyList<StatBreakdown> Aspects);

/// <summary>A resource's current amount and its computed maximum.</summary>
public sealed record ResourceView(string ResourceId, double Current, StatBreakdown Max);

/// <summary>A cultivation vector's stage and how full its breakthrough scale is.</summary>
public sealed record TrackView(string VectorId, int StageIndex, double StageProgress, double ProgressRequired);

public sealed record BuffView(BuffKind Kind, double Points);

/// <summary>A carried item; for a pill, the energy the next one of its type would give.</summary>
public sealed record ItemView(string ItemId, int Count, double? NextPillEnergy);

public sealed record KnowledgeView(string KnowledgeId, int Level);

/// <summary>An item for sale in a shop of the player's zone.</summary>
public sealed record ShopOfferView(string ShopId, string ItemId, int Price);
