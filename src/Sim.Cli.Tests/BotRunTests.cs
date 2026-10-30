using System.Text.Json;
using Core.Content;
using Core.Save;
using Core.State;
using Core.Tests;
using Sim.Cli.Bots;
using Sim.Cli.Runs;

namespace Sim.Cli.Tests;

public sealed class BotRunTests
{
    private static readonly ContentDb Repo = ContentLoader.Load(ContentFiles.FromDirectory(RepoPaths.Content)).Db;

    public static IEnumerable<object[]> Bots => BotRegistry.Names.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(Bots))]
    public void Play_SameSeedAndBot_SameRun(string bot)
    {
        var (first, firstSim) = BotRunner.Play(Repo, bot, 7);
        var (second, secondSim) = BotRunner.Play(Repo, bot, 7);

        Assert.Equal(GameMode.RunEnd, firstSim.State.Mode);
        Assert.Equal(BotRunner.ToJsonLine(first), BotRunner.ToJsonLine(second));
        Assert.Equal(SaveSerializer.HashState(firstSim.State), SaveSerializer.HashState(secondSim.State));
    }

    [Theory]
    [MemberData(nameof(Bots))]
    public void Summary_AccountsForEveryStoneAndWeek(string bot)
    {
        foreach (var summary in BotRunner.PlayMany(Repo, bot, 1, 8))
        {
            var stones = summary.Stones;
            Assert.Equal(stones.End - stones.Start, stones.Earned.Values.Sum() - stones.Spent.Values.Sum());
            Assert.Equal(summary.Weeks, summary.ActivityWeeks.Values.Sum());
            Assert.Equal(summary.Combat.Fights, summary.Combat.ByGroup.Values.Sum(g => g.Won + g.Fled + g.Beaten + g.Died));
        }
    }

    [Fact]
    public void PlayMany_MatchesSequentialRunsInSeedOrder()
    {
        var parallel = BotRunner.PlayMany(Repo, "random", 100, 6);

        for (int i = 0; i < parallel.Length; i++)
            Assert.Equal(BotRunner.ToJsonLine(BotRunner.Play(Repo, "random", 100 + (ulong)i).Summary), BotRunner.ToJsonLine(parallel[i]));
    }

    [Fact]
    public void Fighter_FightsMoreThanCautious()
    {
        int Fights(string bot) => BotRunner.PlayMany(Repo, bot, 1, 10).Sum(s => s.Combat.Fights);

        Assert.True(Fights("fighter") > 2 * Fights("cautious"));
    }

    [Fact]
    public void Fighter_RollsAHiddenAggressionPerRun_CautiousDoesNot()
    {
        var fighters = BotRunner.PlayMany(Repo, "fighter", 1, 6);

        Assert.All(fighters, s => Assert.InRange(s.Aggression!.Value, 0, 1));
        Assert.True(fighters.Select(s => s.Aggression).Distinct().Count() > 1);
        Assert.Null(BotRunner.Play(Repo, "cautious", 1).Summary.Aggression);
    }

    [Fact]
    public void Aggression_MovesThresholdsBetweenTheTempersEnds()
    {
        var fighter = BotProfile.Fighter;
        var careful = fighter.WithAggression(0);
        var reckless = fighter.WithAggression(1);

        Assert.Equal(0.95, careful.MinHealthToFight, 6);
        Assert.Equal(0.35, reckless.MinHealthToFight, 6);
        Assert.Equal(0.1, careful.HealAt, 6);
        Assert.Equal(0.5, reckless.HealAt, 6);
        Assert.Equal(0, careful.EvenFightChance);
        Assert.Equal(1, reckless.EvenFightChance);
        Assert.Equal(0.3, fighter.WithAggression(0.5).HealAt, 6);
        Assert.Same(BotProfile.Cautious, BotProfile.Cautious.WithAggression(1));
    }

    [Fact]
    public void Trace_HasOneTypedLinePerCommand()
    {
        var trace = new StringWriter();
        var (summary, _) = BotRunner.Play(Repo, "cautious", 3, trace);

        var lines = trace.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(lines);
        foreach (string line in lines)
        {
            using var json = JsonDocument.Parse(line);
            Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("command").GetProperty("type").GetString()));
        }
        int weeks = lines.Sum(l => JsonDocument.Parse(l).RootElement.GetProperty("events").EnumerateArray()
            .Count(e => e.GetProperty("type").GetString() == "WeekPassed"));
        Assert.Equal(summary.Weeks, weeks);
    }

    /// <summary>
    /// Golden runs (tech 6.3): fixed seed + fixed bot → hash of the final state, the same as
    /// <c>sim run --bot &lt;bot&gt; --trace 42</c> plays. A change means the rules or a bot changed;
    /// update the hash only deliberately.
    /// </summary>
    [Theory]
    [InlineData("cautious", "0d61f48b1014b185d153cd04b0ca9180d95748a47500d0c84756db5b03164e8c")]
    [InlineData("fighter", "c0f1d74e6ab5f01566fed4fd823f89d02d2515d8504b0c819b725b4a6846d4e4")]
    [InlineData("random", "c1a81c3cf17870c906701c9106c9ffa51561de854f620af66452f9e82cad90b0")]
    public void Golden_Seed42(string bot, string expectedHash)
    {
        var (_, sim) = BotRunner.Play(Repo, bot, 42);

        Assert.Equal(expectedHash, SaveSerializer.HashState(sim.State));
    }
}
