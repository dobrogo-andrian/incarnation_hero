using Core.Content;
using Core.Loc;
using Core.Save;
using Core.Sim;

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
        || !int.TryParse(values.GetValueOrDefault("--weeks"), out int weeks)
        || weeks < 0)
        return Usage();

    var db = LoadContent(values.GetValueOrDefault("--content", "content"));
    if (db is null)
        return 1;

    var sim = Simulation.NewGame(seed, NewGameSettings.FromContent(db));
    var localizer = Localizer.FromContent(db);
    if (weeks > 0)
    {
        var outcome = sim.Execute(new WaitWeeks(weeks));
        if (!outcome.Accepted)
        {
            Console.Error.WriteLine(localizer.Resolve(outcome.RejectReason!));
            return 1;
        }
    }

    foreach (var line in PlayerViewText.Lines(sim.View, db))
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

static int Usage()
{
    Console.Error.WriteLine("""
        Usage:
          sim validate <contentDir>
          sim run --seed <N> --weeks <W> [--content <dir>]   (content defaults to ./content)
        """);
    return 2;
}
