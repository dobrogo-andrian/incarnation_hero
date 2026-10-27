using Core.Content;
using Core.Loc;
using Core.Sim;
using Godot;

/// <summary>
/// What the picked map point offers: a zone's danger and journey with "set out", the current
/// settlement's entrance, a place's reserve, a shop's offers. Rebuilt from the PlayerView on every
/// refresh, so it never shows stale numbers.
/// </summary>
public partial class PointPanel : VBoxContainer
{
    /// <summary>"Enter the settlement" was pressed on the region map.</summary>
    public event Action? EnterPressed;

    public PointPanel()
    {
        CustomMinimumSize = new Vector2(340, 0);
        AddThemeConstantOverride("separation", 12);
    }

    public void Refresh(UiContext ui, PlayerView view, MapTarget? target)
    {
        foreach (var child in GetChildren())
            child.QueueFree();

        switch (target)
        {
            case null:
                AddText(ui.Text("str.ui.select_point"));
                break;
            case ZoneTarget zone:
                ShowZone(ui, view, zone.ZoneId);
                break;
            case PlaceTarget place when view.Places.FirstOrDefault(p => p.PlaceId == place.PlaceId) is { } here:
                AddTitle(ui.Text(SimText.Name(place.PlaceId)));
                AddText(ui.Text(SimText.PlaceText(here)));
                break;
            case ShopTarget shop:
                AddTitle(ui.Text(SimText.Name(shop.ShopId)));
                AddText(ui.Lines(view.ShopOffers.Where(o => o.ShopId == shop.ShopId).Select(SimText.OfferText)));
                break;
            case InquireTarget:
                AddTitle(ui.Text("str.ui.inquire"));
                AddText(ui.Text("str.ui.inquire_hint"));
                break;
        }
    }

    private void ShowZone(UiContext ui, PlayerView view, string zoneId)
    {
        if (view.Region.FirstOrDefault(z => z.ZoneId == zoneId) is not { } zone)
            return;
        AddTitle(ui.Text(SimText.Name(zoneId)));
        AddText(ui.Text(SimText.RegionZoneText(zone, view)));
        bool free = view.Mode == Core.State.GameMode.Overworld;

        if (zoneId != view.Zone.ZoneId)
        {
            if (zone.TravelWeeks is not null)
                AddButton(ui.Text("str.ui.travel"), free, () => ui.Send(new Travel(zoneId)));
            return;
        }
        if (ui.Db.Get<ZoneDef>(zoneId).Settlement)
            AddButton(ui.Text("str.ui.enter_settlement"), true, () => EnterPressed?.Invoke());
        else
            AddText(ui.Lines(SimText.PlaceLines(view)));
    }

    private void AddTitle(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 22);
        AddChild(label);
    }

    private void AddText(string text) =>
        AddChild(new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart });

    private void AddButton(string text, bool enabled, Action pressed)
    {
        var button = new Button { Text = text, Disabled = !enabled };
        button.Pressed += pressed;
        AddChild(button);
    }
}
