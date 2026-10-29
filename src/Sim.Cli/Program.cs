using Core.Content;
using Core.Loc;
using Core.Save;
using Core.Sim;
using Core.State;
using Sim.Cli.Arena;
using Sim.Cli.Bots;
using Sim.Cli.Runs;

return args switch
{
    ["validate", string contentDir] => Validate(contentDir),
    ["run", .. var options] when options.Contains("--bot") => RunBots(options),
    ["run", .. var options] => Run(options),
    ["arena", .. var options] => Arena(options),
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
        || !TryParseWeeks(values.GetValueOrDefault("--weeks", "0"), out int? weeks)
        || !TryParseWeeks(values.GetValueOrDefault("--inquire", "0"), out int? inquireWeeks))
        return Usage();

    var db = LoadContent(values.GetValueOrDefault("--content", "content"));
    if (db is null)
        return 1;

    var sim = Simulation.NewGame(db, seed);
    var localizer = Localizer.FromContent(db);
    if (inquireWeeks != 0 && !RunToEnd(sim, new StartActivity(new InquireActivity(), inquireWeeks), localizer))
        return 1;
    if (values.TryGetValue("--travel", out string? zone) && !RunToEnd(sim, new Travel(zone), localizer))
        return 1;
    foreach (string pill in values.GetValueOrDefault("--pills", "").Split(',', StringSplitOptions.RemoveEmptyEntries))
    {
        if (!RunToEnd(sim, new UsePill(pill), localizer))
            return 1;
    }
    var activity = WeeksActivity(sim, values.GetValueOrDefault("--meditate"), values.GetValueOrDefault("--subject"));
    if (weeks != 0 && !RunToEnd(sim, new StartActivity(activity, weeks), localizer))
        return 1;

    foreach (var line in SimText.ViewLines(sim.View, db))
        Console.WriteLine(localizer.Resolve(line));
    Console.WriteLine($"state hash: {SaveSerializer.HashState(sim.State)}");
    return 0;
}

/// <summary>
/// Plays --runs runs with --bot from --seed-from on and writes one summary line each to --out
/// (or stdout). --trace plays one seed alone and writes its commands and events to --trace-out.
/// </summary>
static int RunBots(string[] options)
{
    if (!TryParseOptions(options, out var values)
        || values.GetValueOrDefault("--bot") is not { } bot || !BotRegistry.Exists(bot)
        || !int.TryParse(values.GetValueOrDefault("--runs", "1"), out int runs) || runs < 1
        || !ulong.TryParse(values.GetValueOrDefault("--seed-from", "1"), out ulong seedFrom))
        return Usage();

    var db = LoadContent(values.GetValueOrDefault("--content", "content"));
    if (db is null)
        return 1;

    if (values.TryGetValue("--trace", out string? traceText))
    {
        if (!ulong.TryParse(traceText, out ulong traceSeed))
            return Usage();
        string tracePath = values.GetValueOrDefault("--trace-out", $"trace-{bot}-{traceSeed}.jsonl");
        using var trace = new StreamWriter(tracePath);
        var (summary, sim) = BotRunner.Play(db, bot, traceSeed, trace);
        Console.WriteLine(BotRunner.ToJsonLine(summary));
        Console.Error.WriteLine($"Trace written to {tracePath}.");
        Console.Error.WriteLine($"state hash: {SaveSerializer.HashState(sim.State)}");
        return 0;
    }

    var clock = System.Diagnostics.Stopwatch.StartNew();
    var summaries = BotRunner.PlayMany(db, bot, seedFrom, runs);
    using var file = values.TryGetValue("--out", out string? outPath) ? new StreamWriter(outPath) : null;
    var output = file ?? Console.Out;
    foreach (var summary in summaries)
        output.WriteLine(BotRunner.ToJsonLine(summary));
    Console.Error.WriteLine($"{runs} runs of {bot} in {clock.Elapsed.TotalSeconds:F1} s.");
    return 0;
}

/// <summary>Fights --n hex battles of side --a against side --b, both played by the AI, and prints the summary.</summary>
static int Arena(string[] options)
{
    if (!TryParseOptions(options, out var values)
        || values.GetValueOrDefault("--a") is not { } sideA || values.GetValueOrDefault("--b") is not { } sideB
        || !int.TryParse(values.GetValueOrDefault("--n", "2000"), out int fights) || fights < 1
        || !ulong.TryParse(values.GetValueOrDefault("--seed-from", "1"), out ulong seedFrom))
        return Usage();

    var db = LoadContent(values.GetValueOrDefault("--content", "content"));
    if (db is null)
        return 1;
    if (ArenaRunner.Side(db, sideA, "a") is not { } a || ArenaRunner.Side(db, sideB, "b") is not { } b)
    {
        Console.Error.WriteLine($"A side is a group (grp.*) or {ArenaRunner.PlayerSide}.");
        return 2;
    }

    var clock = System.Diagnostics.Stopwatch.StartNew();
    var results = ArenaRunner.FightMany(db, a, b, seedFrom, fights);
    if (values.TryGetValue("--out", out string? outPath))
    {
        using var file = new StreamWriter(outPath);
        foreach (var fight in results)
            file.WriteLine(ArenaRunner.ToJsonLine(fight));
    }
    Console.WriteLine($"{sideA} vs {sideB}");
    foreach (string line in ArenaRunner.Report(results))
        Console.WriteLine(line);
    Console.Error.WriteLine($"{fights} fights in {clock.Elapsed.TotalSeconds:F1} s.");
    return 0;
}

/// <summary>
/// What the --weeks are spent on: meditation in <paramref name="direction"/> at the first place of
/// the current zone (studying <paramref name="subject"/> for comprehension), or waiting when no
/// direction is given.
/// </summary>
static Activity WeeksActivity(Simulation sim, string? direction, string? subject)
{
    if (direction is null)
        return new WaitActivity();
    string place = sim.Content.All<PlaceDef>().First(p => p.Zone == sim.State.Player.ZoneId).Id;
    return new MeditateActivity(direction, place, subject);
}

/// <summary>
/// Plays <paramref name="command"/> as a script (<see cref="Script.Play"/>): restarts its activity
/// after interrupts and takes the first available choice in event dialogs, printing notable events,
/// each dialog's text and the choice. False when Core rejects the command.
/// </summary>
static bool RunToEnd(Simulation sim, Command command, Localizer localizer)
{
    int week = sim.State.Week;
    foreach (var (_, outcome) in Script.Play(sim, command))
    {
        if (!outcome.Accepted)
        {
            Console.Error.WriteLine(localizer.Resolve(outcome.RejectReason!));
            return false;
        }
        foreach (var e in outcome.Events)
        {
            if (e is WeekPassed passed)
                week = passed.Week;
            else if (SimText.Describe(e) is { } text)
                Console.WriteLine($"[{week}] {localizer.Resolve(text)}");
        }
        if (sim.View.Event is { } dialog)
        {
            // The script answers with the first available choice, as printed here.
            Console.WriteLine($"[{week}] {localizer.Resolve(SimText.EventText(dialog, sim.View))}");
            Console.WriteLine($"[{week}] > {localizer.Resolve(SimText.EventChoices(dialog).First(c => c.Available).Text)}");
        }
    }
    return true;
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
          sim run --seed <N> [--inquire <W|end>] [--travel <zoneId>] [--pills <itemId,...>] [--weeks <W|end>] [--meditate <directionId> [--subject <folio or element>]] [--content <dir>]
            Asks around the starting settlement for rumors first, travels, takes the pills in order,
            then spends the weeks waiting, or meditating at the zone's first place; in events and
            rumors met on arrival, takes the first available choice. Content defaults to ./content.
          sim run --bot <cautious|fighter|random> [--runs <N>] [--seed-from <N>] [--out <file.jsonl>] [--content <dir>]
            Plays N whole runs (default 1) with the bot from seed --seed-from (default 1) on, in
            parallel, and writes one JSON summary line per run, in seed order, to the file or stdout.
          sim run --bot <name> --trace <seed> [--trace-out <file.jsonl>] [--content <dir>]
            Plays one run and writes every command with its events, one JSON line each, to the
            trace file (default trace-<bot>-<seed>.jsonl); the summary goes to stdout, the final
            state hash to stderr.
          sim arena --a <grp.* | player> --b <grp.* | player> [--n <N>] [--seed-from <N>] [--out <file.jsonl>] [--content <dir>]
            Fights N hex battles (default 2000), the AI playing both sides; player is the hero as a
            run starts. Prints win shares, how fights ended, their length and side A's losses; --out
            writes one JSON line per fight.
        """);
    return 2;
}
