using Core.Content;
using Core.Sim;
using Core.State;
using Godot;

/// <summary>
/// The two ways to spend time from the main screen, under the character preview (D72, D73): meditate,
/// which recovers health, injuries and qi at the zone's best place anywhere while the body needs it
/// and otherwise offers a direction (<see cref="MeditationPicker"/>), and seek adventure in a
/// settlement for likelier events and rumors. Recovery and adventure run until something interrupts them
/// or there is nothing left to do; the weeks are chosen only for a picked direction. Below the buttons
/// the zone's danger and the qi density of the place meditation goes to (D74).
/// </summary>
public partial class QuickActivities : VBoxContainer, IViewPart
{
    private readonly MeditationPicker _picker = new();

    public QuickActivities()
    {
        AddThemeConstantOverride("separation", 6);
        AddChild(_picker);
    }

    public void Refresh(UiContext ui, PlayerView view)
    {
        foreach (var child in GetChildren())
        {
            if (child != _picker)
                child.QueueFree();
        }
        bool free = view.Mode == GameMode.Overworld;

        // Recovery while the body needs it, else the choice of a direction (D73).
        var recovery = MeditationSystem.Recovery(view, ui.Db);
        Button meditate = null!;
        meditate = Wide(Widgets.Button(ui.Text("str.ui.meditate_here"), () =>
        {
            if (recovery is not null)
                ui.Send(new StartActivity(recovery, null));
            else
                _picker.Open(ui, view, meditate);
        }, free));
        meditate.TooltipText = ui.Text(recovery is null ? "str.ui.meditate_pick_hint" : "str.ui.meditate_here_hint");

        bool settlement = ui.Db.Get<ZoneDef>(view.Zone.ZoneId).Settlement;
        var adventure = Wide(Widgets.Button(ui.Text("str.ui.adventure"),
            () => ui.Send(new StartActivity(new AdventureActivity(), null)), free && settlement));
        adventure.TooltipText = ui.Text(settlement ? "str.ui.adventure_hint" : "str.ui.adventure_not_here");
        AddChild(Widgets.Row(meditate, adventure));
        AddChild(new Label { Text = ui.Text(SimText.MeditationHereText(view, ui.Db)), AutowrapMode = TextServer.AutowrapMode.WordSmart });
    }

    private static Button Wide(Button button)
    {
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.CustomMinimumSize = new Vector2(0, 40);
        return button;
    }
}
