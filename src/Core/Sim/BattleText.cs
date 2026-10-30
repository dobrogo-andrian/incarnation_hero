using Core.Combat;
using Core.Loc;

namespace Core.Sim;

/// <summary>
/// Localization keys for the hex battle: fighters' names and the battle log. Fighters are named in
/// the nominative only and lines avoid agreement ("Герой → Сірий вовк 2: шкода 7"), as A9 asks for numbers.
/// </summary>
public static class BattleText
{
    private static readonly LocKey Hero = new("str.battle.hero");
    private static readonly LocKey Numbered = new("str.battle.numbered");
    private static readonly LocKey Turn = new("str.battle.turn");
    private static readonly LocKey Hit = new("str.battle.hit");
    private static readonly LocKey FreeAttack = new("str.battle.free_attack");
    private static readonly LocKey Technique = new("str.battle.technique");
    private static readonly LocKey Guarded = new("str.battle.guarded");
    private static readonly LocKey Waited = new("str.battle.waited");
    private static readonly LocKey FleeBegun = new("str.battle.flee_begun");
    private static readonly LocKey FleeBroken = new("str.battle.flee_broken");
    private static readonly LocKey Fled = new("str.battle.fled");
    private static readonly LocKey Yielded = new("str.battle.yielded");
    private static readonly LocKey Beaten = new("str.battle.beaten");
    private static readonly LocKey Down = new("str.battle.down");
    private static readonly LocKey ShieldGone = new("str.battle.shield_broken");
    private static readonly LocKey TimedOut = new("str.battle.timed_out");

    /// <summary>The player's fighter is "the hero"; an enemy carries its number only when the battle has more than one of its kind.</summary>
    public static LocKey Name(Battle battle, string fighterId)
    {
        if (fighterId == Combatant.PlayerId)
            return Hero;
        int mark = fighterId.LastIndexOf('#');
        if (mark < 0)
            return SimText.Name(fighterId);
        string defId = fighterId[..mark];
        var name = SimText.Name(defId);
        bool alone = battle.Fighters.Count(f => f.Id.StartsWith(defId + "#", StringComparison.Ordinal)) == 1;
        return alone ? name : Numbered with { Args = [new("name", name), new("number", fighterId[(mark + 1)..])] };
    }

    /// <summary>The log line of a battle event; null for those the field itself shows (steps, refills).</summary>
    public static LocKey? Describe(Battle battle, SimEvent e)
    {
        LocArg Who(string id, string arg = "who") => new(arg, Name(battle, id));
        return e switch
        {
            BattleTurnStarted t => Turn with { Args = [Who(t.FighterId)] },
            FighterHit h => Hit with
            {
                Args = [Who(h.AttackerId, "attacker"), Who(h.TargetId, "target"), new("damage", Round(h.Damage)),
                    new("absorbed", Round(h.Absorbed)), new("health", Round(h.ToHealth))],
            },
            FreeAttackProvoked f => FreeAttack with { Args = [Who(f.AttackerId, "attacker"), Who(f.MoverId, "mover")] },
            TechniqueUsed u => Technique with { Args = [Who(u.FighterId), new("technique", SimText.Name(u.TechniqueId))] },
            FighterGuarded g => Guarded with { Args = [Who(g.FighterId)] },
            FighterWaited w => Waited with { Args = [Who(w.FighterId)] },
            FleeBegun f => FleeBegun with { Args = [Who(f.FighterId)] },
            FleeBroken f => FleeBroken with { Args = [Who(f.FighterId), Who(f.ByFighterId, "by")] },
            FighterFled f => Fled with { Args = [Who(f.FighterId)] },
            FighterYielded y => Yielded with { Args = [Who(y.FighterId)] },
            FighterBeaten b => Beaten with { Args = [Who(b.FighterId)] },
            FighterDown d => Down with { Args = [Who(d.FighterId)] },
            ShieldBroken s => ShieldGone with { Args = [Who(s.FighterId)] },
            BattleTimedOut t => TimedOut with { Args = [new("turns", t.Turns)] },
            _ => null,
        };
    }

    private static double Round(double value) => Math.Round(value, 1);
}
