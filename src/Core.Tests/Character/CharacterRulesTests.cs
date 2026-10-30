using Core.Character;
using Core.Sim;
using Core.State;

namespace Core.Tests.Character;

public sealed class CharacterRulesTests
{
    private static Simulation NewGame() => Simulation.NewGame(TestContent.Repo, 1);

    private static Modifier Mod(string target, ModifierOp op, double value, string source = "test.source", int? weeks = null) =>
        new() { TargetId = target, Op = op, Value = value, SourceId = source, WeeksLeft = weeks };

    [Fact]
    public void NewCharacter_StartsWithStatsFromContentAndFullResourcesButNoToxins()
    {
        var player = NewGame().State.Player;

        Assert.Equal(
            ["stat.might", "stat.endurance", "stat.speed", "stat.meridians", "stat.perception", "stat.purity"],
            player.Stats.Select(s => s.StatId));
        Assert.Equal(10, player.Stats[0].Base);
        Assert.Equal(0, player.Stats[4].Base);
        Assert.Equal("aspect_profile.balanced", player.AspectProfileId);
        Assert.Equal(
            [("resource.health", 100.0), ("resource.body_energy", 15.0), ("resource.qi_pool", 100.0), ("resource.combat_qi", 50.0), ("resource.toxins", 0.0)],
            player.Resources.Select(r => (r.ResourceId, r.Current)));
    }

    [Fact]
    public void EvenProfile_GivesEachAspectTheFullStat()
    {
        var sim = NewGame();

        Assert.Equal(10, sim.Rules.Value(sim.State.Player, "aspect.might.vitality"));
        Assert.Equal(10, sim.Rules.Value(sim.State.Player, "aspect.might.resilience"));
    }

    [Fact]
    public void UnevenProfile_ShiftsAspectsWithoutChangingTheirSum()
    {
        var content = TestContent.WithExtra("""
            id: aspect_profile.test
            name: { uk: "Тест" }
            shares:
              aspect.might.vitality: 0.6
              aspect.might.resilience: 0.4
              aspect.endurance.reserve: 0.5
              aspect.endurance.output: 0.5
              aspect.speed.reaction: 0.5
              aspect.speed.agility: 0.5
              aspect.meridians.capacity: 0.5
              aspect.meridians.flow: 0.5
            """);
        var sim = Simulation.NewGame(content, 1);
        var player = sim.State.Player;
        player.AspectProfileId = "aspect_profile.test";

        Assert.Equal(12, sim.Rules.Value(player, "aspect.might.vitality"), 9);
        Assert.Equal(8, sim.Rules.Value(player, "aspect.might.resilience"), 9);
        Assert.Equal(120, sim.Rules.Value(player, "resource.health"), 9);
    }

    [Fact]
    public void ModifierRule_AddsFlatThenAddsPercentages()
    {
        var result = CharacterRules.ApplyModifiers("x", 10,
        [
            Mod("x", ModifierOp.Add, 5),
            Mod("x", ModifierOp.Mul, 0.1),
            Mod("x", ModifierOp.Mul, 0.1),
            Mod("other", ModifierOp.Add, 100),
        ]);

        Assert.Equal(18, result.Value, 9);
        Assert.Equal(10, result.Base);
        Assert.Equal(3, result.Modifiers.Count);
    }

    [Fact]
    public void ModifierRule_LowestCapLimits_AndLatestOverrideWins()
    {
        var capped = CharacterRules.ApplyModifiers("x", 10, [Mod("x", ModifierOp.Cap, 8), Mod("x", ModifierOp.Cap, 6), Mod("x", ModifierOp.Add, 5)]);
        var overridden = CharacterRules.ApplyModifiers("x", 10, [Mod("x", ModifierOp.Override, 3), Mod("x", ModifierOp.Cap, 1), Mod("x", ModifierOp.Override, 4)]);

        Assert.Equal(6, capped.Value);
        Assert.Equal(4, overridden.Value);
    }

    [Fact]
    public void StatModifier_FlowsIntoAspectsAndResourceMaximum()
    {
        var sim = NewGame();
        var player = sim.State.Player;

        ModifierSystem.Apply(player, Mod("stat.might", ModifierOp.Add, 2, source: "buff.test"));

        Assert.Equal(12, sim.Rules.Value(player, "aspect.might.vitality"));
        var health = sim.Rules.Explain(player, "resource.health");
        Assert.Equal(120, health.Value);
        Assert.Empty(health.Modifiers);
        var might = sim.Rules.Explain(player, "stat.might");
        Assert.Equal(new ModifierPart("buff.test", ModifierOp.Add, 2), Assert.Single(might.Modifiers));
    }

    [Fact]
    public void Apply_ReplacesModifierFromSameSourceOnSameTarget()
    {
        var player = NewGame().State.Player;

        ModifierSystem.Apply(player, Mod("stat.might", ModifierOp.Add, 2, source: "a"));
        ModifierSystem.Apply(player, Mod("stat.speed", ModifierOp.Add, 1, source: "a"));
        ModifierSystem.Apply(player, Mod("stat.might", ModifierOp.Add, 5, source: "a"));

        Assert.Equal([("stat.speed", 1.0), ("stat.might", 5.0)], player.Modifiers.Select(m => (m.TargetId, m.Value)));
    }

    [Fact]
    public void TimedModifier_ExpiresAfterItsWeeks()
    {
        var sim = NewGame();
        var player = sim.State.Player;
        ModifierSystem.Apply(player, Mod("stat.speed", ModifierOp.Add, 3, weeks: 2));
        ModifierSystem.Apply(player, Mod("stat.might", ModifierOp.Add, 1));

        sim.Execute(TestContent.Wait(1));
        Assert.Equal(13, sim.Rules.Value(player, "stat.speed"));

        sim.Execute(TestContent.Wait(1));
        Assert.Equal(10, sim.Rules.Value(player, "stat.speed"));
        Assert.Equal("stat.might", Assert.Single(player.Modifiers).TargetId);
    }
}
