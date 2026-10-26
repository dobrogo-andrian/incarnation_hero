using Core.Content;
using Core.Rng;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Fights started by events. Until the formula resolver (stage 1, tech 4.1) a stand-in decides
/// them: the chance to win is the player's power over both sides' power (D38). Each fight draws
/// its own subseed from the combat stream.
/// </summary>
public static class CombatSystem
{
    /// <summary>Fights <paramref name="groupId"/>; a lost fight leaves the group's injuries. True when won.</summary>
    public static bool Fight(PlayerState player, SimContext ctx, string groupId, List<SimEvent> events)
    {
        var group = ctx.Content.Get<EnemyGroupDef>(groupId);
        double power = Power(player, ctx);
        var rng = Xoshiro256StarStar.FromSeed(ctx.Rng.NextSubseed(RngStream.Combat));
        bool won = rng.Chance(power / (power + group.Power));
        events.Add(new CombatResolved(groupId, won));
        if (!won)
        {
            foreach (var (injuryId, amount) in group.LossInjuries.OrderBy(i => i.Key, StringComparer.Ordinal))
                InjurySystem.Inflict(player, ctx, injuryId, amount, events);
        }
        return won;
    }

    /// <summary>The stand-in's power: the configured stats' values plus a bonus per qi stage.</summary>
    public static double Power(PlayerState player, SimContext ctx) =>
        ctx.Combat.StubPowerStats.Sum(id => ctx.Rules.Value(player, id))
        + player.Tracks.First(t => t.VectorId == Simulation.QiVectorId).StageIndex * ctx.Combat.StubPowerPerStage;
}
