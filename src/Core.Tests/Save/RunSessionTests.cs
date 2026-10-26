using Core.Save;
using Core.Sim;

namespace Core.Tests.Save;

public sealed class RunSessionTests : IDisposable
{
    private static readonly NewGameSettings Settings = new(StartAgeWeeks: 12 * 52);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ih-session-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private SaveStore NewStore() => new(Path.Combine(_dir, "run.json"));

    private static Simulation NewGame() => Simulation.NewGame(7, Settings);

    [Fact]
    public void NoSave_StartsNewRunAndSavesIt()
    {
        var store = NewStore();

        var session = RunSession.OpenOrStart(store, NewGame);

        Assert.Equal("str.save.new_run", session.Notice?.Key);
        Assert.Equal(SaveLoadStatus.Ok, store.Load().Status);
    }

    [Fact]
    public void AcceptedCommand_IsSavedBeforeOutcomeReturns()
    {
        var store = NewStore();
        var session = RunSession.OpenOrStart(store, NewGame);

        session.Execute(new WaitWeeks(3));

        var saved = store.Load().State!;
        Assert.Equal(3, saved.Week);
        Assert.Equal(SaveSerializer.HashState(session.Sim.State), SaveSerializer.HashState(saved));
    }

    [Fact]
    public void RejectedCommand_DoesNotWrite()
    {
        var store = NewStore();
        var session = RunSession.OpenOrStart(store, NewGame);
        var writtenAt = File.GetLastWriteTimeUtc(store.Path);
        File.SetLastWriteTimeUtc(store.Path, writtenAt.AddDays(-1));

        var outcome = session.Execute(new WaitWeeks(0));

        Assert.False(outcome.Accepted);
        Assert.Equal(writtenAt.AddDays(-1), File.GetLastWriteTimeUtc(store.Path));
        Assert.False(File.Exists(store.BackupPath));
    }

    [Fact]
    public void ExistingSave_IsResumedWithoutNotice()
    {
        var store = NewStore();
        RunSession.OpenOrStart(store, NewGame).Execute(new WaitWeeks(5));

        var resumed = RunSession.OpenOrStart(store, () => throw new InvalidOperationException("must resume"));

        Assert.Null(resumed.Notice);
        Assert.Equal(5, resumed.Sim.State.Week);
    }

    [Fact]
    public void CorruptSaveWithoutBackup_StartsNewRun()
    {
        var store = NewStore();
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.Path, "not a save");

        var session = RunSession.OpenOrStart(store, NewGame);

        Assert.Equal("str.save.corrupt_new_run", session.Notice?.Key);
        Assert.Equal(0, session.Sim.State.Week);
        Assert.Equal(SaveLoadStatus.Ok, store.Load().Status);
    }
}
