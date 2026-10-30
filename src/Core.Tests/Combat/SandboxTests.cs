using Core.Combat;
using Core.Loc;
using Core.Sim;

namespace Core.Tests.Combat;

public class SandboxTests
{
    private static readonly Localizer Loc = Localizer.FromContent(TestContent.Repo);

    /// <summary>Plays a sandbox fight to the end, the player only waiting, and returns every log line.</summary>
    private static List<string> PlayOut(BattleSession sandbox)
    {
        var battle = sandbox.Battle;
        var lines = new List<string>();
        void Log(IEnumerable<SimEvent> events) =>
            lines.AddRange(events.Select(e => BattleText.Describe(battle, e)).OfType<LocKey>().Select(Loc.Resolve));
        Log(sandbox.Opening);
        while (!battle.IsOver)
            Log(sandbox.Act(new EndTurn()).Events);
        return lines;
    }

    [Fact]
    public void Profiles_are_heroes_with_every_technique()
    {
        var profiles = CombatSandbox.Profiles(TestContent.Repo);
        Assert.Equal(["profile.sandbox.novice", "profile.sandbox.stage_1", "profile.sandbox.stage_2"], profiles);
        var novice = CombatSandbox.Profile(TestContent.Repo, profiles[0]);
        Assert.Equal(Combatant.PlayerId, novice.Id);
        Assert.Equal(4, novice.Techniques.Count);
        Assert.True(CombatSandbox.Profile(TestContent.Repo, profiles[2]).WeaponDamage > novice.WeaponDamage);
    }

    [Fact]
    public void The_hero_snapshot_is_the_runs_player()
    {
        var sim = Simulation.NewGame(TestContent.Repo, 3);
        var hero = CombatSandbox.Hero(sim);
        Assert.Equal(Combatant.PlayerId, hero.Id);
        Assert.Equal(100, hero.MaxHealth);
    }

    [Fact]
    public void A_fight_plays_out_the_same_on_the_same_seed_and_every_line_resolves()
    {
        var hero = CombatSandbox.Profile(TestContent.Repo, "profile.sandbox.novice");
        var first = PlayOut(CombatSandbox.Start(TestContent.Repo, hero, "grp.beast.grey_wolves", 9));
        Assert.Equal(first, PlayOut(CombatSandbox.Start(TestContent.Repo, hero, "grp.beast.grey_wolves", 9)));
        Assert.Contains("Хід: Герой.", first);
        Assert.Contains(first, l => l.Contains("Сірий вовк 2"));
        Assert.All(first, l => Assert.DoesNotContain("{", l));
    }

    [Fact]
    public void A_lone_enemy_has_no_number()
    {
        var sandbox = CombatSandbox.Start(TestContent.Repo, CombatSandbox.Profile(TestContent.Repo, "profile.sandbox.novice"), "grp.beast.lone_wolf", 1);
        var wolf = sandbox.Battle.Fighters[1];
        Assert.DoesNotContain("#", Loc.Resolve(BattleText.Name(sandbox.Battle, wolf.Id)));
        Assert.DoesNotContain(" 1", Loc.Resolve(BattleText.Name(sandbox.Battle, wolf.Id)));
    }

    [Fact]
    public void The_fight_only_ever_waits_for_the_player()
    {
        var sandbox = CombatSandbox.Start(TestContent.Repo, CombatSandbox.Profile(TestContent.Repo, "profile.sandbox.stage_2"), "grp.beast.wild_boar", 1);
        Assert.Equal(0, sandbox.Battle.Active!.Side);
        var events = new List<SimEvent>();
        for (int turn = 0; turn < 4 && !sandbox.Battle.IsOver; turn++)
        {
            events.AddRange(turn % 2 == 0 ? sandbox.Act(new EndTurn()).Events : sandbox.PlayTurnByAi());
            Assert.True(sandbox.Battle.IsOver || sandbox.Battle.Active!.Side == 0);
        }
        Assert.Contains(events, e => e is BattleTurnStarted s && s.FighterId != Combatant.PlayerId);
    }
}
