namespace Core.Combat;

/// <summary>Combat rules shared by the formula resolver (D42) and the hex battle, so the two cannot drift apart.</summary>
public static class CombatMath
{
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
