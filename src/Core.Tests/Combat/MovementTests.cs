using Core.Combat;
using Core.Content;
using Core.Rng;
using Core.Sim;

namespace Core.Tests.Combat;

public class MovementTests
{
    private static CombatConfigDef Field(int radius)
    {
        var config = TestContent.WithExtra("- id: str.test.field\n  text: { uk: \"x\" }").Get<CombatConfigDef>(CombatConfigDef.CombatId);
        config.FieldRadius = radius;
        return config;
    }

    private static Combatant Fighter(string id, double reaction = 10, double health = 100, double agility = 10, double energy = 15) =>
        new(id, health, 100, 0, 0, 0, 0, 10, reaction, agility, 10, 5, energy, "injury.body", 10, [], TestContent.Repo.Get<AiArchetypeDef>("ai.cultivator_cautious"));

    /// <summary>The player (reaction 20) acts twice for each turn of the wolf, which only waits.</summary>
    private static Battle Start(CombatConfigDef config, double playerHealth = 100) =>
        Battle.Start([Fighter("player", reaction: 20, health: playerHealth)], [Fighter("wolf")], config, Xoshiro256StarStar.FromSeed(3));

    private static void UntilPlayersTurn(Battle battle)
    {
        while (battle.Active!.Id != "player")
            battle.Execute(new EndTurn());
    }

    [Fact]
    public void Agility_gives_movement_points_each_turn()
    {
        var battle = Start(Field(4));
        Assert.Equal(3, battle.Active!.MovePoints);
        var slow = Battle.Start([Fighter("player", reaction: 20, agility: 2)], [Fighter("wolf")], Field(4), Xoshiro256StarStar.FromSeed(3));
        Assert.Equal(1, slow.Active!.MovePoints);
    }

    [Fact]
    public void Every_step_costs_energy_and_the_energy_left_caps_the_steps()
    {
        // Three movement points, but energy for two steps.
        var battle = Battle.Start([Fighter("player", reaction: 20, energy: 2)], [Fighter("wolf")], Field(4), Xoshiro256StarStar.FromSeed(3));
        var me = battle.Active!;
        Assert.Equal(3, me.MovePoints);
        Assert.Equal(2, battle.Reachable().Max(r => r.Path.Count));

        Assert.True(battle.Execute(new Move(new Hex(0, 1))).Accepted);
        Assert.Equal(2 - 2 * Field(4).StepEnergy, me.Energy, 9);
        Assert.Equal(1, me.MovePoints);
        Assert.Empty(battle.Reachable());
        Assert.Equal(RejectReasons.Unreachable, battle.Execute(new Move(new Hex(0, 0))).RejectReason);
    }

    [Fact]
    public void Reachable_hexes_lie_within_the_points_inside_the_field_and_off_other_fighters()
    {
        var battle = Start(Field(4));
        var me = battle.Active!;
        var reach = battle.Reachable();
        var expected = battle.Field.Hexes().Where(h => h != me.Position && h.DistanceTo(me.Position) <= 3).ToList();
        Assert.Equal(expected, reach.Select(r => r.To).ToList());
        Assert.All(reach, r => Assert.Equal(r.To.DistanceTo(me.Position), r.Path.Count));
        Assert.All(reach, r => Assert.Equal(r.To, r.Path[^1]));
    }

    [Fact]
    public void Movement_points_split_between_moves_before_the_action()
    {
        var battle = Start(Field(4));
        var me = battle.Active!;
        var outcome = battle.Execute(new Move(new Hex(0, 1)));
        Assert.True(outcome.Accepted);
        Assert.Equal([new FighterMoved("player", new(0, 3), new(0, 2)), new FighterMoved("player", new(0, 2), new(0, 1))],
            outcome.Events.OfType<FighterMoved>().ToList());
        Assert.Equal(1, me.MovePoints);
        Assert.Equal(RejectReasons.Unreachable, battle.Execute(new Move(new Hex(0, -1))).RejectReason);
        Assert.True(battle.Execute(new Move(new Hex(0, 0))).Accepted);
        Assert.Equal(0, me.MovePoints);
        Assert.Empty(battle.Reachable());
    }

    [Fact]
    public void Fighters_block_the_way()
    {
        var battle = Start(Field(1));
        var wolf = battle.Fighters[1];
        Assert.DoesNotContain(battle.Reachable(), r => r.To == wolf.Position || r.Path.Contains(wolf.Position));
        Assert.Equal(RejectReasons.Unreachable, battle.Execute(new Move(wolf.Position)).RejectReason);
    }

    [Fact]
    public void Leaving_an_enemys_side_draws_one_free_attack_a_turn()
    {
        // Radius 1: the player stands in the centre, the wolf next to it at (1,0).
        var battle = Start(Field(1));
        var me = battle.Active!;
        Assert.Equal(new Hex(1, 0), battle.Fighters[1].Position);

        var away = battle.Execute(new Move(new Hex(-1, 0)));
        Assert.Equal(new FreeAttackProvoked("wolf", "player"), Assert.Single(away.Events.OfType<FreeAttackProvoked>()));
        var hit = Assert.Single(away.Events.OfType<FighterHit>());
        Assert.Equal(100 - hit.ToHealth, me.Health, 9);
        Assert.Equal(15 - Field(1).AttackEnergy, battle.Fighters[1].Energy);

        battle.Execute(new Move(new Hex(0, 0)));
        var again = battle.Execute(new Move(new Hex(0, 1)));
        Assert.Empty(again.Events.OfType<FreeAttackProvoked>());
    }

    [Fact]
    public void An_enemy_without_the_energy_for_a_strike_gives_no_free_attack()
    {
        // Radius 1: the wolf next to the player has energy for no basic attack.
        var battle = Battle.Start([Fighter("player", reaction: 20)], [Fighter("wolf", energy: 1)], Field(1), Xoshiro256StarStar.FromSeed(3));
        Assert.All(battle.Reachable(), r => Assert.Equal(0, r.FreeAttacks));
        var away = battle.Execute(new Move(new Hex(-1, 0)));
        Assert.True(away.Accepted);
        Assert.Empty(away.Events.OfType<FreeAttackProvoked>());
        Assert.Equal(1, battle.Fighters[1].Energy);
    }

    [Fact]
    public void A_beast_lets_go_of_whoever_steps_away_from_it()
    {
        // Radius 1: the beast stands next to the player and could pay for a strike.
        var beast = Fighter("wolf") with { Ai = TestContent.Repo.Get<AiArchetypeDef>("ai.beast_aggressive") };
        var battle = Battle.Start([Fighter("player", reaction: 20)], [beast], Field(1), Xoshiro256StarStar.FromSeed(3));

        Assert.All(battle.Reachable(), r => Assert.Equal(0, r.FreeAttacks));
        var away = battle.Execute(new Move(new Hex(-1, 0)));
        Assert.Empty(away.Events.OfType<FreeAttackProvoked>());
        Assert.Equal(100, battle.Fighters[0].Health);
    }

    [Fact]
    public void The_path_goes_around_enemy_sides_when_the_points_allow()
    {
        // Radius 2: the player at (0,1), the wolf at (0,-1). From (1,0) the short way to (-1,0)
        // leaves (0,0), next to the wolf; the three-step way round the back provokes nothing.
        var battle = Start(Field(2));
        battle.Execute(new Move(new Hex(1, 0)));
        battle.Execute(new EndTurn());
        UntilPlayersTurn(battle);

        var reach = battle.Reachable().Single(r => r.To == new Hex(-1, 0));
        Assert.Equal(0, reach.FreeAttacks);
        Assert.Equal([new(0, 1), new(-1, 1), new(-1, 0)], reach.Path);
    }

    [Fact]
    public void A_free_attack_that_brings_the_mover_down_stops_the_walk_and_the_fight()
    {
        var battle = Start(Field(1), playerHealth: 1);
        var outcome = battle.Execute(new Move(new Hex(-1, 0)));
        Assert.Empty(outcome.Events.OfType<FighterMoved>());
        Assert.Equal(new FighterDown("player"), Assert.Single(outcome.Events.OfType<FighterDown>()));
        Assert.Equal(new Hex(0, 0), battle.Fighters[0].Position);
        Assert.True(battle.IsOver);
        Assert.Equal(1, battle.Winner);
    }
}
