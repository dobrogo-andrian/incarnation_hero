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
    /// Fights <paramref name="player"/> against <paramref name="foes"/> to the end. In a bout that
    /// <paramref name="yields"/> (the arena) the player gives up instead of fleeing or dying.
    /// </summary>
    public static CombatResult Resolve(Combatant player, IReadOnlyList<Combatant> foes, CombatConfigDef config,
        Xoshiro256StarStar combat, Xoshiro256StarStar ai, bool yields = false)
    {
        var battle = Battle.Start([player], foes, config, combat, yields);
        while (!battle.IsOver)
            BattleAi.PlayTurn(battle, ai);

        var me = battle.Fighters.First(f => f.Side == 0);
        var end = me.Down ? CombatEnd.Died
            : me.Yielded ? CombatEnd.Yielded
            : battle.Winner == 0 ? CombatEnd.Won
            : CombatEnd.Fled;
        return new CombatResult(end, battle.Turns, me.Health, me.Shield, me.QiPool, me.Injuries.Select(i => (i.Key, i.Value)).ToList());
    }
}
