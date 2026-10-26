using Core.Content;
using Core.Loc;
using Core.Save;
using Core.Sim;
using Core.State;
using Godot;

/// <summary>
/// Temporary shell until the stage 1 UI: shows the PlayerView and sends wait commands. All rules,
/// saving included, stay in Core; this node only loads content, draws text and forwards input.
/// </summary>
public partial class Main : Control
{
    private static readonly LocKey WaitWeek = new("str.shell.wait_week");
    private static readonly LocKey WaitUntilInterrupt = new("str.shell.wait_until_interrupt");
    private static readonly LocKey NewRun = new("str.shell.new_run");
    private static readonly LocKey ContentErrors = new("str.shell.content_errors");

    private ContentDb _db = null!;
    private Localizer _loc = null!;
    private RunSession? _session;

    private Label _view = null!;
    private Label _status = null!;
    private Button _waitWeek = null!;
    private Button _waitOpen = null!;
    private Button _newRun = null!;

    public override void _Ready()
    {
        BuildLayout();

        // Dev build: content lives next to game/, not inside it (packaging is a stage 5 task).
        string contentDir = ProjectSettings.GlobalizePath("res://").PathJoin("../content").SimplifyPath();
        var loaded = ContentLoader.Load(ContentFiles.FromDirectory(contentDir));
        var errors = loaded.Errors.Concat(ContentValidator.Validate(loaded.Db)).ToList();
        _db = loaded.Db;
        _loc = Localizer.FromContent(_db);
        _waitWeek.Text = _loc.Resolve(WaitWeek);
        _waitOpen.Text = _loc.Resolve(WaitUntilInterrupt);
        _newRun.Text = _loc.Resolve(NewRun);

        if (errors.Count > 0)
        {
            _status.Text = _loc.Resolve(ContentErrors) + "\n" + string.Join("\n", errors);
            _waitWeek.Disabled = _waitOpen.Disabled = true;
            _newRun.Visible = false;
            return;
        }

        var store = new SaveStore(ProjectSettings.GlobalizePath("user://run.json"));
        _session = RunSession.OpenOrStart(store, _db, NewSeed);
        _status.Text = _session.Notice is { } notice ? _loc.Resolve(notice) : "";
        Refresh();
    }

    private void BuildLayout()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (string side in new[] { "left", "top", "right", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 24);
        AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        margin.AddChild(column);

        _view = new Label();
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _waitWeek = new Button();
        _waitOpen = new Button();
        _newRun = new Button();
        _waitWeek.Pressed += () => Send(new StartActivity(new WaitActivity(), 1));
        _waitOpen.Pressed += () => Send(new StartActivity(new WaitActivity(), null));
        _newRun.Pressed += OnNewRunPressed;

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        buttons.AddChild(_waitWeek);
        buttons.AddChild(_waitOpen);
        buttons.AddChild(_newRun);

        column.AddChild(_view);
        column.AddChild(buttons);
        column.AddChild(_status);
    }

    private void Send(Command command)
    {
        var outcome = _session!.Execute(command);
        var lines = outcome.RejectReason is { } reason
            ? [_loc.Resolve(reason)]
            : outcome.Events.Select(SimText.Describe).OfType<LocKey>().Select(_loc.Resolve);
        _status.Text = string.Join("\n", lines);
        Refresh();
    }

    private void OnNewRunPressed()
    {
        if (_session!.TryStartNewRun())
            _status.Text = "";
        Refresh();
    }

    private void Refresh()
    {
        var view = _session!.Sim.View;
        _view.Text = string.Join("\n", SimText.ViewLines(view, _db).Select(_loc.Resolve));
        bool over = view.Mode == GameMode.RunEnd;
        _waitWeek.Disabled = _waitOpen.Disabled = over;
        _newRun.Visible = over;
    }

    /// <summary>The run seed comes from the shell; Core itself never reads the clock or OS randomness.</summary>
    private static ulong NewSeed() => BitConverter.ToUInt64(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8));
}
