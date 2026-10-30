using Core.Sim;
using Godot;
using static Widgets;

/// <summary>
/// The open event or rumor dialog, shown in the centre panel in place of the point's actions (D44),
/// right above the journal, so the player's eyes stay in one place. Other actions are refused by
/// Core until a choice is made. While a fight of the event is on, the dialog waits for its end.
/// </summary>
public partial class EventDialog : VBoxContainer
{
    public EventDialog()
    {
        AddThemeConstantOverride("separation", 10);
    }

    public void Refresh(UiContext ui, PlayerView view)
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        Visible = view.Event is not null && view.Fight is null;
        if (!Visible)
            return;
        var dialog = view.Event!;

        AddChild(Text(ui.Text(SimText.EventText(dialog, view))));
        foreach (var (index, text, available) in SimText.EventChoices(dialog))
        {
            var button = Button(ui.Text(text), () => ui.Send(new ChooseEventOption(index)), available);
            button.Alignment = HorizontalAlignment.Left;
            button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            AddChild(button);
        }
    }
}
