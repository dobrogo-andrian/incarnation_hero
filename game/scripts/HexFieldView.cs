using Core.Combat;
using Godot;

/// <summary>
/// The hex field of a battle (D56), pointy side up, scaled to fit the control. It draws what it is
/// given — the fighters as the screen shows them and the highlighted hexes — and reports the hex
/// under the mouse; it knows no rules.
/// </summary>
public partial class HexFieldView : Control
{
    /// <summary>How a highlighted hex is drawn.</summary>
    public enum Mark
    {
        Reach,
        ReachWithFreeAttack,
        Aim,
        Struck,
        Path,
    }

    /// <summary>A fighter as the screen shows it: during playback the position and bars lag behind Core.</summary>
    public sealed record Token(string Label, int Side, Hex Position, double Health, double MaxHealth, double Shield, double MaxShield, bool Active);

    private static readonly Color Background = new(0.10f, 0.11f, 0.13f);
    private static readonly Color HexFill = new(0.20f, 0.21f, 0.24f);
    private static readonly Color RimFill = new(0.26f, 0.24f, 0.21f);
    private static readonly Color HexLine = new(0.36f, 0.37f, 0.41f);
    private static readonly Color HoverLine = new(0.95f, 0.95f, 0.95f);
    private static readonly Color ActiveRing = new(0.95f, 0.80f, 0.35f);
    private static readonly Color[] SideColors = [new(0.30f, 0.55f, 0.85f), new(0.80f, 0.32f, 0.30f)];
    private static readonly Color HealthColor = new(0.85f, 0.25f, 0.25f);
    private static readonly Color ShieldColor = new(0.45f, 0.80f, 0.95f);

    private static readonly Dictionary<Mark, Color> MarkColors = new()
    {
        [Mark.Reach] = new(0.30f, 0.55f, 0.35f),
        [Mark.ReachWithFreeAttack] = new(0.55f, 0.45f, 0.20f),
        [Mark.Aim] = new(0.30f, 0.40f, 0.65f),
        [Mark.Struck] = new(0.70f, 0.25f, 0.25f),
        [Mark.Path] = new(0.40f, 0.50f, 0.70f),
    };

    private HexField? _field;
    private IReadOnlyList<Token> _tokens = [];
    private IReadOnlyDictionary<Hex, Mark> _marks = new Dictionary<Hex, Mark>();
    private Hex? _hover;

    public event Action<Hex>? HexPressed;

    /// <summary>The hex under the mouse changed (null when it left the field).</summary>
    public event Action<Hex?>? HexHovered;

    public HexFieldView()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void Display(HexField field, IReadOnlyList<Token> tokens, IReadOnlyDictionary<Hex, Mark> marks)
    {
        (_field, _tokens, _marks) = (field, tokens, marks);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Background);
        if (_field is not { } field)
            return;
        float size = HexSize(field);
        var font = ThemeDB.FallbackFont;

        foreach (var hex in field.Hexes())
        {
            var corners = Corners(Center(hex, size), size * 0.96f);
            var fill = _marks.TryGetValue(hex, out var mark) ? MarkColors[mark] : field.IsEdge(hex) ? RimFill : HexFill;
            DrawColoredPolygon(corners, fill);
            DrawPolyline([.. corners, corners[0]], hex == _hover ? HoverLine : HexLine, hex == _hover ? 2 : 1);
        }

        foreach (var token in _tokens)
        {
            var center = Center(token.Position, size);
            float radius = size * 0.55f;
            if (token.Active)
                DrawCircle(center, radius + 4, ActiveRing);
            DrawCircle(center, radius, SideColors[token.Side]);
            int fontSize = (int)Math.Max(10, size * 0.42f);
            var text = font.GetStringSize(token.Label, HorizontalAlignment.Left, -1, fontSize);
            DrawString(font, center + new Vector2(-text.X / 2, text.Y * 0.3f), token.Label, HorizontalAlignment.Left, -1, fontSize, Colors.White);
            Bar(center + new Vector2(0, radius + 4), size, token.Health / token.MaxHealth, HealthColor);
            if (token.MaxShield > 0)
                Bar(center + new Vector2(0, radius + 9), size, token.Shield / token.MaxShield, ShieldColor);
        }
    }

    private void Bar(Vector2 top, float size, double share, Color color)
    {
        float width = size * 1.1f;
        var origin = top - new Vector2(width / 2, 0);
        DrawRect(new Rect2(origin, new Vector2(width, 4)), new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(origin, new Vector2(width * (float)Math.Clamp(share, 0, 1), 4)), color);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (_field is not { } field)
            return;
        if (e is InputEventMouseMotion motion)
        {
            var hex = HexAt(motion.Position, field);
            if (hex != _hover)
            {
                _hover = hex;
                HexHovered?.Invoke(hex);
                QueueRedraw();
            }
        }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click && HexAt(click.Position, field) is { } pressed)
        {
            HexPressed?.Invoke(pressed);
            AcceptEvent();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit && _hover is not null)
        {
            _hover = null;
            HexHovered?.Invoke(null);
            QueueRedraw();
        }
        else if (what == NotificationResized)
            QueueRedraw();
    }

    /// <summary>The hex side length that fits the whole field: 2r+1 hexes across, and as many rows of 1.5 sides.</summary>
    private float HexSize(HexField field)
    {
        int across = 2 * field.Radius + 1;
        float byWidth = Size.X / (Mathf.Sqrt(3) * across);
        float byHeight = Size.Y / (1.5f * across + 0.5f);
        return Math.Max(4, Math.Min(byWidth, byHeight));
    }

    /// <summary>Where <paramref name="hex"/> is drawn, in window coordinates; null off the field (UI shots click it).</summary>
    public Vector2? ScreenPoint(Hex hex) =>
        _field is { } field && field.Contains(hex) ? GetGlobalTransformWithCanvas() * Center(hex, HexSize(field)) : null;

    private Vector2 Center(Hex hex, float size) =>
        Size / 2 + new Vector2(size * Mathf.Sqrt(3) * (hex.Q + hex.R / 2f), size * 1.5f * hex.R);

    private Hex? HexAt(Vector2 point, HexField field)
    {
        float size = HexSize(field);
        var p = (point - Size / 2) / size;
        double q = (Mathf.Sqrt(3) / 3 * p.X) - (p.Y / 3);
        double r = 2f / 3 * p.Y;
        var hex = Hex.Round(q, r);
        return field.Contains(hex) ? hex : null;
    }

    private static Vector2[] Corners(Vector2 center, float size) =>
        Enumerable.Range(0, 6).Select(i => center + size * Vector2.FromAngle(Mathf.DegToRad(60 * i - 30))).ToArray();
}
