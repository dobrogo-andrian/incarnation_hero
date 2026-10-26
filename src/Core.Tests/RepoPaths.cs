namespace Core.Tests;

/// <summary>Locates folders of the repository from the test output directory.</summary>
internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string Content => Path.Combine(Root, "content");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "IncarnationHero.sln")))
                return dir.FullName;
        }
        throw new DirectoryNotFoundException("IncarnationHero.sln not found above the test directory.");
    }
}
