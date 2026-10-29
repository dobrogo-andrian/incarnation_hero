using System.Globalization;
using System.Text.Json;
using Godot;

/// <summary>
/// A development-only screenshot mode for tuning the UI without a person at the window. Started by
/// user arguments after <c>--</c> (see <c>tools/ui-shots.ps1</c>):
/// <c>--shot "&lt;scenario&gt;[;&lt;step&gt;...]" [--out &lt;dir&gt;] [--size 1920x1080] [--seed N] [--hero current|profile.*]</c>.
/// <c>Main</c> stages the scenario on a throwaway run with a fixed seed; this node then sizes the
/// window, plays the steps as real input, saves the frame as PNG next to a layout audit (JSON) and a
/// dump of the visible controls (text), and quits; with exit code 1 when the scenario or a step fails.
/// <para>
/// Scenarios: <c>main</c>; <c>battle:&lt;grp.*&gt;[:&lt;hero turns&gt;]</c> (the AI plays the hero's
/// turns); <c>event:&lt;evt.*&gt;</c>.
/// </para>
/// <para>
/// Steps: <c>click:&lt;target&gt;</c>, <c>hover:&lt;target&gt;</c>, <c>key:&lt;Godot Key name&gt;</c>
/// (<c>Space</c>, <c>Key1</c>, <c>Escape</c>), <c>wait:&lt;seconds&gt;</c>, <c>idle</c> (until the weeks or enemy
/// turns being shown are over: the screen ignores input meanwhile), <c>shot[:&lt;label&gt;]</c> (a picture
/// on the way; the last one is always taken). A target is a button's text (exact, else the first
/// that contains it), <c>@x:y</c> in window pixels, or <c>hex:q:r</c> on the battle field.
/// </para>
/// </summary>
public partial class UiShot : Node
{
    /// <summary>Frames to wait after staging and after each step, so containers settle and deferred frees have run.</summary>
    private const int SettleFrames = 8;

    private const double IdleTimeoutSeconds = 30;

    /// <summary>The scenario, then the steps.</summary>
    private string[] _spec = ["main"];
    private int _shots;

    public string Scenario => _spec[0];
    public string Kind => Scenario.Split(':')[0];
    public string[] Args => Scenario.Split(':')[1..];
    public ulong Seed { get; private init; } = 1;
    public string Hero { get; private init; } = "current";
    private string OutDir { get; init; } = "";

    /// <summary>Put before the file names, so runs of the same scenario with other steps keep apart.</summary>
    private string Prefix { get; init; } = "";

    /// <summary>Whether the screen is still playing something back; set by <c>Main</c> while staging.</summary>
    public Func<bool> Busy { get; set; } = () => false;
    private Vector2I Size { get; init; } = new(1920, 1080);

    /// <summary>The shot asked for on the command line, or null for a normal run.</summary>
    public static UiShot? FromArgs()
    {
        var args = OS.GetCmdlineUserArgs();
        string? Value(string name) => Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        if (Value("--shot") is not { } spec)
            return null;
        var size = (Value("--size") ?? "1920x1080").Split('x').Select(int.Parse).ToArray();
        return new UiShot
        {
            _spec = spec.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            OutDir = Value("--out") ?? ProjectSettings.GlobalizePath("user://shots"),
            Prefix = Value("--prefix") ?? "",
            Size = new Vector2I(size[0], size[1]),
            Seed = ulong.Parse(Value("--seed") ?? "1"),
            Hero = Value("--hero") ?? "current",
        };
    }

    public override async void _Ready()
    {
        var window = GetWindow();
        window.Mode = Window.ModeEnum.Windowed;
        window.Borderless = true;
        window.Size = Size;
        window.Position = Vector2I.Zero;
        try
        {
            await Frames(SettleFrames);
            foreach (string step in _spec[1..])
            {
                await Do(step);
                await Frames(SettleFrames);
            }
            Capture(null);
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError($"UI shot {string.Join(";", _spec)} failed: {e.Message}");
            GetTree().Quit(1);
        }
    }

    private async Task Do(string step)
    {
        string[] parts = step.Split(':', 2);
        string arg = parts.Length > 1 ? parts[1] : "";
        switch (parts[0])
        {
            case "click":
                var at = Target(arg);
                Move(at);
                Input.ParseInputEvent(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true });
                await Frames(1);
                Input.ParseInputEvent(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = false });
                break;
            case "hover":
                Move(Target(arg));
                break;
            case "key":
                var key = Enum.Parse<Key>(arg, ignoreCase: true);
                Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
                await Frames(1);
                Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
                break;
            case "wait":
                await ToSignal(GetTree().CreateTimer(double.Parse(arg, CultureInfo.InvariantCulture)), SceneTreeTimer.SignalName.Timeout);
                break;
            case "idle":
                ulong start = Time.GetTicksMsec();
                while (Busy())
                {
                    if (Time.GetTicksMsec() - start > IdleTimeoutSeconds * 1000)
                        throw new TimeoutException($"still busy after {IdleTimeoutSeconds} s");
                    await Frames(1);
                }
                break;
            case "shot":
                Capture(arg.Length > 0 ? arg : (++_shots).ToString(CultureInfo.InvariantCulture));
                break;
            default:
                throw new ArgumentException($"unknown step '{step}'");
        }
    }

    private static void Move(Vector2 at) => Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });

    /// <summary>The window point a step aims at: <c>@x:y</c>, <c>hex:q:r</c>, or the centre of a visible, enabled button with that text.</summary>
    private Vector2 Target(string target)
    {
        var controls = LayoutAudit.Controls((Control)GetParent()).Where(c => c.IsVisibleInTree()).ToList();
        if (target.StartsWith('@'))
        {
            var xy = target[1..].Split(':').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            return new Vector2(xy[0], xy[1]);
        }
        if (target.StartsWith("hex:", StringComparison.Ordinal))
        {
            var qr = target[4..].Split(':').Select(int.Parse).ToArray();
            var field = controls.OfType<HexFieldView>().FirstOrDefault() ?? throw new ArgumentException("no battle field on screen");
            return field.ScreenPoint(new Core.Combat.Hex(qr[0], qr[1])) ?? throw new ArgumentException($"{target} is off the field");
        }
        var buttons = controls.OfType<Button>().Where(b => !b.Disabled).ToList();
        var button = buttons.FirstOrDefault(b => b.Text == target)
            ?? buttons.FirstOrDefault(b => b.Text.Contains(target, StringComparison.CurrentCultureIgnoreCase))
            ?? throw new ArgumentException($"no visible enabled button '{target}'");
        return button.GetGlobalRect().GetCenter();
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>Saves the frame, its layout audit and the control tree as <c>&lt;scenario&gt;[.&lt;label&gt;].*</c>.</summary>
    private void Capture(string? label)
    {
        Directory.CreateDirectory(OutDir);
        string name = Prefix + string.Concat(Scenario.Select(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '_'));
        string basePath = Path.Combine(OutDir, label is null ? name : $"{name}.{label}");

        var image = GetViewport().GetTexture().GetImage();
        image.SavePng(basePath + ".png");

        var root = (Control)GetParent();
        var screen = new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size);
        var issues = LayoutAudit.Run(root, screen);
        if (image.GetSize() != Size)
            issues.Insert(0, new LayoutAudit.Issue("size_mismatch", "", "", [], $"asked {Size.X}x{Size.Y}, got {image.GetWidth()}x{image.GetHeight()}"));
        var report = new { scenario = string.Join(";", _spec), seed = Seed, size = new[] { image.GetWidth(), image.GetHeight() }, issues };
        File.WriteAllText(basePath + ".json", JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));
        File.WriteAllText(basePath + ".tree.txt", LayoutAudit.Tree(root));
        GD.Print($"UI shot {Scenario}: {basePath}.png, issues: {issues.Count}");
    }
}

/// <summary>
/// Finds layout faults a screenshot alone may hide: text cut off by its control, controls squeezed
/// to nothing, controls outside the window or overflowing a clipping parent (scroll containers
/// excepted, they clip on purpose), and text or buttons overlapping where no container lays them out.
/// </summary>
public static class LayoutAudit
{
    public sealed record Issue(string Kind, string Path, string Text, float[] Rect, string Detail);

    public static List<Issue> Run(Control root, Rect2 screen)
    {
        var issues = new List<Issue>();
        // A control that sticks out drags its containers out too: only the innermost ones are reported.
        var outside = Controls(root).Where(c => Clipper(c) is null && !Inside(screen, c.GetGlobalRect())).ToHashSet();
        foreach (var c in Controls(root))
        {
            var rect = c.GetGlobalRect();
            if (TextOf(c) is { Length: > 0 } && (rect.Size.X < 8 || rect.Size.Y < 8))
                issues.Add(Make("collapsed", c, $"size {rect.Size.X:0}x{rect.Size.Y:0}"));
            if (Truncation(c) is { } truncated)
                issues.Add(Make("text_cut", c, truncated));

            if (outside.Contains(c) && !c.GetChildren().OfType<Control>().Any(outside.Contains))
                issues.Add(Make("offscreen", c, $"window {screen.Size.X:0}x{screen.Size.Y:0}"));
            else if (Clipper(c) is { } clipper and not ScrollContainer && !Inside(clipper.GetGlobalRect(), rect))
                issues.Add(Make("clipped", c, $"by {PathOf(clipper)}"));
        }
        foreach (var parent in Controls(root).Where(p => p is not Container))
        {
            var items = parent.GetChildren().OfType<Control>().Where(k => k.IsVisibleInTree() && k is Label or BaseButton).ToList();
            for (int i = 0; i < items.Count; i++)
            for (int j = i + 1; j < items.Count; j++)
            {
                var overlap = items[i].GetGlobalRect().Intersection(items[j].GetGlobalRect());
                if (overlap.Size.X > 1 && overlap.Size.Y > 1)
                    issues.Add(Make("overlap", items[i], $"with {PathOf(items[j])} \"{Short(TextOf(items[j]))}\""));
            }
        }
        return issues;
    }

    /// <summary>Every visible control with its type, text and rectangle, indented by depth.</summary>
    public static string Tree(Control root)
    {
        var lines = new List<string>();
        Walk(root, 0);
        return string.Join("\n", lines) + "\n";

        void Walk(Control c, int depth)
        {
            var r = c.GetGlobalRect();
            string text = TextOf(c) is { Length: > 0 } t ? $" \"{Short(t)}\"" : "";
            lines.Add($"{new string(' ', depth * 2)}{c.GetType().Name}{text} [{r.Position.X:0},{r.Position.Y:0} {r.Size.X:0}x{r.Size.Y:0}]");
            foreach (var child in c.GetChildren().OfType<Control>().Where(k => k.Visible))
                Walk(child, depth + 1);
        }
    }

    /// <summary>Every control under <paramref name="root"/> whose own Visible is set, depth first.</summary>
    public static IEnumerable<Control> Controls(Control root) =>
        root.GetChildren().OfType<Control>().Where(c => c.Visible).SelectMany(c => Controls(c).Prepend(c));

    private static IEnumerable<Control> Ancestors(Control c)
    {
        for (var p = c.GetParent() as Control; p is not null; p = p.GetParent() as Control)
            yield return p;
    }

    private static Control? Clipper(Control c) => Ancestors(c).FirstOrDefault(a => a.ClipContents || a is ScrollContainer);

    private static bool Inside(Rect2 outer, Rect2 inner) =>
        inner.Position.X >= outer.Position.X - 0.5f && inner.Position.Y >= outer.Position.Y - 0.5f &&
        inner.End.X <= outer.End.X + 0.5f && inner.End.Y <= outer.End.Y + 0.5f;

    /// <summary>Why the control shows less text than it has, or null when it all fits.</summary>
    private static string? Truncation(Control c)
    {
        switch (c)
        {
            case Label label when label.Text.Length > 0:
                if (label.AutowrapMode != TextServer.AutowrapMode.Off)
                    return label.GetVisibleLineCount() < label.GetLineCount() ? $"lines {label.GetVisibleLineCount()}/{label.GetLineCount()}" : null;
                if (!label.ClipText && label.TextOverrunBehavior == TextServer.OverrunBehavior.NoTrimming)
                    return null;
                return Wider(label, label.Text, label.GetThemeFont("font"), label.GetThemeFontSize("font_size"), label.GetThemeStylebox("normal"));
            case Button button when button.Text.Length > 0 && (button.ClipText || button.TextOverrunBehavior != TextServer.OverrunBehavior.NoTrimming):
                return Wider(button, button.Text, button.GetThemeFont("font"), button.GetThemeFontSize("font_size"), button.GetThemeStylebox("normal"));
            default:
                return null;
        }
    }

    private static string? Wider(Control c, string text, Font font, int size, StyleBox box)
    {
        float need = font.GetMultilineStringSize(text, HorizontalAlignment.Left, -1, size).X;
        float room = c.Size.X - box.GetMargin(Side.Left) - box.GetMargin(Side.Right);
        return need > room + 0.5f ? $"text needs {need:0}px, has {room:0}px" : null;
    }

    private static string TextOf(Control c) => c switch
    {
        Label l => l.Text,
        Button b => b.Text,
        _ => "",
    };

    private static string Short(string text)
    {
        string line = text.ReplaceLineEndings(" ");
        return line.Length <= 60 ? line : line[..57] + "...";
    }

    private static string PathOf(Control c) =>
        string.Join("/", Ancestors(c).Reverse().Append(c).Select(n => n.GetType().Name));

    private static Issue Make(string kind, Control c, string detail)
    {
        var r = c.GetGlobalRect();
        return new Issue(kind, PathOf(c), Short(TextOf(c)), [r.Position.X, r.Position.Y, r.Size.X, r.Size.Y], detail);
    }
}
