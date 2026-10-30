using Core.Sim;
using Core.State;
using Godot;
using static Widgets;

/// <summary>
/// The settings, opened from the status bar next to quitting: how fast weeks are shown (D47), kept
/// between runs by <see cref="UiSettings"/>, and which interrupts stop an activity (D44), one check
/// box per category. Every toggle sends the whole set to Core at once; categories left unchecked are
/// still reported in the journal. The speed can be changed while weeks play back, the interrupts not.
/// </summary>
public partial class SettingsPopup : PopupPanel
{
    private readonly VBoxContainer _content = new() { CustomMinimumSize = new Vector2(360, 0) };
    private readonly Label _speedText = new();
    private readonly HSlider _speed = new() { MinValue = 0, Step = 1, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
    private readonly VBoxContainer _interrupts = new();
    private UiContext? _ui;

    public SettingsPopup()
    {
        _content.AddThemeConstantOverride("separation", 8);
        AddChild(_content);
    }

    public void Refresh(UiContext ui, PlayerView view, bool playing)
    {
        if (_ui is null)
            Build(ui);

        foreach (var child in _interrupts.GetChildren())
            child.QueueFree();
        var stops = view.StoppingInterrupts.ToHashSet();
        foreach (var category in Enum.GetValues<InterruptCategory>())
        {
            var box = new CheckBox
            {
                Text = ui.Text(SimText.InterruptName(category)),
                ButtonPressed = stops.Contains(category),
                Disabled = playing || view.Mode != GameMode.Overworld,
            };
            box.Toggled += on =>
            {
                if (on)
                    stops.Add(category);
                else
                    stops.Remove(category);
                ui.Send(new SetInterruptStops(Enum.GetValues<InterruptCategory>().Where(stops.Contains).ToList()));
            };
            _interrupts.AddChild(box);
        }
    }

    /// <summary>The slider is built once, so a refresh never interrupts dragging it.</summary>
    private void Build(UiContext ui)
    {
        _ui = ui;
        _speed.MaxValue = UiSettings.Speeds.Length - 1;
        _speed.Value = ui.Settings.Speed;
        _speed.ValueChanged += v =>
        {
            ui.Settings.SetSpeed((int)v);
            ShowSpeed();
        };
        ShowSpeed();

        foreach (var node in new Control[] { Heading("str.ui.speed_title"), _speedText, _speed, new HSeparator(),
                     Heading("str.ui.interrupts_title"), _interrupts })
            _content.AddChild(node);

        // A popup sizes itself to its content, which a wrapping label measured before it has a width blows up.
        Label Heading(string key)
        {
            var label = Title(ui.Text(key));
            label.AutowrapMode = TextServer.AutowrapMode.Off;
            return label;
        }
    }

    private void ShowSpeed()
    {
        double speed = _ui!.Settings.WeeksPerSecond;
        _speedText.Text = double.IsPositiveInfinity(speed)
            ? _ui.Text("str.ui.speed_instant")
            : _ui.Text(new Core.Loc.LocKey("str.ui.speed") with { Args = [new("weeks", speed)] });
    }
}
