using Core.Content;

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
    double EnergyRefill,
    double MaxEnergy,
    string InjuryId,
    double WeaponDamage,
    IReadOnlyList<CombatTechnique> Techniques,
    AiArchetypeDef Ai)
{
    /// <summary>The player's fighter in the game's fights and the sandbox.</summary>
    public const string PlayerId = "player";

    /// <summary>
    /// How dangerous it is: what it takes to bring down times what it deals (<see cref="Damage"/>:
    /// the weapon with its best technique). Difficulty, and so combat insight, compares these.
    /// </summary>
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
public sealed record CombatResult(CombatEnd End, int Turns, double Health, double Shield, double QiPool, IReadOnlyList<(string InjuryId, double Amount)> Injuries);
