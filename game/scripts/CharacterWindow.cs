using Core.Content;
using Core.Loc;
using Core.Sim;
using Core.State;
using Godot;
using static Widgets;

/// <summary>
/// The character window, opened from the status bar or with C. On the left the figure with its
/// slots (the active method, §6 and D35; weapon and armor are shown but stay empty until weapons
/// are items) with health, qi and the other resources beside it (<see cref="ConditionView"/>), above
/// either the stats or cultivation with the breakthrough (<see cref="BreakthroughTab"/>);
/// in the middle the stones (a currency only, D18) and the inventory, a column of square cells
/// per shelf; on the right what the picked item or slot does, with its actions. A method folio dragged onto
/// the method slot makes its method the active one, a pill dropped on the figure is taken; both are
/// the same commands the buttons send.
/// </summary>
public partial class CharacterWindow : PanelContainer
{
    private enum Shelf { Pills, Folios, Weapons, Armor }

    private const string MethodSlot = "slot:method";
    private const string WeaponSlot = "slot:weapon";
    private const string ArmorSlot = "slot:armor";

    private static readonly (Shelf Shelf, string Title)[] Shelves =
    [
        (Shelf.Pills, "str.ui.inv_pills"),
        (Shelf.Folios, "str.ui.inv_folios"),
        (Shelf.Weapons, "str.ui.inv_weapons"),
        (Shelf.Armor, "str.ui.inv_armor"),
    ];

    private static readonly LocKey MethodPick = new("str.ui.method_pick");

    private const int InventoryColumns = 4;
    private const int InventoryCells = 24;

    private static readonly Vector2 SlotSize = new(170, 84);
    private static readonly Vector2 CellSize = new(92, 92);
    private static readonly Color NoticeColor = new(0.95f, 0.55f, 0.45f);
    private static readonly Color FigureColor = new(0.32f, 0.36f, 0.40f);

    private readonly VBoxContainer _figure = new();
    private readonly ConditionView _condition = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(260, 0) };
    private readonly HBoxContainer _pages = new();
    private readonly StatsView _stats = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly BreakthroughTab _cultivation = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Label _stones = new();
    private readonly HBoxContainer _shelves = new();
    private readonly VBoxContainer _shelf = new();
    private readonly VBoxContainer _details = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };

    private UiContext _ui = null!;
    private PlayerView _view = null!;
    private Shelf _openShelf = Shelf.Pills;
    private bool _showCultivation;

    /// <summary>Why Core refused the last command, shown above the details: the journal is under the window.</summary>
    public string? Notice { get; set; }

    /// <summary>The picked item ID or slot; presentation state only, dropped once it is gone.</summary>
    private string? _picked;

    /// <summary>Builds the frame; called once before the first <see cref="Refresh"/>.</summary>
    public void Setup(UiContext ui, Action close)
    {
        _ui = ui;
        Visible = false;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.23f, 0.24f, 0.26f),
            ContentMarginLeft = 20, ContentMarginRight = 20, ContentMarginTop = 16, ContentMarginBottom = 16,
        });
        foreach (var box in new BoxContainer[] { _figure, _pages, _shelves, _shelf, _details })
            box.AddThemeConstantOverride("separation", 8);

        var title = Title(ui.Text("str.ui.character"));
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var header = Row(title, Button(ui.Text("str.ui.close"), close));

        var top = Row(_figure, _condition);
        top.AddThemeConstantOverride("separation", 24);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(860, 0) };
        left.AddThemeConstantOverride("separation", 12);
        left.AddChild(top);
        left.AddChild(_pages);
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddChild(_stats);
        page.AddChild(_cultivation);
        left.AddChild(Scroll(page));

        var inventory = new VBoxContainer();
        inventory.AddThemeConstantOverride("separation", 10);
        inventory.AddChild(Title(ui.Text("str.ui.inventory")));
        inventory.AddChild(_stones);
        inventory.AddChild(_shelves);
        inventory.AddChild(Scroll(_shelf));

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 20);
        body.AddChild(left);
        body.AddChild(new VSeparator());
        body.AddChild(inventory);
        body.AddChild(new VSeparator());
        var details = Scroll(_details);
        details.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.AddChild(details);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        root.AddChild(header);
        root.AddChild(body);
        AddChild(root);
    }

    /// <summary>Redraws from <paramref name="view"/> while open; the actions work only in the overworld.</summary>
    public void Refresh(PlayerView view)
    {
        _view = view;
        if (!Visible)
            return;
        foreach (var box in new Container[] { _figure, _pages, _shelves, _shelf, _details })
            foreach (var child in box.GetChildren())
                child.QueueFree();
        bool free = view.Mode == GameMode.Overworld;
        if (_picked is not null && !_picked.StartsWith("slot:", StringComparison.Ordinal) && view.Items.All(i => i.ItemId != _picked))
            _picked = null;

        AddFigure(view, free);
        _condition.Refresh(_ui, view);
        AddPages(view);
        _stones.Text = _ui.Text(SimText.StonesText(view));
        AddShelf(view, free);
        AddDetails(view, free);
    }

    /// <summary>The weapon slot, the figure and the armor slot in a row, the method slot under them.</summary>
    private void AddFigure(PlayerView view, bool free)
    {
        var figure = new ItemButton
        {
            Flat = true,
            CustomMinimumSize = new Vector2(200, 260),
            FocusMode = FocusModeEnum.None,
            Accepts = id => free && Def(id).Pill is not null,
            Dropped = id => _ui.Send(new UsePill(id)),
        };
        figure.Draw += () => DrawSilhouette(figure);

        var row = Row(Centered(Slot(WeaponSlot, "str.ui.slot_weapon", _ui.Text("str.ui.slot_soon"), enabled: false)),
            figure,
            Centered(Slot(ArmorSlot, "str.ui.slot_armor", _ui.Text("str.ui.slot_soon"), enabled: false)));
        row.Alignment = BoxContainer.AlignmentMode.Center;
        _figure.AddChild(row);

        string method = view.ActiveMethodId is { } id ? _ui.Name(id) : _ui.Text("str.ui.no_method");
        var slot = Slot(MethodSlot, "str.ui.method", method, enabled: true);
        slot.Accepts = item => free && MethodOf(item) is not null;
        slot.Dropped = item => _ui.Send(new SetActiveMethod(MethodOf(item)));
        var methodRow = Row(slot);
        methodRow.Alignment = BoxContainer.AlignmentMode.Center;
        _figure.AddChild(methodRow);
    }

    /// <summary>A slot: its title over what it holds; picking it shows what it gives below the shelves.</summary>
    private ItemButton Slot(string slot, string title, string holds, bool enabled)
    {
        var button = new ItemButton
        {
            Text = _ui.Text(title) + "\n" + holds,
            CustomMinimumSize = SlotSize,
            ToggleMode = true,
            ButtonPressed = _picked == slot,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Disabled = !enabled,
        };
        Cell(button);
        button.Pressed += () => Pick(slot);
        return button;
    }

    /// <summary>The stats or the cultivation page under the figure, picked by two toggles.</summary>
    private void AddPages(PlayerView view)
    {
        foreach (var (cultivation, title) in new[] { (false, "str.ui.stats"), (true, "str.ui.tab_cultivation") })
        {
            var button = new Button { Text = _ui.Text(title), ToggleMode = true, ButtonPressed = cultivation == _showCultivation };
            button.Pressed += () => { _showCultivation = cultivation; Refresh(_view); };
            _pages.AddChild(button);
        }
        _stats.Visible = !_showCultivation;
        _cultivation.Visible = _showCultivation;
        if (_showCultivation)
            _cultivation.Refresh(_ui, view);
        else
            _stats.Refresh(_ui, view);
    }

    /// <summary>
    /// The shelf picker and the picked shelf as a column of square cells, its items first and the
    /// rest empty, as in a classic inventory; weapons and armor do not exist yet.
    /// </summary>
    private void AddShelf(PlayerView view, bool free)
    {
        foreach (var (shelf, title) in Shelves)
        {
            var button = new Button { Text = _ui.Text(title), ToggleMode = true, ButtonPressed = shelf == _openShelf };
            button.Pressed += () => { _openShelf = shelf; Refresh(_view); };
            _shelves.AddChild(button);
        }

        var items = view.Items.Where(i => ShelfOf(i.ItemId) == _openShelf).ToList();
        var grid = new GridContainer { Columns = InventoryColumns };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        _shelf.AddChild(grid);
        foreach (var item in items)
        {
            string name = _ui.Name(item.ItemId);
            var cell = new ItemButton
            {
                Text = $"{name}\n× {item.Count}",
                TooltipText = name,
                DragId = free ? item.ItemId : null,
                CustomMinimumSize = CellSize,
                ToggleMode = true,
                ButtonPressed = _picked == item.ItemId,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                ClipText = true,
            };
            cell.AddThemeFontSizeOverride("font_size", 13);
            Cell(cell);
            cell.Pressed += () => Pick(item.ItemId);
            grid.AddChild(cell);
        }
        int cells = Math.Max(InventoryCells, (items.Count + InventoryColumns - 1) / InventoryColumns * InventoryColumns);
        for (int i = items.Count; i < cells; i++)
        {
            var empty = new Button { CustomMinimumSize = CellSize, Disabled = true, FocusMode = FocusModeEnum.None };
            Cell(empty);
            grid.AddChild(empty);
        }
        if (_openShelf is Shelf.Weapons or Shelf.Armor)
        {
            var soon = Text(_ui.Text("str.ui.inv_soon"));
            soon.CustomMinimumSize = new Vector2(InventoryColumns * (CellSize.X + 6), 0);
            _shelf.AddChild(soon);
        }
    }

    /// <summary>What the picked item or slot does, with the actions it allows.</summary>
    private void AddDetails(PlayerView view, bool free)
    {
        if (Notice is not null)
        {
            var notice = Text(Notice);
            notice.AddThemeColorOverride("font_color", NoticeColor);
            _details.AddChild(notice);
        }
        switch (_picked)
        {
            case null:
                _details.AddChild(Text(_ui.Text("str.ui.pick_hint")));
                break;
            case MethodSlot:
                _details.AddChild(Title(_ui.Text("str.ui.method")));
                AddLines(SimText.MethodDetails(view, _ui.Db));
                foreach (var known in view.Knowledge.Where(k => k.Level > 0 && k.KnowledgeId != view.ActiveMethodId && _ui.Db.Get<KnowledgeDef>(k.KnowledgeId).Method is not null))
                {
                    string pick = _ui.Text(MethodPick with { Args = [new("method", _ui.Name(known.KnowledgeId))] });
                    _details.AddChild(Row(Button(pick, () => _ui.Send(new SetActiveMethod(known.KnowledgeId)), free)));
                }
                if (view.ActiveMethodId is not null)
                    _details.AddChild(Row(Button(_ui.Text("str.ui.method_clear"), () => _ui.Send(new SetActiveMethod(null)), free)));
                break;
            case WeaponSlot or ArmorSlot:
                _details.AddChild(Text(_ui.Text("str.ui.inv_soon")));
                break;
            default:
                var item = view.Items.First(i => i.ItemId == _picked);
                _details.AddChild(Title(_ui.Name(item.ItemId)));
                AddLines(SimText.ItemDetails(item, view, _ui.Db));
                if (Def(item.ItemId).Pill is not null)
                    _details.AddChild(Row(Button(_ui.Text("str.ui.use_pill"), () => _ui.Send(new UsePill(item.ItemId)), free)));
                if (MethodOf(item.ItemId) is { } method)
                    _details.AddChild(Row(Button(_ui.Text("str.ui.method_set"), () => _ui.Send(new SetActiveMethod(method)), free && view.ActiveMethodId != method)));
                break;
        }
    }

    private void AddLines(IEnumerable<LocKey> lines)
    {
        foreach (var line in lines)
            _details.AddChild(Text(_ui.Text(line)));
    }

    /// <summary>Opens or closes the window on the current view.</summary>
    public void SetOpen(PlayerView view, bool open)
    {
        Visible = open;
        Refresh(view);
    }

    private void Pick(string picked)
    {
        Notice = null;
        _picked = _picked == picked ? null : picked;
        Refresh(_view);
    }

    private ItemDef Def(string itemId) => _ui.Db.Get<ItemDef>(itemId);

    /// <summary>The method a folio teaches, or null for any other item.</summary>
    private string? MethodOf(string itemId) =>
        Def(itemId).Folio is { } folio && _ui.Db.Get<KnowledgeDef>(folio.Subject).Method is not null ? folio.Subject : null;

    private Shelf ShelfOf(string itemId) => Def(itemId).Pill is not null ? Shelf.Pills : Shelf.Folios;

    /// <summary>A plain figure, head and body, until there is art for it.</summary>
    public static void DrawSilhouette(Control figure)
    {
        var size = figure.Size;
        float cx = size.X / 2, head = size.Y * 0.11f;
        figure.DrawCircle(new Vector2(cx, size.Y * 0.06f + head), head, FigureColor);
        float top = size.Y * 0.06f + head * 2 + 6;
        figure.DrawColoredPolygon(
        [
            new Vector2(cx - size.X * 0.32f, top + 20), new Vector2(cx + size.X * 0.32f, top + 20),
            new Vector2(cx + size.X * 0.22f, size.Y * 0.62f), new Vector2(cx + size.X * 0.24f, size.Y * 0.98f),
            new Vector2(cx - size.X * 0.24f, size.Y * 0.98f), new Vector2(cx - size.X * 0.22f, size.Y * 0.62f),
        ], FigureColor);
    }

    /// <summary>Gives a slot or card the look of an inventory cell: framed, the picked one in gold.</summary>
    private static void Cell(Button button)
    {
        StyleBoxFlat Box(Color background, Color border) => new()
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6,
        };
        var cell = new Color(0.16f, 0.17f, 0.19f);
        button.AddThemeStyleboxOverride("normal", Box(cell, new Color(0.34f, 0.37f, 0.41f)));
        button.AddThemeStyleboxOverride("hover", Box(cell, new Color(0.55f, 0.6f, 0.66f)));
        button.AddThemeStyleboxOverride("pressed", Box(new Color(0.22f, 0.2f, 0.14f), new Color(0.9f, 0.75f, 0.4f)));
        button.AddThemeStyleboxOverride("hover_pressed", Box(new Color(0.22f, 0.2f, 0.14f), new Color(0.95f, 0.82f, 0.5f)));
        button.AddThemeStyleboxOverride("disabled", Box(new Color(0.12f, 0.12f, 0.13f), new Color(0.22f, 0.23f, 0.25f)));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private static CenterContainer Centered(Control child)
    {
        var center = new CenterContainer();
        center.AddChild(child);
        return center;
    }

    private static ScrollContainer Scroll(Control content)
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        scroll.AddChild(content);
        return scroll;
    }
}
