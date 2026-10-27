using Core.Content;
using Core.Loc;
using Core.Save;
using Core.Sim;
using Godot;

/// <summary>
/// The stage 1 playtest screen (D44): status bar on top, the map with the picked point's panel in
/// the middle, side tabs on the right, the journal at the bottom, event dialogs over everything.
/// All rules, saving included, stay in Core; this node loads content, owns the run session, sends
/// commands and redraws every part from the new PlayerView.
/// </summary>
public partial class Main : Control
{
    private static readonly LocKey ContentErrors = new("str.shell.content_errors");
    private static readonly LocKey JournalLine = new("str.ui.journal_line");

    private UiContext _ui = null!;
    private RunSession _session = null!;

    private readonly StatusBar _status = new();
    private readonly Label _mapTitle = new();
    private readonly MapView _map = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly PointPanel _panel = new();
    private readonly SideTabs _tabs = new();
    private readonly JournalView _journal = new();
    private readonly EventDialog _dialog = new();

    /// <summary>The zone the map was last set up for: arriving somewhere resets the map level and the picked point.</summary>
    private string? _mapZone;
    private bool _regionMap;
    private MapTarget? _target;

    public override void _Ready()
    {
        // Dev build: content lives next to game/, not inside it (packaging is a stage 5 task).
        string contentDir = ProjectSettings.GlobalizePath("res://").PathJoin("../content").SimplifyPath();
        var loaded = ContentLoader.Load(ContentFiles.FromDirectory(contentDir));
        var errors = loaded.Errors.Concat(ContentValidator.Validate(loaded.Db)).ToList();
        var loc = Localizer.FromContent(loaded.Db);
        if (errors.Count > 0)
        {
            AddChild(new Label { Text = loc.Resolve(ContentErrors) + "\n" + string.Join("\n", errors) });
            return;
        }

        _ui = new UiContext(loaded.Db, loc, Send);
        BuildLayout();

        var store = new SaveStore(ProjectSettings.GlobalizePath("user://run.json"));
        _session = RunSession.OpenOrStart(store, loaded.Db, NewSeed);
        if (_session.Notice is { } notice)
            _journal.Add(_ui.Text(notice));
        Refresh();
    }

    private void BuildLayout()
    {
        _status.Setup(_ui, OnNewRunPressed);
        _tabs.Setup(_ui);
        _map.PointPressed += target => { _target = target; Refresh(); };
        _map.ExitPressed += () => { _regionMap = true; _target = null; Refresh(); };
        _panel.EnterPressed += () => { _regionMap = false; _target = null; Refresh(); };
        _mapTitle.AddThemeFontSizeOverride("font_size", 22);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (string side in new[] { "left", "top", "right", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 16);

        var mapRow = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        mapRow.AddThemeConstantOverride("separation", 16);
        mapRow.AddChild(_map);
        mapRow.AddChild(_panel);

        var center = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        center.AddChild(_mapTitle);
        center.AddChild(mapRow);

        var middle = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        middle.AddThemeConstantOverride("separation", 16);
        middle.AddChild(center);
        middle.AddChild(_tabs);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        column.AddChild(_status);
        column.AddChild(middle);
        column.AddChild(_journal);

        margin.AddChild(column);
        AddChild(margin);
        AddChild(_dialog);
    }

    /// <summary>Executes a command and logs what happened: the rejection reason, or each notable event with its week.</summary>
    private void Send(Command command)
    {
        var outcome = _session.Execute(command);
        if (outcome.RejectReason is { } reason)
        {
            _journal.Add(_ui.Text(reason));
        }
        else
        {
            int week = _session.Sim.View.Week - outcome.Events.OfType<WeekPassed>().Count();
            foreach (var e in outcome.Events)
            {
                if (e is WeekPassed passed)
                    week = passed.Week;
                else if (SimText.Describe(e) is { } text)
                    _journal.Add(_ui.Text(JournalLine with { Args = [new("week", week), new("text", text)] }));
            }
        }
        Refresh();
    }

    private void OnNewRunPressed()
    {
        if (_session.TryStartNewRun())
        {
            _journal.Clear();
            _mapZone = null;
        }
        Refresh();
    }

    private void Refresh()
    {
        var view = _session.Sim.View;
        string zoneId = view.Zone.ZoneId;
        bool settlement = _ui.Db.Get<ZoneDef>(zoneId).Settlement;
        if (zoneId != _mapZone)
        {
            _mapZone = zoneId;
            _regionMap = !settlement;
            _target = null;
        }

        if (_regionMap)
        {
            _mapTitle.Text = _ui.Text("str.ui.region");
            _map.ShowRegion(_ui, view);
        }
        else
        {
            _mapTitle.Text = _ui.Text(SimText.Name(zoneId));
            _map.ShowLocal(_ui, view);
        }
        _panel.Refresh(_ui, view, _target);
        _status.Refresh(_ui, view);
        _tabs.Refresh(_ui, view);
        _dialog.Refresh(_ui, view);
    }

    /// <summary>The run seed comes from the shell; Core itself never reads the clock or OS randomness.</summary>
    private static ulong NewSeed() => BitConverter.ToUInt64(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8));
}
