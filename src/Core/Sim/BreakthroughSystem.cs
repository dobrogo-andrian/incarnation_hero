using Core.Character;
using Core.Content;
using Core.Loc;
using Core.Rng;
using Core.State;

namespace Core.Sim;

/// <summary>A breakthrough's quality term by term, for "where does this number come from".</summary>
public sealed record QualityBreakdown(double Base, double Purity, double Toxins, double Injuries, double Age, double Delay, double Ceiling)
{
    /// <summary>Base + purity − toxins − injuries − age − delay, within [0, ceiling].</summary>
    public double Value => StateNumbers.Round(Math.Clamp(Base + Purity - Toxins - Injuries - Age - Delay, 0, Ceiling));
}

/// <summary>
/// What a breakthrough attempt will bring (§4.5). The forecast shows exactly this and the attempt
/// resolves by it, so they cannot disagree. <see cref="FailureZoneId"/> is null without a deficit.
/// </summary>
public sealed record BreakthroughEstimate(
    double Threshold,
    double Foundation,
    double FailureChance,
    string? FailureZoneId,
    bool Fatal,
    QualityBreakdown Quality,
    string RankId,
    int RankIndex);

/// <summary>
/// Breakthroughs (§4.5, D35): a seclusion of the stage's breakthrough weeks that resolves in the
/// last week. Below the threshold the attempt may fail by the deficit's zone; otherwise the vector
/// moves to the next stage, and the quality rank adds years of life and body stats.
/// </summary>
public static class BreakthroughSystem
{
    /// <summary>
    /// The one breakthrough function (tech 4.4): the forecast calls it with the weeks the attempt
    /// still needs, the attempt itself with none. Requires a stage that has a breakthrough.
    /// </summary>
    public static BreakthroughEstimate Estimate(PlayerState player, CultivationTrack track, ContentDb db, CharacterRules rules, int weeksAhead)
    {
        var run = db.Get<RunConfigDef>(RunConfigDef.RunId);
        var config = db.Get<BreakthroughConfigDef>(BreakthroughConfigDef.BreakthroughId);
        var breakthrough = CultivationSystem.Stage(track, db).Breakthrough!;

        double foundation = CultivationSystem.FoundationOf(player, rules, db).Total;
        double deficit = Math.Clamp((breakthrough.Threshold - foundation) / breakthrough.Threshold, 0, 1);
        var (zone, chance) = FailureOf(deficit, config, db);

        var quality = QualityOf(player, track, rules, run, config, db, weeksAhead);
        int rank = config.Ranks.FindLastIndex(r => db.Get<QualityRankDef>(r).MinQuality <= quality.Value);
        return new BreakthroughEstimate(
            breakthrough.Threshold, foundation, StateNumbers.Round(chance), zone?.Id, zone?.Fatal ?? false, quality, config.Ranks[rank], rank);
    }

    /// <summary>Why the breakthrough cannot start now, or null if it can.</summary>
    public static LocKey? Validate(GameState state, SimContext ctx, BreakthroughActivity attempt)
    {
        var player = state.Player;
        if (player.Tracks.Find(t => t.VectorId == attempt.VectorId) is not { } track)
            return RejectReasons.InvalidActivity;
        var stage = CultivationSystem.Stage(track, ctx.Content);
        if (stage.Breakthrough is not { } breakthrough)
            return RejectReasons.NoNextStage;
        if (track.StageProgress < stage.ProgressRequired)
            return RejectReasons.StageScaleNotFull;
        if (!ctx.Content.TryGet<PlaceDef>(attempt.PlaceId, out var place) || place.Zone != player.ZoneId)
            return RejectReasons.PlaceNotHere;
        return Estimate(player, track, ctx.Content, ctx.Rules, breakthrough.Weeks).Fatal && !attempt.AcceptDeathRisk
            ? RejectReasons.DeathRiskNotAccepted
            : null;
    }

    /// <summary>Seclusion weeks of the vector's current breakthrough.</summary>
    public static int Weeks(PlayerState player, ContentDb db, string vectorId) =>
        CultivationSystem.Stage(player.Tracks.First(t => t.VectorId == vectorId), db).Breakthrough!.Weeks;

    /// <summary>The last week of seclusion: roll for failure, then pay its price or take the next stage.</summary>
    public static void Resolve(GameState state, SimContext ctx, BreakthroughActivity attempt, List<SimEvent> events)
    {
        var player = state.Player;
        var track = player.Tracks.First(t => t.VectorId == attempt.VectorId);
        var estimate = Estimate(player, track, ctx.Content, ctx.Rules, 0);
        player.BreakthroughCeilingBonus = 0;

        if (estimate.FailureChance > 0 && ctx.Rng[RngStream.Breakthrough].Chance(estimate.FailureChance))
            Fail(state, ctx, track, FailedZone(estimate.FailureZoneId!, attempt.AcceptDeathRisk, ctx), events);
        else
            Succeed(player, ctx, track, estimate, events);
    }

    private static void Succeed(PlayerState player, SimContext ctx, CultivationTrack track, BreakthroughEstimate estimate, List<SimEvent> events)
    {
        var breakthrough = CultivationSystem.Stage(track, ctx.Content).Breakthrough!;
        double years = breakthrough.LifespanYears[estimate.RankIndex];
        player.LifespanWeeks += (int)Math.Round(years * ctx.Config.WeeksPerYear);
        double gain = breakthrough.StatGain[estimate.RankIndex];
        foreach (var stat in player.Stats.Where(s => ctx.Breakthrough.GainStats.Contains(s.StatId)))
            stat.Base = StateNumbers.Round(stat.Base + gain);

        track.StageIndex++;
        track.StageProgress = 0;
        track.ScaleFullSinceAgeWeeks = null;
        track.LastBreakthroughQuality = estimate.RankIndex;
        events.Add(new BreakthroughSucceeded(track.VectorId, CultivationSystem.Stage(track, ctx.Content).Id, estimate.RankId, years));
    }

    /// <summary>A fatal zone counts only for an attempt that accepted the risk; otherwise the worst survivable zone applies.</summary>
    private static FailureZoneDef FailedZone(string zoneId, bool acceptDeathRisk, SimContext ctx)
    {
        var zones = ctx.Breakthrough.FailureZones.Select(ctx.Content.Get<FailureZoneDef>).ToList();
        var zone = ctx.Content.Get<FailureZoneDef>(zoneId);
        return zone.Fatal && !acceptDeathRisk ? zones.Last(z => !z.Fatal) : zone;
    }

    private static void Fail(GameState state, SimContext ctx, CultivationTrack track, FailureZoneDef zone, List<SimEvent> events)
    {
        if (zone.Fatal)
        {
            RunEndSystem.End(state, RunEndCause.BreakthroughDeath, events);
            return;
        }

        var player = state.Player;
        if (zone.ScaleLoss > 0)
        {
            track.StageProgress = StateNumbers.Round(track.StageProgress * (1 - zone.ScaleLoss));
            track.ScaleFullSinceAgeWeeks = null;
        }
        ResourceSystem.Restore(player, ctx, ctx.Config.ToxinsResource, zone.Toxins);
        var purity = player.Stats.First(s => s.StatId == ctx.Config.PurityStat);
        purity.Base = StateNumbers.Round(purity.Base * (1 - zone.PurityLoss));
        events.Add(new BreakthroughFailed(track.VectorId, zone.Id));
        foreach (var (injuryId, amount) in zone.Injuries.OrderBy(i => i.Key, StringComparer.Ordinal))
            InjurySystem.Inflict(player, ctx, injuryId, amount, events);
    }

    /// <summary>The deficit's zone and the failure chance in it, growing linearly across the zone.</summary>
    private static (FailureZoneDef? Zone, double Chance) FailureOf(double deficit, BreakthroughConfigDef config, ContentDb db)
    {
        if (deficit <= 0)
            return (null, 0);

        double from = 0;
        foreach (string id in config.FailureZones)
        {
            var zone = db.Get<FailureZoneDef>(id);
            if (deficit <= zone.UpTo)
                return (zone, zone.ChanceFrom + (zone.ChanceTo - zone.ChanceFrom) * (deficit - from) / (zone.UpTo - from));
            from = zone.UpTo;
        }
        throw new InvalidOperationException("Failure zones must reach a deficit of 1.");
    }

    /// <summary>
    /// Quality (§4.5): purity counts up to the method's cap; toxins, injuries, age past the free years
    /// and the years since the scale filled take it down; the method (and a breakthrough pill) set the ceiling.
    /// </summary>
    private static QualityBreakdown QualityOf(
        PlayerState player, CultivationTrack track, CharacterRules rules, RunConfigDef run, BreakthroughConfigDef config, ContentDb db, int weeksAhead)
    {
        double ceiling = config.NoMethodQualityCeiling, purityCap = config.NoMethodPurityCap;
        if (player.ActiveMethodId is { } methodId)
        {
            var method = db.Get<KnowledgeDef>(methodId).Method!;
            int level = KnowledgeSystem.Level(player, methodId);
            ceiling = method.QualityCeiling + method.QualityCeilingPerLevel * level;
            purityCap = method.PurityCap + method.PurityCapPerLevel * level;
        }

        int ageWeeks = player.AgeWeeks + weeksAhead;
        double Years(int weeks) => Math.Max(0, weeks) / (double)run.WeeksPerYear;
        double toxins = player.Resources.First(r => r.ResourceId == run.ToxinsResource).Current;
        double delay = track.ScaleFullSinceAgeWeeks is int since ? Years(ageWeeks - since) : 0;

        return new QualityBreakdown(
            config.BaseQuality,
            StateNumbers.Round(Math.Min(rules.Value(player, run.PurityStat), purityCap) * config.QualityPerPurity),
            StateNumbers.Round(toxins * config.QualityPerToxin),
            InjurySystem.QualityPenalty(player, db),
            StateNumbers.Round(Years(ageWeeks - config.AgePenaltyFromYears * run.WeeksPerYear) * config.QualityPerAgeYear),
            StateNumbers.Round(delay * config.QualityPerDelayYear),
            StateNumbers.Round(ceiling + player.BreakthroughCeilingBonus));
    }
}
