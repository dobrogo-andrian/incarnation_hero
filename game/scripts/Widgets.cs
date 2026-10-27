using Godot;

/// <summary>
/// The few control shapes the panels and tabs are built from, so every picker looks and binds the
/// same way. Pickers write the player's pick into <see cref="Choices"/>; Core still checks every command.
/// </summary>
public static class Widgets
{
    public static Label Title(string text)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", 22);
        return label;
    }

    public static Label Text(string text) => new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };

    public static Button Button(string text, Action pressed, bool enabled = true)
    {
        var button = new Button { Text = text, Disabled = !enabled };
        button.Pressed += pressed;
        return button;
    }

    public static HBoxContainer Row(params Control[] children)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        foreach (var child in children)
            row.AddChild(child);
        return row;
    }

    /// <summary>The picked ID if it is still offered, else the first option (null when there are none).</summary>
    public static string? Pick(IReadOnlyList<(string Id, string Text)> options, string? picked) =>
        options.Any(o => o.Id == picked) ? picked : options.FirstOrDefault().Id;

    /// <summary>A drop-down over <paramref name="options"/> showing <paramref name="selected"/>.</summary>
    public static OptionButton Options(IReadOnlyList<(string Id, string Text)> options, string? selected, Action<string> picked)
    {
        var box = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        for (int i = 0; i < options.Count; i++)
        {
            box.AddItem(options[i].Text, i);
            if (options[i].Id == selected)
                box.Selected = i;
        }
        box.ItemSelected += index => picked(options[(int)index].Id);
        return box;
    }

    /// <summary>A whole-number field from 1 to <paramref name="max"/>.</summary>
    public static SpinBox Amount(int value, int max, Action<int> changed)
    {
        var spin = new SpinBox { MinValue = 1, MaxValue = max, Value = value, Rounded = true };
        spin.ValueChanged += v => changed((int)v);
        return spin;
    }

    /// <summary>Weeks of an activity, or "until done" (null weeks), bound to <paramref name="choices"/>.</summary>
    public static HBoxContainer Duration(UiContext ui)
    {
        var choices = ui.Choices;
        var weeks = Amount(choices.Weeks, 52 * 20, v => choices.Weeks = v);
        weeks.Editable = !choices.UntilDone;
        var open = new CheckBox { Text = ui.Text("str.ui.until_done"), ButtonPressed = choices.UntilDone };
        open.Toggled += on =>
        {
            choices.UntilDone = on;
            weeks.Editable = !on;
        };
        return Row(new Label { Text = ui.Text("str.ui.weeks") }, weeks, open);
    }
}
