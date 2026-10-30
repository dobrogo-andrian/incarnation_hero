using Core.Content;
using Core.Rng;
using Core.State;

namespace Core.Sim;

/// <summary>A foe's number against the hero's, in words (D70).</summary>
public enum Comparison
{
    FarBelow,
    Below,
    Even,
    Above,
    FarAbove,
}

/// <summary>
/// What the player makes of one kind of foe before a fight (D70): its numbers against the hero's,
/// null when a higher stage hides them (§2.1); its techniques, null below the perception they need.
/// </summary>
public sealed record FoeScouting(string EnemyId, int Count, StageRelation Stage, Comparison? Vitality, Comparison? Strike, Comparison? Speed,
    IReadOnlyList<string>? Techniques);

/// <summary>The foes of a fight as the player reads them, and how likely each reading is to be right.</summary>
public sealed record FightScouting(IReadOnlyList<FoeScouting> Foes, double Reliability);

/// <summary>
/// Reading the foes before a fight (D70, §2.1): health and shield, the weapon's strike and reaction, each
/// against the hero's as one of five words, and the techniques. Perception makes the words reliable: with
/// less of it a word may be a step off. Whether it is, is fixed by a stable hash of the fight, so the
/// reading neither changes when the view is rebuilt nor gives the truth away.
/// </summary>
public static class Scouting
{
    public static FightScouting Of(PlayerState player, SimContext ctx, ActiveFight fight)
    {
        var config = ctx.Combat.Scouting;
        double perception = ctx.Rules.Value(player, ctx.Events.PerceptionStat);
        double misread = Math.Clamp(config.MisreadChance - config.MisreadPerPerception * perception, 0, 1);
        var hero = CombatSystem.Snapshot(player, ctx);
        var foes = ctx.Content.Get<EnemyGroupDef>(fight.GroupId).Members.Select(member =>
        {
            var def = ctx.Content.Get<EnemyDef>(member.Enemy);
            var foe = CombatSystem.FromDef(ctx, def, def.Id);
            var stage = CombatSystem.StageAgainst(player, def.Stage);
            bool readable = stage != StageRelation.Higher;
            Comparison? Read(string aspect, double mine, double theirs) =>
                readable ? Misread(Compare(theirs, mine, config.Bounds), misread, StableHash.Of(fight.CombatSeed, def.Id, aspect)) : null;
            return new FoeScouting(def.Id, member.Count, stage,
                Read("vitality", hero.MaxHealth + hero.MaxShield, foe.MaxHealth + foe.MaxShield),
                Read("strike", hero.WeaponDamage, foe.WeaponDamage),
                Read("speed", hero.Reaction, foe.Reaction),
                readable && perception >= config.TechniquesMinPerception ? foe.Techniques.Select(t => t.Id).ToList() : null);
        }).ToList();
        return new FightScouting(foes, StateNumbers.Round(1 - misread));
    }

    /// <summary>The word for the foe's number over the hero's: the bounds split the ratios into five.</summary>
    private static Comparison Compare(double theirs, double mine, IReadOnlyList<double> bounds)
    {
        double ratio = mine > 0 ? theirs / mine : theirs > 0 ? double.PositiveInfinity : 1;
        return (Comparison)bounds.Count(b => ratio >= b);
    }

    /// <summary>The true word, or with chance <paramref name="misread"/> a step off it, inwards at the ends.</summary>
    private static Comparison Misread(Comparison truth, double misread, ulong key)
    {
        if ((key >> 11) * (1.0 / (1UL << 53)) >= misread)
            return truth;
        int step = (key & 1) == 0 ? -1 : 1;
        int read = (int)truth + step;
        return (Comparison)(read is < (int)Comparison.FarBelow or > (int)Comparison.FarAbove ? (int)truth - step : read);
    }
}
