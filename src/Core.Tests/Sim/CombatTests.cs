using Core.Combat;
using Core.Content;
using Core.Loc;
using Core.Rng;
using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

/// <summary>
/// Fights of the run (D42): snapshots, the auto-resolved hex battle (its own rules: AutoBattleTests)
/// and what the result writes back. A fresh player has 100 health, a 50 qi shield, a 100 qi pool and
/// 10 in every aspect.
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
          stats: { stat.might: 100000, stat.speed: 100, stat.endurance: 10 }
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
        - id: grp.test.robbers
          name: { uk: "Грабіжники" }
          members: [ { enemy: enemy.test.killer } ]
          kill_chance: 0
        - id: grp.test.murderers
          name: { uk: "Душогуби" }
          members: [ { enemy: enemy.test.killer } ]
          kill_chance: 1
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

    private static SimContext Context(Simulation sim) => new(sim.Content, RngStreams.FromStates(sim.State.RngStates));

    private static double Current(PlayerState player, string id) => player.Resources.First(r => r.ResourceId == id).Current;

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
        // The foe has no energy to strike, yet it is as dangerous as the player: an even fight's insight.
        Assert.Equal(CombatSystem.Snapshot(player, ctx).Threat, CombatSystem.Foes(ctx, "grp.test.weak").Single().Threat);

        Assert.True(CombatSystem.Fight(sim.State, ctx, "grp.test.weak", events));

        Assert.Equal((CombatEnd.Won, 0.0, 0.0), events.OfType<CombatResolved>().Select(c => (c.End, c.HealthLost, c.QiSpent)).Single());
        Assert.Contains(new BuffGained(BuffKind.CombatInsight, ctx.Combat.InsightPerDifficulty), events);
        Assert.Contains(new StonesChanged(5), events);
        Assert.Contains(new ItemsChanged("item.pill.qi_gathering", 2), events);
        Assert.Equal(35, player.Stones);
        var energy = player.Resources.First(r => r.ResourceId == ctx.Combat.EnergyResource).Current;
        Assert.InRange(energy, 0, ctx.Rules.Value(player, ctx.Combat.EnergyResource) - 1);   // the fight's spending stays with the body
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
    public void BeatenBySparingCultivators_ThePlayerLivesOn_WithNothingLeft()
    {
        var sim = Simulation.NewGame(Content, 1);
        var ctx = Context(sim);
        var player = sim.State.Player;
        Assert.NotEmpty(player.Items);
        var events = new List<SimEvent>();

        Assert.False(CombatSystem.Fight(sim.State, ctx, "grp.test.robbers", events));

        Assert.Equal(CombatEnd.Beaten, events.OfType<CombatResolved>().Single().End);
        Assert.Equal((GameMode.Overworld, 1.0, 0), (sim.State.Mode, Current(player, "resource.health"), player.Stones));
        Assert.Empty(player.Items);
        Assert.Contains(events, e => e is StonesChanged { Amount: -30 });
        Assert.Contains(events, e => e is ItemsChanged { Count: < 0 });
        Assert.Contains(player.Injuries, i => i.Value > 0);
    }

    [Fact]
    public void BeatenPlayer_IsFinishedOff_WithTheGroupsKillChance()
    {
        var sim = Simulation.NewGame(Content, 1);
        var events = new List<SimEvent>();

        Assert.False(CombatSystem.Fight(sim.State, Context(sim), "grp.test.murderers", events));

        Assert.Equal(CombatEnd.Died, events.OfType<CombatResolved>().Single().End);
        Assert.Equal((GameMode.RunEnd, RunEndCause.CombatDeath), (sim.State.Mode, sim.State.EndCause));
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
