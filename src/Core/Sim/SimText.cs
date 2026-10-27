using Core.Combat;
using Core.Content;
using Core.Loc;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Turns the PlayerView and SimEvents into localization keys. Shared by Sim.Cli and the Godot
/// shell so both say the same thing; each resolves the keys with its Localizer.
/// </summary>
public static class SimText
{
    private static readonly LocKey Week = new("str.view.week");
    private static readonly LocKey Age = new("str.view.age");
    private static readonly LocKey Lifespan = new("str.view.lifespan");
    private static readonly LocKey Duration = new("str.view.duration");
    private static readonly LocKey ModeLine = new("str.view.mode");
    private static readonly LocKey RunEndLine = new("str.view.run_end");
    private static readonly LocKey Track = new("str.view.track");
    private static readonly LocKey StatLine = new("str.view.stat");
    private static readonly LocKey BodyStatLine = new("str.view.body_stat");
    private static readonly LocKey AspectPart = new("str.view.aspect");
    private static readonly LocKey ResourceLine = new("str.view.resource");
    private static readonly LocKey InjuryLine = new("str.view.injury");
    private static readonly LocKey ZoneLine = new("str.view.zone");
    private static readonly LocKey ZoneHere = new("str.view.zone_here");
    private static readonly LocKey ZoneRoute = new("str.view.zone_route");
    private static readonly LocKey ZoneNoRoute = new("str.view.zone_no_route");
    private static readonly LocKey PlaceLine = new("str.view.place");
    private static readonly LocKey BuffLine = new("str.view.buff");
    private static readonly LocKey StonesLine = new("str.view.stones");
    private static readonly LocKey ItemLine = new("str.view.item");
    private static readonly LocKey PillLine = new("str.view.pill");
    private static readonly LocKey OfferLine = new("str.view.offer");
    private static readonly LocKey FoundationLine = new("str.view.foundation");
    private static readonly LocKey InjuredFoundationLine = new("str.view.foundation_injured");
    private static readonly LocKey KnowledgeLine = new("str.view.knowledge");
    private static readonly LocKey MeditateStudy = new("str.activity.meditate_study");
    private static readonly LocKey LastQualityLine = new("str.view.last_breakthrough");
    private static readonly LocKey ForecastLine = new("str.view.breakthrough_forecast");
    private static readonly LocKey NoDeficit = new("str.view.no_deficit");
    private static readonly LocKey MethodLine = new("str.view.method");
    private static readonly LocKey NoMethodLine = new("str.view.no_method");
    private static readonly LocKey Seclusion = new("str.activity.breakthrough");
    private static readonly LocKey BreakthroughDeath = new("str.end.breakthrough_death");
    private static readonly LocKey Succeeded = new("str.event.breakthrough_succeeded");
    private static readonly LocKey Failed = new("str.event.breakthrough_failed");

    private static readonly LocKey CombatInsight = new("str.buff.combat_insight");
    private static readonly LocKey PillEnergy = new("str.buff.pill_energy");

    private static readonly LocKey Overworld = new("str.mode.overworld");
    private static readonly LocKey EventDialog = new("str.mode.event_dialog");
    private static readonly LocKey Combat = new("str.mode.combat");
    private static readonly LocKey RunEnd = new("str.mode.run_end");

    private static readonly LocKey Wait = new("str.activity.wait");
    private static readonly LocKey TravelTo = new("str.activity.travel");
    private static readonly LocKey Meditate = new("str.activity.meditate");

    private static readonly LocKey OldAge = new("str.end.old_age");

    private static readonly LocKey Finished = new("str.event.activity_finished");
    private static readonly LocKey Interrupted = new("str.event.activity_interrupted");
    private static readonly LocKey LifeEnd = new("str.event.life_end_approaching");
    private static readonly LocKey Ended = new("str.event.run_ended");
    private static readonly LocKey Entered = new("str.event.zone_entered");
    private static readonly LocKey PlaceLow = new("str.event.place_qi_low");
    private static readonly LocKey BuffGone = new("str.event.buff_exhausted");
    private static readonly LocKey ScaleFull = new("str.event.stage_scale_full");
    private static readonly LocKey PillTakenLine = new("str.event.pill_taken");
    private static readonly LocKey LevelUp = new("str.event.knowledge_level_up");
    private static readonly LocKey CapReached = new("str.event.study_cap_reached");
    private static readonly LocKey Injured = new("str.event.injury_received");
    private static readonly LocKey Healed = new("str.event.injury_healed");
    private static readonly LocKey EventOpened = new("str.event.event_started");
    private static readonly LocKey StatChangedLine = new("str.event.stat_changed");
    private static readonly LocKey ResourceChangedLine = new("str.event.resource_changed");
    private static readonly LocKey ItemsChangedLine = new("str.event.items_changed");
    private static readonly LocKey StonesChangedLine = new("str.event.stones_changed");
    private static readonly LocKey BuffGainedLine = new("str.event.buff_gained");
    private static readonly LocKey ReputationChangedLine = new("str.event.reputation_changed");
    private static readonly LocKey RevealedLine = new("str.event.revealed");
    private static readonly LocKey CombatWon = new("str.event.combat_won");
    private static readonly LocKey CombatFled = new("str.event.combat_fled");
    private static readonly LocKey CombatDied = new("str.event.combat_died");
    private static readonly LocKey CombatDeath = new("str.end.combat_death");
    private static readonly LocKey OptionCombat = new("str.dialog.option_combat");
    private static readonly LocKey StageLower = new("str.combat.stage_lower");
    private static readonly LocKey StageEqual = new("str.combat.stage_equal");
    private static readonly LocKey StageHigher = new("str.combat.stage_higher");

    private static readonly LocKey ReputationLine = new("str.view.reputation");
    private static readonly LocKey RumorLine = new("str.view.rumor");
    private static readonly LocKey Inquire = new("str.activity.inquire");
    private static readonly LocKey Heard = new("str.event.rumor_heard");
    private static readonly LocKey Continue = new("str.dialog.continue");
    private static readonly LocKey OptionRequires = new("str.dialog.option_requires");
    private static readonly LocKey OptionChance = new("str.dialog.option_chance");
    private static readonly LocKey NeedMin = new("str.need.min");
    private static readonly LocKey NeedMax = new("str.need.max");
    private static readonly LocKey NeedRange = new("str.need.range");
    private static readonly LocKey NeedZone = new("str.need.zone");
    private static readonly LocKey StonesName = new("str.need.stones");
    private static readonly LocKey DangerName = new("str.need.zone_danger");
    private static readonly LocKey StageName = new("str.need.stage");
    private static readonly LocKey AgeName = new("str.need.age_years");

    /// <summary>The whole PlayerView as lines, section by section, as Sim.Cli prints it.</summary>
    public static IReadOnlyList<LocKey> ViewLines(PlayerView view, ContentDb db) =>
    [
        .. TimeLines(view, db),
        ModeLine with { Args = [new("mode", ModeName(view.Mode))] },
        .. EndLines(view),
        ZoneText(view),
        .. PlaceLines(view),
        .. CharacterLines(view),
        .. CultivationLines(view, db),
        .. ItemLines(view),
        .. view.ShopOffers.Select(OfferText),
        .. SocialLines(view),
    ];

    /// <summary>Week, age and lifespan.</summary>
    public static IReadOnlyList<LocKey> TimeLines(PlayerView view, ContentDb db)
    {
        int weeksPerYear = db.Get<RunConfigDef>(RunConfigDef.RunId).WeeksPerYear;
        return
        [
            Week with { Args = [new("week", view.Week)] },
            Age with { Args = [new("age", Years(view.AgeWeeks, weeksPerYear))] },
            Lifespan with { Args = [new("lifespan", Years(view.LifespanWeeks, weeksPerYear))] },
        ];
    }

    /// <summary>How the run ended, once it has; nothing while it goes on.</summary>
    public static IReadOnlyList<LocKey> EndLines(PlayerView view) =>
        view.EndCause is { } cause ? [RunEndLine with { Args = [new("cause", CauseName(cause))] }] : [];

    public static LocKey ZoneText(PlayerView view) =>
        ZoneLine with { Args = [new("zone", Name(view.Zone.ZoneId)), new("danger", view.Zone.Danger)] };

    /// <summary>The known meditation places of the current zone with their reserves.</summary>
    public static IReadOnlyList<LocKey> PlaceLines(PlayerView view) => view.Places.Select(PlaceText).ToList();

    public static LocKey PlaceText(PlaceView place) => PlaceLine with
    {
        Args =
        [
            new("place", Name(place.PlaceId)),
            new("density", Number(place.Density)),
            new("reserve", Number(place.Reserve)),
            new("capacity", Number(place.Capacity)),
        ],
    };

    /// <summary>A zone on the region map: its danger and the weeks to get there.</summary>
    public static LocKey RegionZoneText(RegionZoneView zone, PlayerView view)
    {
        var danger = new LocArg("danger", zone.Danger);
        if (zone.ZoneId == view.Zone.ZoneId)
            return ZoneHere with { Args = [danger] };
        return zone.TravelWeeks is int weeks
            ? ZoneRoute with { Args = [danger, new("weeks", weeks)] }
            : ZoneNoRoute with { Args = [danger] };
    }

    /// <summary>Stats with their aspects, resources and injuries.</summary>
    public static IReadOnlyList<LocKey> CharacterLines(PlayerView view)
    {
        var lines = new List<LocKey>();
        foreach (var stat in view.Stats)
        {
            var name = new LocArg("stat", Name(stat.Stat.Id));
            var value = new LocArg("value", Number(stat.Stat.Value));
            lines.Add(stat.Aspects.Count == 0
                ? StatLine with { Args = [name, value] }
                : BodyStatLine with
                {
                    Args =
                    [
                        name,
                        value,
                        new("aspects", stat.Aspects.Select(a => AspectPart with { Args = [new("aspect", Name(a.Id)), new("value", Number(a.Value))] }).ToList()),
                    ],
                });
        }

        foreach (var resource in view.Resources)
        {
            lines.Add(ResourceLine with
            {
                Args =
                [
                    new("resource", Name(resource.ResourceId)),
                    new("current", Number(resource.Current)),
                    new("max", Number(resource.Max.Value)),
                ],
            });
        }
        foreach (var injury in view.Injuries)
            lines.Add(InjuryLine with { Args = [new("injury", Name(injury.InjuryId)), new("value", Number(injury.Value)), new("max", Number(injury.Max))] });
        return lines;
    }

    /// <summary>Stage scales with the breakthrough forecast, method, foundation, knowledge and buffs.</summary>
    public static IReadOnlyList<LocKey> CultivationLines(PlayerView view, ContentDb db)
    {
        var lines = new List<LocKey>();
        foreach (var track in view.Tracks)
        {
            lines.Add(TrackText(track, db));
            if (track.LastBreakthroughQuality is int rank)
                lines.Add(LastQualityLine with { Args = [new("rank", Name(db.Get<BreakthroughConfigDef>(BreakthroughConfigDef.BreakthroughId).Ranks[rank]))] });
            if (track.Forecast is { } forecast)
                lines.Add(ForecastText(forecast));
        }
        lines.Add(view.ActiveMethodId is { } method ? MethodLine with { Args = [new("method", Name(method))] } : NoMethodLine);

        var foundation = view.Foundation;
        LocArg[] parts = [new("total", Number(foundation.Total)), new("knowledge", Number(foundation.Knowledge)), new("purity", Number(foundation.Purity))];
        lines.Add(foundation.Injuries > 0
            ? InjuredFoundationLine with { Args = [.. parts, new("injuries", Number(foundation.Injuries))] }
            : FoundationLine with { Args = parts });
        foreach (var knowledge in view.Knowledge)
            lines.Add(KnowledgeLine with { Args = [new("knowledge", Name(knowledge.KnowledgeId)), new("level", knowledge.Level)] });

        foreach (var buff in view.Buffs)
            lines.Add(BuffLine with { Args = [new("buff", BuffName(buff.Kind)), new("points", Number(buff.Points))] });
        return lines;
    }

    /// <summary>A vector's stage and how full its scale is.</summary>
    public static LocKey TrackText(TrackView track, ContentDb db)
    {
        string vectorId = CultivationSystem.VectorDefId(track.VectorId);
        string stageId = db.Get<CultivationVectorDef>(vectorId).Stages[track.StageIndex];
        return Track with
        {
            Args =
            [
                new("vector", Name(vectorId)),
                new("stage", Name(stageId)),
                new("progress", Number(track.StageProgress)),
                new("required", Number(track.ProgressRequired)),
            ],
        };
    }

    /// <summary>Stones and carried items.</summary>
    public static IReadOnlyList<LocKey> ItemLines(PlayerView view) => [StonesText(view), .. view.Items.Select(ItemText)];

    /// <summary>A carried item with its count; a pill also with the energy the next one would give.</summary>
    public static LocKey ItemText(ItemView item)
    {
        LocArg[] args = [new("item", Name(item.ItemId)), new("count", item.Count)];
        return item.NextPillEnergy is double energy
            ? PillLine with { Args = [.. args, new("energy", Number(energy))] }
            : ItemLine with { Args = args };
    }

    public static LocKey StonesText(PlayerView view) => StonesLine with { Args = [new("stones", view.Stones)] };

    public static LocKey OfferText(ShopOfferView offer) =>
        OfferLine with { Args = [new("shop", Name(offer.ShopId)), new("item", Name(offer.ItemId)), new("price", offer.Price)] };

    /// <summary>Sect reputation and the rumors the player has heard.</summary>
    public static IReadOnlyList<LocKey> SocialLines(PlayerView view)
    {
        var lines = new List<LocKey>();
        foreach (var reputation in view.Reputation)
            lines.Add(ReputationLine with { Args = [new("sect", Name(reputation.SectId)), new("value", reputation.Value)] });
        foreach (var rumor in view.Rumors)
        {
            lines.Add(RumorLine with
            {
                Args =
                [
                    new("rumor", Name(rumor.RumorId)),
                    new("zone", Name(rumor.ZoneId)),
                    new("weeks", rumor.WeeksLeft),
                    new("source", Name(rumor.SourceId)),
                    new("low", Number(rumor.Reliability.Low * 100)),
                    new("high", Number(rumor.Reliability.High * 100)),
                ],
            });
            lines.Add(new LocKey(LocTable.ContentKey(rumor.RumorId, "text")));
        }
        return lines;
    }

    /// <summary>The localized name of a content object, for UI labels.</summary>
    public static LocKey Name(string contentId) => new(LocTable.ContentKey(contentId, "name"));

    /// <summary>The localized description of a content object that has one (a place or a shop).</summary>
    public static LocKey Description(string contentId) => new(LocTable.ContentKey(contentId, "description"));

    /// <summary>The open event node's text; the current zone is available to it as <c>{zone}</c>.</summary>
    public static LocKey EventText(EventView view, PlayerView player) =>
        new($"{view.EventId}.nodes.{view.NodeId}.text", [new("zone", Name(player.Zone.ZoneId))]);

    /// <summary>
    /// The choices to offer, each with the index to send in <c>ChooseEventOption</c>: an option's
    /// text with its unmet requirements or its observed chance of success, or "continue" at a node
    /// without options.
    /// </summary>
    public static IReadOnlyList<(int Index, LocKey Text, bool Available)> EventChoices(EventView view)
    {
        if (view.Options.Count == 0)
            return [(0, Continue, true)];
        return view.Options.Select(o =>
        {
            var text = new LocKey($"{view.EventId}.nodes.{view.NodeId}.options.{o.Index}.text");
            if (!o.Available)
                text = OptionRequires with { Args = [new("option", text), new("requires", o.Unmet.Select(Requirement).ToList())] };
            else if (o.Success is { } chance)
                text = OptionChance with { Args = [new("option", text), new("low", Number(chance.Low * 100)), new("high", Number(chance.High * 100))] };
            else if (o.Combat is { } combat)
                text = OptionCombat with { Args = [new("option", text), new("foes", combat.Foes), new("stage", StageRelationName(combat.Stage))] };
            return (o.Index, text, o.Available);
        }).ToList();
    }

    private static LocKey Requirement(EventConditionDef c) => c switch
    {
        { Stat: { } r } => Need(Name(r.Id), r),
        { Resource: { } r } => Need(Name(r.Id), r),
        { Injury: { } r } => Need(Name(r.Id), r),
        { Knowledge: { } r } => Need(Name(r.Id), r),
        { Item: { } r } => Need(Name(r.Id), r),
        { Reputation: { } r } => Need(Name(r.Id), r),
        { Stones: { } r } => Need(StonesName, r),
        { ZoneDanger: { } r } => Need(DangerName, r),
        { Stage: { } r } => Need(StageName, r),
        { AgeYears: { } r } => Need(AgeName, r),
        { Zone: { } zones } => NeedZone with { Args = [new("zones", zones.Select(Name).ToList())] },
        _ => throw new ArgumentOutOfRangeException(nameof(c)),
    };

    private static LocKey Need(LocKey what, NumberRange range) => range switch
    {
        { Min: { } min, Max: null } => NeedMin with { Args = [new("what", what), new("min", min)] },
        { Min: null, Max: { } max } => NeedMax with { Args = [new("what", what), new("max", max)] },
        _ => NeedRange with { Args = [new("what", what), new("min", range.Min ?? 0), new("max", range.Max ?? 0)] },
    };

    /// <summary>A line for the player about <paramref name="e"/>, or null for routine events like a passing week.</summary>
    public static LocKey? Describe(SimEvent e) => e switch
    {
        ActivityFinished f => Finished with { Args = [new("activity", ActivityName(f.Activity))] },
        ActivityInterrupted i => Interrupted with { Args = [new("activity", ActivityName(i.Activity))] },
        LifeEndApproaching l => LifeEnd with { Args = [new("years", l.YearsLeft)] },
        RunEnded r => Ended with { Args = [new("cause", CauseName(r.Cause))] },
        ZoneEntered z => Entered with { Args = [new("zone", Name(z.ZoneId))] },
        PlaceQiLow p => PlaceLow with { Args = [new("place", Name(p.PlaceId))] },
        BuffExhausted b => BuffGone with { Args = [new("buff", BuffName(b.Kind))] },
        KnowledgeLevelUp k => LevelUp with { Args = [new("knowledge", Name(k.KnowledgeId)), new("level", k.Level)] },
        StudyCapReached c => CapReached with { Args = [new("knowledge", Name(c.KnowledgeId))] },
        PillTaken p => PillTakenLine with { Args = [new("item", Name(p.ItemId)), new("energy", Number(p.Energy))] },
        StageScaleFull s => ScaleFull with { Args = [new("vector", Name(CultivationSystem.VectorDefId(s.VectorId)))] },
        BreakthroughSucceeded b => Succeeded with
        {
            Args = [new("stage", Name(b.StageId)), new("rank", Name(b.RankId)), new("years", Number(b.LifespanYears))],
        },
        BreakthroughFailed b => Failed with { Args = [new("zone", Name(b.FailureZoneId))] },
        InjuryReceived i => Injured with { Args = [new("injury", Name(i.InjuryId)), new("amount", Number(i.Amount))] },
        InjuryHealed h => Healed with { Args = [new("injury", Name(h.InjuryId))] },
        EventStarted => EventOpened,
        StatChanged s => StatChangedLine with { Args = [new("stat", Name(s.StatId)), new("amount", Signed(s.Amount))] },
        ResourceChanged r => ResourceChangedLine with { Args = [new("resource", Name(r.ResourceId)), new("amount", Signed(r.Amount))] },
        ItemsChanged i => ItemsChangedLine with { Args = [new("item", Name(i.ItemId)), new("amount", Signed(i.Count))] },
        StonesChanged s => StonesChangedLine with { Args = [new("amount", Signed(s.Amount))] },
        BuffGained b => BuffGainedLine with { Args = [new("buff", BuffName(b.Kind)), new("points", Number(b.Points))] },
        ReputationChanged r => ReputationChangedLine with { Args = [new("sect", Name(r.SectId)), new("amount", Signed(r.Amount))] },
        Revealed r => RevealedLine with { Args = [new("place", Name(r.Id))] },
        RumorHeard r => Heard with { Args = [new("rumor", Name(r.RumorId))] },
        CombatResolved c => (c.End switch { CombatEnd.Won => CombatWon, CombatEnd.Fled => CombatFled, _ => CombatDied }) with
        {
            Args = [new("group", Name(c.GroupId)), new("rounds", c.Rounds), new("health", Number(c.HealthLost)), new("qi", Number(c.QiSpent))],
        },
        _ => null,
    };

    private static LocKey ForecastText(BreakthroughEstimate forecast)
    {
        var q = forecast.Quality;
        return ForecastLine with
        {
            Args =
            [
                new("threshold", Number(forecast.Threshold)),
                new("foundation", Number(forecast.Foundation)),
                new("chance", Number(forecast.FailureChance * 100)),
                new("zone", forecast.FailureZoneId is { } zone ? Name(zone) : NoDeficit),
                new("rank", Name(forecast.RankId)),
                new("quality", Number(q.Value)),
                new("ceiling", Number(q.Ceiling)),
                new("base", Number(q.Base)),
                new("purity", Number(q.Purity)),
                new("toxins", Number(q.Toxins)),
                new("injuries", Number(q.Injuries)),
                new("age", Number(q.Age)),
                new("delay", Number(q.Delay)),
            ],
        };
    }

    /// <summary>Computed values are shown to one decimal place.</summary>
    private static double Number(double value) => Math.Round(value, 1);

    /// <summary>A change with its sign: "+5", "-2".</summary>
    private static string Signed(double value) =>
        Number(value).ToString("+0.#;-0.#;0", System.Globalization.CultureInfo.InvariantCulture);

    private static LocKey Years(int weeks, int weeksPerYear) =>
        Duration with { Args = [new("years", weeks / weeksPerYear), new("weeks", weeks % weeksPerYear)] };

    private static LocKey ActivityName(Activity activity) => activity switch
    {
        WaitActivity => Wait,
        InquireActivity => Inquire,
        TravelActivity t => TravelTo with { Args = [new("zone", Name(t.Destination))] },
        MeditateActivity { SubjectId: { } subject } m => MeditateStudy with
        {
            Args = [new("direction", Name(m.DirectionId)), new("subject", Name(subject)), new("place", Name(m.PlaceId))],
        },
        MeditateActivity m => Meditate with { Args = [new("direction", Name(m.DirectionId)), new("place", Name(m.PlaceId))] },
        BreakthroughActivity b => Seclusion with { Args = [new("place", Name(b.PlaceId))] },
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, null),
    };

    private static LocKey BuffName(BuffKind kind) => kind switch
    {
        BuffKind.CombatInsight => CombatInsight,
        BuffKind.PillEnergy => PillEnergy,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static LocKey CauseName(RunEndCause cause) => cause switch
    {
        RunEndCause.OldAge => OldAge,
        RunEndCause.BreakthroughDeath => BreakthroughDeath,
        RunEndCause.CombatDeath => CombatDeath,
        _ => throw new ArgumentOutOfRangeException(nameof(cause), cause, null),
    };

    private static LocKey StageRelationName(StageRelation relation) => relation switch
    {
        StageRelation.Lower => StageLower,
        StageRelation.Equal => StageEqual,
        StageRelation.Higher => StageHigher,
        _ => throw new ArgumentOutOfRangeException(nameof(relation), relation, null),
    };

    private static LocKey ModeName(GameMode mode) => mode switch
    {
        GameMode.Overworld => Overworld,
        GameMode.EventDialog => EventDialog,
        GameMode.Combat => Combat,
        GameMode.RunEnd => RunEnd,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
