using Core.Content;
using Core.Loc;
using Core.Sim;

namespace Core.Tests.Sim;

public sealed class PlayerViewTextTests
{
    private static readonly ContentDb Repo = LoadRepoContent();

    private static ContentDb LoadRepoContent()
    {
        var loaded = ContentLoader.Load(ContentFiles.FromDirectory(RepoPaths.Content));
        Assert.True(loaded.Ok, string.Join("\n", loaded.Errors));
        return loaded.Db;
    }

    [Fact]
    public void NewGameSettings_FromRepoContent_StartsAtTwelveYears()
    {
        Assert.Equal(12 * 52, NewGameSettings.FromContent(Repo).StartAgeWeeks);
    }

    [Fact]
    public void Lines_ResolveToUkrainianText()
    {
        var sim = Simulation.NewGame(1, NewGameSettings.FromContent(Repo));
        sim.Execute(new WaitWeeks(55));
        var localizer = Localizer.FromContent(Repo);

        var text = PlayerViewText.Lines(sim.View, Repo).Select(localizer.Resolve);

        Assert.Equal(
            [
                "Тиждень забігу: 55",
                "Вік: 13 р. 3 тиж.",
                "Режим: Світ",
                "Ці: Перший етап ці, прогрес 0",
            ],
            text);
    }
}
