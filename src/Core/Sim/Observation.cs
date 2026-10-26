using Core.State;

namespace Core.Sim;

/// <summary>What the player can tell of a hidden number: a range that contains it.</summary>
public sealed record ObservedRange(double Low, double High);

/// <summary>
/// Truth vs knowledge (tech 2.7): the player sees ranges, not true values, and Perception narrows
/// them. Where the truth sits inside the range is fixed by a stable hash of what is observed, so
/// the range neither gives the truth away as its middle nor changes when the view is rebuilt.
/// </summary>
public static class Observation
{
    /// <summary>
    /// A chance (0 to 1) as the player sees it: <c>chance_range_width</c> wide with no perception,
    /// each point narrowing it, bounds rounded outwards to <c>chance_range_step</c>.
    /// </summary>
    public static ObservedRange Chance(double truth, PlayerState player, SimContext ctx, ulong noiseKey)
    {
        var config = ctx.Events;
        double perception = ctx.Rules.Value(player, config.PerceptionStat);
        double width = Math.Max(0, config.ChanceRangeWidth - config.ChanceRangeNarrowPerPerception * perception);
        double below = width * ((noiseKey >> 11) * (1.0 / (1UL << 53)));
        double step = config.ChanceRangeStep;
        double low = Math.Floor(Math.Round((truth - below) / step, 6)) * step;
        double high = Math.Ceiling(Math.Round((truth + width - below) / step, 6)) * step;
        return new(StateNumbers.Round(Math.Clamp(low, 0, 1)), StateNumbers.Round(Math.Clamp(high, 0, 1)));
    }
}
