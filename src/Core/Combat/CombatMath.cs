using Core.Content;
using Core.Rng;

namespace Core.Combat;

/// <summary>What one hit did: how much the shield took, how much reached health, and the injury points it left.</summary>
public sealed record HitResult(double Damage, double Absorbed, double ToHealth, IReadOnlyList<(string InjuryId, double Amount)> Injuries);

/// <summary>Combat rules shared by the formula resolver (D42) and the hex battle, so the two cannot drift apart.</summary>
public static class CombatMath
{
    /// <summary>
    /// One hit (§7.4, §8): <paramref name="damage"/> with its spread breaks the qi shield first; a hit
    /// that breaks it may hurt the meridians; the rest goes to health and leaves injuries of the
    /// attacker's kind over the target's resilience; <paramref name="bodyShare"/> of it lands when the
    /// target guards. The RNG is drawn in this order every time.
    /// </summary>
    public static HitResult Hit(double damage, Combatant attacker, Combatant target, double targetShield, CombatConfigDef config, Xoshiro256StarStar rng, double bodyShare = 1)
    {
        damage *= 1 + config.DamageSpread * (2 * rng.NextDouble() - 1);
        double absorbed = Math.Min(targetShield, damage);
        var injuries = new List<(string, double)>();
        if (targetShield > 0 && targetShield - absorbed <= 0 && rng.Chance(config.ShieldBreakChance))
            injuries.Add((config.ShieldBreakInjury, config.ShieldBreakAmount));

        double toHealth = Math.Max(0, damage - absorbed) * bodyShare;
        if (toHealth > 0)
            injuries.Add((attacker.InjuryId, toHealth / Math.Max(1, target.Resilience) * config.InjuryPerResilience));
        return new HitResult(damage, absorbed, toHealth, injuries);
    }

    /// <summary>What flows from the qi pool into the shield at the end of a turn: the fighter's flow, no more than the shield misses or the pool holds (§7.2).</summary>
    public static double ShieldRefill(Combatant fighter, double shield, double qiPool) =>
        Math.Max(0, Math.Min(fighter.ShieldRefill, Math.Min(fighter.MaxShield - shield, qiPool)));

    /// <summary>
    /// What flows from the energy reserve into energy ready to spend at the end of a turn (D55):
    /// output × the share of health left, no more than the reserve holds but never under the
    /// minimum, which flows even from an empty reserve; energy stops at its capacity.
    /// </summary>
    public static double EnergyRefill(Combatant fighter, double health, double energy, double reserve, double minimum)
    {
        double wanted = fighter.EnergyRefill * Math.Clamp(health / Math.Max(1, fighter.MaxHealth), 0, 1);
        double flow = Math.Max(minimum, Math.Min(wanted, reserve));
        return Math.Max(0, Math.Min(flow, fighter.EnergyCapacity - energy));
    }
}
