using Core.Content;
using Core.Sim;
using Core.State;

namespace Core.Tests;

/// <summary>The repository content, loaded once, plus shorthands shared by tests.</summary>
internal static class TestContent
{
    public static ContentDb Repo { get; } = Load();

    /// <summary>The repository content without weekly event and rumor rolls, for tests of other systems that spend weeks in the settlement.</summary>
    public static ContentDb Calm { get; } = Quiet(Load());

    public static RunConfigDef Config => Repo.Get<RunConfigDef>(RunConfigDef.RunId);

    public static StartActivity Wait(int? weeks) => new(new WaitActivity(), weeks);

    /// <summary>
    /// What a run that goes on by itself sends next, like <c>sim run</c>: the first available choice
    /// in an event dialog, otherwise <paramref name="activity"/> again, since an interrupt or event
    /// ends the activity (D44).
    /// </summary>
    public static Command Proceed(PlayerView view, Command activity) =>
        view.Event is { } dialog
            ? new ChooseEventOption(SimText.EventChoices(dialog).First(c => c.Available).Index)
            : activity;

    /// <summary>The repository content plus one extra file, for tests that need objects the game does not have.</summary>
    public static ContentDb WithExtra(string yaml) => Load(new ContentFile("test/extra.yaml", yaml));

    /// <summary>Turns off weekly event and rumor rolls in <paramref name="db"/>, a copy of its own (never <see cref="Repo"/>).</summary>
    public static ContentDb Quiet(ContentDb db)
    {
        var events = db.Get<EventsConfigDef>(EventsConfigDef.EventsId);
        foreach (string context in events.WeekChance.Keys.ToList())
            events.WeekChance[context] = 0;
        var rumors = db.Get<RumorsConfigDef>(RumorsConfigDef.RumorsId);
        rumors.Settlement.WeekChance = 0;
        rumors.Inquiry.WeekChance = 0;
        return db;
    }

    private static ContentDb Load(params ContentFile[] extra)
    {
        var loaded = ContentLoader.Load(ContentFiles.FromDirectory(RepoPaths.Content).Concat(extra));
        if (!loaded.Ok)
            throw new InvalidOperationException(string.Join("\n", loaded.Errors));
        return loaded.Db;
    }
}
