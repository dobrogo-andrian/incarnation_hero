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
    /// <summary>Same run as <c>sim run --seed 42 --weeks end</c>, which prints the same hash.</summary>
    [Fact]
    public void WaitUntilDeath_Seed42()
    {
        const string expectedHash = "a2778ed6151b07fa03648798148ea78af54020945da946cb8af245285d1e718d";

        var sim = Simulation.NewGame(TestContent.Repo, 42);
        sim.Execute(TestContent.Wait(null));
        while (sim.State.Mode != GameMode.RunEnd)
            sim.Execute(new ContinueActivity());

        Assert.Equal(expectedHash, SaveSerializer.HashState(sim.State));
    }
}
