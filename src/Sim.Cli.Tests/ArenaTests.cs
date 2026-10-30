using Core.Content;
using Core.Tests;
using Sim.Cli.Arena;

namespace Sim.Cli.Tests;

public sealed class ArenaTests
{
    private static readonly ContentDb Repo = ContentLoader.Load(ContentFiles.FromDirectory(RepoPaths.Content)).Db;

    private static double PlayerWins(string group, int fights = 200)
    {
        var results = ArenaRunner.FightMany(Repo, ArenaRunner.Side(Repo, ArenaRunner.PlayerSide, "a")!, ArenaRunner.Side(Repo, group, "b")!, 1, fights);
        return results.Count(f => f.Winner == "a") / (double)fights;
    }

    [Fact]
    public void Same_seed_same_fight()
    {
        var a = ArenaRunner.Side(Repo, ArenaRunner.PlayerSide, "a")!;
        var b = ArenaRunner.Side(Repo, "grp.bandits.road", "b")!;
        Assert.Equal(ArenaRunner.ToJsonLine(ArenaRunner.Fight(Repo, a, b, 5)), ArenaRunner.ToJsonLine(ArenaRunner.Fight(Repo, a, b, 5)));
        Assert.Equal(ArenaRunner.FightMany(Repo, a, b, 1, 20).Select(ArenaRunner.ToJsonLine), ArenaRunner.FightMany(Repo, a, b, 1, 20).Select(ArenaRunner.ToJsonLine));
    }

    [Fact]
    public void Every_fight_ends_one_way()
    {
        var a = ArenaRunner.Side(Repo, "grp.human.relic_hunters", "a")!;
        var b = ArenaRunner.Side(Repo, "grp.human.relic_hunters", "b")!;
        var fights = ArenaRunner.FightMany(Repo, a, b, 1, 50);
        Assert.All(fights, f => Assert.Contains((f.Winner, f.End), new[] { ("a", "down"), ("a", "fled"), ("b", "down"), ("b", "fled"), ("none", "timeout") }));
        Assert.Equal(9, ArenaRunner.Report(fights).Count());
    }

    [Fact]
    public void Sides_are_groups_profiles_or_the_player_and_a_group_can_meet_itself()
    {
        Assert.Null(ArenaRunner.Side(Repo, "grp.nobody", "a"));
        Assert.Equal("a.player", Assert.Single(ArenaRunner.Side(Repo, "profile.sandbox.novice", "a")!).Id);
        var a = ArenaRunner.Side(Repo, "grp.beast.grey_wolves", "a")!;
        var b = ArenaRunner.Side(Repo, "grp.beast.grey_wolves", "b")!;
        Assert.Empty(a.Select(f => f.Id).Intersect(b.Select(f => f.Id)));
    }

    [Fact]
    public void Weaker_enemies_lose_to_the_hero_more_often()
    {
        Assert.True(PlayerWins("grp.beast.red_foxes") > PlayerWins("grp.human.tomb_keeper"));
    }
}
