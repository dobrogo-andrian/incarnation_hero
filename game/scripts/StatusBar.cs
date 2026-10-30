using Core.Sim;
using Core.State;
using Godot;

/// <summary>
/// The top bar (D44): week, age, life left, zone, stones and stage scale on a line of their own, which
/// wraps rather than cuts; below it resting and the stage 2 test fight (D56), and at the right skipping
/// the weeks being shown (D47), the settings (<see cref="SettingsPopup"/>) and quitting the game,
/// which loses nothing since every command is already saved.
/// </summary>
public partial class StatusBar : VBoxContainer
{
    private const string Separator = "   ·   ";

    private readonly Label _info = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button _restWeek = new();
    private readonly Button _restOpen = new();
    private readonly Button _sandbox = new();
    private readonly Button _skip = new();
    private readonly Button _settings = new();
    private readonly SettingsPopup _popup = new();
    private readonly Button _quit = new();

    private UiContext _ui = null!;
    private PlayerView? _view;
    private bool _playing;

    /// <summary>Labels the controls and wires them to <paramref name="ui"/>; called once before the first <see cref="Refresh"/>.</summary>
    public void Setup(UiContext ui, Action skip, Action sandbox)
    {
        _ui = ui;
        AddThemeConstantOverride("separation", 4);
        _restWeek.Text = ui.Text("str.shell.wait_week");
        _restOpen.Text = ui.Text("str.shell.wait_until_interrupt");
        _sandbox.Text = ui.Text("str.ui.sandbox");
        _skip.Text = ui.Text("str.ui.skip");
        _settings.Text = ui.Text("str.ui.settings");
        _quit.Text = ui.Text("str.ui.quit");
        _restWeek.Pressed += () => ui.Send(new StartActivity(new WaitActivity(), 1));
        _restOpen.Pressed += () => ui.Send(new StartActivity(new WaitActivity(), null));
        _sandbox.Pressed += sandbox;
        _skip.Pressed += skip;
        _settings.Pressed += OpenSettings;
        _quit.Pressed += () => GetTree().Quit();

        var controls = new HBoxContainer();
        controls.AddThemeConstantOverride("separation", 8);
        var gap = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var node in new Node[] { _restWeek, _restOpen, _sandbox, gap, _skip, _settings, _quit, _popup })
            controls.AddChild(node);
        foreach (var node in new Node[] { _info, controls })
            AddChild(node);
    }

    /// <summary>
    /// Redraws from <paramref name="view"/>; while weeks are being shown only skipping is offered, and
    /// while the test fight is open nothing that moves the run on.
    /// </summary>
    public void Refresh(PlayerView view, bool playing, bool fighting = false)
    {
        (_view, _playing) = (view, playing);
        if (_popup.Visible)
            _popup.Refresh(_ui, view, playing);
        var parts = SimText.TimeLines(view, _ui.Db)
            .Append(SimText.ZoneText(view))
            .Append(SimText.StonesText(view))
            .Concat(view.Tracks.Select(t => SimText.TrackText(t, _ui.Db)))
            .Concat(SimText.EndLines(view));
        _info.Text = string.Join(Separator, parts.Select(_ui.Text));

        bool over = view.Mode == GameMode.RunEnd;
        _restWeek.Disabled = _restOpen.Disabled = playing || fighting || view.Mode != GameMode.Overworld;
        _restWeek.Visible = _restOpen.Visible = !over;
        _sandbox.Disabled = playing || fighting;
        _skip.Visible = playing;
    }

    /// <summary>Opens the settings under their button, its right edge level with the button's.</summary>
    private void OpenSettings()
    {
        _popup.Refresh(_ui, _view!, _playing);
        _popup.ResetSize();
        var corner = _settings.GlobalPosition + new Vector2(_settings.Size.X - _popup.Size.X, _settings.Size.Y);
        _popup.Position = (Vector2I)corner;
        _popup.Popup();
    }
}
