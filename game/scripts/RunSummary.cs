using Core.Sim;
using Godot;
using static Widgets;

/// <summary>
/// The run's end in the centre panel (D44, D47): how it ended, the age and stage reached, where the
/// weeks went, breakthroughs, fights, pills and stones; and a new run.
/// </summary>
public partial class RunSummary : VBoxContainer
{
    public RunSummary()
    {
        AddThemeConstantOverride("separation", 10);
    }

    public void Refresh(UiContext ui, PlayerView view, Action newRun)
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        Visible = view.EndCause is not null;
        if (!Visible)
            return;

        AddChild(Title(ui.Text("str.ui.summary_title")));
        AddChild(Text(ui.Lines(SimText.EndLines(view)
            .Concat(SimText.TimeLines(view, ui.Db).Skip(1).Take(1))   // the age reached
            .Concat(view.Tracks.Select(t => SimText.TrackText(t, ui.Db))))));
        AddChild(new HSeparator());
        AddChild(Text(ui.Lines(SimText.SummaryLines(view, ui.Db))));
        AddChild(Button(ui.Text("str.shell.new_run"), newRun));
    }
}
