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
    /// Same run as <c>sim run --seed 42 --travel zone.spirit_peak --weeks end</c>, which prints the
    /// same hash: interrupts are continued until the activity ends.
    /// </summary>
    [Fact]
    public void TravelToPeakThenWaitUntilDeath_Seed42()
    {
        const string expectedHash = "d2e79e6e134ea55844342093d9054acf6004452a5f1b485f87bec1b1814dda3b";

        var sim = Simulation.NewGame(TestContent.Repo, 42);
        RunToEnd(sim, new Travel("zone.spirit_peak"));
        RunToEnd(sim, TestContent.Wait(null));

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
