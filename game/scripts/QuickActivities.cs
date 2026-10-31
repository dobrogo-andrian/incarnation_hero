using Core.Content;
using Core.Sim;
using Core.State;
using Godot;

/// <summary>
/// The two ways to spend time from the main screen, under the character preview (D72, D73): meditate,
/// which recovers health, injuries and qi at the zone's best place anywhere while the body needs it
/// and otherwise offers a direction (<see cref="MeditationPicker"/>), and seek adventure in a
/// settlement for likelier events and rumors. Both run until something interrupts them or there is
/// nothing left to do; resting has no button of its own any more.
/// </summary>
public partial class QuickActivities : HBoxContainer, IViewPart
{
    private readonly MeditationPicker _picker = new();

    public QuickActivities()
    {
        AddThemeConstantOverride("separation", 8);
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
        AddChild(meditate);

        bool settlement = ui.Db.Get<ZoneDef>(view.Zone.ZoneId).Settlement;
        var adventure = Wide(Widgets.Button(ui.Text("str.ui.adventure"),
            () => ui.Send(new StartActivity(new AdventureActivity(), null)), free && settlement));
        adventure.TooltipText = ui.Text(settlement ? "str.ui.adventure_hint" : "str.ui.adventure_not_here");
        AddChild(adventure);
    }

    private static Button Wide(Button button)
    {
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.CustomMinimumSize = new Vector2(0, 40);
        return button;
    }
}
