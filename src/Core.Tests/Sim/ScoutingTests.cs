using Core.Content;
using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

/// <summary>Reading the foes before a fight (D70): five words against the hero, made reliable by perception.</summary>
public sealed class ScoutingTests
{
    private static readonly ContentDb Content = TestContent.WithExtra("""
        - id: enemy.test.glass
          name: { uk: "Скляний" }
          ai: ai.beast_aggressive
          stats: { stat.might: 0.5 }
          weapon_damage: 300
        - id: enemy.test.wall
          name: { uk: "Непробивний" }
          ai: ai.cultivator_cautious
          stats: { stat.might: 100000 }
          weapon_damage: 0.1
          techniques: { tech.spear.piercing_dragon: 1 }
        - id: enemy.test.adept
          name: { uk: "Адепт" }
          ai: ai.beast_aggressive
          stage: 1
          stats: { stat.might: 6 }
          techniques: { tech.spear.piercing_dragon: 1 }
        - id: grp.test.mixed
          name: { uk: "Змішані" }
          members: [ { enemy: enemy.test.glass, count: 2 }, { enemy: enemy.test.wall } ]
        - id: grp.test.adepts
          name: { uk: "Адепти" }
          members: [ { enemy: enemy.test.adept } ]
        """);

    private static FightScouting Scout(string groupId, double perception, ulong seed = 1)
    {
        var sim = Simulation.NewGame(Content, seed);
        TestContent.SetStat(sim, "stat.perception", perception);
        var ctx = new SimContext(Content, Core.Rng.RngStreams.FromStates(sim.State.RngStates));
        return Scouting.Of(sim.State.Player, ctx, CombatSystem.NewFight(ctx, groupId, FightKind.Event));
    }

    [Fact]
    public void KeenPerception_ReadsEveryFoeTruly_KindByKind()
    {
        var scouting = Scout("grp.test.mixed", 20);

        Assert.Equal(1, scouting.Reliability);
        var (glass, wall) = (scouting.Foes[0], scouting.Foes[1]);
        Assert.Equal(("enemy.test.glass", 2, StageRelation.Equal), (glass.EnemyId, glass.Count, glass.Stage));
        Assert.Equal((Comparison.FarBelow, Comparison.FarAbove), (glass.Vitality, glass.Strike));
        Assert.Equal((Comparison.FarAbove, Comparison.FarBelow), (wall.Vitality, wall.Strike));
        Assert.Equal(Comparison.FarBelow, wall.Speed);   // no speed stat: next to no reaction
        Assert.Equal(["tech.spear.piercing_dragon"], wall.Techniques);
        Assert.Empty(glass.Techniques!);
    }

    [Fact]
    public void AFoeOfAHigherStage_CannotBeRead()
    {
        var adept = Scout("grp.test.adepts", 20).Foes.Single();

        Assert.Equal(StageRelation.Higher, adept.Stage);
        Assert.Equal((null, null, null), (adept.Vitality, adept.Strike, adept.Speed));
        Assert.Null(adept.Techniques);
    }

    [Fact]
    public void TechniquesShow_OnlyFromTheirPerception()
    {
        Assert.Null(Scout("grp.test.mixed", 1.9).Foes[1].Techniques);
        Assert.NotNull(Scout("grp.test.mixed", 2).Foes[1].Techniques);
    }

    [Fact]
    public void WithoutPerception_AboutHalfTheWordsAreAStepOff_FixedForTheFight()
    {
        var truth = Scout("grp.test.mixed", 20).Foes[1];
        int off = 0, words = 0;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var scouting = Scout("grp.test.mixed", 0, seed);
            Assert.Equal(0.5, scouting.Reliability);
            Assert.Equal(scouting, Scout("grp.test.mixed", 0, seed), new ScoutingComparer());
            var wall = scouting.Foes[1];
            foreach (var (read, real) in new[] { (wall.Vitality, truth.Vitality), (wall.Strike, truth.Strike), (wall.Speed, truth.Speed) })
            {
                Assert.InRange(Math.Abs((int)read!.Value - (int)real!.Value), 0, 1);
                off += read == real ? 0 : 1;
                words++;
            }
        }
        Assert.InRange(off / (double)words, 0.4, 0.6);
    }

    /// <summary>Scoutings are equal when every foe reads the same; records compare their lists by reference.</summary>
    private sealed class ScoutingComparer : IEqualityComparer<FightScouting>
    {
        public bool Equals(FightScouting? a, FightScouting? b) =>
            a!.Reliability == b!.Reliability && a.Foes.Zip(b.Foes).All(p => p.First with { Techniques = null } == p.Second with { Techniques = null });

        public int GetHashCode(FightScouting obj) => 0;
    }
}
