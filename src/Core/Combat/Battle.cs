using Core.Content;
using Core.Rng;
using Core.Sim;

namespace Core.Combat;

/// <summary>A command for the fighter whose turn it is.</summary>
public abstract record BattleCommand;

/// <summary>End the turn without an action: the fighter waits. An action ends the turn by itself (D56).</summary>
public sealed record EndTurn : BattleCommand;

/// <summary>The basic weapon attack on an adjacent enemy, for body energy (D56).</summary>
public sealed record Attack(string TargetId) : BattleCommand;

/// <summary>An action: until the fighter's next turn, hits that get through the shield hurt the body less (D56).</summary>
public sealed record Guard : BattleCommand;

/// <summary>
/// An action on a rim hex with no enemy next to it: the fighter leaves the fight at the start of its
/// next turn, unless an enemy hits it before then (§7.6, D56, D65).
/// </summary>
public sealed record Flee : BattleCommand;

/// <summary>Gives the bout up at once (§5): only side 0, only in an arena bout (<see cref="BattleTerms.Bout"/>).</summary>
public sealed record Yield : BattleCommand;

/// <summary>A technique the fighter knows, aimed at a hex: the adjacent hex its line or arc starts from, or the enemy it strikes (D56).</summary>
public sealed record UseTechnique(string TechniqueId, Hex Target) : BattleCommand;

/// <summary>Walk to a hex within the movement points left, by the path <see cref="Battle.Reachable"/> gives.</summary>
public sealed record Move(Hex To) : BattleCommand;

/// <summary>A hex the active fighter can walk to this turn: the path there (without the start) and how many free attacks it provokes (D54).</summary>
public sealed record Reach(Hex To, IReadOnlyList<Hex> Path, int FreeAttacks);

public sealed record BattleTurnStarted(string FighterId, int MovePoints) : SimEvent;

/// <summary>One step of a walk.</summary>
public sealed record FighterMoved(string FighterId, Hex From, Hex To) : SimEvent;

/// <summary>The mover left a hex next to this enemy, which strikes for free (D54); a <see cref="FighterHit"/> follows.</summary>
public sealed record FreeAttackProvoked(string AttackerId, string MoverId) : SimEvent;

/// <summary>The turn is over: qi flowed into the shield and energy back into the fight's pool (D55).</summary>
public sealed record BattleTurnEnded(string FighterId, double ShieldRefilled, double EnergyRefilled) : SimEvent;

public sealed record FighterWaited(string FighterId) : SimEvent;

public sealed record FighterHit(string AttackerId, string TargetId, double Damage, double Absorbed, double ToHealth) : SimEvent;

/// <summary>The hit emptied the fighter's qi shield: until qi flows back, hits land on the body, harder (§7.4).</summary>
public sealed record ShieldBroken(string FighterId) : SimEvent;

public sealed record TechniqueUsed(string FighterId, string TechniqueId, Hex Target) : SimEvent;

public sealed record FighterGuarded(string FighterId) : SimEvent;

/// <summary>The fighter began to flee: it leaves at the start of its next turn unless an enemy hits it first.</summary>
public sealed record FleeBegun(string FighterId) : SimEvent;

/// <summary>An enemy hit the fleeing fighter: the escape is off, its energy spent.</summary>
public sealed record FleeBroken(string FighterId, string ByFighterId) : SimEvent;

public sealed record FighterFled(string FighterId) : SimEvent;

/// <summary>The fighter gave the bout up; it is out of the fight.</summary>
public sealed record FighterYielded(string FighterId) : SimEvent;

/// <summary>A blow that would have killed the fighter left it at the winners' mercy instead; it is out of the fight.</summary>
public sealed record FighterBeaten(string FighterId) : SimEvent;

/// <summary>What side 0 risks in a fight (§5, §7.7, D62).</summary>
public enum BattleTerms
{
    /// <summary>No health left is death: beasts, and anyone who does not spare.</summary>
    ToDeath,

    /// <summary>A blow that would kill side 0 leaves it beaten at 1 health instead: cultivators who rob rather than kill.</summary>
    Spared,

    /// <summary>An arena bout: spared as well, and side 0 may also yield (<see cref="Yield"/>).</summary>
    Bout,
}

/// <summary>The fight ran out of turns: side 0 is taken to have withdrawn (D59).</summary>
public sealed record BattleTimedOut(int Turns) : SimEvent;

/// <summary>The fighter has no health left and is out of the fight.</summary>
public sealed record FighterDown(string FighterId) : SimEvent;

/// <summary>A fighter on the hex field: the snapshot it came in with and what the fight has done to it since.</summary>
public sealed class BattleFighter(Combatant def, int side, Hex position, int initiativeRate)
{
    public Combatant Def { get; } = def;
    public string Id => Def.Id;

    /// <summary>0 is the side that started the fight (the player's in the game), 1 the other.</summary>
    public int Side { get; } = side;

    public Hex Position { get; internal set; } = position;
    public double Health { get; internal set; } = def.Health;
    public double Shield { get; internal set; } = def.Shield;
    public double QiPool { get; internal set; } = def.QiPool;

    /// <summary>
    /// Body energy set aside for the fight (D55), full at the start: every step, strike and technique
    /// spends it, and each own turn's end refills it, the way qi flows into the shield.
    /// </summary>
    public double Energy { get; internal set; } = def.Energy ?? def.MaxEnergy;

    /// <summary>The CTB gauge (D53): it fills by <see cref="InitiativeRate"/> a tick, and the fighter acts at the threshold.</summary>
    public int Initiative { get; internal set; }

    public int InitiativeRate { get; } = initiativeRate;

    /// <summary>Injury points the fight has left, per scale in ID order.</summary>
    public IReadOnlyDictionary<string, double> Injuries => _injuries;

    private readonly SortedDictionary<string, double> _injuries = new(StringComparer.Ordinal);

    /// <summary>Guarding since its last turn: hits through the shield land at <c>guard_body_share</c>.</summary>
    public bool Guarding { get; internal set; }

    /// <summary>Steps left this turn; they all come before the action, which ends the turn (D56).</summary>
    public int MovePoints { get; internal set; }

    /// <summary>Enemies that already struck this fighter for free this turn: each does so once (D54).</summary>
    internal HashSet<string> FreeAttackers { get; } = new(StringComparer.Ordinal);

    /// <summary>Fleeing since its last turn: it leaves the fight when that turn comes, unless broken (§7.6).</summary>
    public bool Fleeing { get; internal set; }

    public bool Fled { get; internal set; }

    /// <summary>Gave the bout up (§5): out of the fight, alive.</summary>
    public bool Yielded { get; internal set; }

    /// <summary>Struck down but spared (<see cref="BattleTerms.Spared"/>): out of the fight at 1 health.</summary>
    public bool Beaten { get; internal set; }

    public bool Down => Health <= 0;
    public bool InFight => !Down && !Fled && !Yielded && !Beaten;

    internal void Wound(string injuryId, double amount) => _injuries[injuryId] = _injuries.GetValueOrDefault(injuryId) + amount;
}

/// <summary>
/// A fight on the hex field (tech 4.1, D53–D56), built from fighter snapshots of two sides. Every
/// command is for <see cref="Active"/>; the battle is deterministic from its RNG and the commands given.
/// </summary>
public sealed class Battle
{
    private readonly CombatConfigDef _config;
    private readonly List<BattleFighter> _fighters;

    /// <summary>What a spared fighter is left with when the blow that would have killed it is stopped.</summary>
    private const double SparedHealth = 1;

    private Battle(HexField field, List<BattleFighter> fighters, CombatConfigDef config, Xoshiro256StarStar rng, BattleTerms terms)
    {
        Field = field;
        _fighters = fighters;
        _config = config;
        Rng = rng;
        Terms = terms;
        Opening = NextTurn();
    }

    public HexField Field { get; }

    /// <summary>Whether side 0 is spared a deadly blow and may yield.</summary>
    public BattleTerms Terms { get; }
    public IReadOnlyList<BattleFighter> Fighters => _fighters;

    /// <summary>The fight's own stream (a subseed of <c>combat</c>), for damage rolls.</summary>
    internal Xoshiro256StarStar Rng { get; }

    internal CombatConfigDef Config => _config;

    /// <summary>Turns begun so far; at <c>max_turns</c> the fight ends as side 0's withdrawal.</summary>
    public int Turns { get; private set; }

    /// <summary>What happened as the fight began: the first turn.</summary>
    public IReadOnlyList<SimEvent> Opening { get; }

    /// <summary>Whose turn it is; null once the fight is over.</summary>
    public BattleFighter? Active { get; private set; }

    public bool IsOver => Active is null;

    /// <summary>The only side still fighting, once the other has no one left.</summary>
    public int? Winner => IsOver ? _fighters.Where(f => f.InFight).Select(f => (int?)f.Side).FirstOrDefault() : null;

    /// <summary>Side 0 deploys one step inside the near rim, side 1 inside the far one, each spreading out from its anchor.</summary>
    public static Battle Start(IReadOnlyList<Combatant> side0, IReadOnlyList<Combatant> side1, CombatConfigDef config, Xoshiro256StarStar rng, BattleTerms terms = BattleTerms.ToDeath)
    {
        var field = new HexField(config.FieldRadius);
        var fighters = new List<BattleFighter>();
        Deploy(side0, 0, new Hex(0, field.Radius - 1));
        Deploy(side1, 1, new Hex(0, 1 - field.Radius));
        return new Battle(field, fighters, config, rng, terms);

        void Deploy(IReadOnlyList<Combatant> side, int sideIndex, Hex anchor)
        {
            using var free = FreeHexesFrom(anchor).GetEnumerator();
            foreach (var c in side)
            {
                if (!free.MoveNext())
                    throw new InvalidOperationException($"No room on a field of radius {field.Radius} for {c.Id}.");
                fighters.Add(new BattleFighter(c, sideIndex, free.Current, Initiative.Rate(c.Reaction, config)));
            }
        }

        // Breadth-first from the anchor in neighbour order, skipping hexes already taken.
        IEnumerable<Hex> FreeHexesFrom(Hex anchor)
        {
            var seen = new HashSet<Hex> { anchor };
            var queue = new Queue<Hex>([anchor]);
            while (queue.Count > 0)
            {
                var hex = queue.Dequeue();
                if (fighters.All(f => f.Position != hex))
                    yield return hex;
                foreach (var next in hex.Neighbors().Where(n => field.Contains(n) && seen.Add(n)))
                    queue.Enqueue(next);
            }
        }
    }

    /// <summary>
    /// Why the active fighter cannot do <paramref name="command"/> now, or null when it can: the very
    /// check <see cref="Execute"/> makes, so the UI greys out exactly what Core would refuse.
    /// </summary>
    public Loc.LocKey? WhyNot(BattleCommand command) => Check(command).Refusal;

    /// <summary>
    /// Why the active fighter cannot use <paramref name="techniqueId"/> (null: the basic attack) this
    /// turn whatever it aims at — too little energy or qi — or null when it can be
    /// picked. The same check opens <see cref="Execute"/> for an attack or a technique.
    /// </summary>
    public Loc.LocKey? SkillUnavailable(string? techniqueId) =>
        Active is not { } actor ? RejectReasons.BattleOver : SkillRefusal(actor, techniqueId);

    private Loc.LocKey? SkillRefusal(BattleFighter actor, string? techniqueId)
    {
        if (techniqueId is null)
            return ActionRefusal(actor, _config.AttackEnergy);
        if (actor.Def.Techniques.FirstOrDefault(t => t.Id == techniqueId) is not { } technique)
            return RejectReasons.UnknownTechnique;
        return ActionRefusal(actor, technique.Combat.Energy) ?? (actor.Shield < technique.Combat.Qi ? RejectReasons.NotEnoughQi : null);
    }

    /// <summary>What a command needs once it is allowed: the target, the technique and its plan, or the walk.</summary>
    private sealed record Checked(Loc.LocKey? Refusal, BattleFighter? Target = null, CombatTechnique? Technique = null, TechniquePlan? Plan = null, Reach? Reach = null);

    /// <summary>
    /// Validates a command for the active fighter without changing anything. The fighter's own limits
    /// come before the target's, so a skill out of energy or qi is refused the same way for every target.
    /// </summary>
    private Checked Check(BattleCommand command)
    {
        if (Active is not { } actor)
            return new(RejectReasons.BattleOver);
        switch (command)
        {
            case EndTurn:
                return new(null);

            case Attack attack:
                if (SkillRefusal(actor, null) is { } attackRefused)
                    return new(attackRefused);
                if (_fighters.FirstOrDefault(f => f.Id == attack.TargetId) is not { InFight: true } target || target.Side == actor.Side)
                    return new(RejectReasons.InvalidTarget);
                if (actor.Position.DistanceTo(target.Position) != 1)
                    return new(RejectReasons.OutOfReach);
                return new(null, Target: target);

            case UseTechnique use:
                if (SkillRefusal(actor, use.TechniqueId) is { } techniqueRefused)
                    return new(techniqueRefused);
                var technique = actor.Def.Techniques.First(t => t.Id == use.TechniqueId);
                if (Plan(actor, actor.Position, technique.Combat, use.Target) is not { } plan)
                    return new(RejectReasons.InvalidTarget);
                if (plan.Landing is null && technique.Combat.Shape == TechniqueCombatDef.Retreat)
                    return new(RejectReasons.NoRoom);
                return new(null, Technique: technique, Plan: plan);

            case Guard:
                return new(ActionRefusal(actor, _config.GuardEnergy));

            case Flee:
                if (!Field.IsEdge(actor.Position))
                    return new(RejectReasons.NotOnEdge);
                if (_fighters.Any(f => f.InFight && f.Side != actor.Side && f.Position.DistanceTo(actor.Position) == 1))
                    return new(RejectReasons.EnemyAdjacent);
                return new(ActionRefusal(actor, _config.FleeEnergy));

            case Yield:
                return new(Terms == BattleTerms.Bout && actor.Side == 0 ? null : RejectReasons.CannotYield);

            case Move move:
                return Reachable().FirstOrDefault(r => r.To == move.To) is { } reach ? new(null, Reach: reach) : new(RejectReasons.Unreachable);

            default:
                throw new ArgumentException($"Unknown battle command {command.GetType().Name}.", nameof(command));
        }
    }

    public CommandOutcome Execute(BattleCommand command)
    {
        var check = Check(command);
        if (check.Refusal is { } refusal)
            return CommandOutcome.Reject(refusal);
        var actor = Active!;
        var events = new List<SimEvent>();
        switch (command)
        {
            case EndTurn:
                events.Add(new FighterWaited(actor.Id));
                EndOwnTurn(actor, events);
                break;

            case Attack:
                actor.Energy -= _config.AttackEnergy;
                Strike(actor, check.Target!, actor.Def.WeaponDamage, events);
                EndOwnTurn(actor, events);
                break;

            case UseTechnique use:
                actor.Shield -= check.Technique!.Combat.Qi;
                actor.Energy -= check.Technique.Combat.Energy;
                events.Add(new TechniqueUsed(actor.Id, check.Technique.Id, use.Target));
                Perform(actor, check.Technique, check.Plan!, events);
                EndOwnTurn(actor, events);
                break;

            case Guard:
                actor.Energy -= _config.GuardEnergy;
                actor.Guarding = true;
                events.Add(new FighterGuarded(actor.Id));
                EndOwnTurn(actor, events);
                break;

            case Flee:
                actor.Energy -= _config.FleeEnergy;
                actor.Fleeing = true;
                events.Add(new FleeBegun(actor.Id));
                EndOwnTurn(actor, events);
                break;

            case Yield:
                actor.Yielded = true;
                events.Add(new FighterYielded(actor.Id));
                EndOwnTurn(actor, events);
                break;

            case Move:
                Walk(actor, check.Reach!.Path, events);
                break;
        }
        return CommandOutcome.Accept(events);
    }

    /// <summary>
    /// Every hex the active fighter can walk to with the steps left, in hex order. Paths
    /// do not pass through other fighters; of the paths that fit, the one that provokes the fewest
    /// free attacks wins, then the shorter one. The UI highlights these and the AI picks from them.
    /// </summary>
    public IReadOnlyList<Reach> Reachable()
    {
        if (Active is not { } mover)
            return [];
        var best = new SortedDictionary<Hex, (List<Hex> Path, HashSet<string> Provoked)>();
        var layer = new SortedDictionary<Hex, (List<Hex> Path, HashSet<string> Provoked)> { [mover.Position] = ([], []) };
        for (int step = 1; step <= StepsLeft(mover); step++)
        {
            var next = new SortedDictionary<Hex, (List<Hex> Path, HashSet<string> Provoked)>();
            foreach (var (hex, (path, provoked)) in layer)
            {
                var leaving = new HashSet<string>(provoked, StringComparer.Ordinal);
                leaving.UnionWith(Provokers(mover, hex).Select(f => f.Id));
                foreach (var n in hex.Neighbors().Where(n => Field.Contains(n) && _fighters.All(f => !f.InFight || f.Position != n)))
                {
                    if (next.TryGetValue(n, out var known) && known.Provoked.Count <= leaving.Count)
                        continue;
                    next[n] = ([.. path, n], leaving);
                }
            }
            foreach (var (hex, entry) in next)
            {
                if (hex != mover.Position && (!best.TryGetValue(hex, out var known) || entry.Provoked.Count < known.Provoked.Count))
                    best[hex] = entry;
            }
            layer = next;
        }
        return best.Select(b => new Reach(b.Key, b.Value.Path, b.Value.Provoked.Count)).ToList();
    }

    /// <summary>Steps the fighter can still take this turn: the movement points left, as far as its energy pays for them.</summary>
    internal int StepsLeft(BattleFighter f) =>
        _config.StepEnergy <= 0 ? f.MovePoints : Math.Min(f.MovePoints, (int)Math.Floor(Math.Round(f.Energy / _config.StepEnergy, 6)));

    /// <summary>
    /// Enemies next to <paramref name="hex"/> that would strike <paramref name="mover"/> for leaving it,
    /// in fighter order: those that strike at a mover at all (not beasts, D63) and have the energy for
    /// a basic attack, which the free attack costs them (D54).
    /// </summary>
    private IEnumerable<BattleFighter> Provokers(BattleFighter mover, Hex hex) =>
        _fighters.Where(f => f.InFight && f.Side != mover.Side && f.Def.Ai.FreeAttacks && f.Position.DistanceTo(hex) == 1
            && !mover.FreeAttackers.Contains(f.Id) && f.Energy >= _config.AttackEnergy);

    /// <summary>
    /// Step by step, each for its energy: leaving a hex next to an enemy draws its free attack first
    /// (D54); a mover brought down stops there.
    /// </summary>
    private void Walk(BattleFighter mover, IReadOnlyList<Hex> path, List<SimEvent> events)
    {
        foreach (var to in path)
        {
            foreach (var enemy in Provokers(mover, mover.Position).ToList())
            {
                mover.FreeAttackers.Add(enemy.Id);
                enemy.Energy -= _config.AttackEnergy;
                events.Add(new FreeAttackProvoked(enemy.Id, mover.Id));
                Strike(enemy, mover, enemy.Def.WeaponDamage, events);
                if (!mover.InFight)
                    break;
            }
            if (!mover.InFight)
            {
                if (OneSideLeft())
                    Active = null;
                else
                    events.AddRange(NextTurn());
                return;
            }
            events.Add(new FighterMoved(mover.Id, mover.Position, to));
            mover.Position = to;
            mover.MovePoints--;
            mover.Energy -= _config.StepEnergy;
        }
    }

    /// <summary>
    /// Where a technique moves the fighter and what it strikes: steps before the strike, the hexes it
    /// strikes (on the field), the enemies on them — none for a strike into the ground — and where a retreat lands.
    /// </summary>
    public sealed record TechniquePlan(IReadOnlyList<Hex> Approach, IReadOnlyList<Hex> Area, IReadOnlyList<BattleFighter> Targets, Hex? Landing);

    /// <summary>
    /// The plan for a technique used from <paramref name="from"/> (where the actor stands, or where the
    /// AI considers walking) aimed at <paramref name="target"/>, a hex on the field; null when the
    /// shape does not fit. It need not strike anyone: a technique may hit the ground.
    /// </summary>
    internal TechniquePlan? Plan(BattleFighter actor, Hex from, TechniqueCombatDef combat, Hex target)
    {
        if (!Field.Contains(target) || from.StraightTo(target) is not var (direction, steps))
            return null;
        BattleFighter? FighterOn(Hex hex) => _fighters.FirstOrDefault(f => f.InFight && f != actor && f.Position == hex);
        List<Hex> struck;
        List<Hex> approach = [];
        Hex? landing = null;
        switch (combat.Shape)
        {
            case TechniqueCombatDef.Line when steps == 1:
                struck = Enumerable.Range(1, combat.Range).Select(k => from + Hex.Directions[direction] * k).ToList();
                break;
            case TechniqueCombatDef.Arc when steps == 1:
                struck = new[] { -1, 0, 1 }.Select(turn => from + Hex.Directions[(direction + turn + 6) % 6]).ToList();
                break;
            case TechniqueCombatDef.Charge when steps <= combat.Range + 1:
                approach = Enumerable.Range(1, steps - 1).Select(k => from + Hex.Directions[direction] * k).ToList();
                if (approach.Any(h => FighterOn(h) is not null))
                    return null;
                struck = [target];
                break;
            case TechniqueCombatDef.Retreat when steps == 1:
                struck = [target];
                landing = Enumerable.Range(1, combat.Range).Reverse()
                    .Select(k => from + Hex.Directions[(direction + 3) % 6] * k)
                    .Where(h => Field.Contains(h) && FighterOn(h) is null)
                    .Select(h => (Hex?)h)
                    .FirstOrDefault();
                break;
            default:
                return null;
        }
        var area = struck.Where(Field.Contains).ToList();
        var targets = area.Select(FighterOn).OfType<BattleFighter>().Where(f => f.Side != actor.Side).ToList();
        return new TechniquePlan(approach, area, targets, landing);
    }

    /// <summary>
    /// What the active fighter's <paramref name="techniqueId"/> aimed at <paramref name="target"/> would
    /// do, from where it stands: the same plan the technique is then performed by. Null when it does not fit.
    /// </summary>
    public TechniquePlan? Preview(string techniqueId, Hex target) =>
        Active?.Def.Techniques.FirstOrDefault(t => t.Id == techniqueId) is { } technique
            ? Plan(Active, Active.Position, technique.Combat, target)
            : null;

    /// <summary>Moves (no free attacks: the technique carries the fighter, D54), strikes each target, then lands.</summary>
    private void Perform(BattleFighter actor, CombatTechnique technique, TechniquePlan plan, List<SimEvent> events)
    {
        foreach (var to in plan.Approach)
        {
            events.Add(new FighterMoved(actor.Id, actor.Position, to));
            actor.Position = to;
        }
        double damage = TechniqueDamage(actor, technique);
        foreach (var target in plan.Targets)
            Strike(actor, target, damage, events);
        if (plan.Landing is { } landing)
        {
            events.Add(new FighterMoved(actor.Id, actor.Position, landing));
            actor.Position = landing;
        }
    }

    /// <summary>A technique's damage before the spread: weapon × (1 + level × bonus) × its multiplier (D58).</summary>
    internal double TechniqueDamage(BattleFighter actor, CombatTechnique technique) =>
        actor.Def.WeaponDamage * (1 + technique.Level * _config.TechniqueBonusPerLevel) * technique.Combat.Damage;

    /// <summary>Why the turn's action, costing <paramref name="energy"/>, cannot be taken; null when it can.</summary>
    private static Loc.LocKey? ActionRefusal(BattleFighter actor, double energy) =>
        actor.Energy < energy ? RejectReasons.NotEnoughEnergy : null;

    /// <summary>The action or the wait closes the turn: the refill, then the next fighter, unless the fight is won.</summary>
    private void EndOwnTurn(BattleFighter actor, List<SimEvent> events)
    {
        if (OneSideLeft())
        {
            Active = null;
            return;
        }
        FinishTurn(actor, events);
        events.AddRange(NextTurn());
    }

    /// <summary>
    /// One hit through the shared pipeline (§7.4); unless the fight is to the death, a deadly blow on
    /// side 0 is stopped and it is beaten. Any hit, even one the shield takes whole, breaks an escape (§7.6).
    /// </summary>
    private void Strike(BattleFighter attacker, BattleFighter target, double damage, List<SimEvent> events)
    {
        double bodyShare = _config.BodyDamageMultiplier * (target.Guarding ? _config.GuardBodyShare : 1);
        var hit = CombatMath.Hit(damage, attacker.Def, target.Def, target.Shield, _config, Rng, bodyShare);
        bool hadShield = target.Shield > 0;
        target.Shield -= hit.Absorbed;
        target.Health = Math.Max(0, target.Health - hit.ToHealth);
        bool spared = target.Down && Terms != BattleTerms.ToDeath && target.Side == 0;
        if (spared)
            (target.Health, target.Beaten) = (SparedHealth, true);
        foreach (var (injuryId, amount) in hit.Injuries)
            target.Wound(injuryId, amount);
        events.Add(new FighterHit(attacker.Id, target.Id, hit.Damage, hit.Absorbed, hit.ToHealth));
        if (target.Fleeing && !target.Down)
        {
            target.Fleeing = false;
            events.Add(new FleeBroken(target.Id, attacker.Id));
        }
        if (hadShield && target.Shield <= 0)
            events.Add(new ShieldBroken(target.Id));
        if (spared)
            events.Add(new FighterBeaten(target.Id));
        else if (target.Down)
            events.Add(new FighterDown(target.Id));
    }

    private bool OneSideLeft() => _fighters.Where(f => f.InFight).Select(f => f.Side).Distinct().Count() < 2;

    /// <summary>Who acts after the current turn, in order: the same gauge arithmetic as the real turns.</summary>
    public IReadOnlyList<BattleFighter> Forecast(int turns)
    {
        var inFight = _fighters.Where(f => f.InFight).ToList();
        var order = new List<BattleFighter>(turns);
        if (IsOver)
            return order;
        var gauges = inFight.Select(f => f.Initiative).ToArray();
        for (int i = 0; i < turns; i++)
            order.Add(inFight[Initiative.Advance(inFight, gauges, _config.InitiativeThreshold)]);
        return order;
    }

    private IReadOnlyList<SimEvent> NextTurn()
    {
        var inFight = _fighters.Where(f => f.InFight).ToList();
        if (OneSideLeft())
        {
            Active = null;
            return [];
        }
        if (Turns >= _config.MaxTurns)
        {
            foreach (var f in inFight.Where(f => f.Side == 0))
                f.Fled = true;
            Active = null;
            return [new BattleTimedOut(Turns)];
        }
        Turns++;
        var gauges = inFight.Select(f => f.Initiative).ToArray();
        int next = Initiative.Advance(inFight, gauges, _config.InitiativeThreshold);
        for (int i = 0; i < inFight.Count; i++)
            inFight[i].Initiative = gauges[i];
        Active = inFight[next];
        if (Active.Fleeing)
        {
            // The escape held for a whole turn: the fighter is gone, and the next one acts.
            Active.Fleeing = false;
            Active.Fled = true;
            return [new FighterFled(Active.Id), .. NextTurn()];
        }
        Active.Guarding = false;
        Active.MovePoints = MovePointsOf(Active.Def);
        Active.FreeAttackers.Clear();
        return [new BattleTurnStarted(Active.Id, Active.MovePoints)];
    }

    /// <summary>Movement points a turn: agility × <c>move_points_per_agility</c>, rounded down, at least 1.</summary>
    internal int MovePointsOf(Combatant c) => Math.Max(1, (int)Math.Floor(c.Agility * _config.MovePointsPerAgility));

    /// <summary>End of an own turn: qi flows from the pool into the shield, and energy comes back from the body (D55).</summary>
    private void FinishTurn(BattleFighter f, List<SimEvent> events)
    {
        double shield = CombatMath.ShieldRefill(f.Def, f.Shield, f.QiPool);
        f.Shield += shield;
        f.QiPool -= shield;

        double energy = CombatMath.EnergyRefill(f.Def, f.Health, f.Energy, _config.EnergyRefillMin);
        f.Energy += energy;
        events.Add(new BattleTurnEnded(f.Id, shield, energy));
    }
}

/// <summary>The CTB gauge (D53), shared by real turns and the forecast so the queue the UI shows is the queue that happens.</summary>
internal static class Initiative
{
    public static int Rate(double reaction, CombatConfigDef config) =>
        Math.Max(1, (int)Math.Round(reaction * config.InitiativePerReaction, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Fills every gauge by whole ticks until one reaches the threshold, spends the threshold of
    /// the fighter that acts and returns its index. Ties: fuller gauge, then reaction, then ID.
    /// </summary>
    public static int Advance(IReadOnlyList<BattleFighter> fighters, int[] gauges, int threshold)
    {
        int ticks = int.MaxValue;
        for (int i = 0; i < gauges.Length; i++)
        {
            int missing = threshold - gauges[i];
            int rate = fighters[i].InitiativeRate;
            ticks = Math.Min(ticks, missing <= 0 ? 0 : (missing + rate - 1) / rate);
        }

        int best = -1;
        for (int i = 0; i < gauges.Length; i++)
        {
            gauges[i] += ticks * fighters[i].InitiativeRate;
            if (gauges[i] >= threshold && (best < 0 || ActsBefore(i, best)))
                best = i;
        }
        gauges[best] -= threshold;
        return best;

        bool ActsBefore(int a, int b) =>
            gauges[a] != gauges[b] ? gauges[a] > gauges[b]
            : fighters[a].Def.Reaction != fighters[b].Def.Reaction ? fighters[a].Def.Reaction > fighters[b].Def.Reaction
            : string.CompareOrdinal(fighters[a].Id, fighters[b].Id) < 0;
    }
}
