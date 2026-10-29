using Core.Content;
using Core.Loc;
using Core.Sim;
using Godot;

/// <summary>
/// A static map screen (D44): the settlement's local map or the region map. Points are buttons
/// anchored at their YAML position (percent of the map), so the map scales with the window; region
/// routes are lines labelled with their weeks. Only what the PlayerView shows as known is drawn.
/// Local points of one district are framed together under its name (D52).
/// </summary>
public partial class MapView : Control
{
    /// <summary>Share of the map kept free at each edge so points never touch it.</summary>
    private const float Inset = 0.07f;

    private static readonly Color Background = new(0.10f, 0.11f, 0.13f);
    private static readonly Color RouteColor = new(0.50f, 0.46f, 0.38f);
    private static readonly Color HereColor = new(0.95f, 0.80f, 0.35f);
    private static readonly Color DistrictColor = new(0.45f, 0.50f, 0.58f);

    /// <summary>Space between a district's frame and its points.</summary>
    private const float DistrictPadding = 14;

    private readonly List<(Vector2 A, Vector2 B, string Label)> _routes = [];
    private readonly List<(Button Point, string District)> _districtPoints = [];

    /// <summary>A point other than the exit was pressed.</summary>
    public event Action<MapTarget>? PointPressed;

    /// <summary>The settlement's exit was pressed.</summary>
    public event Action? ExitPressed;

    public MapView()
    {
        ClipContents = true;
    }

    /// <summary>The current settlement: its places, shops, hunting grounds, arenas, job board, the inquiry point and the exit.</summary>
    public void ShowLocal(UiContext ui, PlayerView view)
    {
        Clear();
        string zoneId = view.Zone.ZoneId;
        var local = ui.Db.Get<ZoneDef>(zoneId).LocalMap!;

        foreach (var place in view.Places)
            AddLocal(ui, ui.Db.Get<PlaceDef>(place.PlaceId).Map!, ui.Name(place.PlaceId), new PlaceTarget(place.PlaceId));
        foreach (var shop in ui.Db.All<ShopDef>().Where(s => s.Zone == zoneId))
            AddLocal(ui, shop.Map!, ui.Name(shop.Id), new ShopTarget(shop.Id));
        foreach (var hunt in ui.Db.All<HuntDef>().Where(h => h.Zone == zoneId))
            AddLocal(ui, hunt.Map!, ui.Name(hunt.Id), new HuntTarget(hunt.Id));
        foreach (var arena in ui.Db.All<ArenaDef>().Where(a => a.Zone == zoneId))
            AddLocal(ui, arena.Map!, ui.Name(arena.Id), new ArenaTarget(arena.Id));
        if (local.Jobs is { } jobs)
            AddLocal(ui, jobs, ui.Text("str.ui.jobs"), new JobsTarget());
        AddLocal(ui, local.Inquire!, ui.Text("str.ui.inquire"), new InquireTarget());
        AddPoint(local.Exit!, ui.Text("str.ui.exit"), () => ExitPressed?.Invoke());
        QueueRedraw();
    }

    /// <summary>A local point that opens <paramref name="target"/>, remembered with its district for the frames.</summary>
    private void AddLocal(UiContext ui, MapPoint point, string text, MapTarget target)
    {
        var button = AddPoint(point, text, () => PointPressed?.Invoke(target));
        if (point.District is not { } district)
            return;
        _districtPoints.Add((button, ui.Name(district)));
        // Frames follow the points' final sizes, known only after layout.
        button.Resized += QueueRedraw;
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
        foreach (var district in _districtPoints.GroupBy(p => p.District))
        {
            var frame = district.Select(p => p.Point.GetRect()).Aggregate((a, b) => a.Merge(b)).Grow(DistrictPadding);
            DrawRect(frame, DistrictColor, filled: false, width: 2);
            DrawString(font, frame.Position + new Vector2(4, -6), district.Key, HorizontalAlignment.Left, -1, fontSize, DistrictColor);
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
        _districtPoints.Clear();
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
