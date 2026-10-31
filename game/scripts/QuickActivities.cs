using Core.Content;
using Core.Sim;
using Core.State;
using Godot;

/// <summary>
/// The two ways to spend time from the main screen, under the character preview (D72): meditate to
/// recover health and qi at the zone's best place, anywhere, and seek adventure in a settlement for
/// likelier events and rumors. Both run until something interrupts them or there is nothing left to do.
/// </summary>
public partial class QuickActivities : HBoxContainer, IViewPart
{
    public QuickActivities() => AddThemeConstantOverride("separation", 8);

    public void Refresh(UiContext ui, PlayerView view)
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        bool free = view.Mode == GameMode.Overworld;

        var recovery = MeditationSystem.Recovery(view, ui.Db);
        var meditate = Wide(Widgets.Button(ui.Text("str.ui.meditate_here"),
            () => ui.Send(new StartActivity(recovery!, null)), free && recovery is not null));
        meditate.TooltipText = ui.Text(recovery is null ? "str.ui.no_place_to_meditate" : "str.ui.meditate_here_hint");
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
