using Core.Content;
using Core.State;

namespace Core.Sim;

/// <summary>Changes to the player's current resource amounts, always kept within their maximums.</summary>
public static class ResourceSystem
{
    /// <summary>Natural weekly return of each resource (<see cref="ResourceDef.RegenPerWeek"/>).</summary>
    public static void AdvanceWeek(GameState state, SimContext ctx)
    {
        foreach (var resource in state.Player.Resources)
        {
            double regen = ctx.Content.Get<ResourceDef>(resource.ResourceId).RegenPerWeek;
            if (regen > 0)
                Restore(state.Player, ctx, resource.ResourceId, regen);
        }
    }

    /// <summary>
    /// True once <paramref name="resourceId"/> and every resource that returns on its own (health, body
    /// energy…) are at their maximums: recovery has nothing left to do (D72).
    /// </summary>
    public static bool Whole(PlayerState player, SimContext ctx, string resourceId) =>
        player.Resources
            .Where(r => r.ResourceId == resourceId || ctx.Content.Get<ResourceDef>(r.ResourceId).RegenPerWeek > 0)
            .All(r => r.Current >= ctx.Rules.Value(player, r.ResourceId));

    /// <summary>Adds <paramref name="amount"/> to a resource, up to its current maximum.</summary>
    public static void Restore(PlayerState player, SimContext ctx, string resourceId, double amount)
    {
        var resource = player.Resources.First(r => r.ResourceId == resourceId);
        double max = ctx.Rules.Value(player, resourceId);
        resource.Current = StateNumbers.Round(Math.Min(max, resource.Current + amount));
    }

    /// <summary>Brings every resource down to its maximum after the maximums fell: rest only heals up to the ceiling (§8).</summary>
    public static void ClampToMax(PlayerState player, SimContext ctx)
    {
        foreach (var resource in player.Resources)
            resource.Current = StateNumbers.Round(Math.Min(resource.Current, ctx.Rules.Value(player, resource.ResourceId)));
    }

    /// <summary>Takes up to <paramref name="amount"/> from a resource, never below zero; returns what was taken.</summary>
    public static double Drain(PlayerState player, string resourceId, double amount)
    {
        var resource = player.Resources.First(r => r.ResourceId == resourceId);
        double taken = Math.Min(resource.Current, amount);
        resource.Current = StateNumbers.Round(resource.Current - taken);
        return taken;
    }
}
