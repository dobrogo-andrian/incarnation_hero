using Core.Content;
using Core.Loc;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Turns a <see cref="PlayerView"/> into localization keys, one per line. Shared by Sim.Cli and
/// the Godot shell so both show the same thing; each resolves the keys with its Localizer.
/// </summary>
public static class PlayerViewText
{
    private static readonly LocKey Week = new("str.view.week");
    private static readonly LocKey Age = new("str.view.age");
    private static readonly LocKey ModeLine = new("str.view.mode");
    private static readonly LocKey Track = new("str.view.track");

    private static readonly LocKey Overworld = new("str.mode.overworld");
    private static readonly LocKey EventDialog = new("str.mode.event_dialog");
    private static readonly LocKey Combat = new("str.mode.combat");
    private static readonly LocKey RunEnd = new("str.mode.run_end");

    public static IReadOnlyList<LocKey> Lines(PlayerView view, ContentDb db)
    {
        int weeksPerYear = db.Get<RunConfigDef>(RunConfigDef.RunId).WeeksPerYear;
        var lines = new List<LocKey>
        {
            Week with { Args = [new("week", view.Week)] },
            Age with { Args = [new("years", view.AgeWeeks / weeksPerYear), new("weeks", view.AgeWeeks % weeksPerYear)] },
            ModeLine with { Args = [new("mode", ModeName(view.Mode))] },
        };

        foreach (var track in view.Tracks)
        {
            string vectorId = $"vector.{track.VectorId}";
            string stageId = db.Get<CultivationVectorDef>(vectorId).Stages[track.StageIndex];
            lines.Add(Track with
            {
                Args =
                [
                    new("vector", new LocKey(LocTable.ContentKey(vectorId, "name"))),
                    new("stage", new LocKey(LocTable.ContentKey(stageId, "name"))),
                    new("progress", track.StageProgress),
                ],
            });
        }
        return lines;
    }

    private static LocKey ModeName(GameMode mode) => mode switch
    {
        GameMode.Overworld => Overworld,
        GameMode.EventDialog => EventDialog,
        GameMode.Combat => Combat,
        GameMode.RunEnd => RunEnd,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
