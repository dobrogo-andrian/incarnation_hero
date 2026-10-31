using Core.Sim;
using Godot;

/// <summary>
/// The character at a glance on the main screen, above the rumors: the figure and the body's
/// condition beside it (<see cref="ConditionView"/>), without slots or inventory. Pressing the
/// figure opens the <see cref="CharacterWindow"/>.
/// </summary>
public partial class CharacterPreview : PanelContainer, IViewPart
{
    private readonly ConditionView _condition = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };

    /// <summary>Builds the figure; called once before the first <see cref="Refresh"/>.</summary>
    public void Setup(UiContext ui, Action open)
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.17f, 0.17f, 0.18f),
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10,
        });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        var figure = new Button
        {
            Flat = true,
            CustomMinimumSize = new Vector2(120, 170),
            FocusMode = FocusModeEnum.None,
            TooltipText = ui.Text("str.ui.character"),
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        figure.Draw += () => CharacterWindow.DrawSilhouette(figure);
        figure.Pressed += open;
        row.AddChild(figure);
        row.AddChild(_condition);
        AddChild(row);
    }

    public void Refresh(UiContext ui, PlayerView view) => _condition.Refresh(ui, view);
}
