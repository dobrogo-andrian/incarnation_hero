using Core.Content;
using Core.Rng;
using Core.Sim;

namespace Core.Combat;

/// <summary>
/// A hex battle with the AI playing side 1 (D59): a fight of the run by hand, and auto-resolve,
/// which hands side 0 to the AI as well (D61). Side 0 acts through <see cref="Act"/>, and the enemy
/// turns up to its next one follow at once, so the battle only ever waits for side 0. The AI turns
/// draw from <c>ai</c> in turn order whoever plays side 0, so a battle is replayed exactly from its
/// streams and side 0's commands.
/// </summary>
public sealed class BattleSession
{
    private readonly Xoshiro256StarStar _ai;

    private BattleSession(Battle battle, Xoshiro256StarStar ai)
    {
        Battle = battle;
        _ai = ai;
        var opening = new List<SimEvent>(battle.Opening);
        PlayEnemies(opening);
        Opening = opening;
    }

    public Battle Battle { get; }

    /// <summary>What happened before side 0's first turn: the opening and any enemy turns that came first.</summary>
    public IReadOnlyList<SimEvent> Opening { get; }

    public static BattleSession Start(IReadOnlyList<Combatant> side0, IReadOnlyList<Combatant> side1, CombatConfigDef config,
        Xoshiro256StarStar combat, Xoshiro256StarStar ai, BattleTerms terms = BattleTerms.ToDeath) =>
        new(Battle.Start(side0, side1, config, combat, terms), ai);

    /// <summary>Side 0's command, then the enemy turns up to side 0's next turn or the end.</summary>
    public CommandOutcome Act(BattleCommand command)
    {
        if (Battle.Active is { Side: not 0 })
            throw new InvalidOperationException("Side 0 acts only on its own turn.");
        var outcome = Battle.Execute(command);
        if (!outcome.Accepted)
            return outcome;
        var events = outcome.Events.ToList();
        PlayEnemies(events);
        return CommandOutcome.Accept(events);
    }

    /// <summary>The AI plays side 0's turn, then the enemy turns up to side 0's next turn or the end.</summary>
    public IReadOnlyList<SimEvent> PlayTurnByAi()
    {
        if (Battle.Active is not { Side: 0 })
            throw new InvalidOperationException("Side 0 acts only on its own turn.");
        var events = BattleAi.PlayTurn(Battle, _ai).ToList();
        PlayEnemies(events);
        return events;
    }

    /// <summary>The AI plays both sides to the end.</summary>
    public IReadOnlyList<SimEvent> PlayOut()
    {
        var events = new List<SimEvent>();
        while (!Battle.IsOver)
            events.AddRange(BattleAi.PlayTurn(Battle, _ai));
        return events;
    }

    /// <summary>How the battle ended for side 0; only once it is over.</summary>
    public CombatResult Result()
    {
        if (!Battle.IsOver)
            throw new InvalidOperationException("The battle is not over.");
        var me = Battle.Fighters.First(f => f.Side == 0);
        var end = me.Down ? CombatEnd.Died
            : me.Yielded || (me.Beaten && Battle.Terms == BattleTerms.Bout) ? CombatEnd.Yielded
            : me.Beaten ? CombatEnd.Beaten
            : Battle.Winner == 0 ? CombatEnd.Won
            : CombatEnd.Fled;
        return new CombatResult(end, Battle.Turns, me.Health, me.Shield, me.QiPool, me.Energy, me.Injuries.Select(i => (i.Key, i.Value)).ToList());
    }

    private void PlayEnemies(List<SimEvent> events)
    {
        while (Battle.Active is { Side: not 0 })
            events.AddRange(BattleAi.PlayTurn(Battle, _ai));
    }
}
