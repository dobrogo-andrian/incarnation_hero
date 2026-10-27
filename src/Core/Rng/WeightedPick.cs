namespace Core.Rng;

public static class WeightedPick
{
    /// <summary>One item with chance proportional to its weight; items keep their given order, so callers pass a fixed one.</summary>
    public static T PickWeighted<T>(this Xoshiro256StarStar rng, IReadOnlyList<(T Item, double Weight)> items)
    {
        double roll = rng.NextDouble() * items.Sum(i => i.Weight);
        foreach (var (item, weight) in items)
        {
            if (roll < weight)
                return item;
            roll -= weight;
        }
        return items.Last(i => i.Weight > 0).Item;
    }
}
