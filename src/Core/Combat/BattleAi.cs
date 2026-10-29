using Core.Content;
using Core.Rng;
using Core.Sim;

namespace Core.Combat;

/// <summary>
/// Utility AI for the hex battle (tech 4.1, D59). It plays the active fighter's whole turn as one
/// plan: every candidate is a hex it can reach × an action × a target, scored by the weights of its
/// archetype; the best one, with a little noise from the <c>ai</c> stream, is played through the same
/// commands a player sends. The movement points left then take it to the best hex to end on.
/// </summary>
public static class BattleAi
{
    private sealed record Candidate(Hex From, BattleCommand? Action, double Score);

    /// <summary>Plays the turn of <see cref="Battle.Active"/> and returns what happened.</summary>
    public static IReadOnlyList<SimEvent> PlayTurn(Battle battle, Xoshiro256StarStar rng)
    {
        var me = battle.Active ?? throw new InvalidOperationException("The fight is over.");
        var ai = me.Def.Ai;
        var events = new List<SimEvent>();

        var plan = Candidates(battle, me, ai)
            .Select(c => c with { Score = c.Score + rng.NextDouble() * ai.Noise })
            .MaxBy(c => c.Score)!;
        if (plan.From != me.Position)
            Run(new Move(plan.From));
        if (plan.Action is { } action && battle.Active == me)
            Run(action);

        if (battle.Active == me && battle.Reachable() is { Count: > 0 } reach)
        {
            double stay = Position(battle, me, ai, me.Position);
            var step = reach
                .Select(r => (r.To, Score: Position(battle, me, ai, r.To) - FreeAttacksCost(battle, me, ai, r.FreeAttacks)))
                .MaxBy(r => r.Score);
            if (step.Score > stay)
                Run(new Move(step.To));
        }
        if (battle.Active == me)
            Run(new EndTurn());
        return events;

        void Run(BattleCommand command)
        {
            var outcome = battle.Execute(command);
            if (!outcome.Accepted)
                throw new InvalidOperationException($"The AI of {me.Id} sent a rejected {command}: {outcome.RejectReason}.");
            events.AddRange(outcome.Events);
        }
    }

    /// <summary>Every plan for the turn, in a fixed order: where to stand (staying first), then what to do there.</summary>
    private static IEnumerable<Candidate> Candidates(Battle battle, BattleFighter me, AiArchetypeDef ai)
    {
        var config = battle.Config;
        bool fleeing = Fleeing(me, ai);
        var froms = battle.Reachable().Select(r => (r.To, r.FreeAttacks)).Prepend((me.Position, 0));
        foreach (var (from, freeAttacks) in froms)
        {
            double walk = FreeAttacksCost(battle, me, ai, freeAttacks);
            double here = Position(battle, me, ai, from) - walk;
            yield return new Candidate(from, null, here);

            if (fleeing)
            {
                if (battle.Field.IsEdge(from) && me.Energy >= config.FleeEnergy && !Enemies(battle, me).Any(e => e.Position.DistanceTo(from) == 1))
                    yield return new Candidate(from, new Flee(), ai.Flee * 100 - walk);
                continue;
            }

            if (me.Energy >= config.AttackEnergy)
            {
                foreach (var enemy in Enemies(battle, me).Where(e => e.Position.DistanceTo(from) == 1))
                    yield return new Candidate(from, new Attack(enemy.Id), here + Strikes(ai, me.Def.WeaponDamage, [enemy]) - ai.Energy * config.AttackEnergy);
            }

            foreach (var technique in me.Def.Techniques.Where(t => me.Shield >= t.Combat.Qi))
            {
                foreach (var aim in Aims(from, technique.Combat.Range + 1))
                {
                    if (battle.Plan(me, from, technique.Combat, aim) is not { } p
                        || (p.Landing is null && technique.Combat.Shape == TechniqueCombatDef.Retreat))
                        continue;
                    var end = p.Landing ?? (p.Approach.Count > 0 ? p.Approach[^1] : from);
                    double score = Position(battle, me, ai, end) - walk
                        + Strikes(ai, battle.TechniqueDamage(me, technique), p.Targets) - ai.Qi * technique.Combat.Qi;
                    yield return new Candidate(from, new UseTechnique(technique.Id, aim), score);
                }
            }

            if (me.Energy >= config.GuardEnergy)
            {
                double spared = Incoming(battle, me, from) * (1 - config.GuardBodyShare);
                yield return new Candidate(from, new Guard(), here + ai.Risk * spared - ai.Energy * config.GuardEnergy);
            }
        }
    }

    /// <summary>Expected damage dealt, no more than each target has left, plus the bonus for each likely kill.</summary>
    private static double Strikes(AiArchetypeDef ai, double damage, IEnumerable<BattleFighter> targets) =>
        targets.Sum(t => ai.Damage * Math.Min(damage, t.Shield + t.Health) + (damage >= t.Shield + t.Health ? ai.Kill : 0));

    /// <summary>How good a hex is to end the turn on: the danger in reach of it and the way to the enemy, or, when fleeing, to the rim.</summary>
    private static double Position(Battle battle, BattleFighter me, AiArchetypeDef ai, Hex hex)
    {
        double risk = ai.Risk * Incoming(battle, me, hex);
        if (Fleeing(me, ai))
            return -risk - ai.Flee * (battle.Field.Radius - hex.Length);
        int nearest = Enemies(battle, me).Select(e => e.Position.DistanceTo(hex)).DefaultIfEmpty(0).Min();
        return -risk - ai.Approach * nearest;
    }

    /// <summary>Damage the enemies could deal on <paramref name="hex"/> next turn: those whose movement reaches its side.</summary>
    private static double Incoming(Battle battle, BattleFighter me, Hex hex) =>
        Enemies(battle, me).Where(e => e.Position.DistanceTo(hex) <= battle.MovePointsOf(e.Def) + 1).Sum(e => e.Def.WeaponDamage);

    /// <summary>Provoked free attacks, each priced at the enemies' average weapon.</summary>
    private static double FreeAttacksCost(Battle battle, BattleFighter me, AiArchetypeDef ai, int freeAttacks) =>
        freeAttacks == 0 ? 0 : ai.FreeAttack * freeAttacks * Enemies(battle, me).Average(e => e.Def.WeaponDamage);

    private static bool Fleeing(BattleFighter me, AiArchetypeDef ai) =>
        ai.FleeHealthShare > 0 && me.Health <= ai.FleeHealthShare * me.Def.MaxHealth;

    private static IEnumerable<BattleFighter> Enemies(Battle battle, BattleFighter me) =>
        battle.Fighters.Where(f => f.InFight && f.Side != me.Side);

    /// <summary>Hexes straight out from <paramref name="from"/> in all six directions, up to <paramref name="steps"/> away.</summary>
    private static IEnumerable<Hex> Aims(Hex from, int steps) =>
        Hex.Directions.SelectMany(d => Enumerable.Range(1, steps).Select(k => from + d * k));
}
