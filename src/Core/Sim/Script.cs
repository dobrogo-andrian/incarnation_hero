using Core.State;

namespace Core.Sim;

/// <summary>
/// Plays a command the way a headless script does (Sim.Cli <c>run</c>, golden and system tests):
/// answers event dialogs with the first available choice and, since an event or interrupt ends an
/// activity for good (D44), starts it again for the weeks still left — until it finishes, its weeks
/// are used up, the journey arrives, it can no longer start, or the run ends.
/// </summary>
public static class Script
{
    /// <summary>
    /// Every executed command with its outcome, in order. The first is <paramref name="command"/>
    /// itself (possibly rejected, which ends the script); later restarts are only reported when accepted.
    /// </summary>
    public static IEnumerable<(Command Command, CommandOutcome Outcome)> Play(Simulation sim, Command command)
    {
        int startWeek = sim.State.Week;
        var outcome = sim.Execute(command);
        yield return (command, outcome);
        if (!outcome.Accepted)
            yield break;

        while (true)
        {
            bool interrupted = outcome.Events.Any(e => e is ActivityInterrupted);
            while (sim.View.Event is { } dialog)
            {
                var choice = new ChooseEventOption(SimText.EventChoices(dialog).First(c => c.Available).Index);
                outcome = sim.Execute(choice);
                yield return (choice, outcome);
            }
            if (!interrupted || sim.State.Mode != GameMode.Overworld || Resume(sim, command, startWeek) is not { } again)
                yield break;

            outcome = sim.Execute(again);
            if (!outcome.Accepted)
                yield break;
            yield return (again, outcome);
        }
    }

    /// <summary>Plays <paramref name="command"/> to its end and returns whether Core accepted it.</summary>
    public static bool Run(Simulation sim, Command command) => Play(sim, command).ToList()[0].Outcome.Accepted;

    /// <summary>The command that carries an interrupted <paramref name="command"/> on, or null when nothing is left of it.</summary>
    private static Command? Resume(Simulation sim, Command command, int startWeek) => command switch
    {
        StartActivity { Weeks: int weeks } start => startWeek + weeks - sim.State.Week is var left and > 0 ? start with { Weeks = left } : null,
        StartActivity start => start,
        Travel travel => sim.State.Player.ZoneId == travel.ZoneId ? null : travel,
        _ => null,
    };
}
