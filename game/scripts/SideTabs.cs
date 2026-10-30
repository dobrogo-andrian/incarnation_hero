using Core.Sim;
using Godot;

/// <summary>A part of the screen redrawn from each new PlayerView.</summary>
public interface IViewPart
{
    void Refresh(UiContext ui, PlayerView view);
}

/// <summary>
/// The side tabs, always at hand (D44): character, cultivation and breakthrough, items, rumors, settings.
/// Each tab scrolls; text tabs show one SimText section, the others add their actions.
/// </summary>
public partial class SideTabs : TabContainer
{
    private readonly List<IViewPart> _parts = [];

    /// <summary>Builds the tabs; called once before the first <see cref="Refresh"/>.</summary>
    public void Setup(UiContext ui)
    {
        CustomMinimumSize = new Vector2(440, 0);
        ClipTabs = false;   // every tab title in sight: the column grows to fit them rather than scroll
        Add(ui, "str.ui.tab_character", new CharacterTab());
        Add(ui, "str.ui.tab_cultivation", new BreakthroughTab());
        Add(ui, "str.ui.tab_items", new ItemsTab());
        Add(ui, "str.ui.tab_rumors", new TextTab { Lines = (_, view) => SimText.SocialLines(view).Select(ExplainedLine.Plain) });
        Add(ui, "str.ui.tab_settings", new SettingsTab());
    }

    public void Refresh(UiContext ui, PlayerView view)
    {
        foreach (var part in _parts)
            part.Refresh(ui, view);
    }

    private void Add<T>(UiContext ui, string title, T tab) where T : Control, IViewPart
    {
        tab.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.AddChild(tab);
        AddChild(scroll);
        SetTabTitle(GetTabCount() - 1, ui.Text(title));
        _parts.Add(tab);
    }
}
