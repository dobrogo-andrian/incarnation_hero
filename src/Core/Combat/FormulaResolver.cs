using Core.Content;
using Core.Rng;

namespace Core.Combat;

/// <summary>A fighter as a fight sees it: numbers taken from the character when the fight starts (tech 4.1).</summary>
public sealed record Combatant(
    string Id,
    int Stage,
    double Health,
    double MaxHealth,
    double Shield,
    double MaxShield,
    double QiPool,
    double ShieldRefill,
    double Damage,
    double Reaction,
    double Agility,
    double Resilience,
    double Energy,
    double EnergyRefill,
    double EnergyCapacity,
    string InjuryId,
    double WeaponDamage,
    IReadOnlyList<CombatTechnique> Techniques)
{
    /// <summary>How dangerous it is: what it takes to bring down times what it deals. Difficulty compares these.</summary>
    public double Threat => (MaxHealth + MaxShield) * Damage;
}

/// <summary>A technique a fighter knows, at its level, with what it does on the hex field.</summary>
public sealed record CombatTechnique(string Id, int Level, TechniqueCombatDef Combat);

public enum CombatEnd
{
    Won,
    Fled,
    Died,

    /// <summary>An arena bout given up (§5): the player yields where they would flee, and a deadly blow is stopped.</summary>
    Yielded,
}

/// <summary>What the fight left of the player: the numbers to write back, and injury points per scale in ID order.</summary>
public sealed record CombatResult(CombatEnd End, int Rounds, double Health, double Shield, double QiPool, IReadOnlyList<(string InjuryId, double Amount)> Injuries);

/// <summary>
/// The stage 1 formula resolver (D42) behind the combat interface: abstract rounds without a grid.
/// Turn order by reaction; the player hits the weakest enemy, enemies hit the player; hits break
/// the qi shield first, then health, and health damage leaves injuries; shields refill from the
/// qi pool at the end of each round. Below a share of health the player tries to escape, or in a
/// bout that <c>yields</c> gives up at once, and a blow that would kill ends the bout instead. The hex
/// combat of stage 2 replaces this class and keeps its inputs and result.
/// </summary>
public static class FormulaResolver
{
    public const string PlayerId = "player";

    /// <summary>What a yielding player is left with when the blow that would have killed is stopped.</summary>
    private const double YieldedHealth = 1;

    private sealed class Fighter(Combatant c)
    {
        public Combatant Def { get; } = c;
        public double Health { get; set; } = c.Health;
        public double Shield { get; set; } = c.Shield;
        public double QiPool { get; set; } = c.QiPool;
        public bool Down => Health <= 0;
    }

    public static CombatResult Resolve(Combatant player, IReadOnlyList<Combatant> foes, CombatConfigDef config, Xoshiro256StarStar rng, bool yields = false)
    {
        var me = new Fighter(player);
        var enemies = foes.Select(f => new Fighter(f)).ToList();
        var order = enemies.Prepend(me)
            .OrderByDescending(f => f.Def.Reaction)
            .ThenBy(f => f.Def.Id, StringComparer.Ordinal)
            .ToList();
        var injuries = new SortedDictionary<string, double>(StringComparer.Ordinal);
        if (enemies.All(e => e.Down))
            return Result(CombatEnd.Won, 0);

        for (int round = 1; round <= config.MaxRounds; round++)
        {
            foreach (var actor in order)
            {
                if (actor.Down)
                    continue;
                if (actor != me)
                {
                    Hit(actor, me, config, rng, injuries);
                    if (me.Down && yields)
                    {
                        me.Health = YieldedHealth;
                        return Result(CombatEnd.Yielded, round);
                    }
                    if (me.Down)
                        return Result(CombatEnd.Died, round);
                    continue;
                }

                // Escaping is the turn's action: a failed attempt costs the attack.
                if (me.Health <= config.FleeHealthShare * me.Def.MaxHealth)
                {
                    if (yields)
                        return Result(CombatEnd.Yielded, round);
                    if (rng.Chance(FleeChance(me, enemies, config)))
                        return Result(CombatEnd.Fled, round);
                    continue;
                }
                Hit(me, enemies.Where(e => !e.Down).MinBy(e => e.Health)!, config, rng, null);
                if (enemies.All(e => e.Down))
                    return Result(CombatEnd.Won, round);
            }

            foreach (var fighter in order.Where(f => !f.Down))
            {
                double refill = CombatMath.ShieldRefill(fighter.Def, fighter.Shield, fighter.QiPool);
                fighter.Shield += refill;
                fighter.QiPool -= refill;
            }
        }
        return Result(CombatEnd.Fled, config.MaxRounds);

        CombatResult Result(CombatEnd end, int rounds) =>
            new(end, rounds, Math.Max(0, me.Health), me.Shield, me.QiPool, injuries.Select(i => (i.Key, i.Value)).ToList());
    }

    /// <summary>The player's chance to get away this turn (§7.6): agility against the fastest enemy still standing.</summary>
    private static double FleeChance(Fighter me, List<Fighter> enemies, CombatConfigDef config)
    {
        double fastest = enemies.Where(e => !e.Down).Max(e => e.Def.Reaction);
        return Math.Clamp(config.FleeChanceBase + (me.Def.Agility - fastest) * config.FleeChancePerPoint, config.FleeChanceMin, config.FleeChanceMax);
    }

    /// <summary>One hit through the shared pipeline; injuries are counted only where <paramref name="injuries"/> is given (the player).</summary>
    private static void Hit(Fighter attacker, Fighter target, CombatConfigDef config, Xoshiro256StarStar rng, SortedDictionary<string, double>? injuries)
    {
        var hit = CombatMath.Hit(attacker.Def.Damage, attacker.Def, target.Def, target.Shield, config, rng);
        target.Shield -= hit.Absorbed;
        target.Health -= hit.ToHealth;
        foreach (var (injuryId, amount) in hit.Injuries)
            Add(injuries, injuryId, amount);
    }

    private static void Add(SortedDictionary<string, double>? injuries, string injuryId, double amount)
    {
        if (injuries is not null)
            injuries[injuryId] = injuries.GetValueOrDefault(injuryId) + amount;
    }
}
