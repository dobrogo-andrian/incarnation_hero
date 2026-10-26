using Core.Content;
using Core.Loc;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Spirit stones (§13): the starting purse, buying in shops, and burning stones to refill the qi
/// pool (D18) or a meditation place's reserve (D19). Stones never move the stage scale.
/// Each stone action spends only the stones it needs.
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
        return CommandOutcome.Accept([]);
    }

    /// <summary>Refills the qi pool with up to <paramref name="stones"/> stones; not in dangerous zones (§13.4).</summary>
    public static CommandOutcome RestoreQi(PlayerState player, SimContext ctx, int stones)
    {
        if (CannotSpend(player, stones) is { } reason)
            return CommandOutcome.Reject(reason);
        if (ctx.Content.Get<ZoneDef>(player.ZoneId).Danger > ctx.Economy.StoneRestoreMaxDanger)
            return CommandOutcome.Reject(RejectReasons.TooDangerousHere);

        string poolId = ctx.Economy.StoneRestores;
        double missing = ctx.Rules.Value(player, poolId) - player.Resources.First(r => r.ResourceId == poolId).Current;
        if (missing <= 0)
            return CommandOutcome.Reject(RejectReasons.QiPoolFull);

        int used = StonesFor(missing, ctx.Economy.QiPerStone, stones);
        player.Stones -= used;
        ResourceSystem.Restore(player, ctx, poolId, used * ctx.Economy.QiPerStone);
        return CommandOutcome.Accept([]);
    }

    /// <summary>Refills a meditation place of the current zone with up to <paramref name="stones"/> stones, never past its capacity.</summary>
    public static CommandOutcome RefillPlace(GameState state, SimContext ctx, string placeId, int stones)
    {
        var player = state.Player;
        if (CannotSpend(player, stones) is { } reason)
            return CommandOutcome.Reject(reason);
        if (!WorldSystem.IsPlaceHere(state, ctx.Content, placeId))
            return CommandOutcome.Reject(RejectReasons.PlaceNotHere);

        var def = ctx.Content.Get<PlaceDef>(placeId);
        var place = state.World.Places.First(p => p.PlaceId == placeId);
        double room = def.Capacity - place.Reserve;
        if (room <= 0)
            return CommandOutcome.Reject(RejectReasons.PlaceFull);

        int used = StonesFor(room, ctx.Economy.PlaceReservePerStone, stones);
        player.Stones -= used;
        place.Reserve = StateNumbers.Round(Math.Min(def.Capacity, place.Reserve + used * ctx.Economy.PlaceReservePerStone));
        return CommandOutcome.Accept([]);
    }

    private static LocKey? CannotSpend(PlayerState player, int stones) =>
        stones <= 0 ? RejectReasons.InvalidAmount
        : player.Stones < stones ? RejectReasons.NotEnoughStones
        : null;

    /// <summary>Stones needed to cover <paramref name="amount"/> at <paramref name="perStone"/>, but no more than <paramref name="offered"/>.</summary>
    private static int StonesFor(double amount, double perStone, int offered) =>
        Math.Min(offered, (int)Math.Ceiling(StateNumbers.Round(amount / perStone)));
}
