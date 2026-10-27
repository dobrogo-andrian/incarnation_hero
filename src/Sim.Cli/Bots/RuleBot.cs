using Core.Content;
using Core.Rng;
using Core.Sim;
using Core.State;

namespace Sim.Cli.Bots;

/// <summary>
/// The knobs that tell rule bots apart (D43). Numbers are a draft, tuned by the reports.
/// </summary>
public sealed record BotProfile(
    string Name,
    int BaseDanger,
    int DangerPerStage,
    bool Roams,
    bool SeeksCombat,
    double MinHealthToFight,
    double HealAt,
    double MaxToxins,
    IReadOnlyList<string> AcceptedFailureZones,
    double DesperateYearsLeft,
    IReadOnlyList<string> Shopping)
{
    /// <summary>Meditates at safe places, avoids fights and waits out any breakthrough deficit.</summary>
    public static readonly BotProfile Cautious = new(
        "cautious", BaseDanger: 1, DangerPerStage: 0, Roams: false, SeeksCombat: false, MinHealthToFight: 1,
        HealAt: 0.15, MaxToxins: 0.5, AcceptedFailureZones: [], DesperateYearsLeft: 5,
        Shopping: ["item.folio.azure_breath", "item.pill.qi_gathering"]);

    /// <summary>Roams dangerous zones for fights and their insight; breaks through with a light deficit.</summary>
    public static readonly BotProfile Fighter = new(
        "fighter", BaseDanger: 1, DangerPerStage: 1, Roams: true, SeeksCombat: true, MinHealthToFight: 0.6,
        HealAt: 0.3, MaxToxins: 0.8, AcceptedFailureZones: ["failure_zone.light"], DesperateYearsLeft: 5,
        Shopping: ["item.folio.piercing_dragon", "item.pill.qi_gathering"]);

    public int MaxDanger(int stageIndex) => BaseDanger + DangerPerStage * stageIndex;
}

/// <summary>
/// A priority-rule player (D43): continue a commitment, break through when the forecast suits the
/// profile, heal, set a studied method, shop, assimilate buffs, take a pill, then fill the time
/// with foundation work or, for a roamer, with travel. Utility scoring comes when actions multiply.
/// </summary>
public sealed class RuleBot : IBot
{
    /// <summary>Weeks of an open-ended activity before the bot looks around again.</summary>
    private const int ChunkWeeks = 8;
    private const int HealWeeks = 4;

    /// <summary>A place whose reserve covers fewer weeks than this is scored as depleted.</summary>
    private const int MinReserveWeeks = 4;

    /// <summary>The best place here is good enough when it scores at least this share of the best known.</summary>
    private const double StayShare = 0.75;

    private readonly BotProfile _profile;
    private readonly Xoshiro256StarStar _rng;
    private readonly WorldMemory _memory;
    private readonly ContentDb _content;
    private readonly RunConfigDef _run;
    private readonly string _healthResource;
    private readonly MeditationDirectionDef _assimilation;
    private readonly MeditationDirectionDef _purification;
    private readonly MeditationDirectionDef _comprehension;

    public RuleBot(BotProfile profile, ContentDb content, Xoshiro256StarStar rng)
    {
        _profile = profile;
        _rng = rng;
        _content = content;
        _memory = new WorldMemory(content);
        _run = content.Get<RunConfigDef>(RunConfigDef.RunId);
        _healthResource = content.Get<CombatConfigDef>(CombatConfigDef.CombatId).HealthResource;
        var directions = content.All<MeditationDirectionDef>().ToList();
        _assimilation = directions.First(d => d.Target == "vector." + Simulation.QiVectorId);
        _purification = directions.First(d => d.Target == _run.PurityStat);
        _comprehension = directions.First(d => d.Studies);
    }

    public string Name => _profile.Name;

    public void Observe(IReadOnlyList<SimEvent> events) => _memory.Observe(events);

    public IEnumerable<Command> Propose(PlayerView view)
    {
        _memory.See(view);
        return view.Mode == GameMode.EventDialog ? ChooseInEvent(view) : Overworld(view);
    }

    private IEnumerable<Command> ChooseInEvent(PlayerView view)
    {
        var options = view.Event!.Options;
        if (options.Count == 0)
        {
            yield return new ChooseEventOption(0);
            yield break;
        }

        var available = options.Where(o => o.Available).ToList();
        if (_profile.SeeksCombat && Share(Resource(view, _healthResource)) >= _profile.MinHealthToFight)
        {
            foreach (var o in available.Where(o => o.Combat is { Stage: not StageRelation.Higher }))
                yield return new ChooseEventOption(o.Index);
        }
        var peaceful = available.Where(o => o.Combat is null)
            .OrderBy(o => o.Success is null ? 0 : 1)
            .ThenByDescending(o => o.Success?.Low ?? 1);
        foreach (var o in peaceful)
            yield return new ChooseEventOption(o.Index);
        foreach (var o in available.Where(o => o.Combat is not null))
            yield return new ChooseEventOption(o.Index);
    }

    private IEnumerable<Command> Overworld(PlayerView view)
    {
        if (view.Activity?.Activity is BreakthroughActivity or TravelActivity)
            yield return new ContinueActivity();

        var track = view.Tracks.First(t => t.VectorId == Simulation.QiVectorId);
        var placeHere = BestPlaceHere(view);

        if (track.Forecast is { } forecast && Accepts(forecast, view))
        {
            if (placeHere is not null)
                yield return new AttemptBreakthrough(track.VectorId, placeHere.Id);
            else if (TargetPlace(view, track) is { } target)
                yield return new Travel(target.Zone);
        }

        foreach (var command in Heal(view, placeHere))
            yield return command;

        if (view.ActiveMethodId is null)
        {
            foreach (var method in view.Knowledge.Where(k => k.Level > 0 && _content.Get<KnowledgeDef>(k.KnowledgeId).IsMethod))
                yield return new SetActiveMethod(method.KnowledgeId);
        }

        foreach (var command in Shop(view))
            yield return command;

        bool scaleFull = track.StageProgress >= track.ProgressRequired;
        if (!scaleFull && view.Buffs.Count > 0 && placeHere is not null)
            yield return new StartActivity(new MeditateActivity(_assimilation.Id, placeHere.Id), null);

        if (!scaleFull && view.Buffs.All(b => b.Kind != BuffKind.PillEnergy) && EnergyPill(view) is { } pill)
            yield return new UsePill(pill);

        if (_profile.Roams && !scaleFull)
        {
            foreach (var command in Roam(view, track))
                yield return command;
        }

        foreach (var command in FoundationWork(view, track))
            yield return command;

        yield return new StartActivity(new WaitActivity(), ChunkWeeks);
    }

    private bool Accepts(BreakthroughEstimate forecast, PlayerView view)
    {
        if (forecast.Fatal)
            return false;
        if (forecast.FailureZoneId is null || _profile.AcceptedFailureZones.Contains(forecast.FailureZoneId))
            return true;
        double yearsLeft = (view.LifespanWeeks - view.AgeWeeks) / (double)_run.WeeksPerYear;
        return yearsLeft <= _profile.DesperateYearsLeft;
    }

    private IEnumerable<Command> Heal(PlayerView view, PlaceDef? placeHere)
    {
        foreach (var injury in view.Injuries.Where(i => i.Value >= _profile.HealAt * i.Max))
        {
            foreach (var item in view.Items.Where(i => Pill(i.ItemId)?.Heals.ContainsKey(injury.InjuryId) == true))
                yield return new UsePill(item.ItemId);
            var direction = _content.All<MeditationDirectionDef>().FirstOrDefault(d => d.Target == injury.InjuryId);
            if (direction is not null && placeHere is not null)
                yield return new StartActivity(new MeditateActivity(direction.Id, placeHere.Id), HealWeeks);
            else
                yield return new StartActivity(new WaitActivity(), HealWeeks);
        }
    }

    private IEnumerable<Command> Shop(PlayerView view)
    {
        foreach (string itemId in _profile.Shopping)
        {
            if (view.ShopOffers.FirstOrDefault(o => o.ItemId == itemId) is not { } offer || offer.Price > view.Stones)
                continue;
            int carried = view.Items.FirstOrDefault(i => i.ItemId == itemId)?.Count ?? 0;
            var item = _content.Get<ItemDef>(itemId);
            bool wanted = item.Folio is { } folio
                ? carried == 0 && KnowledgeLevel(view, folio.Subject) < folio.MaxLevel
                : carried < 2;
            if (wanted)
                yield return new Buy(itemId);
        }
    }

    /// <summary>The carried pill giving the most energy that keeps toxins within the profile's tolerance.</summary>
    private string? EnergyPill(PlayerView view)
    {
        var toxins = Resource(view, _run.ToxinsResource);
        return view.Items
            .Where(i => i.NextPillEnergy > 0 && toxins.Current + Pill(i.ItemId)!.Toxicity <= _profile.MaxToxins * toxins.Max.Value)
            .OrderByDescending(i => i.NextPillEnergy)
            .Select(i => i.ItemId)
            .FirstOrDefault();
    }

    /// <summary>Travels to the known zones within the profile's danger in random order, never staying put.</summary>
    private IEnumerable<Command> Roam(PlayerView view, TrackView track)
    {
        var zones = _memory.KnownZones()
            .Where(z => z.Danger >= 1 && z.Danger <= _profile.MaxDanger(track.StageIndex) && z.Id != view.Zone.ZoneId)
            .ToList();
        while (zones.Count > 0)
        {
            int i = _rng.NextInt(0, zones.Count);
            yield return new Travel(zones[i].Id);
            zones.RemoveAt(i);
        }
    }

    /// <summary>
    /// Comprehension of carried folios, then of elements sensed without one, then purification —
    /// at the best place within reach, travelling there first. Capped subjects are rejected by Core.
    /// </summary>
    private IEnumerable<Command> FoundationWork(PlayerView view, TrackView track)
    {
        if (TargetPlace(view, track) is not { } place)
            yield break;
        if (place.Zone != view.Zone.ZoneId)
        {
            yield return new Travel(place.Zone);
            yield break;
        }

        var subjects = view.Items.Where(i => _content.Get<ItemDef>(i.ItemId).Folio is not null).Select(i => i.ItemId)
            .Concat(_content.All<KnowledgeDef>().Where(k => k.IsElement).Select(k => k.Id));
        foreach (string subject in subjects)
            yield return new StartActivity(new MeditateActivity(_comprehension.Id, place.Id, subject), ChunkWeeks);
        yield return new StartActivity(new MeditateActivity(_purification.Id, place.Id), ChunkWeeks);
    }

    /// <summary>The best known place within the profile's danger, preferring one here if it is good enough.</summary>
    private PlaceDef? TargetPlace(PlayerView view, TrackView track)
    {
        int maxDanger = _profile.MaxDanger(track.StageIndex);
        var candidates = _content.All<PlaceDef>()
            .Where(p => _memory.IsKnown(p) && _content.Get<ZoneDef>(p.Zone) is var zone && _memory.IsKnown(zone) && zone.Danger <= maxDanger)
            .ToList();
        if (candidates.Count == 0)
            return null;
        var best = candidates.MaxBy(p => Score(p, view.Week))!;
        var here = candidates.Where(p => p.Zone == view.Zone.ZoneId).MaxBy(p => Score(p, view.Week));
        return here is not null && Score(here, view.Week) >= StayShare * Score(best, view.Week) ? here : best;
    }

    private double Score(PlaceDef place, int week) =>
        _memory.ExpectedReserve(place, week) >= place.Density * MinReserveWeeks ? place.Density : place.Density * _run.MeditationDepletedFloor;

    private PlaceDef? BestPlaceHere(PlayerView view) =>
        view.Places.Select(p => _content.Get<PlaceDef>(p.PlaceId)).MaxBy(p => Score(p, view.Week));

    private PillDef? Pill(string itemId) => _content.Get<ItemDef>(itemId).Pill;

    private static int KnowledgeLevel(PlayerView view, string knowledgeId) =>
        view.Knowledge.FirstOrDefault(k => k.KnowledgeId == knowledgeId)?.Level ?? 0;

    internal static ResourceView Resource(PlayerView view, string resourceId) =>
        view.Resources.First(r => r.ResourceId == resourceId);

    private static double Share(ResourceView resource) =>
        resource.Max.Value > 0 ? resource.Current / resource.Max.Value : 0;
}
