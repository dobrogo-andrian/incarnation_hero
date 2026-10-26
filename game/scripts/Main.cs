using Core.Content;
using Core.Loc;
using Core.Save;
using Core.Sim;
using Godot;

/// <summary>
/// Stage 0 shell: shows the PlayerView and sends one command. All rules, saving included, stay
/// in Core; this node only loads content, draws text and forwards the button press.
/// </summary>
public partial class Main : Control
{
    private static readonly LocKey WaitWeek = new("str.shell.wait_week");
    private static readonly LocKey ContentErrors = new("str.shell.content_errors");

    private ContentDb _db = null!;
    private Localizer _loc = null!;
    private RunSession? _session;

    private Label _view = null!;
    private Label _status = null!;
    private Button _wait = null!;

    public override void _Ready()
    {
        BuildLayout();

        // Dev build: content lives next to game/, not inside it (packaging is a stage 5 task).
        string contentDir = ProjectSettings.GlobalizePath("res://").PathJoin("../content").SimplifyPath();
        var loaded = ContentLoader.Load(ContentFiles.FromDirectory(contentDir));
        var errors = loaded.Errors.Concat(ContentValidator.Validate(loaded.Db)).ToList();
        _db = loaded.Db;
        _loc = Localizer.FromContent(_db);
        _wait.Text = _loc.Resolve(WaitWeek);

        if (errors.Count > 0)
        {
            _status.Text = _loc.Resolve(ContentErrors) + "\n" + string.Join("\n", errors);
            _wait.Disabled = true;
            return;
        }

        var store = new SaveStore(ProjectSettings.GlobalizePath("user://run.json"));
        _session = RunSession.OpenOrStart(store, () => Simulation.NewGame(NewSeed(), NewGameSettings.FromContent(_db)));
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
        _wait = new Button { SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        _wait.Pressed += OnWaitPressed;

        column.AddChild(_view);
        column.AddChild(_wait);
        column.AddChild(_status);
    }

    private void OnWaitPressed()
    {
        var outcome = _session!.Execute(new WaitWeeks(1));
        _status.Text = outcome.RejectReason is { } reason ? _loc.Resolve(reason) : "";
        Refresh();
    }

    private void Refresh() =>
        _view.Text = string.Join("\n", PlayerViewText.Lines(_session!.Sim.View, _db).Select(_loc.Resolve));

    /// <summary>The run seed comes from the shell; Core itself never reads the clock or OS randomness.</summary>
    private static ulong NewSeed() => BitConverter.ToUInt64(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8));
}
