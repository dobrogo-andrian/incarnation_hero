using Core.Loc;
using Core.Sim;
using Godot;

/// <summary>
/// The side tabs, always at hand (D44): character, cultivation and breakthrough, items, rumors.
/// Each tab is a scrolling text built from one SimText section.
/// </summary>
public partial class SideTabs : TabContainer
{
    private readonly (string Title, Func<UiContext, PlayerView, IReadOnlyList<LocKey>> Lines)[] _tabs =
    [
        ("str.ui.tab_character", (_, view) => SimText.CharacterLines(view)),
        ("str.ui.tab_cultivation", (ui, view) => SimText.CultivationLines(view, ui.Db)),
        ("str.ui.tab_items", (_, view) => SimText.ItemLines(view)),
        ("str.ui.tab_rumors", (_, view) => SimText.SocialLines(view)),
    ];

    private readonly List<Label> _labels = [];

    /// <summary>Builds the tabs; called once before the first <see cref="Refresh"/>.</summary>
    public void Setup(UiContext ui)
    {
        CustomMinimumSize = new Vector2(420, 0);
        for (int i = 0; i < _tabs.Length; i++)
        {
            var label = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            scroll.AddChild(label);
            AddChild(scroll);
            SetTabTitle(i, ui.Text(_tabs[i].Title));
            _labels.Add(label);
        }
    }

    public void Refresh(UiContext ui, PlayerView view)
    {
        for (int i = 0; i < _tabs.Length; i++)
            _labels[i].Text = ui.Lines(_tabs[i].Lines(ui, view));
    }
}
