using Core.Combat;
using Core.Content;
using Core.Rng;
using Core.Sim;

namespace Core.Tests.Combat;

public class TechniqueTests
{
    private const string Line = "tech.spear.piercing_dragon";
    private const string Arc = "tech.sword.flowing_cut";
    private const string Charge = "tech.movement.wind_step";
    private const string Retreat = "tech.movement.crane_retreat";

    private static CombatConfigDef Field(int radius)
    {
        var config = TestContent.WithExtra("- id: str.test.field\n  text: { uk: \"x\" }").Get<CombatConfigDef>(CombatConfigDef.CombatId);
        config.FieldRadius = radius;
        return config;
    }

    private static CombatTechnique Known(string id, int level = 1) =>
        new(id, level, TestContent.Repo.Get<KnowledgeDef>(id).Combat!);

    private static Combatant Fighter(string id, double reaction = 10, double shield = 30, double damage = 10, params CombatTechnique[] techniques) =>
        new(id, 0, 100, 100, shield, shield, 0, 0, damage, reaction, 10, 10, 100, 5, 15, "injury.body", 10, techniques);

    private static Battle Start(CombatConfigDef config, Combatant player, int wolves = 1) =>
        Battle.Start([player], Enumerable.Range(1, wolves).Select(i => Fighter($"wolf#{i}", shield: 0)).ToList(), config, Xoshiro256StarStar.FromSeed(11));

    private static Combatant Player(params CombatTechnique[] techniques) => Fighter("player", reaction: 20, techniques: techniques);

    [Fact]
    public void Line_pierces_every_enemy_on_its_hexes()
    {
        // Radius 2: wolves at (0,-1) and (1,-1); from (-1,-1) the line runs through both.
        var battle = Start(Field(2), Player(Known(Line)), wolves: 2);
        Assert.True(battle.Execute(new Move(new Hex(-1, -1))).Accepted);
        double shield = battle.Fighters[0].Shield;
        var outcome = battle.Execute(new UseTechnique(Line, new Hex(0, -1)));
        Assert.True(outcome.Accepted);
        Assert.Equal(["wolf#1", "wolf#2"], outcome.Events.OfType<FighterHit>().Select(h => h.TargetId).ToList());
        Assert.Equal(shield - 10, battle.Fighters[0].Shield, 9);
    }

    [Fact]
    public void Arc_strikes_the_aimed_hex_and_both_beside_it()
    {
        // Radius 1: the player in the centre, wolves at (1,0), (1,-1) and (0,-1).
        var battle = Start(Field(1), Player(Known(Arc, level: 2)), wolves: 3);
        var outcome = battle.Execute(new UseTechnique(Arc, new Hex(1, -1)));
        var hits = outcome.Events.OfType<FighterHit>().ToList();
        Assert.Equal(3, hits.Count);
        // Weapon 10 × (1 + 2 × 0.15) × 1.0, with a ±20% spread.
        Assert.All(hits, h => Assert.InRange(h.Damage, 13 * 0.8, 13 * 1.2));
    }

    [Fact]
    public void Charge_dashes_straight_in_without_free_attacks_and_strikes()
    {
        var battle = Start(Field(4), Player(Known(Charge)));
        Assert.Equal(RejectReasons.InvalidTarget, battle.Execute(new UseTechnique(Charge, new Hex(0, -3))).RejectReason);
        battle.Execute(new Move(new Hex(0, 1)));

        var outcome = battle.Execute(new UseTechnique(Charge, new Hex(0, -3)));

        Assert.True(outcome.Accepted);
        Assert.Equal(3, outcome.Events.OfType<FighterMoved>().Count());
        Assert.Empty(outcome.Events.OfType<FreeAttackProvoked>());
        Assert.Equal("wolf#1", Assert.Single(outcome.Events.OfType<FighterHit>()).TargetId);
        Assert.Equal(new Hex(0, -2), battle.Fighters[0].Position);
        Assert.Equal(1, battle.Fighters[0].MovePoints);
    }

    [Fact]
    public void Retreat_strikes_then_leaps_away_without_a_free_attack()
    {
        var battle = Start(Field(1), Player(Known(Retreat)));
        var outcome = battle.Execute(new UseTechnique(Retreat, new Hex(1, 0)));
        Assert.True(outcome.Accepted);
        Assert.Empty(outcome.Events.OfType<FreeAttackProvoked>());
        // Two hexes back would leave the field; it lands on the farthest free one.
        Assert.Equal(new FighterMoved("player", new Hex(0, 0), new Hex(-1, 0)), Assert.Single(outcome.Events.OfType<FighterMoved>()));
    }

    [Fact]
    public void Techniques_need_to_be_known_aimed_at_an_enemy_and_paid_in_combat_qi()
    {
        var battle = Start(Field(1), Player(Known(Arc)));
        Assert.Equal(RejectReasons.UnknownTechnique, battle.Execute(new UseTechnique(Line, new Hex(1, 0))).RejectReason);
        Assert.Equal(RejectReasons.InvalidTarget, battle.Execute(new UseTechnique(Arc, new Hex(-1, 1))).RejectReason);

        var empty = Start(Field(1), Fighter("player", reaction: 20, shield: 5, techniques: Known(Arc)));
        Assert.Equal(RejectReasons.NotEnoughQi, empty.Execute(new UseTechnique(Arc, new Hex(1, 0))).RejectReason);
        Assert.True(empty.Execute(new Attack("wolf#1")).Accepted);
        Assert.Equal(RejectReasons.ActionTaken, empty.Execute(new UseTechnique(Arc, new Hex(1, 0))).RejectReason);
    }

    [Fact]
    public void The_basic_attack_is_the_weapon_alone()
    {
        // Damage (the formula resolver's, with the technique bonus) is ignored on the hex field.
        var battle = Start(Field(1), Fighter("player", reaction: 20, damage: 99));
        var hit = battle.Execute(new Attack("wolf#1")).Events.OfType<FighterHit>().Single();
        Assert.InRange(hit.Damage, 8, 12);
    }

    [Fact]
    public void Snapshots_carry_the_techniques_fighters_know()
    {
        var sim = Simulation.NewGame(TestContent.Repo, 1);
        sim.State.Player.Knowledge.Add(new Core.State.KnowledgeLevel { KnowledgeId = Charge, Level = 2 });
        var ctx = new SimContext(sim.Content, RngStreams.FromStates(sim.State.RngStates));
        var player = CombatSystem.Snapshot(sim.State.Player, ctx);
        Assert.Contains(player.Techniques, t => t.Id == Charge && t.Level == 2);
        Assert.Equal(10, player.WeaponDamage);

        var hunter = CombatSystem.Foes(ctx, "grp.human.relic_hunters")[0];
        Assert.Equal([Retreat], hunter.Techniques.Select(t => t.Id));
        Assert.Empty(CombatSystem.Foes(ctx, "grp.beast.grey_wolves")[0].Techniques);
    }
}
