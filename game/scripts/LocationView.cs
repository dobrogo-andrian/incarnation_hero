using Godot;
using static Widgets;

/// <summary>
/// The place the player has stepped into (D44), drawn where the map was: a tinted backdrop for its
/// kind, its name and description, and the way back to the map. A placeholder until art arrives.
/// </summary>
public partial class LocationView : PanelContainer
{
    private static readonly Color PlaceTint = new(0.10f, 0.17f, 0.13f);
    private static readonly Color ShopTint = new(0.20f, 0.14f, 0.09f);
    private static readonly Color InquireTint = new(0.09f, 0.15f, 0.18f);
    private static readonly Color HuntTint = new(0.12f, 0.15f, 0.08f);
    private static readonly Color ArenaTint = new(0.20f, 0.10f, 0.09f);

    /// <summary>"Back to the map" was pressed.</summary>
    public event Action? LeavePressed;

    public void Refresh(UiContext ui, MapTarget target)
    {
        foreach (var child in GetChildren())
            child.QueueFree();

        var (title, description, tint) = target switch
        {
            PlaceTarget p => (ui.Name(p.PlaceId), DescriptionOf(ui, p.PlaceId), PlaceTint),
            ShopTarget s => (ui.Name(s.ShopId), DescriptionOf(ui, s.ShopId), ShopTint),
            HuntTarget h => (ui.Name(h.HuntId), DescriptionOf(ui, h.HuntId), HuntTint),
            ArenaTarget a => (ui.Name(a.ArenaId), DescriptionOf(ui, a.ArenaId), ArenaTint),
            JobsTarget => (ui.Text("str.ui.jobs"), ui.Text("str.ui.jobs_hint"), ArenaTint),
            _ => (ui.Text("str.ui.inquire"), ui.Text("str.ui.inquire_hint"), InquireTint),
        };
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = tint });

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        column.AddThemeConstantOverride("separation", 24);
        var name = Title(title);
        name.AddThemeFontSizeOverride("font_size", 34);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        var text = Text(description);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(name);
        column.AddChild(text);
        column.AddChild(Button(ui.Text("str.ui.to_map"), () => LeavePressed?.Invoke()));

        var center = new CenterContainer();
        center.AddChild(column);
        AddChild(center);
    }

    private static string DescriptionOf(UiContext ui, string contentId) =>
        ui.Db.Get<Core.Content.ContentDef>(contentId) is Core.Content.IDescribedDef { Description: not null }
            ? ui.Text(Core.Sim.SimText.Description(contentId))
            : "";
}
