using Core.Content;
using Core.Rng;
using Core.Sim;

namespace Core.Combat;

/// <summary>A command for the fighter whose turn it is.</summary>
public abstract record BattleCommand;

/// <summary>End the turn; without an action taken, the fighter waits.</summary>
public sealed record EndTurn : BattleCommand;

/// <summary>The basic weapon attack on an adjacent enemy, for body energy (D56).</summary>
public sealed record Attack(string TargetId) : BattleCommand;

/// <summary>Walk to a hex within the movement points left, by the path <see cref="Battle.Reachable"/> gives.</summary>
public sealed record Move(Hex To) : BattleCommand;

/// <summary>A hex the active fighter can walk to this turn: the path there (without the start) and how many free attacks it provokes (D54).</summary>
public sealed record Reach(Hex To, IReadOnlyList<Hex> Path, int FreeAttacks);

public sealed record BattleTurnStarted(string FighterId, int MovePoints) : SimEvent;

/// <summary>One step of a walk.</summary>
public sealed record FighterMoved(string FighterId, Hex From, Hex To) : SimEvent;

/// <summary>The mover left a hex next to this enemy, which strikes for free (D54); a <see cref="FighterHit"/> follows.</summary>
public sealed record FreeAttackProvoked(string AttackerId, string MoverId) : SimEvent;

/// <summary>The turn is over: qi flowed into the shield and energy out of the reserve (D55).</summary>
public sealed record BattleTurnEnded(string FighterId, double ShieldRefilled, double EnergyRefilled) : SimEvent;

public sealed record FighterWaited(string FighterId) : SimEvent;

public sealed record FighterHit(string AttackerId, string TargetId, double Damage, double Absorbed, double ToHealth) : SimEvent;

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

    /// <summary>Body energy ready to spend; the fight starts with it full (D55).</summary>
    public double Energy { get; internal set; } = def.EnergyCapacity;

    /// <summary>The body's reserve energy flows from; it does not come back during the fight.</summary>
    public double EnergyReserve { get; internal set; } = def.Energy;

    /// <summary>The CTB gauge (D53): it fills by <see cref="InitiativeRate"/> a tick, and the fighter acts at the threshold.</summary>
    public int Initiative { get; internal set; }

    public int InitiativeRate { get; } = initiativeRate;

    /// <summary>Injury points the fight has left, per scale in ID order.</summary>
    public IReadOnlyDictionary<string, double> Injuries => _injuries;

    private readonly SortedDictionary<string, double> _injuries = new(StringComparer.Ordinal);

    /// <summary>The main action of the current turn is spent (D56).</summary>
    public bool ActionTaken { get; internal set; }

    /// <summary>Steps left this turn, before or after the action (D56).</summary>
    public int MovePoints { get; internal set; }

    /// <summary>Enemies that already struck this fighter for free this turn: each does so once (D54).</summary>
    internal HashSet<string> FreeAttackers { get; } = new(StringComparer.Ordinal);

    public bool Fled { get; internal set; }
    public bool Down => Health <= 0;
    public bool InFight => !Down && !Fled;

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

    private Battle(HexField field, List<BattleFighter> fighters, CombatConfigDef config, Xoshiro256StarStar rng)
    {
        Field = field;
        _fighters = fighters;
        _config = config;
        Rng = rng;
        Opening = NextTurn();
    }

    public HexField Field { get; }
    public IReadOnlyList<BattleFighter> Fighters => _fighters;

    /// <summary>The fight's own stream (a subseed of <c>combat</c>), for damage rolls.</summary>
    internal Xoshiro256StarStar Rng { get; }

    /// <summary>What happened as the fight began: the first turn.</summary>
    public IReadOnlyList<SimEvent> Opening { get; }

    /// <summary>Whose turn it is; null once the fight is over.</summary>
    public BattleFighter? Active { get; private set; }

    public bool IsOver => Active is null;

    /// <summary>The only side still fighting, once the other has no one left.</summary>
    public int? Winner => IsOver ? _fighters.Where(f => f.InFight).Select(f => (int?)f.Side).FirstOrDefault() : null;

    /// <summary>Side 0 deploys one step inside the near rim, side 1 inside the far one, each spreading out from its anchor.</summary>
    public static Battle Start(IReadOnlyList<Combatant> side0, IReadOnlyList<Combatant> side1, CombatConfigDef config, Xoshiro256StarStar rng)
    {
        var field = new HexField(config.FieldRadius);
        var fighters = new List<BattleFighter>();
        Deploy(side0, 0, new Hex(0, field.Radius - 1));
        Deploy(side1, 1, new Hex(0, 1 - field.Radius));
        return new Battle(field, fighters, config, rng);

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

    public CommandOutcome Execute(BattleCommand command)
    {
        if (Active is not { } actor)
            return CommandOutcome.Reject(RejectReasons.BattleOver);

        var events = new List<SimEvent>();
        switch (command)
        {
            case EndTurn:
                if (!actor.ActionTaken)
                    events.Add(new FighterWaited(actor.Id));
                FinishTurn(actor, events);
                events.AddRange(NextTurn());
                return CommandOutcome.Accept(events);

            case Attack attack:
                if (actor.ActionTaken)
                    return CommandOutcome.Reject(RejectReasons.ActionTaken);
                if (_fighters.FirstOrDefault(f => f.Id == attack.TargetId) is not { InFight: true } target || target.Side == actor.Side)
                    return CommandOutcome.Reject(RejectReasons.InvalidTarget);
                if (actor.Position.DistanceTo(target.Position) != 1)
                    return CommandOutcome.Reject(RejectReasons.OutOfReach);
                if (actor.Energy < _config.AttackEnergy)
                    return CommandOutcome.Reject(RejectReasons.NotEnoughEnergy);
                actor.Energy -= _config.AttackEnergy;
                actor.ActionTaken = true;
                Strike(actor, target, events);
                if (OneSideLeft())
                    Active = null;
                return CommandOutcome.Accept(events);

            case Move move:
                if (Reachable().FirstOrDefault(r => r.To == move.To) is not { } reach)
                    return CommandOutcome.Reject(RejectReasons.Unreachable);
                Walk(actor, reach.Path, events);
                return CommandOutcome.Accept(events);

            default:
                throw new ArgumentException($"Unknown battle command {command.GetType().Name}.", nameof(command));
        }
    }

    /// <summary>
    /// Every hex the active fighter can walk to with the movement points left, in hex order. Paths
    /// do not pass through other fighters; of the paths that fit, the one that provokes the fewest
    /// free attacks wins, then the shorter one. The UI highlights these and the AI picks from them.
    /// </summary>
    public IReadOnlyList<Reach> Reachable()
    {
        if (Active is not { } mover)
            return [];
        var best = new SortedDictionary<Hex, (List<Hex> Path, HashSet<string> Provoked)>();
        var layer = new SortedDictionary<Hex, (List<Hex> Path, HashSet<string> Provoked)> { [mover.Position] = ([], []) };
        for (int step = 1; step <= mover.MovePoints; step++)
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

    /// <summary>Enemies next to <paramref name="hex"/> that would strike <paramref name="mover"/> for leaving it, in fighter order.</summary>
    private IEnumerable<BattleFighter> Provokers(BattleFighter mover, Hex hex) =>
        _fighters.Where(f => f.InFight && f.Side != mover.Side && f.Position.DistanceTo(hex) == 1 && !mover.FreeAttackers.Contains(f.Id));

    /// <summary>Step by step: leaving a hex next to an enemy draws its free attack first (D54); a mover brought down stops there.</summary>
    private void Walk(BattleFighter mover, IReadOnlyList<Hex> path, List<SimEvent> events)
    {
        foreach (var to in path)
        {
            foreach (var enemy in Provokers(mover, mover.Position).ToList())
            {
                mover.FreeAttackers.Add(enemy.Id);
                events.Add(new FreeAttackProvoked(enemy.Id, mover.Id));
                Strike(enemy, mover, events);
                if (mover.Down)
                    break;
            }
            if (mover.Down)
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
        }
    }

    /// <summary>One hit through the pipeline shared with the formula resolver (§7.4).</summary>
    private void Strike(BattleFighter attacker, BattleFighter target, List<SimEvent> events)
    {
        var hit = CombatMath.Hit(attacker.Def, target.Def, target.Shield, _config, Rng);
        target.Shield -= hit.Absorbed;
        target.Health = Math.Max(0, target.Health - hit.ToHealth);
        foreach (var (injuryId, amount) in hit.Injuries)
            target.Wound(injuryId, amount);
        events.Add(new FighterHit(attacker.Id, target.Id, hit.Damage, hit.Absorbed, hit.ToHealth));
        if (target.Down)
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
        var gauges = inFight.Select(f => f.Initiative).ToArray();
        int next = Initiative.Advance(inFight, gauges, _config.InitiativeThreshold);
        for (int i = 0; i < inFight.Count; i++)
            inFight[i].Initiative = gauges[i];
        Active = inFight[next];
        Active.ActionTaken = false;
        Active.MovePoints = MovePointsOf(Active.Def);
        Active.FreeAttackers.Clear();
        return [new BattleTurnStarted(Active.Id, Active.MovePoints)];
    }

    /// <summary>Movement points a turn: agility × <c>move_points_per_agility</c>, rounded down, at least 1.</summary>
    private int MovePointsOf(Combatant c) => Math.Max(1, (int)Math.Floor(c.Agility * _config.MovePointsPerAgility));

    /// <summary>End of an own turn: qi flows from the pool into the shield, and energy from the reserve (D55).</summary>
    private void FinishTurn(BattleFighter f, List<SimEvent> events)
    {
        double shield = CombatMath.ShieldRefill(f.Def, f.Shield, f.QiPool);
        f.Shield += shield;
        f.QiPool -= shield;

        double energy = CombatMath.EnergyRefill(f.Def, f.Health, f.Energy, f.EnergyReserve, _config.EnergyRefillMin);
        f.Energy += energy;
        f.EnergyReserve = Math.Max(0, f.EnergyReserve - energy);
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
