using Core.Character;
using Core.Combat;
using Core.Content;
using Core.Loc;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Turns the PlayerView and SimEvents into localization keys. Shared by Sim.Cli and the Godot
/// shell so both say the same thing; each resolves the keys with its Localizer.
/// </summary>
/// <summary>
/// A line of the view with what explains its number, for "where does this come from" tooltips
/// (D44); <see cref="Why"/> is empty when there is nothing to explain.
/// </summary>
public sealed record ExplainedLine(LocKey Text, IReadOnlyList<LocKey> Why)
{
    public static ExplainedLine Plain(LocKey text) => new(text, []);
}

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
    private static readonly LocKey RankedStatLine = new("str.view.stat_rank");
    private static readonly LocKey RankedBodyStatLine = new("str.view.body_stat_rank");
    private static readonly LocKey StandingLine = new("str.view.stat_standing");
    private static readonly LocKey StandingTopLine = new("str.view.stat_standing_top");
    private static readonly LocKey ScaleOnlyLine = new("str.view.stat_scale");
    private static readonly LocKey WhyScaleOnly = new("str.why.stat_scale_only");
    private static readonly LocKey WhyScale = new("str.why.stat_scale");
    private static readonly LocKey WhyOverflow = new("str.why.stat_overflow");
    private static readonly LocKey WhyStatBreakthrough = new("str.why.stat_breakthrough");
    private static readonly LocKey DeficitLine = new("str.view.stat_deficit");
    private static readonly LocKey WhyDeficit = new("str.why.stat_deficit");
    private static readonly LocKey WhyCollapse = new("str.why.stat_collapse");
    private static readonly LocKey WhyErosion = new("str.why.stat_erosion");
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
    private static readonly LocKey KnowledgeLine = new("str.view.knowledge");
    private static readonly LocKey MeditateStudy = new("str.activity.meditate_study");
    private static readonly LocKey LastQualityLine = new("str.view.last_breakthrough");
    private static readonly LocKey ForecastLine = new("str.view.breakthrough_forecast");
    private static readonly LocKey NoDeficit = new("str.view.no_deficit");
    private static readonly LocKey MethodLine = new("str.view.method");
    private static readonly LocKey NoMethodLine = new("str.view.no_method");
    private static readonly LocKey MethodLevelLine = new("str.view.method_level");
    private static readonly LocKey MethodLimitsLine = new("str.view.method_limits");
    private static readonly LocKey CountLine = new("str.view.item_count");
    private static readonly LocKey PillEnergyLine = new("str.view.pill_energy");
    private static readonly LocKey PillCeilingLine = new("str.view.pill_ceiling");
    private static readonly LocKey PillHealsLine = new("str.view.pill_heals");
    private static readonly LocKey PillStatLine = new("str.view.pill_stat");
    private static readonly LocKey PillRepeatLine = new("str.view.pill_repeat");
    private static readonly LocKey PillToxicityLine = new("str.view.pill_toxicity");
    private static readonly LocKey FolioSubjectLine = new("str.view.folio_subject");
    private static readonly LocKey FolioLevelLine = new("str.view.folio_level");
    private static readonly LocKey FolioSpeedLine = new("str.view.folio_speed");
    private static readonly LocKey FolioMethodLine = new("str.view.folio_method");
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
    private static readonly LocKey Work = new("str.activity.work");
    private static readonly LocKey Hunt = new("str.activity.hunt");
    private static readonly LocKey Arena = new("str.activity.arena");
    private static readonly LocKey JobLine = new("str.view.job");
    private static readonly LocKey HuntLine = new("str.view.hunt");
    private static readonly LocKey BoutLine = new("str.view.bout");
    private static readonly LocKey TrainsLine = new("str.view.trains");

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
    private static readonly LocKey StatRankUpLine = new("str.event.stat_rank_up");
    private static readonly LocKey StatRankDownLine = new("str.event.stat_rank_down");
    private static readonly LocKey StatRankChangedLine = new("str.event.stat_rank_changed");
    private static readonly LocKey ResourceChangedLine = new("str.event.resource_changed");
    private static readonly LocKey ItemsChangedLine = new("str.event.items_changed");
    private static readonly LocKey StonesChangedLine = new("str.event.stones_changed");
    private static readonly LocKey BuffGainedLine = new("str.event.buff_gained");
    private static readonly LocKey ReputationChangedLine = new("str.event.reputation_changed");
    private static readonly LocKey RevealedLine = new("str.event.revealed");
    private static readonly LocKey CombatWon = new("str.event.combat_won");
    private static readonly LocKey CombatFled = new("str.event.combat_fled");
    private static readonly LocKey CombatYielded = new("str.event.combat_yielded");
    private static readonly LocKey CombatBeaten = new("str.event.combat_beaten");
    private static readonly LocKey CombatDied = new("str.event.combat_died");
    private static readonly LocKey CombatDeath = new("str.end.combat_death");
    private static readonly LocKey OptionCombat = new("str.dialog.option_combat");
    private static readonly LocKey StageLower = new("str.combat.stage_lower");
    private static readonly LocKey StageEqual = new("str.combat.stage_equal");
    private static readonly LocKey StageHigher = new("str.combat.stage_higher");
    private static readonly LocKey FightTitle = new("str.fight.title");
    private static readonly LocKey TermsToDeath = new("str.fight.terms_to_death");
    private static readonly LocKey TermsSpared = new("str.fight.terms_spared");
    private static readonly LocKey TermsBout = new("str.fight.terms_bout");
    private static readonly LocKey FoeOne = new("str.fight.foe");
    private static readonly LocKey FoeMany = new("str.fight.foes");
    private static readonly LocKey FoeReading = new("str.fight.foe_reading");
    private static readonly LocKey FoeHidden = new("str.fight.foe_hidden");
    private static readonly LocKey FoeTechniques = new("str.fight.foe_techniques");
    private static readonly LocKey FoeNoTechniques = new("str.fight.foe_no_techniques");
    private static readonly LocKey FoeTechniquesUnseen = new("str.fight.foe_techniques_unseen");
    private static readonly LocKey ScoutingReliability = new("str.fight.reliability");

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

    private static readonly LocKey WhyItem = new("str.view.why_item");
    private static readonly LocKey WhyBase = new("str.why.base");
    private static readonly LocKey WhyAdd = new("str.why.add");
    private static readonly LocKey WhyMul = new("str.why.mul");
    private static readonly LocKey WhyCap = new("str.why.cap");
    private static readonly LocKey WhyOverride = new("str.why.override");
    private static readonly LocKey WhyThreshold = new("str.why.threshold");
    private static readonly LocKey WhyQualityBase = new("str.why.quality_base");
    private static readonly LocKey WhyPurity = new("str.why.purity");
    private static readonly LocKey WhyAge = new("str.why.age");
    private static readonly LocKey WhyDelay = new("str.why.delay");
    private static readonly LocKey WhyCeiling = new("str.why.ceiling");

    private static readonly LocKey SummaryWeeks = new("str.summary.weeks");
    private static readonly LocKey SummaryBreakthrough = new("str.summary.breakthrough");
    private static readonly LocKey SummaryBreakthroughFailed = new("str.summary.breakthrough_failed");
    private static readonly LocKey SummaryCombats = new("str.summary.combats");
    private static readonly LocKey SummaryPills = new("str.summary.pills");
    private static readonly LocKey SummaryStones = new("str.summary.stones");
    private static readonly LocKey SummaryDialogs = new("str.summary.dialogs");

    /// <summary>The whole PlayerView as lines, section by section, as Sim.Cli prints it; explanations follow their line indented.</summary>
    public static IReadOnlyList<LocKey> ViewLines(PlayerView view, ContentDb db) =>
    [
        .. TimeLines(view, db),
        ModeLine with { Args = [new("mode", ModeName(view.Mode))] },
        .. EndLines(view),
        .. SummaryLines(view, db),
        ZoneText(view),
        .. PlaceLines(view),
        .. Flatten(CharacterLines(view)),
        .. Flatten(CultivationLines(view, db)),
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

    /// <summary>Stats with their aspects, resources and injuries; a stat or resource changed by modifiers explains where its number comes from.</summary>
    public static IReadOnlyList<ExplainedLine> CharacterLines(PlayerView view) => [.. view.Stats.SelectMany(StatLines), .. ConditionLines(view)];

    /// <summary>Resources against their maximums, then injuries.</summary>
    public static IReadOnlyList<ExplainedLine> ConditionLines(PlayerView view)
    {
        var lines = new List<ExplainedLine>();
        foreach (var resource in view.Resources)
        {
            var text = ResourceLine with
            {
                Args =
                [
                    new("resource", Name(resource.ResourceId)),
                    new("current", Number(resource.Current)),
                    new("max", Number(resource.Max.Value)),
                ],
            };
            lines.Add(new(text, BreakdownLines(resource.Max)));
        }
        foreach (var injury in view.Injuries)
            lines.Add(ExplainedLine.Plain(InjuryLine with { Args = [new("injury", Name(injury.InjuryId)), new("value", Number(injury.Value)), new("max", Number(injury.Max))] }));
        return lines;
    }

    /// <summary>A stat with its value and aspects, then, for a ranked stat, its scale, overflow and deficit.</summary>
    public static IReadOnlyList<ExplainedLine> StatLines(StatView stat) =>
        [Title(stat, StatLine, BodyStatLine, new("value", Number(stat.Stat.Value))), .. StandingLines(stat).Select(ExplainedLine.Plain)];

    /// <summary>
    /// A stat's line for a view that draws a ranked stat's scale (§2.3): its rank stands in for the value,
    /// which the scale already shows.
    /// </summary>
    public static ExplainedLine StatTitle(StatView stat) => stat.Standing is { } s
        ? Title(stat, RankedStatLine, RankedBodyStatLine, new("rank", Name(s.RankId)))
        : Title(stat, StatLine, BodyStatLine, new("value", Number(stat.Stat.Value)));

    private static ExplainedLine Title(StatView stat, LocKey plain, LocKey withAspects, LocArg value)
    {
        var name = new LocArg("stat", Name(stat.Stat.Id));
        var text = stat.Aspects.Count == 0
            ? plain with { Args = [name, value] }
            : withAspects with
            {
                Args =
                [
                    name,
                    value,
                    new("aspects", stat.Aspects.Select(a => AspectPart with { Args = [new("aspect", Name(a.Id)), new("value", Number(a.Value))] }).ToList()),
                ],
            };
        return new(text, [.. BreakdownLines(stat.Stat), .. stat.Aspects.SelectMany(BreakdownLines)]);
    }

    /// <summary>A ranked stat's rank, scale and overflow, and its deficit when the rank can fall; nothing for other stats.</summary>
    public static IReadOnlyList<LocKey> StandingLines(StatView stat)
    {
        if (stat.Standing is not { } s)
            return [];
        var lines = new List<LocKey> { StandingText(s) };
        if (s.Collapse is { } collapse)
            lines.Add(DeficitLine with { Args = [new("deficit", Number(s.Deficit)), new("collapse", Number(collapse))] });
        return lines;
    }

    private static LocKey StandingText(StatStanding s) => (s.DecayPerWeek is null ? ScaleOnlyLine : s.Breakthrough is null ? StandingTopLine : StandingLine) with
    {
        Args =
        [
            new("rank", Name(s.RankId)),
            new("filled", Number(s.Filled)),
            new("pool", Number(s.Pool)),
            new("overflow", Number(s.Overflow)),
            new("needed", Number(s.Breakthrough ?? 0)),
        ],
    };

    /// <summary>How a ranked stat's scale, overflow, deficit and their rank changes work, for the bars' tooltip.</summary>
    public static IReadOnlyList<LocKey> StandingHelp(StatView stat)
    {
        var s = stat.Standing!;
        if (s.DecayPerWeek is not { } decay)
            return [WhyScaleOnly];
        var lines = new List<LocKey> { WhyScale, WhyOverflow with { Args = [new("decay", Number(decay * 100))] } };
        if (s.Breakthrough is { } needed)
            lines.Add(WhyStatBreakthrough with { Args = [new("needed", Number(needed))] });
        if (s.HealPerWeek is { } heal)
            lines.Add(WhyDeficit with { Args = [new("heal", Number(heal * 100))] });
        if (s.Collapse is { } collapse)
            lines.Add(WhyCollapse with { Args = [new("collapse", Number(collapse))] });
        if (stat.ErodedBy.Count > 0)
            lines.Add(WhyErosion with { Args = [new("sources", stat.ErodedBy.Select(Name).ToList())] });
        return lines;
    }

    /// <summary>
    /// Where a computed value comes from: its base, then each modifier with its source. Nothing when
    /// no modifier touches it, since the value is then just its base.
    /// </summary>
    public static IReadOnlyList<LocKey> BreakdownLines(StatBreakdown breakdown)
    {
        if (breakdown.Modifiers.Count == 0)
            return [];
        var lines = new List<LocKey> { WhyBase with { Args = [new("name", Name(breakdown.Id)), new("value", Number(breakdown.Base))] } };
        foreach (var part in breakdown.Modifiers)
        {
            var source = new LocArg("source", Name(part.SourceId));
            lines.Add(part.Op switch
            {
                ModifierOp.Add => WhyAdd with { Args = [new("value", Signed(part.Value)), source] },
                ModifierOp.Mul => WhyMul with { Args = [new("value", Signed(part.Value * 100)), source] },
                ModifierOp.Cap => WhyCap with { Args = [new("value", Number(part.Value)), source] },
                ModifierOp.Override => WhyOverride with { Args = [new("value", Number(part.Value)), source] },
                _ => throw new ArgumentOutOfRangeException(nameof(breakdown), part.Op, null),
            });
        }
        return lines;
    }

    /// <summary>Stage scales with the breakthrough forecast (explained), method, foundation, knowledge and buffs.</summary>
    public static IReadOnlyList<ExplainedLine> CultivationLines(PlayerView view, ContentDb db)
    {
        var lines = new List<LocKey>();
        var result = new List<ExplainedLine>();
        foreach (var track in view.Tracks)
        {
            result.Add(ExplainedLine.Plain(TrackText(track, db)));
            if (track.LastBreakthroughQuality is int rank)
                result.Add(ExplainedLine.Plain(LastQualityLine with { Args = [new("rank", Name(db.Get<BreakthroughConfigDef>(BreakthroughConfigDef.BreakthroughId).Ranks[rank]))] }));
            if (track.Forecast is { } forecast)
                result.Add(ForecastText(forecast));
        }
        lines.Add(view.ActiveMethodId is { } method ? MethodLine with { Args = [new("method", Name(method))] } : NoMethodLine);

        var foundation = view.Foundation;
        LocArg[] parts = [new("total", Number(foundation.Total)), new("knowledge", Number(foundation.Knowledge)), new("purity", Number(foundation.Purity))];
        lines.Add(FoundationLine with { Args = parts });
        foreach (var knowledge in view.Knowledge)
            lines.Add(KnowledgeLine with { Args = [new("knowledge", Name(knowledge.KnowledgeId)), new("level", knowledge.Level)] });

        foreach (var buff in view.Buffs)
            lines.Add(BuffLine with { Args = [new("buff", BuffName(buff.Kind)), new("points", Number(buff.Points))] });
        return [.. result, .. lines.Select(ExplainedLine.Plain)];
    }

    /// <summary>The run's record once it is over (D47): weeks by activity, breakthroughs, fights, pills, stones and dialogs.</summary>
    public static IReadOnlyList<LocKey> SummaryLines(PlayerView view, ContentDb db)
    {
        if (view.EndCause is null)
            return [];
        var stats = view.RunStats;
        int weeksPerYear = db.Get<RunConfigDef>(RunConfigDef.RunId).WeeksPerYear;
        int total = Math.Max(1, stats.ActivityWeeks.Sum(t => t.Weeks));
        var lines = new List<LocKey>();
        foreach (var tally in stats.ActivityWeeks.OrderByDescending(t => t.Weeks).ThenBy(t => t.Key, StringComparer.Ordinal))
        {
            lines.Add(SummaryWeeks with
            {
                Args = [new("activity", ActivityKeyName(tally.Key)), new("weeks", tally.Weeks), new("share", Number(100.0 * tally.Weeks / total))],
            });
        }
        foreach (var b in stats.Breakthroughs)
        {
            var age = new LocArg("age", Years(b.AgeWeeks, weeksPerYear));
            lines.Add(b.RankId is { } rank
                ? SummaryBreakthrough with { Args = [age, new("rank", Name(rank))] }
                : SummaryBreakthroughFailed with { Args = [age, new("zone", Name(b.FailureZoneId!))] });
        }
        lines.Add(SummaryCombats with { Args = [new("won", stats.CombatsWon), new("fled", stats.CombatsFled), new("beaten", stats.CombatsBeaten)] });
        lines.Add(SummaryPills with { Args = [new("pills", stats.PillsTaken)] });
        lines.Add(SummaryStones with { Args = [new("earned", stats.StonesEarned), new("spent", stats.StonesSpent)] });
        lines.Add(SummaryDialogs with { Args = [new("dialogs", stats.Dialogs), new("rumors", stats.RumorsHeard)] });
        return lines;
    }

    /// <summary>The player-facing name of an interrupt category, for the interrupt settings.</summary>
    public static LocKey InterruptName(InterruptCategory category) => new($"str.interrupt.{SnakeCase(category.ToString())}");

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

    /// <summary>
    /// What a carried item does, for the inventory: a pill's energy (the next one's), ceiling bonus,
    /// healing, stats and toxicity; a folio's subject with its level against the folio's ceiling, and its speed.
    /// </summary>
    public static IReadOnlyList<LocKey> ItemDetails(ItemView item, PlayerView view, ContentDb db)
    {
        var def = db.Get<ItemDef>(item.ItemId);
        var lines = new List<LocKey> { CountLine with { Args = [new("count", item.Count)] } };
        if (def.Pill is { } pill)
        {
            if (item.NextPillEnergy is > 0 and double energy)
                lines.Add(PillEnergyLine with { Args = [new("energy", Number(energy))] });
            if (pill.CeilingBonus > 0)
                lines.Add(PillCeilingLine with { Args = [new("bonus", Number(pill.CeilingBonus))] });
            foreach (var (injury, points) in pill.Heals.OrderBy(h => h.Key, StringComparer.Ordinal))
                lines.Add(PillHealsLine with { Args = [new("injury", Name(injury)), new("points", Number(points))] });
            foreach (var (stat, points) in pill.Stats.OrderBy(s => s.Key, StringComparer.Ordinal))
                lines.Add(PillStatLine with { Args = [new("stat", Name(stat)), new("points", Number(points))] });
            if (pill.RepeatFactor < 1)
                lines.Add(PillRepeatLine);
            lines.Add(PillToxicityLine with { Args = [new("toxicity", Number(pill.Toxicity))] });
        }
        if (def.Folio is { } folio)
        {
            lines.Add(FolioSubjectLine with { Args = [new("subject", Name(folio.Subject))] });
            lines.Add(FolioLevelLine with { Args = [new("level", LevelOf(view, folio.Subject)), new("max", folio.MaxLevel)] });
            lines.Add(FolioSpeedLine with { Args = [new("speed", Number(folio.Speed))] });
            if (db.Get<KnowledgeDef>(folio.Subject).Method is not null)
                lines.Add(FolioMethodLine);
        }
        return lines;
    }

    /// <summary>
    /// What the method slot holds: the active method's level, breakthrough quality ceiling and purity
    /// cap, or the ceiling and cap without a method (§6, D35).
    /// </summary>
    public static IReadOnlyList<LocKey> MethodDetails(PlayerView view, ContentDb db)
    {
        if (view.ActiveMethodId is not { } methodId)
        {
            var config = db.Get<BreakthroughConfigDef>(BreakthroughConfigDef.BreakthroughId);
            return [NoMethodLine, MethodLimitsLine with { Args = [new("ceiling", Number(config.NoMethodQualityCeiling)), new("cap", Number(config.NoMethodPurityCap))] }];
        }
        var method = db.Get<KnowledgeDef>(methodId).Method!;
        int level = LevelOf(view, methodId);
        return
        [
            MethodLine with { Args = [new("method", Name(methodId))] },
            MethodLevelLine with { Args = [new("level", level)] },
            MethodLimitsLine with { Args = [new("ceiling", Number(method.CeilingAt(level))), new("cap", Number(method.PurityCapAt(level)))] },
        ];
    }

    private static int LevelOf(PlayerView view, string knowledgeId) =>
        view.Knowledge.FirstOrDefault(k => k.KnowledgeId == knowledgeId)?.Level ?? 0;

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

    /// <summary>
    /// The fight on offer (D67, D70): its title and what is at stake, then each kind of foe as the player
    /// reads it, and how far those readings can be trusted.
    /// </summary>
    public static IReadOnlyList<LocKey> FightLines(FightView view)
    {
        var lines = new List<LocKey>
        {
            FightTitle with { Args = [new("group", Name(view.GroupId))] },
            view.Terms switch { BattleTerms.Bout => TermsBout, BattleTerms.Spared => TermsSpared, _ => TermsToDeath },
        };
        foreach (var foe in view.Scouting.Foes)
        {
            lines.Add((foe.Count == 1 ? FoeOne : FoeMany) with
            {
                Args = [new("name", Name(foe.EnemyId)), new("count", foe.Count), new("stage", StageRelationName(foe.Stage))],
            });
            lines.Add(foe is { Vitality: { } vitality, Strike: { } strike, Speed: { } speed }
                ? FoeReading with { Args = [new("vitality", ComparisonName(vitality)), new("strike", ComparisonName(strike)), new("speed", ComparisonName(speed))] }
                : FoeHidden);
            lines.Add(foe.Techniques switch
            {
                null => FoeTechniquesUnseen,
                [] => FoeNoTechniques,
                var known => FoeTechniques with { Args = [new("techniques", known.Select(Name).ToList())] },
            });
        }
        if (view.Scouting.Foes.Any(f => f.Vitality is not null))
            lines.Add(ScoutingReliability with { Args = [new("percent", Number(view.Scouting.Reliability * 100))] });
        return lines;
    }

    private static LocKey ComparisonName(Comparison comparison) => new($"str.fight.cmp.{comparison switch
    {
        Comparison.FarBelow => "far_below",
        Comparison.Below => "below",
        Comparison.Even => "even",
        Comparison.Above => "above",
        Comparison.FarAbove => "far_above",
        _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
    }}");

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

    /// <summary>A job's offer: its weekly pay.</summary>
    public static LocKey JobText(JobDef job) => JobLine with { Args = [new("job", Name(job.Id)), new("pay", job.Pay)] };

    /// <summary>A hunting ground: the chance of prey a week and the beasts it may be.</summary>
    public static LocKey HuntText(HuntDef hunt) => HuntLine with
    {
        Args = [new("chance", Number(hunt.EncounterChance * 100)), new("groups", hunt.Groups.Keys.Order(StringComparer.Ordinal).Select(Name).ToList())],
    };

    /// <summary>One rank of arena opponents with its fee and prize.</summary>
    public static LocKey BoutText(ArenaBoutDef bout) => BoutLine with
    {
        Args = [new("group", Name(bout.Group)), new("fee", bout.Fee), new("prize", bout.Prize)],
    };

    /// <summary>What a training activity trains, by stat name.</summary>
    public static LocKey TrainsText(TrainingDef def) => TrainsLine with
    {
        Args = [new("stats", def.Gains.Keys.Order(StringComparer.Ordinal).Select(Name).ToList())],
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
        StatRankUp r => StatRankUpLine with { Args = [new("stat", Name(r.StatId)), new("rank", Name(r.RankId))] },
        StatRankDown r => StatRankDownLine with { Args = [new("stat", Name(r.StatId)), new("rank", Name(r.RankId))] },
        StatRankChanged r => StatRankChangedLine with { Args = [new("stat", Name(r.StatId)), new("rank", Name(r.RankId))] },
        ResourceChanged r => ResourceChangedLine with { Args = [new("resource", Name(r.ResourceId)), new("amount", Signed(r.Amount))] },
        ItemsChanged i => ItemsChangedLine with { Args = [new("item", Name(i.ItemId)), new("amount", Signed(i.Count))] },
        StonesChanged { Routine: true } => null,
        StonesChanged s => StonesChangedLine with { Args = [new("amount", Signed(s.Amount))] },
        BuffGained b => BuffGainedLine with { Args = [new("buff", BuffName(b.Kind)), new("points", Number(b.Points))] },
        ReputationChanged r => ReputationChangedLine with { Args = [new("sect", Name(r.SectId)), new("amount", Signed(r.Amount))] },
        Revealed r => RevealedLine with { Args = [new("place", Name(r.Id))] },
        RumorHeard r => Heard with { Args = [new("rumor", Name(r.RumorId))] },
        CombatResolved c => (c.End switch { CombatEnd.Won => CombatWon, CombatEnd.Fled => CombatFled, CombatEnd.Yielded => CombatYielded, CombatEnd.Beaten => CombatBeaten, _ => CombatDied }) with
        {
            Args = [new("group", Name(c.GroupId)), new("turns", c.Turns), new("health", Number(c.HealthLost)), new("qi", Number(c.QiSpent))],
        },
        _ => null,
    };

    /// <summary>The forecast's chance and rank, explained by the threshold against the foundation and each part of the quality.</summary>
    private static ExplainedLine ForecastText(BreakthroughEstimate forecast)
    {
        var q = forecast.Quality;
        var text = ForecastLine with
        {
            Args =
            [
                new("chance", Number(forecast.FailureChance * 100)),
                new("zone", forecast.FailureZoneId is { } zone ? Name(zone) : NoDeficit),
                new("rank", Name(forecast.RankId)),
                new("quality", Number(q.Value)),
            ],
        };
        LocKey Part(LocKey key, double value) => key with { Args = [new("value", Number(value))] };
        return new(text,
        [
            WhyThreshold with { Args = [new("threshold", Number(forecast.Threshold)), new("foundation", Number(forecast.Foundation))] },
            Part(WhyQualityBase, q.Base),
            Part(WhyPurity, q.Purity),
            Part(WhyAge, q.Age),
            Part(WhyDelay, q.Delay),
            Part(WhyCeiling, q.Ceiling),
        ]);
    }

    /// <summary>Each line followed by its explanations, indented: for plain-text output.</summary>
    private static IEnumerable<LocKey> Flatten(IEnumerable<ExplainedLine> lines) =>
        lines.SelectMany(l => l.Why.Select(w => WhyItem with { Args = [new("text", w)] }).Prepend(l.Text));

    /// <summary>What a <see cref="RunStatsSystem.ActivityKey"/> is called: a meditation direction by its own name.</summary>
    private static LocKey ActivityKeyName(string key) => key switch
    {
        "wait" => new("str.summary.wait"),
        RunStatsSystem.EventKey => new("str.summary.event"),
        "inquire" => new("str.summary.inquire"),
        "travel" => new("str.summary.travel"),
        "breakthrough" => new("str.summary.breakthrough_seclusion"),
        _ => Name(key),
    };

    private static string SnakeCase(string name) =>
        string.Concat(name.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

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
        WorkActivity w => Work with { Args = [new("job", Name(w.JobId))] },
        HuntActivity h => Hunt with { Args = [new("hunt", Name(h.HuntId))] },
        ArenaActivity a => Arena with { Args = [new("arena", Name(a.ArenaId))] },
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
