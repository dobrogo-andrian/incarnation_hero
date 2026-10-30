using Core.Combat;
using Core.Content;
using Core.Rng;
using Core.Sim;

namespace Core.Tests.Combat;

/// <summary>The AI behaviour checklist of stage 2 (tech 9), plus the turn limit and whole AI-only fights.</summary>
public class AiTests
{
    private const string Beast = "ai.beast_aggressive";
    private const string Cautious = "ai.cultivator_cautious";

    private static CombatConfigDef Field(int radius, int? maxTurns = null)
    {
        var config = TestContent.WithExtra("- id: str.test.field\n  text: { uk: \"x\" }").Get<CombatConfigDef>(CombatConfigDef.CombatId);
        config.FieldRadius = radius;
        if (maxTurns is { } turns)
            config.MaxTurns = turns;
        return config;
    }

    private static Combatant Fighter(string id, string ai, double reaction = 10, double health = 100, double energy = 15,
        double shield = 30, params string[] techniques) =>
        new(id, 0, health, 100, shield, shield, 0, 0, 10, reaction, 10, 10, energy, 5, energy, "injury.body", 10,
            techniques.Select(t => new CombatTechnique(t, 1, TestContent.Repo.Get<KnowledgeDef>(t).Combat!)).ToList(),
            TestContent.Repo.Get<AiArchetypeDef>(ai));

    private static Battle Start(CombatConfigDef config, Combatant me, params Combatant[] foes) =>
        Battle.Start([me], foes, config, Xoshiro256StarStar.FromSeed(21));

    private static Xoshiro256StarStar Ai => Xoshiro256StarStar.FromSeed(22);

    [Fact]
    public void It_finishes_off_the_weakest_enemy_in_reach()
    {
        // Radius 1: the AI in the centre, three wolves around it; the middle one is nearly down.
        var battle = Start(Field(1), Fighter("me", Beast, reaction: 20),
            Fighter("wolf#1", Beast), Fighter("wolf#2", Beast, health: 3, shield: 0), Fighter("wolf#3", Beast));
        var events = BattleAi.PlayTurn(battle, Ai);
        Assert.Equal("wolf#2", Assert.Single(events.OfType<FighterHit>()).TargetId);
        Assert.Contains(new FighterDown("wolf#2"), events);
    }

    [Fact]
    public void A_wounded_cultivator_makes_for_the_rim_and_escapes()
    {
        var battle = Start(Field(4), Fighter("me", Cautious, reaction: 20, health: 20), Fighter("wolf", Beast));
        var events = BattleAi.PlayTurn(battle, Ai);
        Assert.Contains(new FighterFled("me"), events);
        Assert.True(battle.IsOver);
    }

    [Fact]
    public void A_wounded_beast_fights_on()
    {
        var battle = Start(Field(4), Fighter("wolf", Beast, reaction: 20, health: 20), Fighter("me", Cautious));
        var events = BattleAi.PlayTurn(battle, Ai);
        Assert.DoesNotContain(events, e => e is FighterFled);
        Assert.True(battle.Fighters[0].Position.DistanceTo(battle.Fighters[1].Position) < 6);
    }

    [Fact]
    public void A_cultivator_that_cannot_strike_keeps_out_of_enemy_reach()
    {
        // No energy to attack or guard and no techniques: nothing worth stepping next to the wolf for.
        var battle = Start(Field(4), Fighter("me", Cautious, reaction: 20, energy: 2), Fighter("wolf", Beast));
        BattleAi.PlayTurn(battle, Ai);
        var me = battle.Fighters[0];
        var wolf = battle.Fighters[1];
        Assert.True(me.Position.DistanceTo(wolf.Position) > 4);   // the wolf walks 3, then bites
    }

    [Fact]
    public void A_beast_closes_in()
    {
        var battle = Start(Field(4), Fighter("wolf", Beast, reaction: 20), Fighter("me", Cautious));
        BattleAi.PlayTurn(battle, Ai);
        Assert.Equal(3, battle.Fighters[0].Position.DistanceTo(battle.Fighters[1].Position));
    }

    [Fact]
    public void It_charges_an_enemy_out_of_walking_reach()
    {
        // Six hexes apart: three steps and a basic attack fall short, a walk and a wind step do not.
        var battle = Start(Field(4), Fighter("me", Beast, reaction: 20, techniques: "tech.movement.wind_step"), Fighter("wolf", Beast));
        var events = BattleAi.PlayTurn(battle, Ai);
        Assert.Equal("tech.movement.wind_step", Assert.Single(events.OfType<TechniqueUsed>()).TechniqueId);
        Assert.Equal("wolf", Assert.Single(events.OfType<FighterHit>()).TargetId);
    }

    [Fact]
    public void A_cultivator_strikes_and_leaps_out_of_a_zone_of_control_instead_of_walking_out()
    {
        // The wolf (faster) closes in and bites; the cultivator, with the energy for crane retreat
        // but not for a basic attack, answers with it: it strikes and leaves without a free attack.
        var config = Field(2);
        config.AttackEnergy = 5;
        var battle = Start(config, Fighter("me", Cautious, energy: 4, techniques: "tech.movement.crane_retreat"), Fighter("wolf", Beast, reaction: 20));
        var ai = Ai;
        while (battle.Active!.Id == "wolf")
            BattleAi.PlayTurn(battle, ai);
        Assert.Equal(1, battle.Fighters[0].Position.DistanceTo(battle.Fighters[1].Position));

        var events = BattleAi.PlayTurn(battle, ai);
        Assert.Equal("tech.movement.crane_retreat", Assert.Single(events.OfType<TechniqueUsed>()).TechniqueId);
        Assert.DoesNotContain(events, e => e is FreeAttackProvoked { MoverId: "me" });
    }

    [Fact]
    public void A_fight_that_runs_out_of_turns_is_side_0_withdrawing()
    {
        var battle = Start(Field(4, maxTurns: 10), Fighter("me", Cautious), Fighter("wolf", Beast));
        var events = new List<SimEvent>();
        while (!battle.IsOver)
            events.AddRange(battle.Execute(new EndTurn()).Events);
        Assert.Equal(new BattleTimedOut(10), events.OfType<BattleTimedOut>().Single());
        Assert.True(battle.Fighters[0].Fled);
        Assert.Equal(1, battle.Winner);
    }

    [Fact]
    public void Whole_fights_between_ais_end_and_replay_the_same()
    {
        string Fight(ulong seed)
        {
            var sim = Simulation.NewGame(TestContent.Repo, 1);
            var ctx = new SimContext(sim.Content, RngStreams.FromStates(sim.State.RngStates));
            var player = CombatSystem.Snapshot(sim.State.Player, ctx);
            var battle = Battle.Start([player], CombatSystem.Foes(ctx, "grp.beast.grey_wolves"), ctx.Combat, Xoshiro256StarStar.FromSeed(seed));
            var ai = Xoshiro256StarStar.FromSeed(seed + 1000);
            int turns = 0;
            while (!battle.IsOver)
            {
                BattleAi.PlayTurn(battle, ai);
                Assert.True(++turns <= ctx.Combat.MaxTurns);
            }
            return string.Join(";", battle.Fighters.Select(f => $"{f.Id}:{f.Health:0.###}:{f.Position}:{f.Fled}")) + $"|{battle.Turns}";
        }
        for (ulong seed = 1; seed <= 20; seed++)
            Assert.Equal(Fight(seed), Fight(seed));
    }
}
