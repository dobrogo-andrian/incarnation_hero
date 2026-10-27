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
        const string expectedHash = "97c29994b70f1147dc79fceb81944d0767e8717568427a484793038b3adec17d";

        var sim = Simulation.NewGame(TestContent.Repo, 42);
        RunToEnd(sim, new Travel("zone.spirit_peak"));
        RunToEnd(sim, new StartActivity(new MeditateActivity("meditation.purification", "place.spirit_peak.vein"), null));

        Assert.Equal(GameMode.RunEnd, sim.State.Mode);
        Assert.Equal(expectedHash, SaveSerializer.HashState(sim.State));
    }

    /// <summary>
    /// Same run as <c>sim run --seed 42 --travel zone.spirit_peak --pills item.pill.qi_gathering,item.pill.qi_gathering
    /// --weeks end --meditate meditation.assimilation</c>: pills with diminishing energy feed the stage scale.
    /// </summary>
    [Fact]
    public void TravelToPeakTakeTwoPillsThenAssimilateUntilDeath_Seed42()
    {
        const string expectedHash = "8318b56aa6367d17a13831a0187a939b51207621ca1df856132df8e11639c9ae";

        var sim = Simulation.NewGame(TestContent.Repo, 42);
        RunToEnd(sim, new Travel("zone.spirit_peak"));
        Assert.True(sim.Execute(new UsePill("item.pill.qi_gathering")).Accepted);
        Assert.True(sim.Execute(new UsePill("item.pill.qi_gathering")).Accepted);
        RunToEnd(sim, new StartActivity(new MeditateActivity("meditation.assimilation", "place.spirit_peak.vein"), null));

        Assert.Equal(54, sim.State.Player.Tracks.Single().StageProgress);
        Assert.Equal(expectedHash, SaveSerializer.HashState(sim.State));
    }

    /// <summary>
    /// A first breakthrough end to end (no CLI equivalent yet): buy pill energy at the hub, purify at
    /// the peak until the foundation covers the threshold, fill the scale, flush the pills' toxins,
    /// break through, purify until death.
    /// </summary>
    [Fact]
    public void PurifyFillTheScaleAndBreakThroughThenPurifyUntilDeath_Seed42()
    {
        const string expectedHash = "19d1f8f95b0eb9f5075289b9351840850fcf0fbbf7bdf11814593c39863e970f";
        var purify = new MeditateActivity("meditation.purification", "place.spirit_peak.vein");

        var sim = Simulation.NewGame(TestContent.Repo, 42);
        Assert.True(sim.Execute(new Buy("item.pill.spirit_dew")).Accepted);
        RunToEnd(sim, new Travel("zone.spirit_peak"));
        RunToEnd(sim, new StartActivity(purify, 400));
        foreach (string pill in new[] { "item.pill.qi_gathering", "item.pill.qi_gathering", "item.pill.spirit_dew" })
            Assert.True(sim.Execute(new UsePill(pill)).Accepted);
        sim.Execute(new SetInterruptStops([InterruptCategory.StageScaleFull]));
        Assert.True(sim.Execute(new StartActivity(new MeditateActivity("meditation.assimilation", "place.spirit_peak.vein"), null)).Accepted);
        sim.Execute(new CancelActivity());
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

    /// <summary>Like <c>sim run</c>: continues through interrupts and takes the first available choice in events.</summary>
    private static void RunToEnd(Simulation sim, Command command)
    {
        Assert.True(sim.Execute(command).Accepted);
        while (sim.State.Event is not null || sim.State.Activity is not null)
            Assert.True(sim.Execute(TestContent.Proceed(sim.View)).Accepted);
    }
}
