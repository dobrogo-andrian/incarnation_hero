using Core.Content;
using Core.Loc;
using Core.Sim;
using Core.State;

namespace Core.Save;

/// <summary>
/// A run bound to the ironman slot: resumes the saved run or starts a new one, and saves after
/// every accepted command before the caller sees the outcome (commit-before-reveal).
/// </summary>
public sealed class RunSession
{
    private static readonly LocKey NewRun = new("str.save.new_run");
    private static readonly LocKey RestoredFromBackup = new("str.save.restored_from_backup");
    private static readonly LocKey CorruptNewRun = new("str.save.corrupt_new_run");
    private static readonly LocKey IncompatibleNewRun = new("str.save.incompatible_new_run");

    private readonly SaveStore _store;
    private readonly ContentDb _content;
    private readonly Func<ulong> _newSeed;

    private RunSession(SaveStore store, ContentDb content, Func<ulong> newSeed, Simulation sim, LocKey? notice)
    {
        _store = store;
        _content = content;
        _newSeed = newSeed;
        Sim = sim;
        Notice = notice;
    }

    public Simulation Sim { get; private set; }

    /// <summary>What the player should be told about how the run was opened; null for a clean resume.</summary>
    public LocKey? Notice { get; }

    /// <summary>
    /// Resumes the saved run, or starts one seeded by <paramref name="newSeed"/> when there is no
    /// usable save. A corrupt or outdated save is replaced: run saves may be invalidated by updates.
    /// The seed comes from the caller because Core never reads the clock or OS randomness.
    /// </summary>
    public static RunSession OpenOrStart(SaveStore store, ContentDb content, Func<ulong> newSeed)
    {
        var loaded = store.Load();
        if (loaded.Status is SaveLoadStatus.Ok or SaveLoadStatus.RestoredFromBackup)
        {
            var notice = loaded.Status == SaveLoadStatus.RestoredFromBackup ? RestoredFromBackup : null;
            return new RunSession(store, content, newSeed, Simulation.Resume(content, loaded.State!), notice);
        }

        var session = new RunSession(store, content, newSeed, Simulation.NewGame(content, newSeed()), loaded.Status switch
        {
            SaveLoadStatus.Corrupt => CorruptNewRun,
            SaveLoadStatus.Incompatible => IncompatibleNewRun,
            _ => NewRun,
        });
        store.Save(session.Sim.State);
        return session;
    }

    public CommandOutcome Execute(Command command)
    {
        var outcome = Sim.Execute(command);
        if (outcome.Accepted)
            _store.Save(Sim.State);
        return outcome;
    }

    /// <summary>Replaces a finished run with a new one in the same slot. Refused while the run is still going.</summary>
    public bool TryStartNewRun()
    {
        if (Sim.State.Mode != GameMode.RunEnd)
            return false;

        Sim = Simulation.NewGame(_content, _newSeed());
        _store.Save(Sim.State);
        return true;
    }
}
