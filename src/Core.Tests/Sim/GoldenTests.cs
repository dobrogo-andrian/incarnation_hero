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
        const string expectedHash = "448ff4a5e2540c84d5ac392479c984d199a686a3f2d06847abeb024caa25927f";

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
        const string expectedHash = "e6e3d2f237ea7f13394388fae1337da49733c5d5bd68f3cb99341d105216cbcf";

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
        const string expectedHash = "f0c9645337b4ad8e1417eb1380189325a70dc63818ef7de051262db0d5103a88";
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

    private static void RunToEnd(Simulation sim, Command command)
    {
        Assert.True(sim.Execute(command).Accepted);
        while (sim.State.Activity is not null)
            sim.Execute(new ContinueActivity());
    }
}
