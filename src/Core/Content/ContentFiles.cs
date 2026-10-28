using System.Reflection;

namespace Core.Content;

/// <summary>Collects content files: the copy shipped inside Core, or a directory on disk.</summary>
public static class ContentFiles
{
    /// <summary>Resource name prefix of the YAML that <c>Core.csproj</c> embeds from <c>/content</c>.</summary>
    private const string EmbeddedPrefix = "content/";

    /// <summary>
    /// The content shipped with the game: every <c>/content</c> YAML embedded in Core at build time,
    /// so the editor run and the exported game read the same files and the player never sees loose YAML.
    /// </summary>
    public static IReadOnlyList<ContentFile> Embedded()
    {
        var assembly = typeof(ContentFiles).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(EmbeddedPrefix, StringComparison.Ordinal))
            .Select(name => new ContentFile(name[EmbeddedPrefix.Length..].Replace('\\', '/'), ReadResource(assembly, name)))
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>All <c>*.yaml</c> files under <paramref name="root"/>, with '/'-separated relative paths (tests, Sim.Cli).</summary>
    public static IReadOnlyList<ContentFile> FromDirectory(string root) =>
        Directory.EnumerateFiles(root, "*.yaml", SearchOption.AllDirectories)
            .Select(path => new ContentFile(
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                File.ReadAllText(path)))
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .ToList();

    private static string ReadResource(Assembly assembly, string name)
    {
        using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
        return reader.ReadToEnd();
    }
}
