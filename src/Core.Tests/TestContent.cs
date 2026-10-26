using Core.Content;
using Core.Sim;
using Core.State;

namespace Core.Tests;

/// <summary>The repository content, loaded once, plus shorthands shared by tests.</summary>
internal static class TestContent
{
    public static ContentDb Repo { get; } = Load();

    public static RunConfigDef Config => Repo.Get<RunConfigDef>(RunConfigDef.RunId);

    public static StartActivity Wait(int? weeks) => new(new WaitActivity(), weeks);

    private static ContentDb Load()
    {
        var loaded = ContentLoader.Load(ContentFiles.FromDirectory(RepoPaths.Content));
        if (!loaded.Ok)
            throw new InvalidOperationException(string.Join("\n", loaded.Errors));
        return loaded.Db;
    }
}
