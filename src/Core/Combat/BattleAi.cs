using Core.Content;
using Core.Rng;
using Core.Sim;

namespace Core.Combat;

/// <summary>
/// Utility AI for the hex battle (tech 4.1, D59). It plays the active fighter's whole turn as one
/// plan: every candidate is a hex it can reach × an action × a target, scored by the weights of its
/// archetype; the best one, with a little noise from the <c>ai</c> stream, is played through the same
/// commands a player sends. The action ends the turn, so the hex to act from is the hex it ends on.
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
        // A fighter brought down on the way has no turn left; otherwise the action, or the wait, ends it.
        // Free attacks on the way may have cost the qi or the target the plan counted on: then it waits.
        if (battle.Active == me)
            Run(plan.Action is { } action && battle.WhyNot(action) is null ? action : new EndTurn());
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
        var froms = battle.Reachable().Select(r => (r.To, r.FreeAttacks, Steps: r.Path.Count)).Prepend((me.Position, 0, 0));
        foreach (var (from, freeAttacks, steps) in froms)
        {
            // The walk spends energy as well: what is left there pays for the action.
            double stepsEnergy = steps * config.StepEnergy;
            double energy = me.Energy - stepsEnergy;
            double walk = FreeAttacksCost(battle, me, ai, freeAttacks) + ai.Energy * stepsEnergy;
            // Where the turn ends and with what energy left: that decides whether it can strike next turn.
            double At(Hex end, double spent) => Position(battle, me, ai, end, energy - spent) - walk;
            yield return new Candidate(from, null, At(from, 0));

            if (fleeing)
            {
                if (battle.Field.IsEdge(from) && energy >= config.FleeEnergy && !Enemies(battle, me).Any(e => e.Position.DistanceTo(from) == 1))
                    yield return new Candidate(from, new Flee(), ai.Flee * 100 - walk);
                continue;
            }

            if (energy >= config.AttackEnergy)
            {
                foreach (var enemy in Enemies(battle, me).Where(e => e.Position.DistanceTo(from) == 1))
                    yield return new Candidate(from, new Attack(enemy.Id), At(from, config.AttackEnergy) + Strikes(ai, me.Def.WeaponDamage, [enemy]) - ai.Energy * config.AttackEnergy);
            }

            foreach (var technique in me.Def.Techniques.Where(t => me.Shield >= t.Combat.Qi && energy >= t.Combat.Energy))
            {
                foreach (var aim in Aims(from, technique.Combat.Range + 1))
                {
                    // The AI spends qi only on strikes that reach someone.
                    if (battle.Plan(me, from, technique.Combat, aim) is not { Targets.Count: > 0 } p
                        || (p.Landing is null && technique.Combat.Shape == TechniqueCombatDef.Retreat))
                        continue;
                    var end = p.Landing ?? (p.Approach.Count > 0 ? p.Approach[^1] : from);
                    double score = At(end, technique.Combat.Energy)
                        + Strikes(ai, battle.TechniqueDamage(me, technique), p.Targets) - ai.Qi * technique.Combat.Qi - ai.Energy * technique.Combat.Energy;
                    yield return new Candidate(from, new UseTechnique(technique.Id, aim), score);
                }
            }

            if (energy >= config.GuardEnergy)
            {
                double spared = Incoming(battle, me, from) * (1 - config.GuardBodyShare);
                yield return new Candidate(from, new Guard(), At(from, config.GuardEnergy) + ai.Risk * spared - ai.Energy * config.GuardEnergy);
            }
        }
    }

    /// <summary>Expected damage dealt, no more than each target has left, plus the bonus for each likely kill.</summary>
    private static double Strikes(AiArchetypeDef ai, double damage, IEnumerable<BattleFighter> targets) =>
        targets.Sum(t => ai.Damage * Math.Min(damage, t.Shield + t.Health) + (damage >= t.Shield + t.Health ? ai.Kill : 0));

    /// <summary>
    /// How good a hex is to end the turn on, with <paramref name="energyLeft"/>: the danger in reach of
    /// it against the chance to strike from it next turn — which takes the energy to walk there and
    /// strike, after the turn's refill — and the way to the enemy, or, when fleeing, to the rim.
    /// </summary>
    private static double Position(Battle battle, BattleFighter me, AiArchetypeDef ai, Hex hex, double energyLeft)
    {
        double risk = ai.Risk * Incoming(battle, me, hex);
        if (Fleeing(me, ai))
            return -risk - ai.Flee * (battle.Field.Radius - hex.Length);
        int nearest = Enemies(battle, me).Select(e => e.Position.DistanceTo(hex)).DefaultIfEmpty(0).Min();
        double energyNext = energyLeft + CombatMath.EnergyRefill(me.Def, me.Health, energyLeft, battle.Config.EnergyRefillMin);
        bool canStrike = nearest <= battle.MovePointsOf(me.Def) + 1 && energyNext >= StrikeEnergy(battle, nearest);
        return -risk + (canStrike ? ai.Opportunity * me.Def.WeaponDamage : 0) - ai.Approach * nearest;
    }

    /// <summary>
    /// Damage the enemies could deal on <paramref name="hex"/> next turn: those whose movement reaches
    /// its side and whose energy pays for the walk and the strike. A spent enemy is no threat.
    /// </summary>
    private static double Incoming(Battle battle, BattleFighter me, Hex hex) =>
        Enemies(battle, me)
            .Where(e => e.Position.DistanceTo(hex) is var d && d <= battle.MovePointsOf(e.Def) + 1 && e.Energy >= StrikeEnergy(battle, d))
            .Sum(e => e.Def.WeaponDamage);

    /// <summary>The energy to walk up to an enemy <paramref name="distance"/> hexes away and strike it.</summary>
    private static double StrikeEnergy(Battle battle, int distance) =>
        battle.Config.AttackEnergy + Math.Max(0, distance - 1) * battle.Config.StepEnergy;

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
