using Core.Combat;
using Core.Content;
using Core.Rng;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Fights started by events, hunting and the arena (tech 4.1, D42, D67): offers the fight, fought by hand or
/// auto-resolved as the same hex battle on the fight's own subseeds from the combat and AI streams, and writes
/// the result back: health and qi, injuries, combat insight by difficulty, loot of a won fight, or death.
/// </summary>
public static class CombatSystem
{
    /// <summary>
    /// A fight of the run (D67), its <c>combat</c> and <c>ai</c> subseeds drawn now. An event's
    /// win and loss nodes, or an arena bout's prize, say what it goes on to.
    /// </summary>
    public static ActiveFight NewFight(SimContext ctx, string groupId, FightKind kind, string? winNode = null, string? lossNode = null, int prize = 0) => new()
    {
        GroupId = groupId,
        Kind = kind,
        CombatSeed = ctx.Rng.NextSubseed(RngStream.Combat),
        AiSeed = ctx.Rng.NextSubseed(RngStream.Ai),
        WinNode = winNode,
        LossNode = lossNode,
        Prize = prize,
    };

    /// <summary>
    /// Offers <paramref name="fight"/>: the run waits in <see cref="GameMode.Combat"/> for the player to
    /// fight by hand or auto-resolve, unless its kind is auto-resolved unasked (D67). Whether it was
    /// won once it is over; null while it waits.
    /// </summary>
    public static bool? Offer(GameState state, SimContext ctx, ActiveFight fight, List<SimEvent> events)
    {
        if (fight.Kind != FightKind.Event && state.AutoFights.Contains(fight.Kind))
            return PlayOut(state, ctx, fight, Session(state, ctx, fight), events);
        state.Fight = fight;
        state.Mode = GameMode.Combat;
        events.Add(new FightOffered(fight.GroupId, fight.Kind));
        return null;
    }

    /// <summary>Auto-resolves a fight of <paramref name="kind"/> against <paramref name="groupId"/> at once; true when won.</summary>
    public static bool Fight(GameState state, SimContext ctx, string groupId, List<SimEvent> events, FightKind kind = FightKind.Hunt)
    {
        var fight = NewFight(ctx, groupId, kind);
        return PlayOut(state, ctx, fight, Session(state, ctx, fight), events);
    }

    /// <summary>
    /// The battle of <paramref name="fight"/> replayed to where the player left it: the same subseeds and
    /// the player's commands give the same battle (tech 4.1). The player has not changed since it was offered.
    /// </summary>
    public static BattleSession Session(GameState state, SimContext ctx, ActiveFight fight)
    {
        var session = BattleSession.Start([Snapshot(state.Player, ctx)], Foes(ctx, fight.GroupId), ctx.Combat,
            Xoshiro256StarStar.FromSeed(fight.CombatSeed), Xoshiro256StarStar.FromSeed(fight.AiSeed), Terms(ctx, fight));
        foreach (var command in fight.Commands)
        {
            if (!session.Act(command).Accepted)
                throw new InvalidOperationException($"The saved fight command {command} does not replay.");
        }
        return session;
    }

    /// <summary>What the player risks (D62): an arena bout ends in giving up, a group that spares robs, the rest kill.</summary>
    public static BattleTerms Terms(SimContext ctx, ActiveFight fight) =>
        fight.Kind == FightKind.Arena ? BattleTerms.Bout
        : ctx.Content.Get<EnemyGroupDef>(fight.GroupId).KillChance is null ? BattleTerms.ToDeath
        : BattleTerms.Spared;

    /// <summary>Takes the offered fight by hand: the opening, and the enemy turns before the player's first.</summary>
    public static CommandOutcome ByHand(GameState state, SimContext ctx, BattleSession session)
    {
        var fight = state.Fight!;
        if (fight.ByHand)
            return CommandOutcome.Reject(RejectReasons.FightUnderway);
        fight.ByHand = true;
        var events = session.Opening.ToList();
        if (session.Battle.IsOver)
            Conclude(state, ctx, fight, session.Result(), events);
        return CommandOutcome.Accept(events);
    }

    /// <summary>The player's command in a fight by hand, kept for the replay; the fight's end writes its result back.</summary>
    public static CommandOutcome Act(GameState state, SimContext ctx, BattleSession session, BattleCommand command)
    {
        var fight = state.Fight!;
        if (!fight.ByHand)
            return CommandOutcome.Reject(RejectReasons.FightNotTaken);
        var outcome = session.Act(command);
        if (!outcome.Accepted)
            return outcome;
        fight.Commands.Add(command);
        var events = outcome.Events.ToList();
        if (session.Battle.IsOver)
            Conclude(state, ctx, fight, session.Result(), events);
        return CommandOutcome.Accept(events);
    }

    /// <summary>The AI fights on to the end: the whole offered fight, or the rest of one fought by hand.</summary>
    public static CommandOutcome AutoResolve(GameState state, SimContext ctx, BattleSession session)
    {
        var events = new List<SimEvent>();
        PlayOut(state, ctx, state.Fight!, session, events);
        return CommandOutcome.Accept(events);
    }

    /// <summary>The AI plays the battle out; a fight on screen shows its turns, an unseen one only its result.</summary>
    private static bool PlayOut(GameState state, SimContext ctx, ActiveFight fight, BattleSession session, List<SimEvent> events)
    {
        var turns = session.PlayOut();
        if (fight.ByHand)
            events.AddRange(turns);
        return Conclude(state, ctx, fight, session.Result(), events);
    }

    /// <summary>
    /// Writes the result back (D42, D62) and goes on: an event to its win or loss node, a hunt or
    /// the arena back to the map, with a won bout's prize. An escape is a loss; death ends the run.
    /// A group that spares robs a beaten player of everything, or finishes them off with its kill chance.
    /// </summary>
    private static bool Conclude(GameState state, SimContext ctx, ActiveFight fight, CombatResult result, List<SimEvent> events)
    {
        var player = state.Player;
        var config = ctx.Combat;
        var group = ctx.Content.Get<EnemyGroupDef>(fight.GroupId);
        var me = Snapshot(player, ctx);
        var foes = Foes(ctx, fight.GroupId);
        state.Fight = null;
        if (result.End == CombatEnd.Beaten && ctx.Rng[RngStream.Combat].Chance(group.KillChance!.Value))
            result = result with { End = CombatEnd.Died, Health = 0 };

        double qiBefore = me.Shield + me.QiPool;
        SetCurrent(player, config.HealthResource, result.Health);
        SetCurrent(player, config.ShieldResource, result.Shield);
        SetCurrent(player, config.QiPoolResource, result.QiPool);
        SetCurrent(player, config.EnergyResource, result.Energy);
        events.Add(new CombatResolved(fight.GroupId, result.End, result.Turns,
            StateNumbers.Round(me.Health - result.Health), StateNumbers.Round(qiBefore - result.Shield - result.QiPool)));
        foreach (var (injuryId, amount) in result.Injuries)
            InjurySystem.Inflict(player, ctx, injuryId, amount, events);

        if (result.End == CombatEnd.Died)
        {
            RunEndSystem.End(state, RunEndCause.CombatDeath, events);
            return false;
        }

        double insight = StateNumbers.Round(Math.Min(config.InsightMax, config.InsightPerDifficulty * foes.Sum(f => f.Threat) / me.Threat)
            * (result.End == CombatEnd.Won ? 1 : config.InsightFledShare));
        if (insight >= ctx.Config.CombatInsightMinPoints)
        {
            CultivationSystem.GrantCombatInsight(player, insight);
            events.Add(new BuffGained(BuffKind.CombatInsight, insight));
        }
        bool won = result.End == CombatEnd.Won;
        if (won)
            GiveLoot(player, ctx, group.Loot, events);
        if (result.End == CombatEnd.Beaten)
            LoseEverything(player, events);
        if (won && fight.Prize > 0)
        {
            player.Stones += fight.Prize;
            events.Add(new StonesChanged(fight.Prize));
        }

        if (fight.Kind == FightKind.Event)
        {
            state.Mode = GameMode.EventDialog;
            state.Event!.NodeId = won ? fight.WinNode! : fight.LossNode!;
        }
        else
        {
            state.Mode = GameMode.Overworld;
        }
        return won;
    }

    /// <summary>What the player sees of a group before choosing to fight: how many, and their stage against the player's (§2.1).</summary>
    public static CombatPreview Preview(PlayerState player, SimContext ctx, string groupId)
    {
        var members = ctx.Content.Get<EnemyGroupDef>(groupId).Members;
        return new CombatPreview(groupId, members.Sum(m => m.Count), StageAgainst(player, members.Max(m => ctx.Content.Get<EnemyDef>(m.Enemy).Stage)));
    }

    /// <summary>A qi stage against the player's, known without perception (§2.1).</summary>
    public static StageRelation StageAgainst(PlayerState player, int stage) => stage.CompareTo(QiStage(player)) switch
    {
        < 0 => StageRelation.Lower,
        0 => StageRelation.Equal,
        _ => StageRelation.Higher,
    };

    /// <summary>Whether the player has the health hunting and the arena ask for (§5).</summary>
    public static bool FitToFight(PlayerState player, SimContext ctx)
    {
        string health = ctx.Combat.HealthResource;
        return player.Resources.First(r => r.ResourceId == health).Current >= ctx.Combat.FightMinHealthShare * ctx.Rules.Value(player, health);
    }

    /// <summary>The player as a fighter, from current resources and computed values.</summary>
    public static Combatant Snapshot(PlayerState player, SimContext ctx)
    {
        int technique = player.Knowledge.Where(k => ctx.Content.Get<KnowledgeDef>(k.KnowledgeId).IsTechnique).Select(k => k.Level).DefaultIfEmpty(0).Max();
        var techniques = player.Knowledge.Where(k => k.Level > 0).Select(k => (k.KnowledgeId, k.Level));
        return Fighter(Combatant.PlayerId, player, ctx, QiStage(player), ctx.Combat.WeaponDamage, technique, techniques, ctx.Combat.PlayerAi, ctx.Combat.BodyInjury,
            id => player.Resources.First(r => r.ResourceId == id).Current);
    }

    /// <summary>The group's enemies as fighters, at full health and qi; instances are numbered within the group.</summary>
    public static IReadOnlyList<Combatant> Foes(SimContext ctx, string groupId) =>
        ctx.Content.Get<EnemyGroupDef>(groupId).Members
            .SelectMany(m => Enumerable.Range(1, m.Count).Select(i => FromDef(ctx, ctx.Content.Get<EnemyDef>(m.Enemy), $"{m.Enemy}#{i}")))
            .ToList();

    /// <summary>A character written in content (an enemy, or a sandbox profile of the player) as a fighter at full health and qi.</summary>
    public static Combatant FromDef(SimContext ctx, EnemyDef def, string id)
    {
        var body = new EnemyBody(ctx, def);
        return Fighter(id, body, ctx, def.Stage, def.WeaponDamage ?? ctx.Combat.WeaponDamage, 0,
            def.Techniques.Select(t => (t.Key, t.Value)), def.Ai, def.Injury ?? ctx.Combat.BodyInjury, valueId => ctx.Rules.Value(body, valueId));
    }

    private static Combatant Fighter(string id, ICharacter c, SimContext ctx, int stage, double weapon, int technique,
        IEnumerable<(string Id, int Level)> known, string aiId, string injuryId, Func<string, double> current)
    {
        var config = ctx.Combat;
        double Value(string valueId) => ctx.Rules.Value(c, valueId);
        double weaponDamage = weapon * (1 + stage * config.QiDensityPerStage);
        var techniques = known
            .Select(t => (t, Def: ctx.Content.Get<KnowledgeDef>(t.Id)))
            .Where(t => t.Def.Combat is not null)
            .OrderBy(t => t.t.Id, StringComparer.Ordinal)
            .Select(t => new CombatTechnique(t.t.Id, t.t.Level, t.Def.Combat!))
            .ToList();
        return new Combatant(
            id,
            Health: current(config.HealthResource),
            MaxHealth: Value(config.HealthResource),
            Shield: current(config.ShieldResource),
            MaxShield: Value(config.ShieldResource),
            QiPool: current(config.QiPoolResource),
            ShieldRefill: Value(config.FlowAspect) * config.RefillPerFlow,
            Damage: weaponDamage * (1 + technique * config.TechniqueBonusPerLevel),
            Reaction: Value(config.ReactionAspect),
            Agility: Value(config.AgilityAspect),
            Resilience: Value(config.ResilienceAspect),
            EnergyRefill: Value(config.EnergyOutputAspect) * config.EnergyRefillPerOutput,
            MaxEnergy: Value(config.EnergyResource),
            injuryId,
            weaponDamage,
            techniques,
            ctx.Content.Get<AiArchetypeDef>(aiId),
            Energy: current(config.EnergyResource));
    }

    private static int QiStage(PlayerState player) => player.Tracks.First(t => t.VectorId == Simulation.QiVectorId).StageIndex;

    private static void SetCurrent(PlayerState player, string resourceId, double value) =>
        player.Resources.First(r => r.ResourceId == resourceId).Current = StateNumbers.Round(value);

    private static void GiveLoot(PlayerState player, SimContext ctx, LootDef loot, List<SimEvent> events)
    {
        var rng = ctx.Rng[RngStream.Loot];
        int stones = loot.StonesMax > 0 ? rng.NextInt(loot.StonesMin, loot.StonesMax + 1) : 0;
        if (stones > 0)
        {
            player.Stones += stones;
            events.Add(new StonesChanged(stones));
        }
        foreach (var item in loot.Items)
        {
            if (!rng.Chance(item.Chance))
                continue;
            ItemSystem.Change(player.Items, item.Item, item.Count);
            events.Add(new ItemsChanged(item.Item, item.Count));
        }
    }

    /// <summary>The winners take every stone and item the beaten player carries (D62).</summary>
    private static void LoseEverything(PlayerState player, List<SimEvent> events)
    {
        if (player.Stones > 0)
        {
            events.Add(new StonesChanged(-player.Stones));
            player.Stones = 0;
        }
        foreach (var item in player.Items)
            events.Add(new ItemsChanged(item.ItemId, -item.Count));
        player.Items.Clear();
    }

    /// <summary>An enemy's body for the character rules: its stats (0 where not given), its profile, no modifiers.</summary>
    private sealed class EnemyBody(SimContext ctx, EnemyDef def) : ICharacter
    {
        public List<StatValue> Stats { get; } =
            ctx.Rules.Stats.Select(s => new StatValue { StatId = s.Id, Base = def.Stats.GetValueOrDefault(s.Id) }).ToList();

        public string AspectProfileId { get; } = def.AspectProfile ?? ctx.Rules.StartAspectProfile;

        public List<Modifier> Modifiers { get; } = [];
    }
}

public enum StageRelation
{
    Lower,
    Equal,
    Higher,
}

/// <summary>The basic knowledge of a fight ahead, available without perception (§2.1).</summary>
public sealed record CombatPreview(string GroupId, int Foes, StageRelation Stage);

/// <summary>The fight waiting for the player (D67): offered with the foes as the player reads them (D70), or fought by hand on the battle screen.</summary>
public sealed record FightView(string GroupId, FightKind Kind, bool ByHand, BattleTerms Terms, FightScouting Scouting);
