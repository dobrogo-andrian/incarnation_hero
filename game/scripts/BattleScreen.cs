using Core.Combat;
using Core.Content;
using Core.Loc;
using Core.Sim;
using Godot;
using static Widgets;

/// <summary>
/// The hex fight, over the run's screen below the status bar: the test fight (D56), outside the run,
/// where the player picks the hero (a sandbox profile or a snapshot of the current hero) and an enemy
/// group; or a fight of the run taken by hand (D67), fought through Core's commands and saved as it
/// goes. The player clicks a lit hex to step and an adjacent enemy to strike. The skill bar under the
/// field holds the techniques (keys 1–5) and, apart at the far right, the basic attack and ending the
/// turn (Space); picking a skill lights where it can go. Guard, flee, giving an arena bout up and
/// handing the fight to the AI are buttons beside the log. What Core would refuse is greyed out with
/// its reason. Every command's events, the enemy turns after it included, are played back event by
/// event (steps, hits) with the CTB queue on top; Core has already decided them. A fight of the run
/// ends on what it left behind, and "continue" goes back to the run.
/// </summary>
public partial class BattleScreen : PanelContainer
{
    /// <summary>Seconds each step or hit stays on screen.</summary>
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
    private Func<Command, CommandOutcome> _execute = null!;
    private Action _back = null!;

    private readonly Label _title = new() { AutowrapMode = TextServer.AutowrapMode.Off, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private Button _leave = null!;
    private readonly VBoxContainer _setup = new();
    private readonly HBoxContainer _fightView = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
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

    private IFight? _fight;
    private string? _aiming;
    private Hex? _hover;

    /// <summary>What a fight of the run left behind, as journal lines: the result, injuries, insight, loot.</summary>
    private readonly List<LocKey> _aftermath = [];

    /// <summary>Fighters as shown while events are played back, by ID; empty when the screen shows Core's state.</summary>
    private readonly Dictionary<string, HexFieldView.Token> _shown = [];
    private readonly Queue<SimEvent> _playback = new();
    private double _clock;

    /// <param name="execute">Runs a command of the run's fight through Core and the save, as the main screen does.</param>
    /// <param name="back">Called once the screen closes, to redraw the run.</param>
    /// <param name="newSeed">The test fight seeds, from the shell like a run's.</param>
    public void Setup(UiContext ui, Func<Combatant> currentHero, Func<Command, CommandOutcome> execute, Action back, Func<ulong> newSeed)
    {
        _ui = ui;
        _currentHero = currentHero;
        _execute = execute;
        _back = back;
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
        _fightView.AddThemeConstantOverride("separation", 16);
        _fightView.AddChild(left);
        _fightView.AddChild(side);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        // A wrapping label in a row is squeezed to nothing and grows tall: the title stays on one line.
        _title.AddThemeFontSizeOverride("font_size", 20);
        _leave = Button(ui.Text("str.ui.sandbox_back"), Close);
        column.AddChild(Row(_title, _leave));
        column.AddChild(_setup);
        column.AddChild(_fightView);
        var margin = new MarginContainer();
        foreach (string edge in new[] { "left", "top", "right", "bottom" })
            margin.AddThemeConstantOverride($"margin_{edge}", 16);
        margin.AddChild(column);
        AddChild(margin);
    }

    /// <summary>Whether a fight of the run is on screen; the main screen opens it once for each fight taken by hand.</summary>
    public bool InRun => Visible && _fight is RunFight;

    /// <summary>Opens the test fight on the picker; the previous picks are kept.</summary>
    public void Open()
    {
        Visible = true;
        _fight = null;
        _title.Text = _ui.Text("str.ui.sandbox");
        _leave.Visible = true;
        ShowSetup();
    }

    /// <summary>
    /// Shows a fight of the run taken by hand, <paramref name="battle"/> as Core keeps it, and plays
    /// <paramref name="opening"/> back from where the fighters stood; none after a load, which shows the fight as it stands.
    /// </summary>
    public void OpenRun(Battle battle, string title, IReadOnlyList<SimEvent> opening)
    {
        Visible = true;
        _title.Text = title;
        _leave.Visible = false;
        _aftermath.Clear();
        _aftermath.AddRange(opening.Select(SimText.Describe).OfType<LocKey>());
        Begin(new RunFight(battle, this), _currentHero(), opening);
    }

    private void Close()
    {
        Visible = false;
        _fight = null;
        _back();
    }

    private void ShowSetup()
    {
        _fightView.Visible = false;
        _setup.Visible = true;
        Clear(_setup);
        var heroes = CombatSandbox.Profiles(_ui.Db).Select(id => (id, _ui.Name(id)))
            .Prepend((CurrentHero, _ui.Text("str.ui.sandbox_current_hero"))).ToList();
        var groups = _ui.Db.All<EnemyGroupDef>().Select(g => (g.Id, $"{_ui.Name(g.Id)} ({g.Members.Sum(m => m.Count)})")).ToList();
        _groupPick = Pick(groups, _groupPick);
        _setup.AddChild(Text(_ui.Text("str.ui.sandbox_note")));
        _setup.AddChild(Row(new Label { Text = _ui.Text("str.ui.sandbox_hero"), CustomMinimumSize = new Vector2(90, 0) }, Options(heroes, _heroPick, id => _heroPick = id)));
        _setup.AddChild(Row(new Label { Text = _ui.Text("str.ui.sandbox_foes"), CustomMinimumSize = new Vector2(90, 0) }, Options(groups, _groupPick, id => _groupPick = id)));
        _setup.AddChild(Button(_ui.Text("str.ui.sandbox_start"), () => StartSandbox(_newSeed())));
    }

    /// <summary>
    /// For a UI shot (<see cref="UiShot"/>): the hero the picker offers first, a sandbox profile or the current hero.
    /// </summary>
    public void Preselect(string hero) => _heroPick = hero;

    /// <summary>
    /// For a UI shot: starts the test fight against <paramref name="group"/>, and has the AI play the hero's
    /// first <paramref name="heroTurns"/> turns, every turn shown at once.
    /// </summary>
    public void Stage(string group, int heroTurns)
    {
        Open();
        _groupPick = group;
        StartSandbox(_newSeed());
        FinishPlayback();
        var session = ((SandboxFight)_fight!).Session;
        for (int turn = 0; turn < heroTurns && !Battle.IsOver; turn++)
            Log(session.PlayTurnByAi());
        FinishPlayback();
    }

    private void StartSandbox(ulong seed)
    {
        var hero = _heroPick == CurrentHero ? _currentHero() : CombatSandbox.Profile(_ui.Db, _heroPick);
        var session = CombatSandbox.Start(_ui.Db, hero, _groupPick!, seed);
        Begin(new SandboxFight(session), hero, session.Opening);
    }

    private void Begin(IFight fight, Combatant hero, IReadOnlyList<SimEvent> opening)
    {
        _fight = fight;
        (_aiming, _clock) = (null, 0);
        _playback.Clear();
        _shown.Clear();
        _log.Clear();
        _hint.Text = _ui.Text("str.ui.battle_hint");
        _setup.Visible = false;
        _fightView.Visible = true;
        BuildSkillBar(hero);
        // The opening is played back from where everyone was deployed, at full strength.
        Play(Battle.Fighters.Select(f => (f.Id, new HexFieldView.Token(
            Initials(FighterName(f.Id)), f.Side, f.StartPosition, f.Def.Health, f.Def.MaxHealth, f.Def.Shield, f.Def.MaxShield, false))), opening);
        ShowState();
    }

    private Battle Battle => _fight!.Battle;

    /// <summary>Whether events are still being shown; the player's input waits meanwhile.</summary>
    public bool Playing => _playback.Count > 0;

    private bool MyTurn => _fight is not null && !Playing && Battle.Active is { Side: 0 };

    /// <summary>Queues events to show, played from the fighters as they stood before them (<paramref name="from"/>).</summary>
    private void Play(IEnumerable<(string Id, HexFieldView.Token Token)> from, IEnumerable<SimEvent> events)
    {
        foreach (var (id, token) in from)
            _shown[id] = token;
        foreach (var e in events)
            _playback.Enqueue(e);
        if (!Playing)
            _shown.Clear();
    }

    /// <summary>Shows whatever is left of the playback at once.</summary>
    private void FinishPlayback()
    {
        while (Playing)
            Reveal(_playback.Dequeue());
        _shown.Clear();
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
            case FighterDown d:
                return _shown.Remove(d.FighterId);
            case FighterFled f:
                return _shown.Remove(f.FighterId);
            case FighterBeaten b:
                return _shown.Remove(b.FighterId);
            case FighterYielded y:
                return _shown.Remove(y.FighterId);
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
        if (!MyTurn)
            return;
        var before = Tokens().ToList();
        var outcome = _fight!.Act(command);
        if (outcome.RejectReason is { } reason)
        {
            _hint.Text = _ui.Text(reason);
            ShowState();
            return;
        }
        _aiming = null;
        _hint.Text = _ui.Text("str.ui.battle_hint");
        Play(before, outcome.Events);
        ShowState();
    }

    /// <summary>The AI fights the rest; it is shown at once rather than turn by turn.</summary>
    private void HandOver()
    {
        if (!MyTurn)
            return;
        _aiming = null;
        var before = Tokens().ToList();
        Play(before, _fight!.HandOver());
        FinishPlayback();
    }

    private void OnHexPressed(Hex hex)
    {
        if (!MyTurn)
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
        if (!MyTurn)
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
                Args = [new("key", key), new("name", SimText.Name(technique.Id)), new("qi", technique.Combat.Qi), new("energy", technique.Combat.Energy)],
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
        if (!Visible || !_fightView.Visible || _fight is null || e is not InputEventKey { Pressed: true, Echo: false } key)
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
        if (_fight is null)
            return;
        var tokens = Playing ? _shown.Values.ToList() : Tokens().Select(t => t.Token).ToList();
        _field.Display(Battle.Field, tokens, Marks());

        IEnumerable<BattleFighter> queue = Battle.Active is { } active ? Battle.Forecast(8).Prepend(active) : [];
        _queue.Text = _ui.Text("str.ui.battle_queue") + " " + string.Join("  ·  ", queue.Select(f => FighterName(f.Id)));
        _active.Text = Battle.Active is { } a ? FighterText(a) : "";
        _hovered.Text = !Playing && _hover is { } hex && Battle.Fighters.FirstOrDefault(f => f.InFight && f.Position == hex) is { } under && under != Battle.Active
            ? FighterText(under)
            : "";

        bool myTurn = MyTurn;
        _hint.Visible = !Battle.IsOver;
        Clear(_actions);
        _actions.Visible = myTurn;
        // What Core would refuse now is greyed out, with its reason on hover.
        foreach (var (skill, button) in _skills)
            Allow(button, myTurn ? SkillRefusal(skill) : null, myTurn, myTurn && _aiming == skill);
        if (_endTurn is { } endTurn)
            Allow(endTurn, null, myTurn, false);
        if (myTurn)
        {
            var row = Row(ActionButton("str.ui.battle_guard", new Guard()), ActionButton("str.ui.battle_flee", new Flee()));
            if (Battle.Terms == BattleTerms.Bout)
                row.AddChild(ActionButton("str.ui.battle_yield", new Yield()));
            _actions.AddChild(row);
            _actions.AddChild(Row(Button(_ui.Text("str.ui.battle_hand_over"), HandOver)));
            if (_aiming is not null)
                _actions.AddChild(Button(_ui.Text("str.ui.battle_cancel"), () => Aim(null)));
        }

        Clear(_result);
        if (!Playing && Battle.IsOver)
        {
            _result.AddChild(Title(_ui.Text(ResultTitle(Battle.Fighters.First(f => f.Side == 0)))));
            if (_fight is RunFight)
            {
                foreach (var line in _aftermath)
                    _result.AddChild(Text(_ui.Text(line)));
                _result.AddChild(Row(Button(_ui.Text("str.ui.battle_continue"), Close)));
            }
            else
            {
                _result.AddChild(Row(Button(_ui.Text("str.ui.sandbox_again"), ShowSetup)));
            }
        }
    }

    private string ResultTitle(BattleFighter hero) =>
        Battle.Winner == 0 ? "str.ui.battle_won"
        : hero.Yielded ? "str.ui.battle_yielded"
        : hero.Beaten ? "str.ui.battle_beaten"
        : hero.Fled ? "str.ui.battle_escaped"
        : "str.ui.battle_lost";

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
        if (!MyTurn)
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
            new("shield", Math.Round(f.Shield, 1)), new("max_shield", Math.Round(f.Def.MaxShield, 1)), new("pool", Math.Round(f.QiPool, 1)),
            new("energy", Math.Round(f.Energy, 1)), new("capacity", Math.Round(f.Def.MaxEnergy, 1)), new("points", f.MovePoints),
            new("injuries", Injuries(f)),
        ],
    });

    /// <summary>The injuries this fight has left, scale by scale, so a hit on the body is not forgotten.</summary>
    private string Injuries(BattleFighter f)
    {
        var scales = f.Injuries.Where(i => i.Value > 0).Select(i => $"{_ui.Name(i.Key)} {Math.Round(i.Value, 1)}").ToList();
        return scales.Count == 0 ? _ui.Text("str.ui.battle_no_injuries") : string.Join(" · ", scales);
    }

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

    /// <summary>What the screen fights: the side-0 command with the enemy turns after it, and handing the rest to the AI.</summary>
    private interface IFight
    {
        Battle Battle { get; }

        CommandOutcome Act(BattleCommand command);

        IReadOnlyList<SimEvent> HandOver();
    }

    /// <summary>The test fight: a battle session of its own, nothing saved.</summary>
    private sealed class SandboxFight(BattleSession session) : IFight
    {
        public BattleSession Session { get; } = session;

        public Battle Battle => Session.Battle;

        public CommandOutcome Act(BattleCommand command) => Session.Act(command);

        public IReadOnlyList<SimEvent> HandOver() => Session.PlayOut();
    }

    /// <summary>A fight of the run: every command goes through Core, which saves it; its end is kept for the result.</summary>
    private sealed class RunFight(Battle battle, BattleScreen screen) : IFight
    {
        public Battle Battle { get; } = battle;

        public CommandOutcome Act(BattleCommand command) => Run(new FightAction(command));

        public IReadOnlyList<SimEvent> HandOver() => Run(new AutoResolveFight()).Events;

        private CommandOutcome Run(Command command)
        {
            var outcome = screen._execute(command);
            screen._aftermath.AddRange(outcome.Events.Select(SimText.Describe).OfType<LocKey>());
            return outcome;
        }
    }
}
