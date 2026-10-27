using Core.Content;
using Core.Loc;
using Core.Sim;
using Godot;

/// <summary>
/// A static map screen (D44): the settlement's local map or the region map. Points are buttons
/// anchored at their YAML position (percent of the map), so the map scales with the window; region
/// routes are lines labelled with their weeks. Only what the PlayerView shows as known is drawn.
/// </summary>
public partial class MapView : Control
{
    /// <summary>Share of the map kept free at each edge so points never touch it.</summary>
    private const float Inset = 0.07f;

    private static readonly Color Background = new(0.10f, 0.11f, 0.13f);
    private static readonly Color RouteColor = new(0.50f, 0.46f, 0.38f);
    private static readonly Color HereColor = new(0.95f, 0.80f, 0.35f);

    private readonly List<(Vector2 A, Vector2 B, string Label)> _routes = [];

    /// <summary>A point other than the exit was pressed.</summary>
    public event Action<MapTarget>? PointPressed;

    /// <summary>The settlement's exit was pressed.</summary>
    public event Action? ExitPressed;

    public MapView()
    {
        ClipContents = true;
    }

    /// <summary>The current settlement: its places, shops, the inquiry point and the exit.</summary>
    public void ShowLocal(UiContext ui, PlayerView view)
    {
        Clear();
        string zoneId = view.Zone.ZoneId;
        var local = ui.Db.Get<ZoneDef>(zoneId).LocalMap!;

        foreach (var place in view.Places)
            AddPoint(ui.Db.Get<PlaceDef>(place.PlaceId).Map!, ui.Text(SimText.Name(place.PlaceId)), () => PointPressed?.Invoke(new PlaceTarget(place.PlaceId)));
        foreach (var shop in ui.Db.All<ShopDef>().Where(s => s.Zone == zoneId))
            AddPoint(shop.Map!, ui.Text(SimText.Name(shop.Id)), () => PointPressed?.Invoke(new ShopTarget(shop.Id)));
        AddPoint(local.Inquire!, ui.Text("str.ui.inquire"), () => PointPressed?.Invoke(new InquireTarget()));
        AddPoint(local.Exit!, ui.Text("str.ui.exit"), () => ExitPressed?.Invoke());
        QueueRedraw();
    }

    /// <summary>Every known zone with the routes between known zones; the player's zone is highlighted.</summary>
    public void ShowRegion(UiContext ui, PlayerView view)
    {
        Clear();
        var known = view.Region.Select(z => z.ZoneId).ToHashSet();
        foreach (var route in ui.Db.All<RouteDef>().Where(r => known.Contains(r.A) && known.Contains(r.B)))
        {
            var label = ui.Text(LocKey.Of("str.ui.route_weeks", new LocArg("weeks", route.Weeks)));
            _routes.Add((MapAnchor(ui.Db.Get<ZoneDef>(route.A).Map!), MapAnchor(ui.Db.Get<ZoneDef>(route.B).Map!), label));
        }
        foreach (var zone in view.Region)
        {
            var button = AddPoint(ui.Db.Get<ZoneDef>(zone.ZoneId).Map!, ui.Text(SimText.Name(zone.ZoneId)), () => PointPressed?.Invoke(new ZoneTarget(zone.ZoneId)));
            if (zone.ZoneId == view.Zone.ZoneId)
                button.AddThemeColorOverride("font_color", HereColor);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Background);
        var font = GetThemeDefaultFont();
        int fontSize = GetThemeDefaultFontSize();
        foreach (var (a, b, label) in _routes)
        {
            Vector2 from = a * Size, to = b * Size;
            DrawLine(from, to, RouteColor, 3);
            DrawString(font, (from + to) / 2 + new Vector2(-60, -6), label, HorizontalAlignment.Center, 120, fontSize, RouteColor);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            QueueRedraw();
    }

    private void Clear()
    {
        _routes.Clear();
        foreach (var child in GetChildren())
            child.QueueFree();
    }

    /// <summary>A button centred on the point: anchored there and growing both ways from it.</summary>
    private Button AddPoint(MapPoint point, string text, Action pressed)
    {
        var at = MapAnchor(point);
        var button = new Button
        {
            Text = text,
            AnchorLeft = at.X,
            AnchorRight = at.X,
            AnchorTop = at.Y,
            AnchorBottom = at.Y,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both,
        };
        button.Pressed += pressed;
        AddChild(button);
        return button;
    }

    /// <summary>A map point as a share of the map's size, inside the inset.</summary>
    private static Vector2 MapAnchor(MapPoint point) =>
        new(Inset + (1 - 2 * Inset) * (float)point.X / 100, Inset + (1 - 2 * Inset) * (float)point.Y / 100);
}
