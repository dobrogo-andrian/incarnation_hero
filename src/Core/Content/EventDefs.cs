namespace Core.Content;

/// <summary>
/// An event (§10, tech 4.3, D38): a small dialog of nodes with options, rolled weekly in the
/// contexts it lists when its conditions hold. The dialog starts at the node <see cref="StartNode"/>.
/// </summary>
public sealed class EventDef : ContentDef, IValidatedDef
{
    public const string StartNode = "start";

    /// <summary>Where it can happen: values of <see cref="EventContexts"/>.</summary>
    public List<string> Contexts { get; set; } = [];

    /// <summary>Relative chance among the events eligible in the same week.</summary>
    public double Weight { get; set; }

    public bool OncePerRun { get; set; }

    /// <summary>Weeks after it was shown before it can happen again.</summary>
    public int CooldownWeeks { get; set; }

    /// <summary>All must hold for the event to be eligible.</summary>
    public List<EventConditionDef> Conditions { get; set; } = [];

    public Dictionary<string, EventNodeDef> Nodes { get; set; } = [];

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (Contexts.Count == 0 || Contexts.Any(c => !EventContexts.All.Contains(c)))
            yield return $"needs contexts out of: {string.Join(", ", EventContexts.All)}.";
        if (Weight <= 0 || CooldownWeeks < 0)
            yield return "needs a positive weight and a cooldown of zero or more.";
        if (!Nodes.ContainsKey(StartNode))
        {
            yield return $"needs a '{StartNode}' node.";
            yield break;
        }

        foreach (var (nodeId, node) in Nodes.OrderBy(n => n.Key, StringComparer.Ordinal))
        {
            for (int i = 0; i < node.Options.Count; i++)
            {
                foreach (string message in node.Options[i].Problems(Nodes))
                    yield return $"node '{nodeId}' option {i}: {message}";
            }
        }

        var unreachable = Nodes.Keys.Except(Reachable()).Order(StringComparer.Ordinal).ToList();
        if (unreachable.Count > 0)
            yield return $"nodes unreachable from '{StartNode}': {string.Join(", ", unreachable)}.";
    }

    /// <summary>Node IDs the dialog can get to from the start node.</summary>
    private HashSet<string> Reachable()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { StartNode };
        var queue = new Queue<string>([StartNode]);
        while (queue.TryDequeue(out var id))
        {
            foreach (string next in Nodes[id].Options.SelectMany(o => o.NextNodes()))
            {
                if (Nodes.ContainsKey(next) && seen.Add(next))
                    queue.Enqueue(next);
            }
        }
        return seen;
    }
}

/// <summary>The kinds of weeks events are rolled in (D38).</summary>
public static class EventContexts
{
    public const string Travel = "travel";
    public const string Meditation = "meditation";
    public const string Settlement = "settlement";
    public const string Rest = "rest";

    public static readonly IReadOnlyList<string> All = [Travel, Meditation, Settlement, Rest];
}

/// <summary>One screen of an event. A node without options ends the dialog with an implicit "continue".</summary>
public sealed class EventNodeDef
{
    public LocalizedText Text { get; set; } = new();

    public List<EventOptionDef> Options { get; set; } = [];
}

/// <summary>
/// A choice: available when its conditions hold; applies its effects, then one weighted outcome if
/// it has any, and goes to <see cref="Next"/> (or the outcome's, or a combat's) or ends the dialog.
/// </summary>
public sealed class EventOptionDef
{
    public LocalizedText Text { get; set; } = new();

    public List<EventConditionDef> Conditions { get; set; } = [];

    public List<EventEffectDef> Effects { get; set; } = [];

    public List<EventOutcomeDef> Outcomes { get; set; } = [];

    public string? Next { get; set; }

    internal IEnumerable<string> NextNodes() =>
        EventEffectDef.WithCombatNodes(Next, Effects).Concat(Outcomes.SelectMany(o => o.NextNodes()));

    internal IEnumerable<string> Problems(Dictionary<string, EventNodeDef> nodes)
    {
        if (Outcomes.Count > 0 && Next is not null)
            yield return "has outcomes, so the outcomes say where to go next.";
        if (Outcomes.Any(o => o.Weight < 0) || Outcomes.Count > 0 && Outcomes.All(o => o.Weight <= 0))
            yield return "outcome weights must not be negative and must not all be zero.";
        foreach (string message in EventEffectDef.ListProblems(Effects, Next is not null || Outcomes.Count > 0))
            yield return message;
        foreach (var outcome in Outcomes)
        {
            foreach (string message in EventEffectDef.ListProblems(outcome.Effects, outcome.Next is not null))
                yield return message;
        }
        foreach (string next in NextNodes().Where(n => !nodes.ContainsKey(n)))
            yield return $"goes to missing node '{next}'.";
    }
}

/// <summary>
/// One possible result of an option. Its weight grows with the player's stats by
/// <see cref="WeightPerStat"/>, so a stronger character succeeds more often (D38).
/// </summary>
public sealed class EventOutcomeDef : IValidatedDef
{
    public double Weight { get; set; }

    /// <summary>Stat or aspect → weight added per point of its value.</summary>
    public Dictionary<string, double> WeightPerStat { get; set; } = [];

    /// <summary>Counts towards the option's shown chance of success.</summary>
    public bool Success { get; set; }

    public List<EventEffectDef> Effects { get; set; } = [];

    public string? Next { get; set; }

    internal IEnumerable<string> NextNodes() =>
        EventEffectDef.WithCombatNodes(Next, Effects);

    public IEnumerable<string> Validate(ContentDb db) =>
        WeightPerStat.Keys.Order(StringComparer.Ordinal).SelectMany(id => OneOf.CheckId(db, id, "weight_per_stat", typeof(StatDef), typeof(AspectDef)));
}

/// <summary>
/// One typed test of the player or the world (D38); exactly one field is set. A range holds when
/// the value is at least <c>min</c> and at most <c>max</c>, whichever are given.
/// </summary>
public sealed class EventConditionDef : IValidatedDef
{
    /// <summary>Computed value of a stat or aspect.</summary>
    public IdRange? Stat { get; set; }

    /// <summary>Current amount of a resource.</summary>
    public IdRange? Resource { get; set; }

    public IdRange? Injury { get; set; }

    /// <summary>Level of a technique, element or method.</summary>
    public IdRange? Knowledge { get; set; }

    /// <summary>How many of an item are carried.</summary>
    public IdRange? Item { get; set; }

    /// <summary>Reputation with a sect (D39).</summary>
    public IdRange? Reputation { get; set; }

    public NumberRange? Stones { get; set; }

    public NumberRange? ZoneDanger { get; set; }

    /// <summary>Stage index of the qi track; 0 is mortal.</summary>
    public NumberRange? Stage { get; set; }

    public NumberRange? AgeYears { get; set; }

    /// <summary>The player is in one of these zones.</summary>
    [ContentRef(typeof(ZoneDef))]
    public List<string>? Zone { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (OneOf.Count(this) != 1)
            yield return "condition must set exactly one field.";
        foreach (string message in OneOf.CheckId(db, Stat?.Id, "stat", typeof(StatDef), typeof(AspectDef))
            .Concat(OneOf.CheckId(db, Resource?.Id, "resource", typeof(ResourceDef)))
            .Concat(OneOf.CheckId(db, Injury?.Id, "injury", typeof(InjuryDef)))
            .Concat(OneOf.CheckId(db, Knowledge?.Id, "knowledge", typeof(KnowledgeDef)))
            .Concat(OneOf.CheckId(db, Item?.Id, "item", typeof(ItemDef)))
            .Concat(OneOf.CheckId(db, Reputation?.Id, "reputation", typeof(SectDef))))
            yield return message;
    }
}

/// <summary>A range on a value that belongs to a content object (a stat, an item…).</summary>
public sealed class IdRange : NumberRange
{
    public string Id { get; set; } = "";
}

/// <summary>At least <see cref="Min"/> and at most <see cref="Max"/>, whichever are given.</summary>
public class NumberRange
{
    public double? Min { get; set; }
    public double? Max { get; set; }

    public bool Holds(double value) => (Min is not { } min || value >= min) && (Max is not { } max || value <= max);
}

/// <summary>
/// One typed change an event makes (D38); exactly one field is set. Negative amounts take away.
/// A new kind of effect is a field here plus its case in <c>EventSystem</c>.
/// </summary>
public sealed class EventEffectDef : IValidatedDef
{
    /// <summary>Weeks pass for the world while the player is busy with the event.</summary>
    public int? Time { get; set; }

    /// <summary>Changes a stat's base value for good.</summary>
    public IdAmount? Stat { get; set; }

    /// <summary>Restores or drains a resource's current amount (toxins included).</summary>
    public IdAmount? Resource { get; set; }

    /// <summary>Inflicts (positive) or heals (negative) an injury scale.</summary>
    public IdAmount? Injury { get; set; }

    /// <summary>Gives or takes items; a whole number.</summary>
    public IdAmount? Item { get; set; }

    /// <summary>Gives or takes spirit stones; never below zero.</summary>
    public int? Stones { get; set; }

    public double? CombatInsight { get; set; }

    public double? PillEnergy { get; set; }

    /// <summary>Comprehension points towards a technique, element or method, up to the level table's end.</summary>
    public IdAmount? Knowledge { get; set; }

    /// <summary>Changes reputation with a sect; a whole number.</summary>
    public IdAmount? Reputation { get; set; }

    /// <summary>Reveals a hidden zone or meditation place (D40).</summary>
    public string? Reveal { get; set; }

    /// <summary>A fight; it decides the next node, so it is the last effect of its list.</summary>
    public CombatEffectDef? StartCombat { get; set; }

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (OneOf.Count(this) != 1)
            yield return "effect must set exactly one field.";
        if (Time is <= 0)
            yield return "time must be at least one week.";
        if (IsFraction(Item?.Amount) || IsFraction(Reputation?.Amount))
            yield return "item and reputation amounts must be whole numbers.";
        if (Knowledge?.Amount <= 0 || CombatInsight <= 0 || PillEnergy <= 0)
            yield return "knowledge, combat_insight and pill_energy only give, so they must be positive.";
        if (Reveal is { } id && !(db.TryGet<ZoneDef>(id, out var zone) && zone.Hidden) && !(db.TryGet<PlaceDef>(id, out var place) && place.Hidden))
            yield return $"reveal needs a hidden zone or place, not '{id}'.";
        foreach (string message in OneOf.CheckId(db, Stat?.Id, "stat", typeof(StatDef))
            .Concat(OneOf.CheckId(db, Resource?.Id, "resource", typeof(ResourceDef)))
            .Concat(OneOf.CheckId(db, Injury?.Id, "injury", typeof(InjuryDef)))
            .Concat(OneOf.CheckId(db, Item?.Id, "item", typeof(ItemDef)))
            .Concat(OneOf.CheckId(db, Knowledge?.Id, "knowledge", typeof(KnowledgeDef)))
            .Concat(OneOf.CheckId(db, Reputation?.Id, "reputation", typeof(SectDef))))
            yield return message;
    }

    /// <summary>A combat decides where to go, so it must come last and alone, in a list that has no other way on.</summary>
    internal static IEnumerable<string> ListProblems(List<EventEffectDef> effects, bool hasNext)
    {
        int combat = effects.FindIndex(e => e.StartCombat is not null);
        if (combat >= 0 && (combat != effects.Count - 1 || hasNext))
            yield return "start_combat must be the last effect of a list with no other next node.";
    }

    /// <summary><paramref name="next"/>, if any, and the nodes a combat among <paramref name="effects"/> goes on to.</summary>
    internal static IEnumerable<string> WithCombatNodes(string? next, List<EventEffectDef> effects) =>
        effects.Select(e => e.StartCombat).OfType<CombatEffectDef>().SelectMany(c => new[] { c.Win, c.Loss }).Prepend(next).OfType<string>();

    private static bool IsFraction(double? value) => value is { } v && v != Math.Floor(v);
}

public sealed class IdAmount
{
    public string Id { get; set; } = "";
    public double Amount { get; set; }
}

/// <summary>A fight with an enemy group; the dialog goes on at <see cref="Win"/> or <see cref="Loss"/>.</summary>
public sealed class CombatEffectDef
{
    [ContentRef(typeof(EnemyGroupDef))]
    public string Group { get; set; } = "";

    public string Win { get; set; } = "";

    public string Loss { get; set; } = "";
}

/// <summary>Checks for blocks where exactly one field is set and its ID is of the right type.</summary>
internal static class OneOf
{
    public static int Count(object block) =>
        block.GetType().GetProperties().Count(p => p.GetValue(block) is not null);

    public static IEnumerable<string> CheckId(ContentDb db, string? id, string field, params Type[] types)
    {
        if (id is null)
            yield break;
        if (!db.TryGet<ContentDef>(id, out var def))
            yield return $"{field} references missing id '{id}'.";
        else if (!types.Any(t => t.IsInstanceOfType(def)))
            yield return $"{field} references '{id}', which is {def.GetType().Name}, not {string.Join(" or ", types.Select(t => t.Name))}.";
    }
}

/// <summary>A sect of the settlement (D39); the player has a reputation with each.</summary>
public sealed class SectDef : ContentDef
{
    public LocalizedText Name { get; set; } = new();
}

/// <summary>Enemies an event can make the player fight (<c>grp.*</c>).</summary>
public sealed class EnemyGroupDef : ContentDef, IValidatedDef
{
    public LocalizedText Name { get; set; } = new();

    /// <summary>Strength in the stage 1 stand-in resolver (D38).</summary>
    public double Power { get; set; }

    /// <summary>Injuries a lost fight leaves.</summary>
    [ContentRef(typeof(InjuryDef))]
    public Dictionary<string, double> LossInjuries { get; set; } = [];

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (Power <= 0)
            yield return "needs a positive power.";
    }
}

/// <summary>Weekly event rolls (<c>config.events</c>, D38).</summary>
public sealed class EventsConfigDef : ContentDef, IValidatedDef
{
    public const string EventsId = "config.events";

    /// <summary>Context → chance that a week in it brings an event.</summary>
    public Dictionary<string, double> WeekChance { get; set; } = [];

    /// <summary>An event shown within this many weeks has its weight multiplied by <see cref="RecentWeightFactor"/>.</summary>
    public int RecentWeeks { get; set; }

    public double RecentWeightFactor { get; set; }

    /// <summary>How wide the shown chance of success is with no perception, and how much each point narrows it.</summary>
    public double ChanceRangeWidth { get; set; }

    public double ChanceRangeNarrowPerPerception { get; set; }

    /// <summary>Shown chance bounds are rounded to this step.</summary>
    public double ChanceRangeStep { get; set; }

    [ContentRef(typeof(StatDef))]
    public string PerceptionStat { get; set; } = "";

    public IEnumerable<string> Validate(ContentDb db)
    {
        if (WeekChance.Keys.Any(c => !EventContexts.All.Contains(c)) || WeekChance.Values.Any(p => p is < 0 or > 1))
            yield return $"week_chance needs contexts out of {string.Join(", ", EventContexts.All)} with chances from 0 to 1.";
        if (ChanceRangeStep <= 0)
            yield return "chance_range_step must be positive.";
    }
}

/// <summary>Fights (<c>config.combat</c>). Until the formula resolver, only the stand-in's numbers (D38).</summary>
public sealed class CombatConfigDef : ContentDef
{
    public const string CombatId = "config.combat";

    /// <summary>The player's power is the sum of these stats' values plus a bonus per qi stage.</summary>
    [ContentRef(typeof(StatDef))]
    public List<string> StubPowerStats { get; set; } = [];

    public double StubPowerPerStage { get; set; }
}
