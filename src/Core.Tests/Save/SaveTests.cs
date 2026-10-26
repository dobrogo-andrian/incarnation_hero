using System.Text;
using Core.Save;
using Core.Sim;
using Core.State;

namespace Core.Tests.Save;

public sealed class SaveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ih-save-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private SaveStore NewStore() => new(Path.Combine(_dir, "run.json"));

    private static GameState PlayedState(ulong seed, int weeks)
    {
        var sim = Simulation.NewGame(TestContent.Repo, seed);
        sim.Execute(TestContent.Wait(weeks));
        return sim.State;
    }

    [Fact]
    public void Encode_Decode_Encode_IsByteIdentical()
    {
        byte[] first = SaveSerializer.Encode(PlayedState(42, 10));

        var decoded = SaveSerializer.Decode(first);

        Assert.Equal(LoadStatus.Ok, decoded.Status);
        Assert.Equal(first, SaveSerializer.Encode(decoded.State!));
    }

    [Fact]
    public void TravelActivity_SurvivesSaveLoad()
    {
        var state = PlayedState(42, 1);
        state.Activity = new ActiveActivity
        {
            Activity = new TravelActivity([new TravelLeg("zone.bamboo_grove", 1), new TravelLeg("zone.misty_hills", 2)]),
            WeeksLeft = 2,
        };

        var decoded = SaveSerializer.Decode(SaveSerializer.Encode(state)).State!;

        var travel = Assert.IsType<TravelActivity>(decoded.Activity!.Activity);
        Assert.Equal([new TravelLeg("zone.bamboo_grove", 1), new TravelLeg("zone.misty_hills", 2)], travel.Legs);
        Assert.Equal(SaveSerializer.HashState(state), SaveSerializer.HashState(decoded));
    }

    [Fact]
    public void InterruptedActivity_SurvivesSaveLoad()
    {
        var state = PlayedState(42, 10);
        state.Activity = new ActiveActivity { Activity = new WaitActivity(), WeeksLeft = 3 };
        state.EndCause = RunEndCause.OldAge;

        var decoded = SaveSerializer.Decode(SaveSerializer.Encode(state)).State!;

        Assert.IsType<WaitActivity>(decoded.Activity!.Activity);
        Assert.Equal(3, decoded.Activity.WeeksLeft);
        Assert.Equal(RunEndCause.OldAge, decoded.EndCause);
        Assert.Equal(SaveSerializer.HashState(state), SaveSerializer.HashState(decoded));
    }

    [Fact]
    public void Save_Load_PreservesStateHash()
    {
        var state = PlayedState(42, 10);
        var store = NewStore();

        store.Save(state);
        var loaded = store.Load();

        Assert.Equal(SaveLoadStatus.Ok, loaded.Status);
        Assert.Equal(SaveSerializer.HashState(state), SaveSerializer.HashState(loaded.State!));
    }

    [Fact]
    public void LoadedState_ContinuesExactlyLikeTheOriginal()
    {
        var original = Simulation.NewGame(TestContent.Repo, 7);
        original.Execute(TestContent.Wait(3));
        var store = NewStore();
        store.Save(original.State);

        var resumed = Simulation.Resume(TestContent.Repo, store.Load().State!);
        original.Execute(TestContent.Wait(5));
        resumed.Execute(TestContent.Wait(5));

        Assert.Equal(SaveSerializer.HashState(original.State), SaveSerializer.HashState(resumed.State));
    }

    [Fact]
    public void Save_UsesSnakeCaseAndStringEnums()
    {
        string json = Encoding.UTF8.GetString(SaveSerializer.Encode(PlayedState(1, 1)));

        Assert.Contains("\"schema_version\"", json);
        Assert.Contains("\"age_weeks\"", json);
        Assert.Contains("\"mode\": \"overworld\"", json);
    }

    [Fact]
    public void Load_MissingFile_IsNotFound()
    {
        Assert.Equal(SaveLoadStatus.NotFound, NewStore().Load().Status);
    }

    [Fact]
    public void Save_LeavesNoTempFile_AndKeepsPreviousAsBackup()
    {
        var store = NewStore();
        store.Save(PlayedState(1, 1));
        store.Save(PlayedState(1, 2));

        Assert.False(File.Exists(store.TempPath));
        Assert.True(File.Exists(store.BackupPath));
        Assert.Equal(1, SaveSerializer.Decode(File.ReadAllBytes(store.BackupPath)).State!.Week);
    }

    [Fact]
    public void Load_TamperedMain_FallsBackToBackup()
    {
        var store = NewStore();
        store.Save(PlayedState(1, 1));
        store.Save(PlayedState(1, 2));
        Tamper(store.Path);

        var loaded = store.Load();

        Assert.Equal(SaveLoadStatus.RestoredFromBackup, loaded.Status);
        Assert.Equal(1, loaded.State!.Week);
    }

    [Fact]
    public void Load_TamperedMainWithoutBackup_IsCorrupt()
    {
        var store = NewStore();
        store.Save(PlayedState(1, 1));
        Tamper(store.Path);

        Assert.Equal(SaveLoadStatus.Corrupt, store.Load().Status);
    }

    [Fact]
    public void Load_GarbageFile_IsCorrupt()
    {
        var store = NewStore();
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.Path, "not json at all");

        Assert.Equal(SaveLoadStatus.Corrupt, store.Load().Status);
    }

    [Fact]
    public void Decode_NewerSchema_IsIncompatible()
    {
        var state = PlayedState(1, 1);
        state.SchemaVersion = GameState.CurrentSchemaVersion + 1;

        Assert.Equal(LoadStatus.Incompatible, SaveSerializer.Decode(SaveSerializer.Encode(state)).Status);
    }

    [Fact]
    public void Decode_UnknownStateField_IsCorrupt()
    {
        // Valid checksum but a field the schema does not know.
        string state = "{\"bogus\":1}";
        string checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(state))).ToLowerInvariant();
        string forged = $"{{\"schema_version\":{GameState.CurrentSchemaVersion},\"checksum\":\"{checksum}\",\"state\":{state}}}";

        Assert.Equal(LoadStatus.Corrupt, SaveSerializer.Decode(Encoding.UTF8.GetBytes(forged)).Status);
    }

    private static void Tamper(string path)
    {
        string text = File.ReadAllText(path);
        File.WriteAllText(path, text.Replace("\"week\": ", "\"week\": 9"));
    }
}
