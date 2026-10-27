using Core.Sim;
using Godot;

/// <summary>
/// The open event or rumor dialog as a modal window (D44): the screen behind is dimmed and takes
/// no clicks until a choice is made.
/// </summary>
public partial class EventDialog : Control
{
    private readonly Label _text = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly VBoxContainer _choices = new();

    public EventDialog()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Ignore };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(720, 0) };
        var margin = new MarginContainer();
        foreach (string side in new[] { "left", "top", "right", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 24);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        _choices.AddThemeConstantOverride("separation", 8);

        column.AddChild(_text);
        column.AddChild(_choices);
        margin.AddChild(column);
        panel.AddChild(margin);
        center.AddChild(panel);
        AddChild(dim);
        AddChild(center);
    }

    public void Refresh(UiContext ui, PlayerView view)
    {
        foreach (var child in _choices.GetChildren())
            child.QueueFree();
        Visible = view.Event is not null;
        if (view.Event is not { } dialog)
            return;

        _text.Text = ui.Text(SimText.EventText(dialog, view));
        foreach (var (index, text, available) in SimText.EventChoices(dialog))
        {
            var button = new Button { Text = ui.Text(text), Disabled = !available, Alignment = HorizontalAlignment.Left };
            button.Pressed += () => ui.Send(new ChooseEventOption(index));
            _choices.AddChild(button);
        }
    }
}
