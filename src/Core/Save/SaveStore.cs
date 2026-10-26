using Core.State;

namespace Core.Save;

public enum SaveLoadStatus
{
    Ok,
    RestoredFromBackup,
    NotFound,
    Corrupt,
    Incompatible,
}

public sealed record SaveLoadResult(SaveLoadStatus Status, GameState? State, string? Error = null);

/// <summary>
/// The single ironman slot. Callers must save after every command and before showing its
/// result (commit-before-reveal). Writes go to a temp file that atomically replaces the
/// slot; the previous save is kept as one backup, used only when the main file fails its checksum.
/// </summary>
public sealed class SaveStore
{
    public SaveStore(string path)
    {
        Path = path;
        TempPath = path + ".tmp";
        BackupPath = path + ".bak";
    }

    public string Path { get; }
    public string TempPath { get; }
    public string BackupPath { get; }

    public void Save(GameState state)
    {
        byte[] bytes = SaveSerializer.Encode(state);

        string? dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
        if (dir is not null)
            Directory.CreateDirectory(dir);

        using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(Path))
            File.Replace(TempPath, Path, BackupPath, ignoreMetadataErrors: true);
        else
            File.Move(TempPath, Path);
    }

    public SaveLoadResult Load()
    {
        if (!File.Exists(Path))
        {
            // A crash can only leave the slot missing before the very first save completes,
            // but a lone backup is still the best state we have.
            return File.Exists(BackupPath)
                ? FromBackup(null)
                : new SaveLoadResult(SaveLoadStatus.NotFound, null);
        }

        var main = SaveSerializer.Decode(File.ReadAllBytes(Path));
        return main.Status switch
        {
            LoadStatus.Ok => new SaveLoadResult(SaveLoadStatus.Ok, main.State),
            LoadStatus.Incompatible => new SaveLoadResult(SaveLoadStatus.Incompatible, null, main.Error),
            _ => FromBackup(main.Error),
        };
    }

    private SaveLoadResult FromBackup(string? mainError)
    {
        if (!File.Exists(BackupPath))
            return new SaveLoadResult(SaveLoadStatus.Corrupt, null, mainError);

        var backup = SaveSerializer.Decode(File.ReadAllBytes(BackupPath));
        return backup.Status switch
        {
            LoadStatus.Ok => new SaveLoadResult(SaveLoadStatus.RestoredFromBackup, backup.State, mainError),
            LoadStatus.Incompatible => new SaveLoadResult(SaveLoadStatus.Incompatible, null, backup.Error),
            _ => new SaveLoadResult(SaveLoadStatus.Corrupt, null, mainError ?? backup.Error),
        };
    }
}
