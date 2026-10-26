using Core.Loc;
using Core.Sim;

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

    private RunSession(SaveStore store, Simulation sim, LocKey? notice)
    {
        _store = store;
        Sim = sim;
        Notice = notice;
    }

    public Simulation Sim { get; }

    /// <summary>What the player should be told about how the run was opened; null for a clean resume.</summary>
    public LocKey? Notice { get; }

    /// <summary>
    /// Resumes the saved run, or starts one with <paramref name="newGame"/> when there is no usable
    /// save. A corrupt or outdated save is replaced: run saves may be invalidated by updates.
    /// </summary>
    public static RunSession OpenOrStart(SaveStore store, Func<Simulation> newGame)
    {
        var loaded = store.Load();
        if (loaded.Status is SaveLoadStatus.Ok or SaveLoadStatus.RestoredFromBackup)
        {
            var notice = loaded.Status == SaveLoadStatus.RestoredFromBackup ? RestoredFromBackup : null;
            return new RunSession(store, Simulation.Resume(loaded.State!), notice);
        }

        var sim = newGame();
        store.Save(sim.State);
        var reason = loaded.Status switch
        {
            SaveLoadStatus.Corrupt => CorruptNewRun,
            SaveLoadStatus.Incompatible => IncompatibleNewRun,
            _ => NewRun,
        };
        return new RunSession(store, sim, reason);
    }

    public CommandOutcome Execute(Command command)
    {
        var outcome = Sim.Execute(command);
        if (outcome.Accepted)
            _store.Save(Sim.State);
        return outcome;
    }
}
