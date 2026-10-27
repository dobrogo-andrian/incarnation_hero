using Core.Sim;
using Core.State;
using Godot;

/// <summary>
/// The top bar (D44): time, life left, zone, stones and stage scale; the current activity with
/// continue and cancel; resting; a new run once this one is over.
/// </summary>
public partial class StatusBar : VBoxContainer
{
    private const string Separator = "     ·     ";

    private readonly Label _info = new();
    private readonly Label _state = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Button _continue = new();
    private readonly Button _cancel = new();
    private readonly Button _restWeek = new();
    private readonly Button _restOpen = new();
    private readonly Button _newRun = new();

    /// <summary>Labels the buttons and wires them to <paramref name="ui"/>; called once before the first <see cref="Refresh"/>.</summary>
    public void Setup(UiContext ui, Action newRun)
    {
        AddThemeConstantOverride("separation", 8);
        _continue.Text = ui.Text("str.ui.continue");
        _cancel.Text = ui.Text("str.ui.cancel");
        _restWeek.Text = ui.Text("str.shell.wait_week");
        _restOpen.Text = ui.Text("str.shell.wait_until_interrupt");
        _newRun.Text = ui.Text("str.shell.new_run");
        _continue.Pressed += () => ui.Send(new ContinueActivity());
        _cancel.Pressed += () => ui.Send(new CancelActivity());
        _restWeek.Pressed += () => ui.Send(new StartActivity(new WaitActivity(), 1));
        _restOpen.Pressed += () => ui.Send(new StartActivity(new WaitActivity(), null));
        _newRun.Pressed += newRun;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        foreach (var control in new Control[] { _state, _continue, _cancel, _restWeek, _restOpen, _newRun })
            row.AddChild(control);
        AddChild(_info);
        AddChild(row);
    }

    public void Refresh(UiContext ui, PlayerView view)
    {
        var parts = SimText.TimeLines(view, ui.Db)
            .Append(SimText.ZoneText(view))
            .Append(SimText.StonesText(view))
            .Concat(view.Tracks.Select(t => SimText.TrackText(t, ui.Db)));
        _info.Text = string.Join(Separator, parts.Select(ui.Text));
        _state.Text = ui.Text(SimText.StateText(view));

        bool free = view.Mode == GameMode.Overworld;
        _continue.Visible = _cancel.Visible = free && view.Activity is not null;
        _restWeek.Disabled = _restOpen.Disabled = !free;
        _newRun.Visible = view.Mode == GameMode.RunEnd;
    }
}
