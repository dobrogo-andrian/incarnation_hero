using Core.Sim;
using Core.State;
using Godot;
using static Widgets;

/// <summary>The "items" tab: stones and carried items, a pill can be taken, stones can refill the qi pool.</summary>
public partial class ItemsTab : VBoxContainer, IViewPart
{
    public ItemsTab()
    {
        AddThemeConstantOverride("separation", 8);
    }

    public void Refresh(UiContext ui, PlayerView view)
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        bool free = view.Mode == GameMode.Overworld;
        var choices = ui.Choices;

        AddChild(Text(ui.Text(SimText.StonesText(view))));
        AddChild(Row(
            Amount(choices.Stones, 9999, v => choices.Stones = v),
            Button(ui.Text("str.ui.restore_qi"), () => ui.Send(new RestoreQiWithStones(choices.Stones)), free)));
        AddChild(new HSeparator());

        foreach (var item in view.Items)
        {
            var line = Text(ui.Text(SimText.ItemText(item)));
            line.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            if (item.NextPillEnergy is null)
                AddChild(line);
            else
                AddChild(Row(line, Button(ui.Text("str.ui.use_pill"), () => ui.Send(new UsePill(item.ItemId)), free)));
        }
    }
}
