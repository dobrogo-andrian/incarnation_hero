using Core.Sim;
using Godot;

/// <summary>A tab that is just text: one SimText section, with tooltips where a number is explained.</summary>
public partial class TextTab : MarginContainer, IViewPart
{
    public Func<UiContext, PlayerView, IEnumerable<ExplainedLine>> Lines { get; init; } = (_, _) => [];

    public void Refresh(UiContext ui, PlayerView view)
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        AddChild(Widgets.Explained(ui, Lines(ui, view)));
    }
}
