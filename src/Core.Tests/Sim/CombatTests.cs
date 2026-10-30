using Core.Combat;
using Core.Content;
using Core.Loc;
using Core.Rng;
using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

/// <summary>
/// The stage 1 formula resolver (D42). Resolver tests use their own config without damage spread,
/// so every number is exact; system tests use the repo config. A fresh player has 100 health, a
/// 50 qi shield, a 100 qi pool and 10 in every aspect.
/// </summary>
public sealed class CombatTests
{
    private static readonly ContentDb Content = TestContent.WithExtra("""
        - id: enemy.test.glass
          name: { uk: "Скляний" }
          ai: ai.beast_aggressive
          stats: { stat.might: 0.5 }
          weapon_damage: 300
        - id: enemy.test.wall
          name: { uk: "Непробивний" }
          ai: ai.beast_aggressive
          stats: { stat.might: 100000 }
          weapon_damage: 0.1
        - id: enemy.test.killer
          name: { uk: "Убивця" }
          ai: ai.beast_aggressive
          stats: { stat.might: 100000, stat.speed: 100 }
          weapon_damage: 100000
        - id: enemy.test.adept
          name: { uk: "Адепт" }
          ai: ai.beast_aggressive
          stage: 1
          stats: { stat.might: 6 }
        - id: grp.test.weak
          name: { uk: "Слабкі" }
          members: [ { enemy: enemy.test.glass } ]
          loot: { stones_min: 5, stones_max: 5, items: [ { item: item.pill.qi_gathering, chance: 1, count: 2 } ] }
        - id: grp.test.wall
          name: { uk: "Стіна" }
          members: [ { enemy: enemy.test.wall } ]
          loot: { stones_min: 5, stones_max: 5 }
        - id: grp.test.killer
          name: { uk: "Убивці" }
          members: [ { enemy: enemy.test.killer } ]
        - id: grp.test.adepts
          name: { uk: "Адепти" }
          members: [ { enemy: enemy.test.adept, count: 2 } ]
        - id: evt.test.ambush
          contexts: [rest]
          weight: 1
          nodes:
            start:
              text: { uk: "Засідка" }
              options:
                - text: { uk: "Битися" }
                  effects: [ { start_combat: { group: grp.test.killer, win: won, loss: lost } } ]
                - text: { uk: "Битися з адептами" }
                  effects: [ { start_combat: { group: grp.test.adepts, win: won, loss: lost } } ]
            won: { text: { uk: "Перемога" } }
            lost: { text: { uk: "Поразка" } }
        """);

    private static CombatConfigDef Exact(Action<CombatConfigDef>? change = null)
    {
        var config = new CombatConfigDef
        {
            DamageSpread = 0,
            InjuryPerResilience = 1,
            ShieldBreakInjury = "injury.internal",
            ShieldBreakChance = 0,
            ShieldBreakAmount = 3,
            FleeHealthShare = 0.3,
            FleeChanceBase = 0.5,
            FleeChanceMin = 0,
            FleeChanceMax = 1,
            MaxRounds = 30,
        };
        change?.Invoke(config);
        return config;
    }

    private static Combatant Fighter(string id, double health, double damage = 10, double reaction = 10, double shield = 0,
        double pool = 0, double refill = 0, double maxShield = -1) =>
        new(id, 0, health, MaxHealth: 100, shield, maxShield < 0 ? shield : maxShield, pool, refill, damage, reaction,
            Agility: 10, Resilience: 10, EnergyRefill: 0, MaxEnergy: 0, "injury.body", WeaponDamage: damage, Techniques: [], TestContent.Repo.Get<AiArchetypeDef>("ai.cultivator_cautious"));

    private static CombatResult Resolve(Combatant player, CombatConfigDef config, params Combatant[] foes) =>
        FormulaResolver.Resolve(player, foes, config, Xoshiro256StarStar.FromSeed(7));

    private static SimContext Context(Simulation sim) => new(sim.Content, RngStreams.FromStates(sim.State.RngStates));

    private static double Current(PlayerState player, string id) => player.Resources.First(r => r.ResourceId == id).Current;

    [Fact]
    public void Hits_BreakTheShieldFirst_ThenHealth_WhichLeavesInjuriesOverResilience()
    {
        // Round 1: 25 → 15, shield 15 → 5. Round 2: 15 → 5, shield takes 5 and health 5 (0.5 injury). Round 3: the foe falls.
        var result = Resolve(Fighter(FormulaResolver.PlayerId, 100, reaction: 20, shield: 15), Exact(), Fighter("foe", 25, reaction: 1));

        Assert.Equal((CombatEnd.Won, 3, 95.0, 0.0), (result.End, result.Rounds, result.Health, result.Shield));
        Assert.Equal([("injury.body", 0.5)], result.Injuries);
    }

    [Fact]
    public void BreakingTheShield_CanHurtTheMeridians()
    {
        var config = Exact(c => c.ShieldBreakChance = 1);

        var result = Resolve(Fighter(FormulaResolver.PlayerId, 100, reaction: 20, shield: 5), config, Fighter("foe", 15, reaction: 1));

        Assert.Equal([("injury.internal", 3.0), ("injury.body", 0.5)], result.Injuries.OrderByDescending(i => i.InjuryId).ToList());
    }

    [Fact]
    public void Shield_RefillsFromThePoolEachRound_UntilThePoolIsEmpty()
    {
        var config = Exact(c => c.MaxRounds = 3);

        var result = Resolve(Fighter(FormulaResolver.PlayerId, 100, damage: 1, pool: 7, refill: 5, maxShield: 20), config, Fighter("foe", 1000, damage: 0));

        Assert.Equal((CombatEnd.Fled, 3, 7.0, 0.0), (result.End, result.Rounds, result.Shield, result.QiPool));
    }

    [Fact]
    public void BelowTheHealthShare_ThePlayerTriesToEscape_InsteadOfAttacking()
    {
        var escapes = Resolve(Fighter(FormulaResolver.PlayerId, 20, reaction: 20), Exact(c => c.FleeChanceMin = 1), Fighter("foe", 10));
        Assert.Equal((CombatEnd.Fled, 1, 20.0), (escapes.End, escapes.Rounds, escapes.Health));

        // Never gets away: 20 → 10 → 0, and the foe it never attacks survives.
        var dies = Resolve(Fighter(FormulaResolver.PlayerId, 20, reaction: 20), Exact(c => c.FleeChanceMax = 0), Fighter("foe", 10));
        Assert.Equal((CombatEnd.Died, 2, 0.0), (dies.End, dies.Rounds, dies.Health));
    }

    [Fact]
    public void SameSubseed_SameFight()
    {
        var config = TestContent.Repo.Get<CombatConfigDef>(CombatConfigDef.CombatId);
        var player = Fighter(FormulaResolver.PlayerId, 100, shield: 50);
        Combatant[] foes = [Fighter("a", 60, damage: 8), Fighter("b", 60, damage: 8)];

        var first = FormulaResolver.Resolve(player, foes, config, Xoshiro256StarStar.FromSeed(3));
        var second = FormulaResolver.Resolve(player, foes, config, Xoshiro256StarStar.FromSeed(3));

        Assert.Equal((first.End, first.Rounds, first.Health, first.Shield), (second.End, second.Rounds, second.Health, second.Shield));
        Assert.Equal(first.Injuries, second.Injuries);
    }

    [Fact]
    public void Enemies_AreSnapshotsComputedByTheCharacterRules()
    {
        var sim = Simulation.NewGame(Content, 1);

        var foes = CombatSystem.Foes(Context(sim), "grp.test.adepts");

        Assert.Equal(["enemy.test.adept#1", "enemy.test.adept#2"], foes.Select(f => f.Id));
        var adept = foes[0];
        // Might 6 gives 60 health; no meridians, no shield; the base weapon 10 × (1 + stage 1 × 0.3).
        Assert.Equal((60.0, 60.0, 0.0, 13.0, 0.0), (adept.Health, adept.MaxHealth, adept.MaxShield, adept.Damage, adept.Reaction));
    }

    [Fact]
    public void PlayerSnapshot_TakesCurrentResources_AndTheBestTechnique()
    {
        var sim = Simulation.NewGame(Content, 1);
        var player = sim.State.Player;
        player.Resources.First(r => r.ResourceId == "resource.health").Current = 70;
        player.Knowledge.Add(new KnowledgeLevel { KnowledgeId = "tech.spear.piercing_dragon", Level = 2 });

        var me = CombatSystem.Snapshot(player, Context(sim));

        Assert.Equal((70.0, 100.0, 50.0, 100.0, 5.0, 13.0), (me.Health, me.MaxHealth, me.Shield, me.QiPool, me.ShieldRefill, me.Damage));
    }

    [Fact]
    public void WonFight_WritesBackResources_GivesInsightByDifficulty_AndLoot()
    {
        var sim = Simulation.NewGame(Content, 1);
        var ctx = Context(sim);
        var player = sim.State.Player;
        var events = new List<SimEvent>();
        // The foe falls before it acts, yet it is as dangerous as the player: an even fight's insight.
        Assert.Equal(CombatSystem.Snapshot(player, ctx).Threat, CombatSystem.Foes(ctx, "grp.test.weak").Single().Threat);

        Assert.True(CombatSystem.Fight(sim.State, ctx, "grp.test.weak", events));

        Assert.Equal(new CombatResolved("grp.test.weak", CombatEnd.Won, 1, 0, 0), events[0]);
        Assert.Contains(new BuffGained(BuffKind.CombatInsight, ctx.Combat.InsightPerDifficulty), events);
        Assert.Contains(new StonesChanged(5), events);
        Assert.Contains(new ItemsChanged("item.pill.qi_gathering", 2), events);
        Assert.Equal(35, player.Stones);
    }

    [Fact]
    public void EscapedFight_IsALoss_WithPartOfTheInsight_AndNoLoot()
    {
        var sim = Simulation.NewGame(Content, 1);
        var ctx = Context(sim);
        var events = new List<SimEvent>();

        Assert.False(CombatSystem.Fight(sim.State, ctx, "grp.test.wall", events));

        Assert.Equal(CombatEnd.Fled, events.OfType<CombatResolved>().Single().End);
        Assert.Equal(StateNumbers.Round(ctx.Combat.InsightMax * ctx.Combat.InsightFledShare), events.OfType<BuffGained>().Single().Points);
        Assert.DoesNotContain(events, e => e is StonesChanged);
        Assert.Equal(GameMode.Overworld, sim.State.Mode);
    }

    [Fact]
    public void DeathInAFight_EndsTheRun()
    {
        var sim = Simulation.NewGame(Content, 1);
        EventSystem.Start(sim.State, Content.Get<EventDef>("evt.test.ambush"), []);

        var outcome = sim.Execute(new ChooseEventOption(0));

        Assert.True(outcome.Accepted);
        Assert.Equal((GameMode.RunEnd, RunEndCause.CombatDeath), (sim.State.Mode, sim.State.EndCause));
        Assert.Equal(CombatEnd.Died, outcome.Events.OfType<CombatResolved>().Single().End);
        Assert.Equal(0, Current(sim.State.Player, "resource.health"));
        Assert.Contains(new RunEnded(RunEndCause.CombatDeath), outcome.Events);
    }

    [Fact]
    public void FightOption_ShowsTheEnemyCount_AndTheirStageAgainstThePlayers()
    {
        var sim = Simulation.NewGame(Content, 1);
        EventSystem.Start(sim.State, Content.Get<EventDef>("evt.test.ambush"), []);

        var option = sim.View.Event!.Options[1];

        Assert.Equal(new CombatPreview("grp.test.adepts", 2, StageRelation.Higher), option.Combat);
        Assert.Equal(
            "Битися з адептами (супротивників: 2; етап: вищий за ваш)",
            Localizer.FromContent(Content).Resolve(SimText.EventChoices(sim.View.Event!)[1].Text));
    }

}
