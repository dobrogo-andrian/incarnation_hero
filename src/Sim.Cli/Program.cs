using Core.Content;
using Core.Loc;
using Core.Save;
using Core.Sim;
using Core.State;

return args switch
{
    ["validate", string contentDir] => Validate(contentDir),
    ["run", .. var options] => Run(options),
    _ => Usage(),
};

static int Validate(string contentDir)
{
    var db = LoadContent(contentDir);
    return db is null ? 1 : 0;
}

static int Run(string[] options)
{
    if (!TryParseOptions(options, out var values)
        || !ulong.TryParse(values.GetValueOrDefault("--seed"), out ulong seed)
        || !TryParseWeeks(values.GetValueOrDefault("--weeks", "0"), out int? weeks))
        return Usage();

    var db = LoadContent(values.GetValueOrDefault("--content", "content"));
    if (db is null)
        return 1;

    var sim = Simulation.NewGame(db, seed);
    var localizer = Localizer.FromContent(db);
    var commands = new List<Command>();
    if (values.TryGetValue("--travel", out string? zone))
        commands.Add(new Travel(zone));
    if (weeks != 0)
        commands.Add(new StartActivity(new WaitActivity(), weeks));

    foreach (var command in commands)
    {
        if (!RunToEnd(sim, command, localizer))
            return 1;
    }

    foreach (var line in SimText.ViewLines(sim.View, db))
        Console.WriteLine(localizer.Resolve(line));
    Console.WriteLine($"state hash: {SaveSerializer.HashState(sim.State)}");
    return 0;
}

/// <summary>
/// Executes <paramref name="command"/> and keeps continuing its activity through interrupts,
/// printing notable events. False when Core rejects it.
/// </summary>
static bool RunToEnd(Simulation sim, Command command, Localizer localizer)
{
    while (true)
    {
        var outcome = sim.Execute(command);
        if (!outcome.Accepted)
        {
            Console.Error.WriteLine(localizer.Resolve(outcome.RejectReason!));
            return false;
        }
        int week = sim.State.Week;
        foreach (var e in outcome.Events)
        {
            if (e is WeekPassed passed)
                week = passed.Week;
            else if (SimText.Describe(e) is { } text)
                Console.WriteLine($"[{week}] {localizer.Resolve(text)}");
        }
        if (sim.State.Activity is null)
            return true;
        command = new ContinueActivity();
    }
}

/// <summary>Loads and validates content, printing every error. Null means the content is unusable.</summary>
static ContentDb? LoadContent(string contentDir)
{
    if (!Directory.Exists(contentDir))
    {
        Console.Error.WriteLine($"Content directory not found: {contentDir}");
        return null;
    }

    var loaded = ContentLoader.Load(ContentFiles.FromDirectory(contentDir));
    var errors = loaded.Errors.Concat(ContentValidator.Validate(loaded.Db)).ToList();

    foreach (var error in errors)
        Console.Error.WriteLine(error);
    Console.Error.WriteLine($"{loaded.Db.Count} objects, {errors.Count} errors.");
    return errors.Count == 0 ? loaded.Db : null;
}

static bool TryParseOptions(string[] options, out Dictionary<string, string> values)
{
    values = [];
    if (options.Length % 2 != 0)
        return false;
    for (int i = 0; i < options.Length; i += 2)
    {
        if (!options[i].StartsWith("--") || !values.TryAdd(options[i], options[i + 1]))
            return false;
    }
    return true;
}

/// <summary>A non-negative week count, or "end" (null) for no limit.</summary>
static bool TryParseWeeks(string? text, out int? weeks)
{
    weeks = null;
    if (text == "end")
        return true;
    if (!int.TryParse(text, out int value) || value < 0)
        return false;
    weeks = value;
    return true;
}

static int Usage()
{
    Console.Error.WriteLine("""
        Usage:
          sim validate <contentDir>
          sim run --seed <N> [--travel <zoneId>] [--weeks <W|end>] [--content <dir>]
            Travels first, then waits; content defaults to ./content.
        """);
    return 2;
}
