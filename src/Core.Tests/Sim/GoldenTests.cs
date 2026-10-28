using Core.Save;
using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

/// <summary>
/// Fixed seed + fixed command script → hash of the final state. A failure here means the rules
/// changed. Update the expected hash only deliberately, and say why in the commit message.
/// </summary>
public sealed class GoldenTests
{
    /// <summary>
    /// Same run as <c>sim run --seed 42 --travel zone.spirit_peak --weeks end --meditate meditation.purification</c>,
    /// which prints the same hash: interrupts are continued until the activity ends.
    /// </summary>
    [Fact]
    public void TravelToPeakThenPurifyUntilDeath_Seed42()
    {
        const string expectedHash = "008d36a0d5049580f6907bb60a8e0594556f0ff04b0dc743d75c2b47b09cc828";

        var sim = Simulation.NewGame(TestContent.Repo, 42);
        RunToEnd(sim, new Travel("zone.spirit_peak"));
        RunToEnd(sim, new StartActivity(new MeditateActivity("meditation.purification", "place.spirit_peak.vein"), null));

        Assert.Equal(GameMode.RunEnd, sim.State.Mode);
        Assert.Equal(expectedHash, SaveSerializer.HashState(sim.State));
    }

    /// <summary>
    /// Same run as <c>sim run --seed 42 --travel zone.spirit_peak --pills item.pill.qi_gathering,item.pill.qi_gathering
    /// --weeks end --meditate meditation.assimilation</c>: pills with diminishing energy feed the stage scale,
    /// and the assimilation ends itself once the pill energy is spent (D44).
    /// </summary>
    [Fact]
    public void TravelToPeakTakeTwoPillsThenAssimilateUntilSpent_Seed42()
    {
        const string expectedHash = "e1194138e2e4b30da9299ee0afa188fc55d9389898c62fc20c39e8ac12a8da15";

        var sim = Simulation.NewGame(TestContent.Repo, 42);
        RunToEnd(sim, new Travel("zone.spirit_peak"));
        Assert.True(sim.Execute(new UsePill("item.pill.qi_gathering")).Accepted);
        Assert.True(sim.Execute(new UsePill("item.pill.qi_gathering")).Accepted);
        RunToEnd(sim, new StartActivity(new MeditateActivity("meditation.assimilation", "place.spirit_peak.vein"), null));

        Assert.Equal(54, sim.State.Player.Tracks.Single().StageProgress);
        Assert.Equal(expectedHash, SaveSerializer.HashState(sim.State));
    }

    /// <summary>
    /// A first breakthrough end to end (no CLI equivalent yet): take the starting pills at the hub
    /// (carried, they could go to events on the way) and buy one more, purify at the peak until the
    /// foundation covers the threshold and the toxins are gone, take the bought pill, fill the scale, purify a little more, break through,
    /// purify until death. Events on the way are passed with their first available choice.
    /// </summary>
    [Fact]
    public void PurifyFillTheScaleAndBreakThroughThenPurifyUntilDeath_Seed42()
    {
        const string expectedHash = "2983186b36a0e2d98e2c5f477b30f6b71144a7ab281b7b2fc972de945aa73633";
        var purify = new MeditateActivity("meditation.purification", "place.spirit_peak.vein");

        var sim = Simulation.NewGame(TestContent.Repo, 42);
        Assert.True(sim.Execute(new Buy("item.pill.spirit_dew")).Accepted);
        Assert.True(sim.Execute(new UsePill("item.pill.qi_gathering")).Accepted);
        Assert.True(sim.Execute(new UsePill("item.pill.qi_gathering")).Accepted);
        RunToEnd(sim, new Travel("zone.spirit_peak"));
        RunToEnd(sim, new StartActivity(purify, 400));
        Assert.True(sim.Execute(new UsePill("item.pill.spirit_dew")).Accepted);
        // A full scale ends the assimilation itself (D44).
        RunToEnd(sim, new StartActivity(new MeditateActivity("meditation.assimilation", "place.spirit_peak.vein"), null));
        Assert.Equal(sim.State.Player.Tracks.Single().StageProgress, sim.View.Tracks.Single().ProgressRequired);
        RunToEnd(sim, new StartActivity(purify, 15));
        var forecast = sim.View.Tracks.Single().Forecast!;
        RunToEnd(sim, new AttemptBreakthrough("qi", "place.spirit_peak.vein"));
        var afterBreakthrough = (sim.State.Player.Tracks.Single().StageIndex, sim.State.Player.LifespanWeeks);
        RunToEnd(sim, new StartActivity(purify, null));

        Assert.Equal((0, "rank.common"), (forecast.FailureChance, forecast.RankId));
        Assert.Equal((1, 64 * 52), afterBreakthrough);
        Assert.Equal(GameMode.RunEnd, sim.State.Mode);
        Assert.Equal(expectedHash, SaveSerializer.HashState(sim.State));
    }

    /// <summary>Like <c>sim run</c>: restarts the activity after interrupts and takes the first available choice in events.</summary>
    private static void RunToEnd(Simulation sim, Command command) => Assert.True(Script.Run(sim, command));
}
