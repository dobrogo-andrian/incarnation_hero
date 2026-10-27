using Core.Sim;
using Core.State;
using Godot;

/// <summary>
/// The top bar (D44): week, age, life left, zone, stones and stage scale on one line; resting; how
/// the run ended and a new run once it is over.
/// </summary>
public partial class StatusBar : HBoxContainer
{
    private const string Separator = "   ·   ";

    private readonly Label _info = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
    private readonly Button _restWeek = new();
    private readonly Button _restOpen = new();
    private readonly Button _newRun = new();

    /// <summary>Labels the buttons and wires them to <paramref name="ui"/>; called once before the first <see cref="Refresh"/>.</summary>
    public void Setup(UiContext ui, Action newRun)
    {
        AddThemeConstantOverride("separation", 8);
        _restWeek.Text = ui.Text("str.shell.wait_week");
        _restOpen.Text = ui.Text("str.shell.wait_until_interrupt");
        _newRun.Text = ui.Text("str.shell.new_run");
        _restWeek.Pressed += () => ui.Send(new StartActivity(new WaitActivity(), 1));
        _restOpen.Pressed += () => ui.Send(new StartActivity(new WaitActivity(), null));
        _newRun.Pressed += newRun;
        foreach (var control in new Control[] { _info, _restWeek, _restOpen, _newRun })
            AddChild(control);
    }

    public void Refresh(UiContext ui, PlayerView view)
    {
        var parts = SimText.TimeLines(view, ui.Db)
            .Append(SimText.ZoneText(view))
            .Append(SimText.StonesText(view))
            .Concat(view.Tracks.Select(t => SimText.TrackText(t, ui.Db)))
            .Concat(SimText.EndLines(view));
        _info.Text = string.Join(Separator, parts.Select(ui.Text));

        bool over = view.Mode == GameMode.RunEnd;
        _restWeek.Disabled = _restOpen.Disabled = view.Mode != GameMode.Overworld;
        _restWeek.Visible = _restOpen.Visible = !over;
        _newRun.Visible = over;
    }
}
