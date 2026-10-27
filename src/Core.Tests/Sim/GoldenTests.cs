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
        const string expectedHash = "ccd0759ede4f8b312aacaf31956fa0dc03f1ad52ec766bb50302170b15c2907e";

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
        const string expectedHash = "e7c2180a6fdd2bcc1308078069925f722a48287fe623dac2ac7e19a967505990";

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
        const string expectedHash = "287535d68e6a21e883ade2cc97af282ea9d1ca2d708a74828056e30a77768ad0";
        var purify = new MeditateActivity("meditation.purification", "place.spirit_peak.vein");

        var sim = Simulation.NewGame(TestContent.Repo, 42);
        Assert.True(sim.Execute(new Buy("item.pill.spirit_dew")).Accepted);
        Assert.True(sim.Execute(new UsePill("item.pill.qi_gathering")).Accepted);
        Assert.True(sim.Execute(new UsePill("item.pill.qi_gathering")).Accepted);
        RunToEnd(sim, new Travel("zone.spirit_peak"));
        RunToEnd(sim, new StartActivity(purify, 400));
        Assert.True(sim.Execute(new UsePill("item.pill.spirit_dew")).Accepted);
        sim.Execute(new SetInterruptStops([InterruptCategory.StageScaleFull]));
        Command next = new StartActivity(new MeditateActivity("meditation.assimilation", "place.spirit_peak.vein"), null);
        while (true)
        {
            var outcome = sim.Execute(next);
            Assert.True(outcome.Accepted, outcome.RejectReason?.Key);
            if (outcome.Events.Any(e => e is StageScaleFull))
                break;
            next = TestContent.Proceed(sim.View);
        }
        while (sim.State.Event is not null)
            sim.Execute(TestContent.Proceed(sim.View));
        Assert.True(sim.Execute(new CancelActivity()).Accepted);
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
        var outcome = sim.Execute(command);
        Assert.True(outcome.Accepted, outcome.RejectReason?.Key);
        while (sim.State.Event is not null || sim.State.Activity is not null)
            Assert.True(sim.Execute(TestContent.Proceed(sim.View)).Accepted);
    }
}
