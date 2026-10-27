using Core.Sim;
using Core.State;
using Godot;

/// <summary>
/// Which interrupts stop an activity (D44): one check box per category. Every toggle sends the
/// whole set to Core at once; categories left unchecked are still reported in the journal.
/// </summary>
public partial class InterruptsPopup : PopupPanel
{
    private readonly VBoxContainer _list = new();

    public InterruptsPopup()
    {
        AddChild(_list);
    }

    public void Refresh(UiContext ui, PlayerView view)
    {
        foreach (var child in _list.GetChildren())
            child.QueueFree();
        _list.AddChild(Widgets.Title(ui.Text("str.ui.interrupts_title")));

        var stops = view.StoppingInterrupts.ToHashSet();
        foreach (var category in Enum.GetValues<InterruptCategory>())
        {
            var box = new CheckBox
            {
                Text = ui.Text(SimText.InterruptName(category)),
                ButtonPressed = stops.Contains(category),
                Disabled = view.Mode != GameMode.Overworld,
            };
            box.Toggled += on =>
            {
                if (on)
                    stops.Add(category);
                else
                    stops.Remove(category);
                ui.Send(new SetInterruptStops(Enum.GetValues<InterruptCategory>().Where(stops.Contains).ToList()));
            };
            _list.AddChild(box);
        }
    }
}
