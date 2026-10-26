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
        || !TryParseWeeks(values.GetValueOrDefault("--weeks"), out int? weeks))
        return Usage();

    var db = LoadContent(values.GetValueOrDefault("--content", "content"));
    if (db is null)
        return 1;

    var sim = Simulation.NewGame(db, seed);
    var localizer = Localizer.FromContent(db);
    if (weeks != 0)
    {
        // Interrupts stop the wait; a headless run just reports them and keeps going.
        Command command = new StartActivity(new WaitActivity(), weeks);
        while (true)
        {
            var outcome = sim.Execute(command);
            if (!outcome.Accepted)
            {
                Console.Error.WriteLine(localizer.Resolve(outcome.RejectReason!));
                return 1;
            }
            foreach (var e in outcome.Events)
            {
                if (SimText.Describe(e) is { } text)
                    Console.WriteLine($"[{sim.State.Week}] {localizer.Resolve(text)}");
            }
            if (sim.State.Activity is null)
                break;
            command = new ContinueActivity();
        }
    }

    foreach (var line in SimText.ViewLines(sim.View, db))
        Console.WriteLine(localizer.Resolve(line));
    Console.WriteLine($"state hash: {SaveSerializer.HashState(sim.State)}");
    return 0;
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
          sim run --seed <N> --weeks <W|end> [--content <dir>]   (content defaults to ./content)
        """);
    return 2;
}
