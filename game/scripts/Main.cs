using Core.Content;
using Core.Loc;
using Core.Save;
using Core.Sim;
using Godot;

/// <summary>
/// The stage 1 playtest screen (D44): a slim status bar on top; side tabs on the left; in the
/// centre the picked point's actions (or the open event) above the journal, so everything that
/// happens is read in one place; the map on the right, replaced by the place itself once the
/// player steps into one; the run's summary in the centre once it is over; the test fight (D56) over
/// everything when opened from the status bar. All rules, saving
/// included, stay in Core; this node loads content, owns the run session, sends commands and
/// redraws every part from the new PlayerView.
/// <para>
/// Core runs a command's weeks at once and saves before anything is shown (commit-before-reveal);
/// the screen then plays them back at the player's speed (D47): the time in the status bar and the
/// journal follow week by week while the rest waits, and input is blocked until the last week or a skip.
/// </para>
/// </summary>
public partial class Main : Control
{
    private static readonly LocKey ContentErrors = new("str.shell.content_errors");
    private static readonly LocKey JournalLine = new("str.ui.journal_line");

    private UiContext _ui = null!;
    private RunSession _session = null!;

    private readonly StatusBar _status = new();
    private readonly SideTabs _tabs = new();
    private readonly PointPanel _panel = new();
    private readonly EventDialog _dialog = new();
    private readonly JournalView _journal = new();
    private readonly Label _mapTitle = new();
    private readonly MapView _map = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly LocationView _location = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly RunSummary _summary = new();
    private readonly BattleScreen _battle = new();
    private readonly Control _blocker = new() { MouseFilter = MouseFilterEnum.Stop, Visible = false };

    /// <summary>Events of the last command not yet shown; empty when nothing is being played back.</summary>
    private readonly Queue<SimEvent> _playback = new();
    private PlayerView _shownView = null!;
    private int _shownWeek;
    private double _clock;

    /// <summary>The zone the screen was last set up for: arriving somewhere resets the map level and the picked point.</summary>
    private string? _shownZone;
    private bool _regionMap;
    private MapTarget? _target;

    public override void _Ready()
    {
        // The editor run and the exported game read the same content, embedded in Core at build time.
        var loaded = ContentLoader.Load(ContentFiles.Embedded());
        var errors = loaded.Errors.Concat(ContentValidator.Validate(loaded.Db)).ToList();
        var loc = Localizer.FromContent(loaded.Db);
        if (errors.Count > 0)
        {
            AddChild(new Label { Text = loc.Resolve(ContentErrors) + "\n" + string.Join("\n", errors) });
            return;
        }

        // A UI shot plays on a fresh throwaway run with a fixed seed and never touches the real save.
        var shot = UiShot.FromArgs();
        Func<ulong> newSeed = shot is null ? NewSeed : () => shot.Seed;
        _ui = new UiContext(loaded.Db, loc, Send);
        BuildLayout(newSeed);

        string savePath = ProjectSettings.GlobalizePath(shot is null ? "user://run.json" : "user://shot_run.json");
        if (shot is not null)
            File.Delete(savePath);
        _session = RunSession.OpenOrStart(new SaveStore(savePath), loaded.Db, newSeed);
        if (_session.Notice is { } notice)
            _journal.Add(_ui.Text(notice));
        Refresh();
        if (shot is not null)
            Stage(shot);
    }

    /// <summary>Opens what a UI shot asks for (see <see cref="UiShot"/>), then lets it take the picture.</summary>
    private void Stage(UiShot shot)
    {
        try
        {
            StageScenario(shot);
        }
        catch (Exception e)
        {
            GD.PushError($"UI shot {shot.Scenario} failed: {e}");
            GetTree().Quit(1);
            return;
        }
        Refresh();
        AddChild(shot);
    }

    private void StageScenario(UiShot shot)
    {
        shot.Busy = () => _playback.Count > 0 || _battle.Playing;
        _battle.Preselect(shot.Hero);
        switch (shot.Kind)
        {
            case "main":
                break;
            case "event":
                EventSystem.Start(_session.Sim.State, _ui.Db.Get<EventDef>(shot.Args[0]), []);
                break;
            case "battle":
                _battle.Stage(shot.Args[0], shot.Args.Length > 1 ? int.Parse(shot.Args[1]) : 0);
                break;
            default:
                throw new ArgumentException($"Unknown UI shot scenario: {shot.Scenario}");
        }
    }

    private void BuildLayout(Func<ulong> newSeed)
    {
        _status.Setup(_ui, FinishPlayback, () => { _battle.Open(); Refresh(); });
        _battle.Setup(_ui, () => CombatSandbox.Hero(_session.Sim), Refresh, newSeed);
        _tabs.Setup(_ui);
        _map.PointPressed += Pick;
        _map.ExitPressed += () => { _regionMap = true; Pick(null); };
        _panel.EnterPressed += () => { _regionMap = false; Pick(null); };
        _panel.TargetPicked += Pick;
        _location.LeavePressed += () => Pick(HomeTarget(_session.Sim.View));
        _mapTitle.AddThemeFontSizeOverride("font_size", 22);

        var center = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        center.AddThemeConstantOverride("separation", 12);
        center.AddChild(_summary);
        center.AddChild(_panel);
        center.AddChild(_dialog);
        center.AddChild(new HSeparator());
        center.AddChild(_journal);

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddChild(_mapTitle);
        right.AddChild(_map);
        right.AddChild(_location);

        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 20);
        columns.AddChild(_tabs);
        columns.AddChild(center);
        columns.AddChild(right);

        // The blocker lies over the columns while weeks are played back; the status bar stays live for "skip".
        // The test fight covers the columns too, leaving the status bar in sight.
        var stack = new MarginContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        stack.AddChild(columns);
        stack.AddChild(_blocker);
        stack.AddChild(_battle);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        column.AddChild(_status);
        column.AddChild(stack);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (string side in new[] { "left", "top", "right", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 16);
        margin.AddChild(column);
        AddChild(margin);
    }

    private void Pick(MapTarget? target)
    {
        _target = target;
        Refresh();
    }

    /// <summary>What the centre shows with nothing picked: a wild zone's own panel (its places), or nothing in a settlement.</summary>
    private MapTarget? HomeTarget(PlayerView view) =>
        _ui.Db.Get<ZoneDef>(view.Zone.ZoneId).Settlement ? null : new ZoneTarget(view.Zone.ZoneId);

    /// <summary>Executes a command; logs the rejection reason, or plays its events back week by week.</summary>
    private void Send(Command command)
    {
        var before = _session.Sim.View;
        var outcome = _session.Execute(command);
        if (outcome.RejectReason is { } reason)
        {
            _journal.Add(_ui.Text(reason));
            Refresh();
            return;
        }

        (_shownView, _shownWeek, _clock) = (before, before.Week, 0);
        foreach (var e in outcome.Events)
            _playback.Enqueue(e);
        if (double.IsPositiveInfinity(_ui.Settings.WeeksPerSecond) || !outcome.Events.OfType<WeekPassed>().Any())
            FinishPlayback();
        else
            ShowPlayback();
    }

    public override void _Process(double delta)
    {
        if (_playback.Count == 0)
            return;
        _clock += delta * _ui.Settings.WeeksPerSecond;
        while (_clock >= 1 && _playback.Count > 0)
        {
            if (Reveal(_playback.Dequeue()))
                _clock -= 1;
        }
        // A week's own events follow its WeekPassed: show them with their week, not with the next one.
        while (_playback.TryPeek(out var next) && next is not WeekPassed)
            Reveal(_playback.Dequeue());
        if (_playback.Count == 0)
            FinishPlayback();
        else
            ShowPlayback();
    }

    /// <summary>Shows one event: a week moves the clock on, a notable event goes to the journal. True for a week.</summary>
    private bool Reveal(SimEvent e)
    {
        if (e is WeekPassed passed)
        {
            _shownWeek = passed.Week;
            _shownView = _shownView with { Week = passed.Week, AgeWeeks = passed.AgeWeeks };
            return true;
        }
        if (SimText.Describe(e) is { } text)
            _journal.Add(_ui.Text(JournalLine with { Args = [new("week", _shownWeek), new("text", text)] }));
        return false;
    }

    private void ShowPlayback()
    {
        _blocker.Visible = true;
        _status.Refresh(_shownView, playing: true);
    }

    /// <summary>Shows whatever is left of the playback at once and redraws the screen from the new view.</summary>
    private void FinishPlayback()
    {
        while (_playback.Count > 0)
            Reveal(_playback.Dequeue());
        _blocker.Visible = false;
        Refresh();
    }

    private void OnNewRunPressed()
    {
        if (_session.TryStartNewRun())
        {
            _journal.Clear();
            _shownZone = null;
        }
        Refresh();
    }

    private void Refresh()
    {
        var view = _session.Sim.View;
        string zoneId = view.Zone.ZoneId;
        if (zoneId != _shownZone)
        {
            _shownZone = zoneId;
            _regionMap = !_ui.Db.Get<ZoneDef>(zoneId).Settlement;
            _target = HomeTarget(view);
        }

        // Stepping into a place swaps the map for the place itself; a zone picked on the region map keeps the map.
        bool inside = _target is PlaceTarget or ShopTarget or InquireTarget or HuntTarget or ArenaTarget or JobsTarget;
        _map.Visible = !inside;
        _location.Visible = inside;
        if (inside)
            _location.Refresh(_ui, _target!);
        else if (_regionMap)
            _map.ShowRegion(_ui, view);
        else
            _map.ShowLocal(_ui, view);
        _mapTitle.Text = _regionMap && !inside ? _ui.Text("str.ui.region") : _ui.Name(zoneId);

        _summary.Refresh(_ui, view, OnNewRunPressed);
        _panel.Visible = view.Event is null && view.EndCause is null;
        _panel.Refresh(_ui, view, _target);
        _dialog.Refresh(_ui, view);
        _status.Refresh(view, playing: false, fighting: _battle.Visible);
        _tabs.Refresh(_ui, view);
    }

    /// <summary>Run and fight seeds come from the shell; Core itself never reads the clock or OS randomness.</summary>
    private static ulong NewSeed() => BitConverter.ToUInt64(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8));
}
