using Core.Character;
using Core.Content;
using Core.Loc;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Cultivation buffs and the stage scale (§4.1, §4.3): buffs are gained, combat insight fades
/// weekly, and assimilation spends buff points on stage progress until the scale is full.
/// </summary>
public static class CultivationSystem
{
    /// <summary>Content ID of a track's vector (<c>qi</c> → <c>vector.qi</c>).</summary>
    public static string VectorDefId(string vectorId) => $"vector.{vectorId}";

    public static CultivationStageDef Stage(CultivationTrack track, ContentDb db) =>
        db.Get<CultivationStageDef>(db.Get<CultivationVectorDef>(VectorDefId(track.VectorId)).Stages[track.StageIndex]);

    /// <summary>
    /// The foundation a breakthrough is measured against (§4.1, §4.5): knowledge levels and purity,
    /// each weighted by <c>config.run</c>.
    /// </summary>
    public static Foundation FoundationOf(PlayerState player, CharacterRules rules, RunConfigDef config) => new(
        StateNumbers.Round(player.Knowledge.Sum(k => k.Level) * config.FoundationPerKnowledgeLevel),
        StateNumbers.Round(rules.Value(player, config.PurityStat) * config.FoundationPerPurity));

    /// <summary>
    /// Makes a method studied to level 1 or more the active one, taking its aspect profile (A2); null
    /// goes back to no method and the starting profile. Instant.
    /// </summary>
    public static CommandOutcome SetActiveMethod(PlayerState player, SimContext ctx, string? methodId)
    {
        if (methodId is null)
        {
            player.ActiveMethodId = null;
            player.AspectProfileId = ctx.Config.StartAspectProfile;
            return CommandOutcome.Accept([]);
        }
        if (!ctx.Content.TryGet<KnowledgeDef>(methodId, out var knowledge) || knowledge.Method is not { } method)
            return CommandOutcome.Reject(RejectReasons.NotAMethod);
        if (KnowledgeSystem.Level(player, methodId) == 0)
            return CommandOutcome.Reject(RejectReasons.MethodNotStudied);

        player.ActiveMethodId = methodId;
        player.AspectProfileId = method.AspectProfile;
        return CommandOutcome.Accept([]);
    }

    /// <summary>A new combat insight does not add to an older one: the stronger of the two remains.</summary>
    public static void GrantCombatInsight(PlayerState player, double points)
    {
        var buff = GetOrAdd(player, BuffKind.CombatInsight);
        buff.Points = StateNumbers.Round(Math.Max(buff.Points, points));
    }

    /// <summary>Pill energy adds up: every pill tops up the same pool of points.</summary>
    public static void AddPillEnergy(PlayerState player, double points)
    {
        var buff = GetOrAdd(player, BuffKind.PillEnergy);
        buff.Points = StateNumbers.Round(buff.Points + points);
    }

    /// <summary>World phase: combat insight fades and vanishes below the configured minimum.</summary>
    public static void AdvanceWeek(GameState state, SimContext ctx, List<SimEvent> events)
    {
        var player = state.Player;
        if (player.Buffs.Find(b => b.Kind == BuffKind.CombatInsight) is not { } insight)
            return;

        insight.Points = StateNumbers.Round(insight.Points * (1 - ctx.Config.CombatInsightDecay));
        if (insight.Points < ctx.Config.CombatInsightMinPoints)
            Remove(player, insight, events);
    }

    /// <summary>Why assimilation into <paramref name="vectorDefId"/> cannot start now, or null if it can.</summary>
    public static LocKey? CannotAssimilate(PlayerState player, SimContext ctx, string vectorDefId)
    {
        if (player.Buffs.Count == 0)
            return RejectReasons.NoCultivationBuff;
        return IsScaleFull(Track(player, vectorDefId), ctx.Content) ? RejectReasons.StageScaleFull : null;
    }

    /// <summary>
    /// Spends up to <paramref name="capacity"/> buff points, in <see cref="BuffKind"/> order, on the
    /// vector's stage scale; never more than the buffs hold or the scale has room for.
    /// </summary>
    public static void Assimilate(PlayerState player, SimContext ctx, string vectorDefId, double capacity, List<SimEvent> events)
    {
        var track = Track(player, vectorDefId);
        double required = Stage(track, ctx.Content).ProgressRequired;
        double room = required - track.StageProgress;
        double amount = Math.Min(Math.Min(capacity, room), player.Buffs.Sum(b => b.Points));
        if (amount <= 0)
            return;

        double left = amount;
        foreach (var buff in player.Buffs.ToList())
        {
            double spent = Math.Min(buff.Points, left);
            buff.Points = StateNumbers.Round(buff.Points - spent);
            left -= spent;
            if (buff.Points <= 0)
                Remove(player, buff, events);
            if (left <= 0)
                break;
        }

        track.StageProgress = amount >= room ? required : StateNumbers.Round(track.StageProgress + amount);
        if (IsScaleFull(track, ctx.Content))
        {
            track.ScaleFullSinceAgeWeeks = player.AgeWeeks;
            events.Add(new StageScaleFull(track.VectorId));
        }
    }

    private static bool IsScaleFull(CultivationTrack track, ContentDb db) =>
        track.StageProgress >= Stage(track, db).ProgressRequired;

    private static CultivationTrack Track(PlayerState player, string vectorDefId) =>
        player.Tracks.First(t => VectorDefId(t.VectorId) == vectorDefId);

    private static CultivationBuff GetOrAdd(PlayerState player, BuffKind kind)
    {
        if (player.Buffs.Find(b => b.Kind == kind) is { } existing)
            return existing;

        var buff = new CultivationBuff { Kind = kind };
        player.Buffs.Add(buff);
        player.Buffs.Sort((a, b) => a.Kind.CompareTo(b.Kind));
        return buff;
    }

    private static void Remove(PlayerState player, CultivationBuff buff, List<SimEvent> events)
    {
        player.Buffs.Remove(buff);
        events.Add(new BuffExhausted(buff.Kind));
    }
}

/// <summary>The two parts of the foundation (§4.1); breakthroughs compare <see cref="Total"/> with a stage's threshold.</summary>
public sealed record Foundation(double Knowledge, double Purity)
{
    public double Total => Knowledge + Purity;
}
