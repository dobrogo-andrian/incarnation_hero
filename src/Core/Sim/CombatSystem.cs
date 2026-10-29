using Core.Combat;
using Core.Content;
using Core.Rng;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Fights started by events, hunting and the arena (tech 4.1, D42): builds the fighters' snapshots, lets the resolver
/// decide on the fight's own subseed from the combat stream, and writes the result back: health
/// and qi, injuries, combat insight by difficulty, loot of a won fight, or death.
/// </summary>
public static class CombatSystem
{
    /// <summary>
    /// Fights <paramref name="groupId"/>; true when won. An escape is a loss; death ends the run.
    /// A bout that <paramref name="yields"/> (the arena) ends in giving up instead of escape or death.
    /// </summary>
    public static bool Fight(GameState state, SimContext ctx, string groupId, List<SimEvent> events, bool yields = false)
    {
        var player = state.Player;
        var config = ctx.Combat;
        var me = Snapshot(player, ctx);
        var foes = Foes(ctx, groupId);
        var result = FormulaResolver.Resolve(me, foes, config, Xoshiro256StarStar.FromSeed(ctx.Rng.NextSubseed(RngStream.Combat)), yields);

        double qiBefore = me.Shield + me.QiPool;
        SetCurrent(player, config.HealthResource, result.Health);
        SetCurrent(player, config.ShieldResource, result.Shield);
        SetCurrent(player, config.QiPoolResource, result.QiPool);
        events.Add(new CombatResolved(groupId, result.End, result.Rounds,
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
        if (result.End == CombatEnd.Won)
            GiveLoot(player, ctx, ctx.Content.Get<EnemyGroupDef>(groupId).Loot, events);
        return result.End == CombatEnd.Won;
    }

    /// <summary>What the player sees of a group before choosing to fight: how many, and their stage against the player's (§2.1).</summary>
    public static CombatPreview Preview(PlayerState player, SimContext ctx, string groupId)
    {
        var members = ctx.Content.Get<EnemyGroupDef>(groupId).Members;
        int stage = members.Max(m => ctx.Content.Get<EnemyDef>(m.Enemy).Stage);
        return new CombatPreview(groupId, members.Sum(m => m.Count), stage.CompareTo(QiStage(player)) switch
        {
            < 0 => StageRelation.Lower,
            0 => StageRelation.Equal,
            _ => StageRelation.Higher,
        });
    }

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
        return Fighter(FormulaResolver.PlayerId, player, ctx, QiStage(player), ctx.Combat.WeaponDamage, technique, techniques, ctx.Combat.PlayerAi, ctx.Combat.BodyInjury,
            id => player.Resources.First(r => r.ResourceId == id).Current);
    }

    /// <summary>The group's enemies as fighters, at full health and qi; instances are numbered within the group.</summary>
    public static IReadOnlyList<Combatant> Foes(SimContext ctx, string groupId)
    {
        var config = ctx.Combat;
        var foes = new List<Combatant>();
        foreach (var member in ctx.Content.Get<EnemyGroupDef>(groupId).Members)
        {
            var def = ctx.Content.Get<EnemyDef>(member.Enemy);
            var body = new EnemyBody(ctx, def);
            for (int i = 1; i <= member.Count; i++)
            {
                foes.Add(Fighter($"{def.Id}#{i}", body, ctx, def.Stage, def.WeaponDamage ?? config.WeaponDamage, def.TechniqueLevel,
                    def.Techniques.Select(t => (t.Key, t.Value)), def.Ai, def.Injury ?? config.BodyInjury, id => ctx.Rules.Value(body, id)));
            }
        }
        return foes;
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
            stage,
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
            Energy: current(config.EnergyResource),
            EnergyRefill: Value(config.EnergyOutputAspect) * config.EnergyRefillPerOutput,
            EnergyCapacity: Math.Max(config.EnergyRefillMin, Value(config.EnergyOutputAspect) * config.EnergyCapacityPerOutput),
            injuryId,
            weaponDamage,
            techniques,
            ctx.Content.Get<AiArchetypeDef>(aiId));
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
