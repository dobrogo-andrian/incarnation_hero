using Core.Sim;
using Core.State;
using Godot;
using static Widgets;

/// <summary>
/// The "settings" tab: how fast weeks are shown (D47), kept between runs by <see cref="UiSettings"/>,
/// and which interrupts stop an activity (D44), one check box per category. Every toggle sends the
/// whole set to Core at once; categories left unchecked are still reported in the journal.
/// </summary>
public partial class SettingsTab : VBoxContainer, IViewPart
{
    private readonly Label _speedText = new();
    private readonly HSlider _speed = new() { MinValue = 0, Step = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly VBoxContainer _interrupts = new();
    private UiContext? _ui;

    public SettingsTab()
    {
        AddThemeConstantOverride("separation", 8);
    }

    public void Refresh(UiContext ui, PlayerView view)
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
                Disabled = view.Mode != GameMode.Overworld,
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

        AddChild(Title(ui.Text("str.ui.speed_title")));
        AddChild(_speedText);
        AddChild(_speed);
        AddChild(new HSeparator());
        AddChild(Title(ui.Text("str.ui.interrupts_title")));
        AddChild(_interrupts);
    }

    private void ShowSpeed()
    {
        double speed = _ui!.Settings.WeeksPerSecond;
        _speedText.Text = double.IsPositiveInfinity(speed)
            ? _ui.Text("str.ui.speed_instant")
            : _ui.Text(new Core.Loc.LocKey("str.ui.speed") with { Args = [new("weeks", speed)] });
    }
}
