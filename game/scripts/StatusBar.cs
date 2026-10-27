using Core.Sim;
using Core.State;
using Godot;

/// <summary>
/// The top bar (D44): week, age, life left, zone, stones and stage scale on one line; resting; the
/// interrupt settings; how fast weeks are shown (D47), and skipping the weeks being shown.
/// </summary>
public partial class StatusBar : HBoxContainer
{
    private const string Separator = "   ·   ";

    private readonly Label _info = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
    private readonly Button _restWeek = new();
    private readonly Button _restOpen = new();
    private readonly Button _interrupts = new();
    private readonly InterruptsPopup _popup = new();
    private readonly Label _speedText = new() { CustomMinimumSize = new Vector2(190, 0) };
    private readonly HSlider _speed = new() { MinValue = 0, Step = 1, CustomMinimumSize = new Vector2(110, 0), SizeFlagsVertical = SizeFlags.ShrinkCenter };
    private readonly Button _skip = new();

    private UiContext _ui = null!;
    private PlayerView? _view;

    /// <summary>Labels the controls and wires them to <paramref name="ui"/>; called once before the first <see cref="Refresh"/>.</summary>
    public void Setup(UiContext ui, Action skip)
    {
        _ui = ui;
        AddThemeConstantOverride("separation", 8);
        _restWeek.Text = ui.Text("str.shell.wait_week");
        _restOpen.Text = ui.Text("str.shell.wait_until_interrupt");
        _interrupts.Text = ui.Text("str.ui.interrupts");
        _skip.Text = ui.Text("str.ui.skip");
        _restWeek.Pressed += () => ui.Send(new StartActivity(new WaitActivity(), 1));
        _restOpen.Pressed += () => ui.Send(new StartActivity(new WaitActivity(), null));
        _interrupts.Pressed += OpenInterrupts;
        _skip.Pressed += skip;

        _speed.MaxValue = UiSettings.Speeds.Length - 1;
        _speed.Value = ui.Settings.Speed;
        _speed.ValueChanged += v =>
        {
            ui.Settings.SetSpeed((int)v);
            ShowSpeed();
        };
        ShowSpeed();

        foreach (var node in new Node[] { _info, _restWeek, _restOpen, _interrupts, _speedText, _speed, _skip, _popup })
            AddChild(node);
    }

    /// <summary>Redraws from <paramref name="view"/>; while weeks are being shown only skipping is offered.</summary>
    public void Refresh(PlayerView view, bool playing)
    {
        _view = view;
        var parts = SimText.TimeLines(view, _ui.Db)
            .Append(SimText.ZoneText(view))
            .Append(SimText.StonesText(view))
            .Concat(view.Tracks.Select(t => SimText.TrackText(t, _ui.Db)))
            .Concat(SimText.EndLines(view));
        _info.Text = string.Join(Separator, parts.Select(_ui.Text));

        bool over = view.Mode == GameMode.RunEnd;
        _restWeek.Disabled = _restOpen.Disabled = playing || view.Mode != GameMode.Overworld;
        _restWeek.Visible = _restOpen.Visible = !over;
        _interrupts.Disabled = playing;
        _skip.Visible = playing;
    }

    private void OpenInterrupts()
    {
        _popup.Refresh(_ui, _view!);
        _popup.Position = (Vector2I)(_interrupts.GlobalPosition + new Vector2(0, _interrupts.Size.Y));
        _popup.Popup();
    }

    private void ShowSpeed()
    {
        double speed = _ui.Settings.WeeksPerSecond;
        _speedText.Text = double.IsPositiveInfinity(speed)
            ? _ui.Text("str.ui.speed_instant")
            : _ui.Text(new Core.Loc.LocKey("str.ui.speed") with { Args = [new("weeks", speed)] });
    }
}
