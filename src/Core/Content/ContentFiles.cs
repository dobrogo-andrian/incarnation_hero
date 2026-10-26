namespace Core.Content;

/// <summary>Reads content files from disk for tests, Sim.Cli and the dev build.</summary>
public static class ContentFiles
{
    /// <summary>All <c>*.yaml</c> files under <paramref name="root"/>, with '/'-separated relative paths.</summary>
    public static IReadOnlyList<ContentFile> FromDirectory(string root) =>
        Directory.EnumerateFiles(root, "*.yaml", SearchOption.AllDirectories)
            .Select(path => new ContentFile(
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                File.ReadAllText(path)))
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .ToList();
}
