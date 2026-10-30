using Core.Content;
using Core.Rng;

namespace Core.Combat;

/// <summary>
/// Auto-resolve (tech 4.1): the hex battle itself with the AI playing both sides, so a fight of an
/// event, a hunt or the arena runs on the very rules, techniques and AI of a fight on screen.
/// </summary>
public static class AutoBattle
{
    /// <summary>
    /// Fights <paramref name="player"/> against <paramref name="foes"/> to the end, on the <paramref name="terms"/>
    /// that decide whether a deadly blow kills, leaves the player beaten, or ends an arena bout.
    /// </summary>
    public static CombatResult Resolve(Combatant player, IReadOnlyList<Combatant> foes, CombatConfigDef config,
        Xoshiro256StarStar combat, Xoshiro256StarStar ai, BattleTerms terms = BattleTerms.ToDeath)
    {
        var session = BattleSession.Start([player], foes, config, combat, ai, terms);
        session.PlayOut();
        return session.Result();
    }
}
