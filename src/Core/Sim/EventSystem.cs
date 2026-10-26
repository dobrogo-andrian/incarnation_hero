using Core.Content;
using Core.Rng;
using Core.State;

namespace Core.Sim;

/// <summary>
/// Events (§10, tech 4.3, D38): the weekly roll in the player's context, the dialog's choices, and
/// the typed conditions and effects. A new condition or effect is a field on its content block plus
/// a case in <see cref="Holds"/> or <see cref="Apply"/>.
/// </summary>
public static class EventSystem
{
    /// <summary>The kind of week the current activity makes, or null when no events happen (seclusion).</summary>
    public static string? ContextOf(GameState state, ContentDb db) => state.Activity?.Activity switch
    {
        null or BreakthroughActivity => null,
        TravelActivity => EventContexts.Travel,
        _ when db.Get<ZoneDef>(state.Player.ZoneId).Settlement => EventContexts.Settlement,
        MeditateActivity => EventContexts.Meditation,
        WaitActivity => EventContexts.Rest,
        _ => null,
    };

    /// <summary>Event phase of a week: maybe opens one of the events eligible in <paramref name="context"/>.</summary>
    public static void RollWeek(GameState state, SimContext ctx, string? context, List<SimEvent> events)
    {
        if (context is null || state.Mode != GameMode.Overworld)
            return;
        var rng = ctx.Rng[RngStream.Events];
        double chance = ctx.Events.WeekChance.GetValueOrDefault(context);
        if (chance <= 0 || !rng.Chance(chance))
            return;

        var candidates = Eligible(state, ctx, context).Select(e => (Def: e, Weight: WeightOf(state, ctx, e))).ToList();
        if (candidates.Count == 0)
            return;
        Start(state, PickWeighted(candidates, rng), events);
    }

    /// <summary>Opens <paramref name="def"/> at its start node and records that it was shown.</summary>
    public static void Start(GameState state, EventDef def, List<SimEvent> events)
    {
        state.Mode = GameMode.EventDialog;
        state.Event = new ActiveEvent { EventId = def.Id, NodeId = EventDef.StartNode, StartedWeek = state.Week };
        if (History(state, def.Id) is not { } record)
        {
            record = new EventRecord { EventId = def.Id };
            state.EventHistory.Add(record);
            state.EventHistory.Sort((a, b) => string.CompareOrdinal(a.EventId, b.EventId));
        }
        record.Count++;
        record.LastWeek = state.Week;
        events.Add(new EventStarted(def.Id));
    }

    /// <summary>
    /// Takes option <paramref name="index"/> of the current node: its effects, then one weighted
    /// outcome; then the next node, or the dialog closes. Death on the way ends the run instead.
    /// </summary>
    public static CommandOutcome Choose(GameState state, SimContext ctx, int index)
    {
        var active = state.Event!;
        var node = ctx.Content.Get<EventDef>(active.EventId).Nodes[active.NodeId];
        var events = new List<SimEvent>();
        if (node.Options.Count == 0)
        {
            if (index != 0)
                return CommandOutcome.Reject(RejectReasons.InvalidOption);
            Close(state, events);
            return CommandOutcome.Accept(events);
        }
        if (index < 0 || index >= node.Options.Count)
            return CommandOutcome.Reject(RejectReasons.InvalidOption);
        var option = node.Options[index];
        if (!option.Conditions.All(c => Holds(c, state, ctx)))
            return CommandOutcome.Reject(RejectReasons.OptionUnavailable);

        string? next = Apply(option.Effects, state, ctx, events) ?? option.Next;
        if (option.Outcomes.Count > 0 && state.Mode != GameMode.RunEnd)
        {
            var outcome = PickWeighted(option.Outcomes.Select(o => (o, OutcomeWeight(o, state.Player, ctx))).ToList(), ctx.Rng[RngStream.Events]);
            next = Apply(outcome.Effects, state, ctx, events) ?? outcome.Next;
        }

        if (state.Mode == GameMode.RunEnd)
            return CommandOutcome.Accept(events);
        if (next is null)
            Close(state, events);
        else
            active.NodeId = next;
        return CommandOutcome.Accept(events);
    }

    /// <summary>The current node with each option's availability and, where it has one, the chance of success as the player sees it.</summary>
    public static EventView? View(GameState state, SimContext ctx)
    {
        if (state.Event is not { } active)
            return null;
        var node = ctx.Content.Get<EventDef>(active.EventId).Nodes[active.NodeId];
        var options = node.Options.Select((option, i) =>
        {
            var unmet = option.Conditions.Where(c => !Holds(c, state, ctx)).ToList();
            return new EventOptionView(i, unmet.Count == 0, unmet, unmet.Count == 0 ? SuccessChance(option, state, ctx, active, i) : null);
        });
        return new EventView(active.EventId, active.NodeId, options.ToList());
    }

    public static int ReputationOf(PlayerState player, string sectId) =>
        player.Reputation.Find(r => r.SectId == sectId)?.Value ?? 0;

    public static bool Holds(EventConditionDef c, GameState state, SimContext ctx)
    {
        var player = state.Player;
        return c switch
        {
            { Stat: { } r } => r.Holds(ctx.Rules.Value(player, r.Id)),
            { Resource: { } r } => r.Holds(player.Resources.First(x => x.ResourceId == r.Id).Current),
            { Injury: { } r } => r.Holds(InjurySystem.Value(player, r.Id)),
            { Knowledge: { } r } => r.Holds(KnowledgeSystem.Level(player, r.Id)),
            { Item: { } r } => r.Holds(ItemSystem.Count(player.Items, r.Id)),
            { Reputation: { } r } => r.Holds(ReputationOf(player, r.Id)),
            { Stones: { } r } => r.Holds(player.Stones),
            { ZoneDanger: { } r } => r.Holds(ctx.Content.Get<ZoneDef>(player.ZoneId).Danger),
            { Stage: { } r } => r.Holds(player.Tracks.First(t => t.VectorId == Simulation.QiVectorId).StageIndex),
            { AgeYears: { } r } => r.Holds(player.AgeWeeks / ctx.Config.WeeksPerYear),
            { Zone: { } zones } => zones.Contains(player.ZoneId),
            _ => throw new InvalidOperationException("Condition with no field set."),
        };
    }

    /// <summary>Applies effects in order; returns the node a fight among them leads to, if any.</summary>
    private static string? Apply(List<EventEffectDef> effects, GameState state, SimContext ctx, List<SimEvent> events)
    {
        var player = state.Player;
        foreach (var effect in effects)
        {
            if (state.Mode == GameMode.RunEnd)
                return null;
            switch (effect)
            {
                case { Time: int weeks }:
                    TimeSystem.PassWeeks(state, ctx, weeks, events);
                    break;
                case { Stat: { } stat }:
                    var value = player.Stats.First(s => s.StatId == stat.Id);
                    double before = value.Base;
                    value.Base = StateNumbers.Round(Math.Max(0, value.Base + stat.Amount));
                    ResourceSystem.ClampToMax(player, ctx);
                    events.Add(new StatChanged(stat.Id, StateNumbers.Round(value.Base - before)));
                    break;
                case { Resource: { } resource }:
                    double had = player.Resources.First(r => r.ResourceId == resource.Id).Current;
                    if (resource.Amount >= 0)
                        ResourceSystem.Restore(player, ctx, resource.Id, resource.Amount);
                    else
                        ResourceSystem.Drain(player, resource.Id, -resource.Amount);
                    events.Add(new ResourceChanged(resource.Id, StateNumbers.Round(player.Resources.First(r => r.ResourceId == resource.Id).Current - had)));
                    break;
                case { Injury: { } injury }:
                    if (injury.Amount >= 0)
                        InjurySystem.Inflict(player, ctx, injury.Id, injury.Amount, events);
                    else
                        InjurySystem.Heal(player, ctx, injury.Id, -injury.Amount, events);
                    break;
                case { Item: { } item }:
                    int count = Math.Max((int)item.Amount, -ItemSystem.Count(player.Items, item.Id));
                    if (count != 0)
                    {
                        ItemSystem.Change(player.Items, item.Id, count);
                        events.Add(new ItemsChanged(item.Id, count));
                    }
                    break;
                case { Stones: int stones }:
                    int change = Math.Max(stones, -player.Stones);
                    player.Stones += change;
                    events.Add(new StonesChanged(change));
                    break;
                case { CombatInsight: double insight }:
                    CultivationSystem.GrantCombatInsight(player, insight);
                    events.Add(new BuffGained(BuffKind.CombatInsight, insight));
                    break;
                case { PillEnergy: double energy }:
                    CultivationSystem.AddPillEnergy(player, energy);
                    events.Add(new BuffGained(BuffKind.PillEnergy, energy));
                    break;
                case { Knowledge: { } knowledge }:
                    KnowledgeSystem.Gain(player, ctx, knowledge.Id, knowledge.Amount, null, events);
                    break;
                case { Reputation: { } reputation }:
                    ChangeReputation(player, reputation.Id, (int)reputation.Amount);
                    events.Add(new ReputationChanged(reputation.Id, (int)reputation.Amount));
                    break;
                case { Reveal: { } id }:
                    if (WorldSystem.Reveal(state.World, id))
                        events.Add(new Revealed(id));
                    break;
                case { StartCombat: { } combat }:
                    return CombatSystem.Fight(player, ctx, combat.Group, events) ? combat.Win : combat.Loss;
                default:
                    throw new InvalidOperationException("Effect with no field set.");
            }
        }
        return null;
    }

    private static void Close(GameState state, List<SimEvent> events)
    {
        events.Add(new EventEnded(state.Event!.EventId));
        state.Event = null;
        state.Mode = GameMode.Overworld;
    }

    /// <summary>Events that can happen now in <paramref name="context"/>, in ID order.</summary>
    public static IEnumerable<EventDef> Eligible(GameState state, SimContext ctx, string context) =>
        ctx.Content.All<EventDef>().Where(e => e.Contexts.Contains(context) && IsEligible(state, ctx, e));

    private static bool IsEligible(GameState state, SimContext ctx, EventDef def)
    {
        if (History(state, def.Id) is { } record
            && (def.OncePerRun || state.Week - record.LastWeek < def.CooldownWeeks))
            return false;
        return def.Conditions.All(c => Holds(c, state, ctx));
    }

    /// <summary>The event's weight, cut while it was shown recently so the same events do not keep coming back.</summary>
    private static double WeightOf(GameState state, SimContext ctx, EventDef def) =>
        History(state, def.Id) is { } record && state.Week - record.LastWeek < ctx.Events.RecentWeeks
            ? def.Weight * ctx.Events.RecentWeightFactor
            : def.Weight;

    private static double OutcomeWeight(EventOutcomeDef outcome, PlayerState player, SimContext ctx) =>
        Math.Max(0, outcome.Weight + outcome.WeightPerStat.Sum(w => w.Value * ctx.Rules.Value(player, w.Key)));

    /// <summary>Share of the weight on successful outcomes, observed as a range; null for an option with nothing to succeed at.</summary>
    private static ObservedRange? SuccessChance(EventOptionDef option, GameState state, SimContext ctx, ActiveEvent active, int index)
    {
        if (!option.Outcomes.Any(o => o.Success))
            return null;
        var weights = option.Outcomes.Select(o => (o.Success, Weight: OutcomeWeight(o, state.Player, ctx))).ToList();
        double total = weights.Sum(w => w.Weight);
        double truth = total > 0 ? weights.Where(w => w.Success).Sum(w => w.Weight) / total : 0;
        return Observation.Chance(truth, state.Player, ctx, StableHash.Of(state.Seed, active.EventId, active.NodeId, index, active.StartedWeek));
    }

    private static T PickWeighted<T>(List<(T Item, double Weight)> items, Xoshiro256StarStar rng)
    {
        double roll = rng.NextDouble() * items.Sum(i => i.Weight);
        foreach (var (item, weight) in items)
        {
            if (roll < weight)
                return item;
            roll -= weight;
        }
        return items.Last(i => i.Weight > 0).Item;
    }

    private static EventRecord? History(GameState state, string eventId) =>
        state.EventHistory.Find(r => r.EventId == eventId);

    private static void ChangeReputation(PlayerState player, string sectId, int amount)
    {
        if (player.Reputation.Find(r => r.SectId == sectId) is not { } entry)
        {
            entry = new Reputation { SectId = sectId };
            player.Reputation.Add(entry);
            player.Reputation.Sort((a, b) => string.CompareOrdinal(a.SectId, b.SectId));
        }
        entry.Value += amount;
        if (entry.Value == 0)
            player.Reputation.Remove(entry);
    }
}

/// <summary>The open event node as the player sees it; a node without options closes with "continue".</summary>
public sealed record EventView(string EventId, string NodeId, IReadOnlyList<EventOptionView> Options);

/// <summary>
/// One option: unavailable ones list the conditions the player does not meet; available ones with
/// a chance of success show it as an observed range.
/// </summary>
public sealed record EventOptionView(int Index, bool Available, IReadOnlyList<EventConditionDef> Unmet, ObservedRange? Success);
