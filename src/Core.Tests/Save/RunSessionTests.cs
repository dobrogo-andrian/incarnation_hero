using Core.Save;
using Core.Sim;
using Core.State;

namespace Core.Tests.Save;

public sealed class RunSessionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ih-session-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private SaveStore NewStore() => new(Path.Combine(_dir, "run.json"));

    private static RunSession Open(SaveStore store, ulong seed = 7) =>
        RunSession.OpenOrStart(store, TestContent.Repo, () => seed);

    [Fact]
    public void NoSave_StartsNewRunAndSavesIt()
    {
        var store = NewStore();

        var session = Open(store);

        Assert.Equal("str.save.new_run", session.Notice?.Key);
        Assert.Equal(7UL, session.Sim.State.Seed);
        Assert.Equal(SaveLoadStatus.Ok, store.Load().Status);
    }

    [Fact]
    public void AcceptedCommand_IsSavedBeforeOutcomeReturns()
    {
        var store = NewStore();
        var session = Open(store);

        session.Execute(TestContent.Wait(3));

        var saved = store.Load().State!;
        Assert.Equal(3, saved.Week);
        Assert.Equal(SaveSerializer.HashState(session.Sim.State), SaveSerializer.HashState(saved));
    }

    [Fact]
    public void RejectedCommand_DoesNotWrite()
    {
        var store = NewStore();
        var session = Open(store);
        var writtenAt = File.GetLastWriteTimeUtc(store.Path);
        File.SetLastWriteTimeUtc(store.Path, writtenAt.AddDays(-1));

        var outcome = session.Execute(TestContent.Wait(0));

        Assert.False(outcome.Accepted);
        Assert.Equal(writtenAt.AddDays(-1), File.GetLastWriteTimeUtc(store.Path));
        Assert.False(File.Exists(store.BackupPath));
    }

    [Fact]
    public void ExistingSave_IsResumedWithoutNotice()
    {
        var store = NewStore();
        Open(store).Execute(TestContent.Wait(5));

        var resumed = RunSession.OpenOrStart(store, TestContent.Repo, () => throw new InvalidOperationException("must resume"));

        Assert.Null(resumed.Notice);
        Assert.Equal(5, resumed.Sim.State.Week);
    }

    [Fact]
    public void CorruptSaveWithoutBackup_StartsNewRun()
    {
        var store = NewStore();
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.Path, "not a save");

        var session = Open(store);

        Assert.Equal("str.save.corrupt_new_run", session.Notice?.Key);
        Assert.Equal(0, session.Sim.State.Week);
        Assert.Equal(SaveLoadStatus.Ok, store.Load().Status);
    }

    [Fact]
    public void TryStartNewRun_IsRefusedWhileTheRunGoesOn()
    {
        var store = NewStore();
        var session = Open(store);
        session.Execute(TestContent.Wait(2));

        Assert.False(session.TryStartNewRun());
        Assert.Equal(2, store.Load().State!.Week);
    }

    [Fact]
    public void TryStartNewRun_AfterDeath_ReplacesTheSavedRun()
    {
        var store = NewStore();
        ulong seed = 7;
        var session = RunSession.OpenOrStart(store, TestContent.Repo, () => seed++);
        session.Execute(TestContent.Wait(null));
        while (session.Sim.State.Mode != GameMode.RunEnd)
            session.Execute(TestContent.Proceed(session.Sim.View, TestContent.Wait(null)));

        Assert.True(session.TryStartNewRun());

        var saved = store.Load().State!;
        Assert.Equal(GameMode.Overworld, saved.Mode);
        Assert.Equal(0, saved.Week);
        Assert.Equal(8UL, saved.Seed);
    }
}
