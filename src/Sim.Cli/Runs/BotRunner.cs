using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Content;
using Core.Sim;
using Core.State;
using Sim.Cli.Bots;

namespace Sim.Cli.Runs;

/// <summary>Plays whole runs with a bot (tech 6.1) and writes them as JSONL.</summary>
public static class BotRunner
{
    /// <summary>A run is a few thousand weeks; this many commands means a bot is stuck in a loop of instant ones.</summary>
    private const int MaxCommands = 200_000;

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Plays seed <paramref name="seed"/> to the end of the run. With <paramref name="trace"/>, writes
    /// one JSON line per executed command: the week, the command and the events it produced.
    /// </summary>
    public static (RunSummary Summary, Simulation Sim) Play(ContentDb content, string botName, ulong seed, TextWriter? trace = null)
    {
        var sim = Simulation.NewGame(content, seed);
        var bot = BotRegistry.Create(botName, content, seed);
        var recorder = new RunRecorder(sim, bot);

        for (int n = 0; sim.State.Mode != GameMode.RunEnd; n++)
        {
            if (n == MaxCommands)
                throw new InvalidOperationException($"Bot {botName} made {MaxCommands} commands on seed {seed} without ending the run.");

            var view = sim.View;
            // Bots always auto-resolve (D67).
            var proposals = view.Mode == GameMode.Combat ? [new AutoResolveFight()] : bot.Propose(view);
            var (command, outcome) = ExecuteFirstAccepted(sim, proposals)
                ?? throw new InvalidOperationException($"Core rejected every command of bot {botName} on seed {seed}, week {view.Week}.");
            bot.Observe(outcome.Events);
            recorder.Record(view, command, outcome.Events);
            trace?.WriteLine(TraceLine(view.Week, command, outcome.Events));
        }
        return (recorder.Finish(), sim);
    }

    /// <summary>
    /// Plays seeds <paramref name="seedFrom"/>, +1, … in parallel (each run is independent and
    /// deterministic) and returns the summaries in seed order.
    /// </summary>
    public static RunSummary[] PlayMany(ContentDb content, string botName, ulong seedFrom, int runs)
    {
        var summaries = new RunSummary[runs];
        Parallel.For(0, runs, i => summaries[i] = Play(content, botName, seedFrom + (ulong)i).Summary);
        return summaries;
    }

    public static string ToJsonLine(RunSummary summary) => JsonSerializer.Serialize(summary, Json);

    private static (Command, CommandOutcome)? ExecuteFirstAccepted(Simulation sim, IEnumerable<Command> proposals)
    {
        foreach (var command in proposals)
        {
            var outcome = sim.Execute(command);
            if (outcome.Accepted)
                return (command, outcome);
        }
        return null;
    }

    private static string TraceLine(int week, Command command, IReadOnlyList<SimEvent> events) =>
        new JsonObject
        {
            ["week"] = week,
            ["command"] = Typed(command),
            ["events"] = new JsonArray(events.Select(e => (JsonNode?)Typed(e)).ToArray()),
        }.ToJsonString(Json);

    /// <summary>A record as JSON with its type name first, so commands and events of every kind read alike.</summary>
    private static JsonObject Typed(object value)
    {
        var node = new JsonObject { ["type"] = value.GetType().Name };
        foreach (var (key, field) in JsonSerializer.SerializeToNode(value, value.GetType(), Json)!.AsObject().ToList())
            node[key] = field?.DeepClone();
        return node;
    }
}
