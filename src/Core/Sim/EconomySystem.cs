using Core.Content;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Spirit stones (§13): the starting purse and buying in shops. Stones are only a currency: they
/// are never burnt for qi or a place's reserve (D18), the qi pool is refilled by pills.
/// </summary>
public static class EconomySystem
{
    public static void InitPlayer(PlayerState player, EconomyConfigDef economy)
    {
        player.Stones = economy.StartStones;
        foreach (var (itemId, count) in economy.StartItems.OrderBy(i => i.Key, StringComparer.Ordinal))
            ItemSystem.Change(player.Items, itemId, count);
    }

    /// <summary>Shops of the player's zone with their offers, in shop ID order.</summary>
    public static IEnumerable<ShopDef> ShopsHere(PlayerState player, ContentDb db) =>
        db.All<ShopDef>().Where(s => s.Zone == player.ZoneId);

    /// <summary>Buys one <paramref name="itemId"/> at the cheapest shop of the current zone that sells it.</summary>
    public static CommandOutcome Buy(PlayerState player, SimContext ctx, string itemId)
    {
        var prices = ShopsHere(player, ctx.Content).Where(s => s.Prices.ContainsKey(itemId)).Select(s => s.Prices[itemId]).ToList();
        if (prices.Count == 0)
            return CommandOutcome.Reject(RejectReasons.NotSoldHere);
        int price = prices.Min();
        if (player.Stones < price)
            return CommandOutcome.Reject(RejectReasons.NotEnoughStones);

        player.Stones -= price;
        ItemSystem.Change(player.Items, itemId, 1);
        return CommandOutcome.Accept([new StonesChanged(-price), new ItemsChanged(itemId, 1)]);
    }
}
