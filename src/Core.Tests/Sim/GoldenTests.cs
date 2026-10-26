using Core.Content;
using Core.Save;
using Core.Sim;

namespace Core.Tests.Sim;

/// <summary>
/// Fixed seed + fixed command script → hash of the final state. A failure here means the rules
/// changed. Update the expected hash only deliberately, and say why in the commit message.
/// </summary>
public sealed class GoldenTests
{
    [Fact]
    public void WaitTenYears_Seed42()
    {
        const string expectedHash = "2c7aa769f2e905cb2e17863d3ba72cf91a7e84570bf2699e5bc5ec65cc3b746c";

        var loaded = ContentLoader.Load(ContentFiles.FromDirectory(RepoPaths.Content));
        var sim = Simulation.NewGame(42, NewGameSettings.FromContent(loaded.Db));
        for (int i = 0; i < 10; i++)
            sim.Execute(new WaitWeeks(52));

        Assert.Equal(expectedHash, SaveSerializer.HashState(sim.State));
    }
}
