using Core.Combat;
using Core.Content;
using Core.Rng;
using Core.Sim;

namespace Core.Tests.Combat;

/// <summary>Auto-resolve: the hex battle played to the end by the AI on both sides, and the arena's yielding (§5).</summary>
public class AutoBattleTests
{
    private static CombatConfigDef Config => TestContent.Repo.Get<CombatConfigDef>(CombatConfigDef.CombatId);

    private static AiArchetypeDef Ai(string id) => TestContent.Repo.Get<AiArchetypeDef>(id);

    private static Combatant Fighter(string id, double health = 100, double weapon = 10, string ai = "ai.cultivator_cautious") =>
        new(id, 0, health, 100, 0, 0, 0, 0, weapon, 10, 10, 10, 5, 15, "injury.body", weapon, [], Ai(ai));

    private static CombatResult Resolve(Combatant player, Combatant foe, bool yields = false, ulong seed = 1) =>
        AutoBattle.Resolve(player, [foe], Config, Xoshiro256StarStar.FromSeed(seed), Xoshiro256StarStar.FromSeed(seed + 100), yields);

    [Fact]
    public void A_much_stronger_player_wins_and_keeps_what_the_fight_left()
    {
        var result = Resolve(Fighter(Combatant.PlayerId, weapon: 200), Fighter("wolf", health: 10, weapon: 1, ai: "ai.beast_aggressive"));

        Assert.Equal(CombatEnd.Won, result.End);
        Assert.True(result.Turns > 1);
        Assert.True(result.Health > 90);
    }

    [Fact]
    public void A_hopeless_fight_ends_in_death_against_a_beast_that_never_lets_go()
    {
        var result = Resolve(Fighter(Combatant.PlayerId, weapon: 0.1), Fighter("killer", weapon: 1000, ai: "ai.beast_aggressive"));

        Assert.Equal((CombatEnd.Died, 0.0), (result.End, result.Health));
        Assert.Contains(result.Injuries, i => i.InjuryId == "injury.body");
    }

    [Fact]
    public void In_a_bout_to_giving_up_a_deadly_blow_is_stopped()
    {
        var result = Resolve(Fighter(Combatant.PlayerId, weapon: 0.1), Fighter("killer", weapon: 1000, ai: "ai.beast_aggressive"), yields: true);

        Assert.Equal((CombatEnd.Yielded, 1.0), (result.End, result.Health));
    }

    [Fact]
    public void A_wounded_player_yields_where_it_would_flee()
    {
        var player = Fighter(Combatant.PlayerId, health: 20);

        var result = Resolve(player, Fighter("adept", weapon: 1), yields: true);

        // Below the archetype's flee share from the start: the first own turn gives up.
        Assert.True(20 <= Ai("ai.cultivator_cautious").FleeHealthShare * 100);
        Assert.Equal((CombatEnd.Yielded, 20.0), (result.End, result.Health));
    }

    [Fact]
    public void Only_side_0_of_a_bout_to_giving_up_may_yield()
    {
        var plain = Battle.Start([Fighter(Combatant.PlayerId)], [Fighter("wolf")], Config, Xoshiro256StarStar.FromSeed(1));
        Assert.Equal(RejectReasons.CannotYield, plain.WhyNot(new Yield()));

        var bout = Battle.Start([Fighter(Combatant.PlayerId, weapon: 1)], [Fighter("wolf")], Config, Xoshiro256StarStar.FromSeed(1), yields: true);
        Assert.Null(bout.WhyNot(new Yield()));
        bout.Execute(new EndTurn());
        Assert.Equal((1, RejectReasons.CannotYield), (bout.Active!.Side, bout.WhyNot(new Yield())));
    }

    [Fact]
    public void Same_seeds_same_fight()
    {
        var player = Fighter(Combatant.PlayerId);
        var foe = Fighter("bandit", weapon: 8);

        var first = Resolve(player, foe, seed: 3);
        var second = Resolve(player, foe, seed: 3);

        Assert.Equal((first.End, first.Turns, first.Health, first.Shield), (second.End, second.Turns, second.Health, second.Shield));
        Assert.Equal(first.Injuries, second.Injuries);
    }
}
