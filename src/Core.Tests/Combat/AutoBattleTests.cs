using Core.Combat;
using Core.Content;
using Core.Rng;
using Core.Sim;

namespace Core.Tests.Combat;

/// <summary>Auto-resolve: the hex battle played to the end by the AI on both sides, on its terms: to the death, spared (D62), an arena bout (§5).</summary>
public class AutoBattleTests
{
    private static CombatConfigDef Config => TestContent.Repo.Get<CombatConfigDef>(CombatConfigDef.CombatId);

    private static AiArchetypeDef Ai(string id) => TestContent.Repo.Get<AiArchetypeDef>(id);

    private static Combatant Fighter(string id, double health = 100, double weapon = 10, string ai = "ai.cultivator_cautious") =>
        new(id, health, 100, 0, 0, 0, 0, weapon, 10, 10, 10, 5, 15, "injury.body", weapon, [], Ai(ai));

    private static CombatResult Resolve(Combatant player, Combatant foe, BattleTerms terms = BattleTerms.ToDeath, ulong seed = 1) =>
        AutoBattle.Resolve(player, [foe], Config, Xoshiro256StarStar.FromSeed(seed), Xoshiro256StarStar.FromSeed(seed + 100), terms);

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

    [Theory]
    [InlineData(BattleTerms.Spared, CombatEnd.Beaten)]
    [InlineData(BattleTerms.Bout, CombatEnd.Yielded)]
    public void Unless_the_fight_is_to_the_death_a_deadly_blow_is_stopped(BattleTerms terms, CombatEnd end)
    {
        var result = Resolve(Fighter(Combatant.PlayerId, weapon: 0.1), Fighter("killer", weapon: 1000, ai: "ai.beast_aggressive"), terms);

        Assert.Equal((end, 1.0), (result.End, result.Health));
    }

    [Fact]
    public void A_wounded_player_yields_where_it_would_flee()
    {
        var player = Fighter(Combatant.PlayerId, health: 20);

        var result = Resolve(player, Fighter("adept", weapon: 1), BattleTerms.Bout);

        // Below the archetype's flee share from the start: the first own turn gives up.
        Assert.True(20 <= Ai("ai.cultivator_cautious").FleeHealthShare * 100);
        Assert.Equal((CombatEnd.Yielded, 20.0), (result.End, result.Health));
    }

    [Fact]
    public void Only_side_0_of_an_arena_bout_may_yield()
    {
        var spared = Battle.Start([Fighter(Combatant.PlayerId)], [Fighter("wolf")], Config, Xoshiro256StarStar.FromSeed(1), BattleTerms.Spared);
        Assert.Equal(RejectReasons.CannotYield, spared.WhyNot(new Yield()));

        var bout = Battle.Start([Fighter(Combatant.PlayerId, weapon: 1)], [Fighter("wolf")], Config, Xoshiro256StarStar.FromSeed(1), BattleTerms.Bout);
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
