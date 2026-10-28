using Core.Content;
using Core.State;

namespace Core.Character;

/// <summary>
/// Where a ranked stat stands within its rank (D48, D49), for the rules and the view alike:
/// <see cref="Filled"/> of <see cref="Pool"/> on the scale, <see cref="Overflow"/> above it towards
/// <see cref="Breakthrough"/> (null on the last rank) and <see cref="Deficit"/> below it towards
/// <see cref="Collapse"/> (null on the first rank or a stat without a deficit).
/// </summary>
public sealed record StatStanding(
    string RankId,
    int RankIndex,
    int RankCount,
    double RankBase,
    double Pool,
    double Filled,
    double Overflow,
    double? Breakthrough,
    double DecayPerWeek,
    double Deficit,
    double? Collapse,
    double? HealPerWeek)
{
    public double Top => RankBase + Pool;
}

/// <summary>The rank arithmetic of ranked stats (§2.1, D48): a rank's base is the sum of the pools below it.</summary>
public static class StatProgress
{
    public static StatStanding Of(StatValue value, StatProgressDef progress, ContentDb db)
    {
        var ranks = progress.Ranks.Select(db.Get<StatRankDef>).ToList();
        var rank = ranks[value.Rank];
        double rankBase = ranks.Take(value.Rank).Sum(r => r.Pool);
        return new(rank.Id, value.Rank, ranks.Count, rankBase, rank.Pool, StateNumbers.Round(value.Base - rankBase), value.Overflow,
            rank.Breakthrough, progress.OverflowDecayPerWeek, value.Deficit, rank.Collapse, progress.DeficitHealPerWeek);
    }

    /// <summary>The lowest rank whose scale reaches <paramref name="value"/>: a fresh character starts there.</summary>
    public static int RankFor(double value, StatProgressDef progress, ContentDb db)
    {
        double top = 0;
        for (int i = 0; i < progress.Ranks.Count; i++)
        {
            top += db.Get<StatRankDef>(progress.Ranks[i]).Pool;
            if (value <= top)
                return i;
        }
        return progress.Ranks.Count - 1;
    }
}
