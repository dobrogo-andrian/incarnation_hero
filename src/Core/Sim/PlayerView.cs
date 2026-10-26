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
    IReadOnlyList<TrackView> Tracks)
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
            player.Tracks.Select(t => new TrackView(t.VectorId, t.StageIndex, t.StageProgress)).ToList());
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

public sealed record TrackView(string VectorId, int StageIndex, int StageProgress);
