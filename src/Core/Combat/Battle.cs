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

public sealed record BattleTurnStarted(string FighterId) : SimEvent;

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

            default:
                throw new ArgumentException($"Unknown battle command {command.GetType().Name}.", nameof(command));
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
        return [new BattleTurnStarted(Active.Id)];
    }

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
