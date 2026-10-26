using Core.Content;
using Core.Loc;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Comprehension (§6): studying a carried folio, or sensing an element without one, turns
/// meditation gain into knowledge levels up to the source's ceiling.
/// </summary>
public static class KnowledgeSystem
{
    /// <summary>What a study subject teaches, how far and how fast.</summary>
    private sealed record Source(string KnowledgeId, int MaxLevel, double Speed);

    public static int Level(PlayerState player, string knowledgeId) =>
        player.Knowledge.Find(k => k.KnowledgeId == knowledgeId)?.Level ?? 0;

    /// <summary>Why <paramref name="subjectId"/> cannot be studied now, or null if it can.</summary>
    public static LocKey? CannotStudy(PlayerState player, SimContext ctx, string? subjectId)
    {
        if (subjectId is null || SourceOf(player, ctx, subjectId) is not { } source)
            return RejectReasons.NothingToStudy;
        return Level(player, source.KnowledgeId) >= source.MaxLevel ? RejectReasons.StudyCapReached : null;
    }

    /// <summary>
    /// Adds <paramref name="gain"/> × the source's speed; levels up while the points cover the next
    /// level. At the ceiling nothing is gained, and the interrupt fires only on reaching it.
    /// </summary>
    public static void Study(PlayerState player, SimContext ctx, string subjectId, double gain, List<SimEvent> events)
    {
        var source = SourceOf(player, ctx, subjectId)!;
        if (Level(player, source.KnowledgeId) >= source.MaxLevel)
            return;

        var knowledge = GetOrAdd(player, source.KnowledgeId);
        var points = ctx.Config.KnowledgeLevelPoints;

        double progress = knowledge.Progress + gain * source.Speed;
        while (knowledge.Level < source.MaxLevel && progress >= points[knowledge.Level])
        {
            progress -= points[knowledge.Level];
            knowledge.Level++;
            events.Add(new KnowledgeLevelUp(source.KnowledgeId, knowledge.Level));
        }

        if (knowledge.Level >= source.MaxLevel)
        {
            knowledge.Progress = 0;
            events.Add(new StudyCapReached(source.KnowledgeId));
        }
        else
        {
            knowledge.Progress = StateNumbers.Round(progress);
        }
    }

    /// <summary>
    /// A carried folio teaches its subject up to its own ceiling; an element can be sensed without a
    /// folio up to the basic ceiling. Nothing goes beyond the level table.
    /// </summary>
    private static Source? SourceOf(PlayerState player, SimContext ctx, string subjectId)
    {
        int tableMax = ctx.Config.KnowledgeLevelPoints.Count;
        if (ctx.Content.TryGet<ItemDef>(subjectId, out var item))
        {
            return item.Folio is { } folio && ItemSystem.Count(player.Items, subjectId) > 0
                ? new Source(folio.Subject, Math.Min(folio.MaxLevel, tableMax), folio.Speed)
                : null;
        }
        return ctx.Content.TryGet<KnowledgeDef>(subjectId, out var knowledge) && knowledge.IsElement
            ? new Source(subjectId, Math.Min(ctx.Config.ElementBasicMaxLevel, tableMax), 1)
            : null;
    }

    private static KnowledgeLevel GetOrAdd(PlayerState player, string knowledgeId)
    {
        if (player.Knowledge.Find(k => k.KnowledgeId == knowledgeId) is { } existing)
            return existing;

        var knowledge = new KnowledgeLevel { KnowledgeId = knowledgeId };
        player.Knowledge.Add(knowledge);
        player.Knowledge.Sort((a, b) => string.CompareOrdinal(a.KnowledgeId, b.KnowledgeId));
        return knowledge;
    }
}
