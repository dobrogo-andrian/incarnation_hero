using Core.Combat;
using Core.Content;
using Core.Rng;
using Core.Sim;

namespace Core.Tests.Combat;

public class ActionTests
{
    private static CombatConfigDef Field(int radius)
    {
        var config = TestContent.WithExtra("- id: str.test.field\n  text: { uk: \"x\" }").Get<CombatConfigDef>(CombatConfigDef.CombatId);
        config.FieldRadius = radius;
        return config;
    }

    private static Combatant Fighter(string id, double reaction = 10) =>
        new(id, 0, 100, 100, 0, 0, 0, 0, 10, reaction, 10, 10, 100, 5, 15, "injury.body", 10, [], TestContent.Repo.Get<AiArchetypeDef>("ai.cultivator_cautious"));

    private static Battle Start(CombatConfigDef config, double playerReaction = 20, double wolfReaction = 10) =>
        Battle.Start([Fighter("player", playerReaction)], [Fighter("wolf", wolfReaction)], config, Xoshiro256StarStar.FromSeed(5));

    [Fact]
    public void An_action_ends_the_turn_and_the_steps_left_with_it()
    {
        // Equal reactions: the player goes first by ID, then the wolf.
        var battle = Start(Field(4), playerReaction: 10, wolfReaction: 10);
        var me = battle.Active!;
        battle.Execute(new Move(new Hex(0, 2)));
        Assert.Equal(2, me.MovePoints);

        var outcome = battle.Execute(new Guard());

        Assert.True(outcome.Accepted);
        Assert.Empty(outcome.Events.OfType<FighterWaited>());
        var ended = Assert.Single(outcome.Events.OfType<BattleTurnEnded>());
        Assert.Equal("player", ended.FighterId);
        Assert.Equal(15 - Field(4).GuardEnergy + ended.EnergyRefilled, me.Energy, 9);
        Assert.Equal("wolf", battle.Active!.Id);
    }

    [Fact]
    public void WhyNot_tells_what_Execute_would_refuse_and_changes_nothing()
    {
        var battle = Start(Field(4));
        var me = battle.Active!;
        Assert.Null(battle.WhyNot(new Guard()));
        Assert.Equal(RejectReasons.NotOnEdge, battle.WhyNot(new Flee()));
        Assert.Equal(RejectReasons.OutOfReach, battle.WhyNot(new Attack("wolf")));
        Assert.Null(battle.WhyNot(new EndTurn()));
        Assert.Same(me, battle.Active);
        Assert.Equal(15, me.Energy);

        // Far from the wolf the attack can still be picked: a walk may bring it in reach.
        Assert.Null(battle.SkillUnavailable(null));
    }

    [Fact]
    public void A_skill_with_a_target_in_reach_is_available()
    {
        var battle = Start(Field(1));
        Assert.Null(battle.SkillUnavailable(null));
        Assert.Equal(RejectReasons.UnknownTechnique, battle.SkillUnavailable("tech.sword.flowing_cut"));
    }

    [Fact]
    public void Guard_lets_half_of_a_hit_reach_the_body_until_the_next_own_turn()
    {
        // Equal reactions: the player goes first by ID, then the wolf strikes with the same roll either way.
        double TakenBy(bool guard)
        {
            var battle = Start(Field(1), playerReaction: 10, wolfReaction: 10);
            Assert.Equal("player", battle.Active!.Id);
            Assert.True(battle.Execute(guard ? new Guard() : new EndTurn()).Accepted);
            var hit = battle.Execute(new Attack("player")).Events.OfType<FighterHit>().Single();
            Assert.Equal("player", battle.Active!.Id);
            Assert.False(battle.Fighters[0].Guarding);
            return hit.ToHealth;
        }
        Assert.Equal(TakenBy(false) * Field(1).GuardBodyShare, TakenBy(true), 9);
    }

    [Fact]
    public void Escape_from_a_rim_hex_leaves_the_fight()
    {
        var battle = Start(Field(4));
        Assert.Equal(RejectReasons.NotOnEdge, battle.Execute(new Flee()).RejectReason);
        battle.Execute(new Move(new Hex(0, 4)));
        var outcome = battle.Execute(new Flee());
        Assert.Equal(new FighterFled("player"), Assert.Single(outcome.Events.OfType<FighterFled>()));
        Assert.True(battle.Fighters[0].Fled);
        Assert.True(battle.IsOver);
        Assert.Equal(1, battle.Winner);
    }

    [Fact]
    public void An_enemy_next_to_the_fighter_bars_the_escape()
    {
        // Radius 1: the wolf at (1,0) is on the rim, the player beside it in the centre.
        var battle = Start(Field(1), playerReaction: 10, wolfReaction: 20);
        Assert.Equal("wolf", battle.Active!.Id);
        Assert.Equal(RejectReasons.EnemyAdjacent, battle.Execute(new Flee()).RejectReason);
        Assert.Equal("wolf", battle.Active!.Id);
    }
}
