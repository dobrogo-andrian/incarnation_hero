using Core.Loc;
using Core.Sim;
using Godot;

/// <summary>A tab that is just text: one SimText section.</summary>
public partial class TextTab : Label, IViewPart
{
    public Func<UiContext, PlayerView, IReadOnlyList<LocKey>> Lines { get; init; } = (_, _) => [];

    public TextTab()
    {
        AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    public void Refresh(UiContext ui, PlayerView view) => Text = ui.Lines(Lines(ui, view));
}
