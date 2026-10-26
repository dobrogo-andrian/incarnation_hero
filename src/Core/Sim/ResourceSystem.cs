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

    /// <summary>Adds <paramref name="amount"/> to a resource, up to its current maximum.</summary>
    public static void Restore(PlayerState player, SimContext ctx, string resourceId, double amount)
    {
        var resource = player.Resources.First(r => r.ResourceId == resourceId);
        double max = ctx.Rules.Value(player, resourceId);
        resource.Current = StateNumbers.Round(Math.Min(max, resource.Current + amount));
    }
}
