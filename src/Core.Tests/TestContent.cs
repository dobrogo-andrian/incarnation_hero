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

    /// <summary>The repository content plus one extra file, for tests that need objects the game does not have.</summary>
    public static ContentDb WithExtra(string yaml) => Load(new ContentFile("test/extra.yaml", yaml));

    private static ContentDb Load(params ContentFile[] extra)
    {
        var loaded = ContentLoader.Load(ContentFiles.FromDirectory(RepoPaths.Content).Concat(extra));
        if (!loaded.Ok)
            throw new InvalidOperationException(string.Join("\n", loaded.Errors));
        return loaded.Db;
    }
}
