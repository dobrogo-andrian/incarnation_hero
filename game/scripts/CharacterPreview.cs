using Core.Sim;
using Godot;

/// <summary>
/// The character at a glance on the main screen, above the rumors: the figure and the body's
/// condition beside it (<see cref="ConditionView"/>), without slots or inventory. A click anywhere
/// on it, the figure or the bars, opens the <see cref="CharacterWindow"/>; it is the only way in
/// besides the C key.
/// </summary>
public partial class CharacterPreview : PanelContainer, IViewPart
{
    private readonly ConditionView _condition = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private Action _open = () => { };

    /// <summary>Builds the figure; called once before the first <see cref="Refresh"/>.</summary>
    public void Setup(UiContext ui, Action open)
    {
        _open = open;
        TooltipText = ui.Text("str.ui.character");
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.17f, 0.17f, 0.18f),
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10,
        });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        var figure = new Control { CustomMinimumSize = new Vector2(120, 170), MouseFilter = MouseFilterEnum.Pass };
        figure.Draw += () => CharacterWindow.DrawSilhouette(figure);
        row.AddChild(figure);
        row.AddChild(_condition);
        AddChild(row);
    }

    public void Refresh(UiContext ui, PlayerView view) => _condition.Refresh(ui, view);

    /// <summary>A left click on the panel or anything in it (the bars pass their clicks up) opens the window.</summary>
    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            _open();
            AcceptEvent();
        }
    }
}
