using Core.Sim;
using Godot;
using static Widgets;

/// <summary>
/// The body's condition beside the figure in the character window: each resource (health, body
/// energy, the qi pools) as its numbers over a coloured bar, its maximum's sources in the tooltip,
/// then the injuries. A resource drawn over a stat's scale (toxins on purity) is left out.
/// </summary>
public partial class ConditionView : VBoxContainer, IViewPart
{
    /// <summary>Bar colours by resource; any other resource is grey. Presentation only.</summary>
    private static readonly Dictionary<string, Color> Colors = new()
    {
        ["resource.health"] = new(0.78f, 0.27f, 0.25f),
        ["resource.body_energy"] = new(0.85f, 0.7f, 0.3f),
        ["resource.qi_pool"] = new(0.3f, 0.5f, 0.85f),
        ["resource.combat_qi"] = new(0.35f, 0.75f, 0.85f),
    };

    private static readonly Color OtherColor = new(0.6f, 0.6f, 0.6f);

    public ConditionView()
    {
        AddThemeConstantOverride("separation", 4);
    }

    public void Refresh(UiContext ui, PlayerView view)
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        foreach (var resource in view.Resources.Where(r => !StatsView.ShownOnAStat(view, r.ResourceId)))
        {
            string tooltip = ui.Lines(SimText.BreakdownLines(resource.Max));
            var label = new Label { Text = ui.Text(SimText.ResourceText(resource)), TooltipText = tooltip, MouseFilter = MouseFilterEnum.Pass };
            AddChild(label);
            AddChild(Bar(resource.Max.Value, resource.Current, Colors.GetValueOrDefault(resource.ResourceId, OtherColor), tooltip, height: 14));
        }
        var injuries = SimText.ConditionLines(view with { Resources = [] });
        if (injuries.Count > 0)
        {
            AddChild(new HSeparator());
            AddChild(Explained(ui, injuries));
        }
    }
}
