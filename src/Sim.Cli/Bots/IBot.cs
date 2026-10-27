using Core.Sim;

namespace Sim.Cli.Bots;

/// <summary>
/// A player policy for headless runs (tech 6.1, D43). A bot is honest: it sees only what the UI
/// sees — the <see cref="PlayerView"/>, the SimEvents and public content such as the map and
/// prices — and plays through the same commands.
/// </summary>
public interface IBot
{
    string Name { get; }

    /// <summary>
    /// Commands in order of preference. The runner executes the first one Core accepts, so a bot
    /// leaves validation to Core instead of repeating its checks; a rejected command changes nothing.
    /// </summary>
    IEnumerable<Command> Propose(PlayerView view);

    /// <summary>The events of the executed command, as the UI receives them.</summary>
    void Observe(IReadOnlyList<SimEvent> events);
}
