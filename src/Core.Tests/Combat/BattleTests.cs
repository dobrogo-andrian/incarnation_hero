using Core.Combat;
using Core.Content;
using Core.Rng;
using Core.Sim;

namespace Core.Tests.Combat;

public class BattleTests
{
    private static CombatConfigDef Config => TestContent.Repo.Get<CombatConfigDef>(CombatConfigDef.CombatId);

    private static Combatant Fighter(string id, double reaction = 10, double health = 100, double shield = 0, double maxShield = 0,
        double qiPool = 0, double shieldRefill = 0, double energy = 15, double energyRefill = 5, double maxEnergy = 15) =>
        new(id, 0, health, 100, shield, maxShield, qiPool, shieldRefill, 10, reaction, 10, 10, energy, energyRefill, maxEnergy, "injury.body", 10, [], TestContent.Repo.Get<AiArchetypeDef>("ai.cultivator_cautious"));

    /// <summary>A field of radius 1: the second side deploys next to the first, so fighters can hit without moving.</summary>
    private static readonly CombatConfigDef Close = CloseConfig();

    private static CombatConfigDef CloseConfig()
    {
        var config = TestContent.WithExtra("- id: str.test.close\n  text: { uk: \"x\" }").Get<CombatConfigDef>(CombatConfigDef.CombatId);
        config.FieldRadius = 1;
        return config;
    }

    private static Battle Start(IReadOnlyList<Combatant> side0, IReadOnlyList<Combatant> side1, CombatConfigDef? config = null) =>
        Battle.Start(side0, side1, config ?? Config, Xoshiro256StarStar.FromSeed(7));

    private static List<string> WaitTurns(Battle battle, int turns)
    {
        var acted = new List<string>();
        for (int i = 0; i < turns; i++)
        {
            acted.Add(battle.Active!.Id);
            Assert.True(battle.Execute(new EndTurn()).Accepted);
        }
        return acted;
    }

    [Fact]
    public void Attack_breaks_the_shield_first_then_health_and_leaves_injuries()
    {
        var battle = Start([Fighter("player", reaction: 20)], [Fighter("wolf", shield: 4, maxShield: 4)], Close);
        var me = battle.Fighters[0];
        var wolf = battle.Fighters[1];
        Assert.Equal(1, me.Position.DistanceTo(wolf.Position));

        var outcome = battle.Execute(new Attack("wolf"));

        Assert.True(outcome.Accepted);
        var ended = Assert.Single(outcome.Events.OfType<BattleTurnEnded>());
        var hit = Assert.Single(outcome.Events.OfType<FighterHit>());
        Assert.InRange(hit.Damage, 8, 12);
        Assert.Equal(4, hit.Absorbed);
        Assert.Equal(hit.Damage - 4, hit.ToHealth, 9);
        Assert.Equal(0, wolf.Shield);
        Assert.Equal(100 - hit.ToHealth, wolf.Health, 9);
        Assert.Equal(hit.ToHealth / 10, wolf.Injuries["injury.body"], 9);
        Assert.Equal(15 - Close.AttackEnergy + ended.EnergyRefilled, me.Energy, 9);
    }

    [Fact]
    public void The_action_ends_the_turn_and_ending_without_one_is_a_wait()
    {
        var battle = Start([Fighter("player")], [Fighter("wolf")], Close);
        var attacked = battle.Execute(new Attack("wolf"));
        Assert.Empty(attacked.Events.OfType<FighterWaited>());
        Assert.Equal("player", Assert.Single(attacked.Events.OfType<BattleTurnEnded>()).FighterId);
        Assert.Equal("wolf", Assert.Single(attacked.Events.OfType<BattleTurnStarted>()).FighterId);

        var waited = battle.Execute(new EndTurn());
        Assert.Equal(new FighterWaited("wolf"), Assert.Single(waited.Events.OfType<FighterWaited>()));
        Assert.Single(waited.Events.OfType<BattleTurnEnded>());
    }

    [Fact]
    public void Attacks_need_an_adjacent_enemy_and_the_energy()
    {
        var far = Start([Fighter("player", reaction: 20), Fighter("ally")], [Fighter("wolf")]);
        Assert.Equal(RejectReasons.OutOfReach, far.Execute(new Attack("wolf")).RejectReason);
        Assert.Equal(RejectReasons.InvalidTarget, far.Execute(new Attack("ally")).RejectReason);
        Assert.Equal(RejectReasons.InvalidTarget, far.Execute(new Attack("nobody")).RejectReason);

        var tired = Start([Fighter("player", reaction: 20, energy: 3)], [Fighter("wolf")], Close);
        Assert.Equal(RejectReasons.NotEnoughEnergy, tired.Execute(new Attack("wolf")).RejectReason);
    }

    [Fact]
    public void Bringing_down_the_last_enemy_ends_the_fight()
    {
        var battle = Start([Fighter("player", reaction: 20)], [Fighter("wolf", health: 1)], Close);
        var outcome = battle.Execute(new Attack("wolf"));
        Assert.Equal(new FighterDown("wolf"), Assert.Single(outcome.Events.OfType<FighterDown>()));
        Assert.Equal(0, battle.Fighters[1].Health);
        Assert.True(battle.IsOver);
        Assert.Equal(0, battle.Winner);
        Assert.Equal(RejectReasons.BattleOver, battle.Execute(new EndTurn()).RejectReason);
    }

    [Fact]
    public void A_fighter_down_leaves_the_turn_order()
    {
        var battle = Start([Fighter("player", reaction: 20)], [Fighter("wolf#1", health: 1), Fighter("wolf#2")], Close);
        Assert.True(battle.Execute(new Attack("wolf#1")).Accepted);
        Assert.DoesNotContain(battle.Forecast(6), f => f.Id == "wolf#1");
        Assert.False(battle.IsOver);
    }

    [Fact]
    public void Same_seed_and_commands_give_the_same_fight()
    {
        double Play()
        {
            var battle = Start([Fighter("player", reaction: 20)], [Fighter("wolf", shield: 5, maxShield: 5)], Close);
            for (int i = 0; i < 6 && !battle.IsOver; i++)
            {
                battle.Execute(new Attack(battle.Active!.Side == 0 ? "wolf" : "player"));
            }
            return battle.Fighters.Sum(f => f.Health);
        }
        Assert.Equal(Play(), Play());
    }

    [Fact]
    public void Sides_deploy_on_opposite_halves_on_distinct_hexes()
    {
        var battle = Start([Fighter("player")], [Fighter("wolf#1"), Fighter("wolf#2"), Fighter("wolf#3")]);
        var hexes = battle.Fighters.Select(f => f.Position).ToList();
        Assert.Equal(hexes.Count, hexes.Distinct().Count());
        Assert.All(hexes, h => Assert.True(battle.Field.Contains(h)));
        Assert.All(battle.Fighters.Where(f => f.Side == 0), f => Assert.True(f.Position.R > 0));
        Assert.All(battle.Fighters.Where(f => f.Side == 1), f => Assert.True(f.Position.R < 0));
        Assert.Equal(new Hex(0, 3), battle.Fighters[0].Position);
    }

    [Fact]
    public void Faster_fighters_act_more_often()
    {
        var battle = Start([Fighter("player", reaction: 10)], [Fighter("wolf", reaction: 15)]);
        var acted = WaitTurns(battle, 50);
        Assert.Equal(30, acted.Count(id => id == "wolf"));
        Assert.Equal(20, acted.Count(id => id == "player"));
        Assert.Equal("wolf", acted[0]);
    }

    [Fact]
    public void Ties_go_to_the_quicker_reaction_then_the_id()
    {
        // 10.4 and 10 both fill 10 a tick; the higher reaction still goes first.
        var battle = Start([Fighter("b", reaction: 10)], [Fighter("a", reaction: 10), Fighter("c", reaction: 10.4)]);
        Assert.Equal(["c", "a", "b", "c", "a", "b"], WaitTurns(battle, 6));
    }

    [Fact]
    public void Forecast_matches_the_turns_that_follow()
    {
        var battle = Start([Fighter("player", reaction: 11)], [Fighter("wolf#1", reaction: 13), Fighter("bandit#1", reaction: 8)]);
        var forecast = battle.Forecast(12).Select(f => f.Id).ToList();
        battle.Execute(new EndTurn());
        Assert.Equal(forecast, WaitTurns(battle, 12));
    }

    [Fact]
    public void Turn_end_refills_the_shield_from_the_pool()
    {
        var battle = Start([Fighter("player", reaction: 20, shield: 10, maxShield: 50, qiPool: 3, shieldRefill: 5)], [Fighter("wolf")]);
        var me = battle.Fighters[0];

        var outcome = battle.Execute(new EndTurn());

        // The pool holds only 3; energy starts full, so none comes back.
        Assert.Equal(new BattleTurnEnded("player", 3, 0), Assert.Single(outcome.Events.OfType<BattleTurnEnded>()));
        Assert.Equal(13, me.Shield);
        Assert.Equal(0, me.QiPool);
        Assert.Equal(15, me.Energy);
    }

    [Theory]
    [InlineData(100, 4, 5)]     // full health: the whole output
    [InlineData(50, 4, 2.5)]    // half health: half of it
    [InlineData(10, 4, 2)]      // never under the minimum
    [InlineData(100, 0, 5)]     // an empty body gets it all the same
    [InlineData(100, 14, 1)]    // energy stops at its maximum of 15
    public void Energy_comes_back_by_output_and_health_within_the_minimum_and_maximum(double health, double energy, double expected)
    {
        Assert.Equal(expected, CombatMath.EnergyRefill(Fighter("player"), health, energy, Config.EnergyRefillMin), 9);
    }

    [Fact]
    public void A_fight_with_one_side_down_is_over_and_takes_no_commands()
    {
        var battle = Start([Fighter("player")], [Fighter("wolf", health: 0)]);
        Assert.True(battle.IsOver);
        Assert.Equal(0, battle.Winner);
        Assert.Empty(battle.Forecast(3));
        Assert.Equal(RejectReasons.BattleOver, battle.Execute(new EndTurn()).RejectReason);
    }

    [Fact]
    public void Player_snapshot_carries_energy_from_the_body()
    {
        var sim = Simulation.NewGame(TestContent.Repo, 1);
        var ctx = new SimContext(sim.Content, RngStreams.FromStates(sim.State.RngStates));
        var player = CombatSystem.Snapshot(sim.State.Player, ctx);
        Assert.Equal(100, player.Energy);
        Assert.Equal(5, player.EnergyRefill);
        Assert.Equal(100, player.MaxEnergy);
    }
}
