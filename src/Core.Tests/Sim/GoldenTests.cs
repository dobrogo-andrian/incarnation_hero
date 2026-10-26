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
        const string expectedHash = "de6691647cf2bb77768b131cb32da5bb4d01b8b8e811ed003c2f5bc8019aebea";

        var sim = Simulation.NewGame(TestContent.Repo, 42);
        RunToEnd(sim, new Travel("zone.spirit_peak"));
        RunToEnd(sim, new StartActivity(new MeditateActivity("meditation.purification", "place.spirit_peak.vein"), null));

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
