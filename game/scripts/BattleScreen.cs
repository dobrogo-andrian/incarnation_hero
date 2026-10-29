using Core.Combat;
using Core.Content;
using Core.Loc;
using Core.Rng;
using Core.Sim;
using Godot;
using static Widgets;

/// <summary>
/// The stage 2 test fight (D56), over the run's screen below the status bar and outside the run:
/// pick the hero (a sandbox profile or a snapshot of the current hero) and an enemy group, then fight
/// on the hex field. The player clicks a lit hex to step and an adjacent enemy to strike. The skill
/// bar under the field holds the techniques (keys 1–5) and, apart at the far right, the basic attack
/// and ending the turn (Space); picking a skill lights where it can go. Dash, guard and flee are
/// buttons beside the log. What Core would refuse is greyed out with its reason. Enemy turns are
/// played back event by event (steps, hits) with the CTB queue on top; Core has already decided them.
/// </summary>
public partial class BattleScreen : PanelContainer
{
    /// <summary>Seconds each step or hit of an enemy turn stays on screen.</summary>
    private const double EventSeconds = 0.3;

    private const string CurrentHero = "current";

    /// <summary>The skill-bar slot of the basic attack, aimed like a technique.</summary>
    private const string AttackSkill = "attack";

    /// <summary>Techniques reachable by the number keys 1..SkillKeys.</summary>
    private const int SkillKeys = 5;

    private static readonly LocKey FighterLines = new("str.ui.battle_fighter");
    private static readonly LocKey TechniqueButton = new("str.ui.battle_technique");
    private static readonly LocKey AttackButton = new("str.ui.battle_attack");

    private UiContext _ui = null!;
    private Func<Combatant> _currentHero = null!;
    private Func<ulong> _newSeed = null!;

    private readonly VBoxContainer _setup = new();
    private readonly HBoxContainer _fight = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly Label _queue = new() { ClipText = true };
    private readonly HexFieldView _field = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly Label _active = new();
    private readonly Label _hovered = new();
    private readonly VBoxContainer _actions = new();
    private readonly HBoxContainer _skillBar = new();
    private readonly List<(string Skill, Button Button)> _skills = [];
    private Button? _endTurn;
    private readonly Label _hint = Text("");
    private readonly RichTextLabel _log = new() { ScrollFollowing = true, SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 160) };
    private readonly VBoxContainer _result = new();

    private string _heroPick = CurrentHero;
    private string? _groupPick;

    private CombatSandbox? _sandbox;
    private string? _aiming;
    private Hex? _hover;

    /// <summary>Fighters as shown while an enemy turn is played back, by ID; empty when the screen shows Core's state.</summary>
    private readonly Dictionary<string, HexFieldView.Token> _shown = [];
    private readonly Queue<SimEvent> _playback = new();
    private double _clock;

    /// <param name="newSeed">The fight seeds, from the shell like a run's.</param>
    public void Setup(UiContext ui, Func<Combatant> currentHero, Action back, Func<ulong> newSeed)
    {
        _ui = ui;
        _currentHero = currentHero;
        _newSeed = newSeed;
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.08f, 0.09f, 0.10f) });

        _field.HexPressed += OnHexPressed;
        _field.HexHovered += hex => { _hover = hex; ShowState(); };

        var side = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };
        side.AddThemeConstantOverride("separation", 10);
        foreach (var node in new Control[] { _active, new HSeparator(), _hovered, _actions, _hint, _result, _log })
            side.AddChild(node);
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        left.AddChild(_queue);
        left.AddChild(_field);
        _skillBar.AddThemeConstantOverride("separation", 8);
        left.AddChild(_skillBar);
        _fight.AddThemeConstantOverride("separation", 16);
        _fight.AddChild(left);
        _fight.AddChild(side);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        // A wrapping label in a row is squeezed to nothing and grows tall: the title stays on one line.
        var title = Title(ui.Text("str.ui.sandbox"));
        title.AutowrapMode = TextServer.AutowrapMode.Off;
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        column.AddChild(Row(title, Button(ui.Text("str.ui.sandbox_back"), () => { Visible = false; back(); })));
        column.AddChild(_setup);
        column.AddChild(_fight);
        var margin = new MarginContainer();
        foreach (string edge in new[] { "left", "top", "right", "bottom" })
            margin.AddThemeConstantOverride($"margin_{edge}", 16);
        margin.AddChild(column);
        AddChild(margin);
    }

    /// <summary>Opens the screen on the picker; the previous picks are kept.</summary>
    public void Open()
    {
        Visible = true;
        _sandbox = null;
        ShowSetup();
    }

    private void ShowSetup()
    {
        _fight.Visible = false;
        _setup.Visible = true;
        Clear(_setup);
        var heroes = CombatSandbox.Profiles(_ui.Db).Select(id => (id, _ui.Name(id)))
            .Prepend((CurrentHero, _ui.Text("str.ui.sandbox_current_hero"))).ToList();
        var groups = _ui.Db.All<EnemyGroupDef>().Select(g => (g.Id, $"{_ui.Name(g.Id)} ({g.Members.Sum(m => m.Count)})")).ToList();
        _groupPick = Pick(groups, _groupPick);
        _setup.AddChild(Text(_ui.Text("str.ui.sandbox_note")));
        _setup.AddChild(Row(new Label { Text = _ui.Text("str.ui.sandbox_hero"), CustomMinimumSize = new Vector2(90, 0) }, Options(heroes, _heroPick, id => _heroPick = id)));
        _setup.AddChild(Row(new Label { Text = _ui.Text("str.ui.sandbox_foes"), CustomMinimumSize = new Vector2(90, 0) }, Options(groups, _groupPick, id => _groupPick = id)));
        _setup.AddChild(Button(_ui.Text("str.ui.sandbox_start"), () => StartFight(_newSeed())));
    }

    /// <summary>
    /// For a UI shot (<see cref="UiShot"/>): the hero the picker offers first, a sandbox profile or the current hero.
    /// </summary>
    public void Preselect(string hero) => _heroPick = hero;

    /// <summary>
    /// For a UI shot: starts the fight against <paramref name="group"/>, and has the AI play the hero's
    /// first <paramref name="heroTurns"/> turns, every turn shown at once.
    /// </summary>
    public void Stage(string group, int heroTurns)
    {
        Open();
        _groupPick = group;
        ulong seed = _newSeed();
        StartFight(seed);
        var ai = Xoshiro256StarStar.FromSeed(seed);
        for (int turn = 0; turn <= heroTurns; turn++)
        {
            while (Playing)
                Reveal(_playback.Dequeue());
            _shown.Clear();
            if (turn == heroTurns || Battle.IsOver)
                break;
            Log(BattleAi.PlayTurn(Battle, ai));
            PlayEnemies();
        }
        ShowState();
    }

    private void StartFight(ulong seed)
    {
        var hero = _heroPick == CurrentHero ? _currentHero() : CombatSandbox.Profile(_ui.Db, _heroPick);
        _sandbox = CombatSandbox.Start(_ui.Db, hero, _groupPick!, seed);
        (_aiming, _clock) = (null, 0);
        _playback.Clear();
        _shown.Clear();
        _log.Clear();
        _hint.Text = _ui.Text("str.ui.battle_hint");
        _setup.Visible = false;
        _fight.Visible = true;
        BuildSkillBar(hero);
        Log(_sandbox.Battle.Opening);
        PlayEnemies();
    }

    private Battle Battle => _sandbox!.Battle;

    /// <summary>Whether enemy turns are still being shown; the player's input waits meanwhile.</summary>
    public bool Playing => _playback.Count > 0;

    /// <summary>While enemies are to act, has Core play their turns and queues the events to show.</summary>
    private void PlayEnemies()
    {
        if (!Playing && !Battle.IsOver && !_sandbox!.PlayersTurn)
        {
            foreach (var token in Tokens())
                _shown[token.Id] = token.Token;
            while (!Battle.IsOver && !_sandbox.PlayersTurn)
            {
                foreach (var e in _sandbox.PlayEnemyTurn())
                    _playback.Enqueue(e);
            }
        }
        ShowState();
    }

    public override void _Process(double delta)
    {
        if (!Visible || !Playing)
            return;
        _clock += delta;
        while (_clock >= EventSeconds && Playing)
        {
            if (Reveal(_playback.Dequeue()))
                _clock -= EventSeconds;
        }
        if (!Playing)
        {
            _shown.Clear();
            _clock = 0;
        }
        ShowState();
    }

    /// <summary>Logs one event and moves the shown fighters on; true for an event worth a pause (a step or a hit).</summary>
    private bool Reveal(SimEvent e)
    {
        Log([e]);
        switch (e)
        {
            case FighterMoved m:
                _shown[m.FighterId] = _shown[m.FighterId] with { Position = m.To };
                return true;
            case FighterHit h:
                var t = _shown[h.TargetId];
                _shown[h.TargetId] = t with { Shield = t.Shield - h.Absorbed, Health = Math.Max(0, t.Health - h.ToHealth) };
                return true;
            case FighterDown or FighterFled:
                _shown.Remove(e is FighterDown d ? d.FighterId : ((FighterFled)e).FighterId);
                return true;
            case BattleTurnStarted s:
                foreach (var (id, token) in _shown)
                    _shown[id] = token with { Active = id == s.FighterId };
                return false;
            default:
                return false;
        }
    }

    private void Send(BattleCommand command)
    {
        if (Playing || Battle.IsOver || !_sandbox!.PlayersTurn)
            return;
        var outcome = Battle.Execute(command);
        if (outcome.RejectReason is { } reason)
        {
            _hint.Text = _ui.Text(reason);
            ShowState();
            return;
        }
        _aiming = null;
        _hint.Text = _ui.Text("str.ui.battle_hint");
        Log(outcome.Events);
        PlayEnemies();
    }

    private void OnHexPressed(Hex hex)
    {
        if (_sandbox is null || Playing || Battle.IsOver || !_sandbox.PlayersTurn)
            return;
        var me = Battle.Active!;
        var there = Battle.Fighters.FirstOrDefault(f => f.InFight && f.Position == hex);
        if (_aiming == AttackSkill)
        {
            if (there is not null && there.Side != me.Side)
                Send(new Attack(there.Id));
            else
                Aim(null);
            return;
        }
        if (_aiming is { } technique)
        {
            Send(new UseTechnique(technique, hex));
            return;
        }
        if (there is not null && there.Side != me.Side)
            Send(new Attack(there.Id));
        else if (there is null)
            Send(new Move(hex));
    }

    private void Aim(string? skill)
    {
        if (Playing || _sandbox is not { PlayersTurn: true } || Battle.IsOver)
            return;
        _aiming = skill;
        _hint.Text = _ui.Text(skill switch { null => "str.ui.battle_hint", AttackSkill => "str.ui.battle_aim_attack", _ => "str.ui.battle_aim" });
        ShowState();
    }

    /// <summary>The skill bar for this fight's hero: techniques with their keys, then the basic attack at the far right.</summary>
    private void BuildSkillBar(Combatant hero)
    {
        Clear(_skillBar);
        _skills.Clear();
        for (int i = 0; i < hero.Techniques.Count; i++)
        {
            var technique = hero.Techniques[i];
            string key = i < SkillKeys ? (i + 1).ToString() : "";
            AddSkill(technique.Id, _ui.Text(TechniqueButton with
            {
                Args = [new("key", key), new("name", SimText.Name(technique.Id)), new("qi", technique.Combat.Qi)],
            }));
        }
        _skillBar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        double energy = _ui.Db.Get<CombatConfigDef>(CombatConfigDef.CombatId).AttackEnergy;
        AddSkill(AttackSkill, _ui.Text(AttackButton with { Args = [new("energy", energy)] }));
        _endTurn = new Button { Text = _ui.Text("str.ui.battle_end_turn"), FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(120, 56) };
        _endTurn.Pressed += () => Send(new EndTurn());
        _skillBar.AddChild(_endTurn);

        void AddSkill(string skill, string label)
        {
            var button = new Button { Text = label, ToggleMode = true, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(120, 56) };
            button.Pressed += () => Aim(_aiming == skill ? null : skill);
            _skillBar.AddChild(button);
            _skills.Add((skill, button));
        }
    }

    /// <summary>
    /// Number keys pick a technique, Esc drops the picked skill, Space ends the turn. Read before the
    /// controls, so Space never presses a focused button instead.
    /// </summary>
    public override void _Input(InputEvent e)
    {
        if (!Visible || !_fight.Visible || _sandbox is null || e is not InputEventKey { Pressed: true, Echo: false } key)
            return;
        if (key.Keycode == Key.Space)
        {
            if (_endTurn is { Disabled: false })
                Send(new EndTurn());
            GetViewport().SetInputAsHandled();
            return;
        }
        int slot = (int)key.Keycode - (int)Key.Key1;
        var techniques = _skills.Where(s => s.Skill != AttackSkill).ToList();
        if (slot >= 0 && slot < Math.Min(SkillKeys, techniques.Count))
        {
            // A greyed-out skill picked by key says why instead.
            var (skill, button) = techniques[slot];
            if (button.Disabled)
                _hint.Text = button.TooltipText;
            else
                Aim(_aiming == skill ? null : skill);
        }
        else if (key.Keycode == Key.Escape)
            Aim(null);
        else
            return;
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Redraws the field, the queue, the fighters' numbers, the action buttons and the result.</summary>
    private void ShowState()
    {
        if (_sandbox is null)
            return;
        var tokens = Playing ? _shown.Values.ToList() : Tokens().Select(t => t.Token).ToList();
        _field.Display(Battle.Field, tokens, Marks());

        IEnumerable<BattleFighter> queue = Battle.Active is { } active ? Battle.Forecast(8).Prepend(active) : [];
        _queue.Text = _ui.Text("str.ui.battle_queue") + " " + string.Join("  ·  ", queue.Select(f => FighterName(f.Id)));
        _active.Text = Battle.Active is { } a ? FighterText(a) : "";
        _hovered.Text = !Playing && _hover is { } hex && Battle.Fighters.FirstOrDefault(f => f.InFight && f.Position == hex) is { } under && under != Battle.Active
            ? FighterText(under)
            : "";

        bool myTurn = !Playing && !Battle.IsOver && _sandbox.PlayersTurn;
        Clear(_actions);
        _actions.Visible = myTurn;
        // What Core would refuse now is greyed out, with its reason on hover.
        foreach (var (skill, button) in _skills)
            Allow(button, myTurn ? SkillRefusal(skill) : null, myTurn, myTurn && _aiming == skill);
        if (_endTurn is { } endTurn)
            Allow(endTurn, null, myTurn, false);
        if (myTurn)
        {
            _actions.AddChild(Row(
                ActionButton("str.ui.battle_dash", new Dash()),
                ActionButton("str.ui.battle_guard", new Guard()),
                ActionButton("str.ui.battle_flee", new Flee())));
            if (_aiming is not null)
                _actions.AddChild(Button(_ui.Text("str.ui.battle_cancel"), () => Aim(null)));
        }

        Clear(_result);
        if (!Playing && Battle.IsOver)
        {
            var hero = Battle.Fighters.First(f => f.Side == 0);
            string outcome = Battle.Winner == 0 ? "str.ui.battle_won" : hero.Fled ? "str.ui.battle_escaped" : "str.ui.battle_lost";
            _result.AddChild(Title(_ui.Text(outcome)));
            _result.AddChild(Row(Button(_ui.Text("str.ui.sandbox_again"), ShowSetup)));
        }
    }

    private LocKey? SkillRefusal(string skill) => Battle.SkillUnavailable(skill == AttackSkill ? null : skill);

    private Button ActionButton(string text, BattleCommand command)
    {
        var button = Button(_ui.Text(text), () => Send(command));
        Allow(button, Battle.WhyNot(command), true, false);
        return button;
    }

    private void Allow(Button button, LocKey? refusal, bool myTurn, bool pressed)
    {
        button.Disabled = !myTurn || refusal is not null;
        button.TooltipText = refusal is null ? "" : _ui.Text(refusal);
        button.SetPressedNoSignal(pressed);
    }

    /// <summary>The highlighted hexes: where the player can step, or, while aiming, where the technique can go and what it would strike.</summary>
    private Dictionary<Hex, HexFieldView.Mark> Marks()
    {
        var marks = new Dictionary<Hex, HexFieldView.Mark>();
        if (Playing || Battle.IsOver || !_sandbox!.PlayersTurn)
            return marks;
        if (_aiming == AttackSkill)
        {
            var me = Battle.Active!;
            foreach (var enemy in Battle.Fighters.Where(f => f.InFight && f.Side != me.Side && f.Position.DistanceTo(me.Position) == 1))
                marks[enemy.Position] = enemy.Position == _hover ? HexFieldView.Mark.Struck : HexFieldView.Mark.Aim;
            return marks;
        }
        if (_aiming is { } technique)
        {
            // Every hex Core would accept as the aim, enemy or bare ground; on hover, the whole area it strikes and the way there.
            var aims = Battle.Field.Hexes().Where(h => Battle.WhyNot(new UseTechnique(technique, h)) is null).ToHashSet();
            foreach (var hex in aims)
                marks[hex] = HexFieldView.Mark.Aim;
            if (_hover is { } aim && aims.Contains(aim) && Battle.Preview(technique, aim) is { } plan)
            {
                foreach (var step in plan.Landing is { } landing ? plan.Approach.Append(landing) : plan.Approach)
                    marks[step] = HexFieldView.Mark.Path;
                foreach (var hex in plan.Area)
                    marks[hex] = HexFieldView.Mark.Struck;
            }
            return marks;
        }
        foreach (var reach in Battle.Reachable())
            marks[reach.To] = reach.FreeAttacks > 0 ? HexFieldView.Mark.ReachWithFreeAttack : HexFieldView.Mark.Reach;
        return marks;
    }

    /// <summary>The fighters still on the field as Core has them.</summary>
    private IEnumerable<(string Id, HexFieldView.Token Token)> Tokens() =>
        Battle.Fighters.Where(f => f.InFight).Select(f => (f.Id, new HexFieldView.Token(
            Initials(FighterName(f.Id)), f.Side, f.Position, f.Health, f.Def.MaxHealth, f.Shield, f.Def.MaxShield, f == Battle.Active)));

    private string FighterText(BattleFighter f) => _ui.Text(FighterLines with
    {
        Args =
        [
            new("name", FighterName(f.Id)), new("health", Math.Round(f.Health, 1)), new("max_health", Math.Round(f.Def.MaxHealth, 1)),
            new("shield", Math.Round(f.Shield, 1)), new("max_shield", Math.Round(f.Def.MaxShield, 1)),
            new("energy", Math.Round(f.Energy, 1)), new("capacity", Math.Round(f.Def.EnergyCapacity, 1)), new("points", f.MovePoints),
        ],
    });

    private string FighterName(string fighterId) => _ui.Text(BattleText.Name(Battle, fighterId));

    /// <summary>The token's letters: the first letter of each word and the number, if any ("Сірий вовк 2" → "СВ2").</summary>
    private static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => char.IsDigit(w[0]) ? w : w[..1].ToUpperInvariant()));

    private void Log(IEnumerable<SimEvent> events)
    {
        foreach (var e in events)
        {
            if (BattleText.Describe(Battle, e) is { } line)
                _log.AddText(_ui.Text(line) + "\n");
        }
    }

    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
            child.QueueFree();
    }
}
