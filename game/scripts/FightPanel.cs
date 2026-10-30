using Core.Sim;
using Godot;
using static Widgets;

/// <summary>
/// The fight on offer (D67), in the centre panel like an event's dialog (D46): what is at stake and
/// each kind of foe as the player reads it (D70), then fighting by hand or leaving it to the AI.
/// Everything else waits: Core takes only the fight's commands until it is over.
/// </summary>
public partial class FightPanel : VBoxContainer
{
    public FightPanel()
    {
        AddThemeConstantOverride("separation", 8);
    }

    public void Refresh(UiContext ui, PlayerView view)
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        Visible = view.Fight is { ByHand: false };
        if (view.Fight is not { ByHand: false } fight)
            return;

        var lines = SimText.FightLines(fight);
        AddChild(Title(ui.Text(lines[0])));
        foreach (var line in lines.Skip(1))
            AddChild(Text(ui.Text(line)));
        AddChild(Row(
            Button(ui.Text("str.ui.fight_by_hand"), () => ui.Send(new FightByHand())),
            Button(ui.Text("str.ui.fight_auto"), () => ui.Send(new AutoResolveFight()))));
    }
}
