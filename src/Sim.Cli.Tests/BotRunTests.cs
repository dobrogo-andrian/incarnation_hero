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
            Assert.Equal(summary.Combat.Fights, summary.Combat.ByGroup.Values.Sum(g => g.Won + g.Fled + g.Died));
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
    [InlineData("cautious", "48e7d104c4b9f2f06ccbf47e2563e65538734ae7fcebab1abf70c9ea37c9c5d5")]
    [InlineData("fighter", "b0c63f1125b0f8d003f1c79cba09a77a8d900486a6a023814648dac8ec9e5280")]
    [InlineData("random", "2bb1ef8dc7dc445b74721651d3d32ddb81c036746ad12cc170b61c470fd93784")]
    public void Golden_Seed42(string bot, string expectedHash)
    {
        var (_, sim) = BotRunner.Play(Repo, bot, 42);

        Assert.Equal(expectedHash, SaveSerializer.HashState(sim.State));
    }
}
