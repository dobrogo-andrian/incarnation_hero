using System.Globalization;
using System.Text.Json;
using Core.Combat;
using Core.Content;
using Core.Rng;
using Core.Sim;
using Sim.Cli.Runs;

namespace Sim.Cli.Arena;

/// <summary>
/// One arena fight from side A's view: the winning side ("a", "b", or "none" when the turns ran
/// out), how the losing side left (down, fled or timeout), the turns it took, and what A was left with.
/// </summary>
public sealed record ArenaFight(ulong Seed, string Winner, string End, int Turns, double AHealthShare, IReadOnlyDictionary<string, double> AInjuries);

/// <summary>
/// The combat arena of Sim.Cli (tech 6.1): hex fights between two sides with the AI playing both
/// (D59), each on its own seed. A side is an enemy group or <c>player</c>, the hero as a run starts.
/// </summary>
public static class ArenaRunner
{
    public const string PlayerSide = "player";

    /// <summary>The fighters of a side, their IDs prefixed so that a group can meet itself; null for an unknown side.</summary>
    public static IReadOnlyList<Combatant>? Side(ContentDb content, string side, string prefix)
    {
        var sim = Simulation.NewGame(content, 1);
        var ctx = new SimContext(content, RngStreams.FromStates(sim.State.RngStates));
        IReadOnlyList<Combatant> fighters;
        if (side == PlayerSide)
            fighters = [CombatSystem.Snapshot(sim.State.Player, ctx)];
        else if (content.All<EnemyGroupDef>().Any(g => g.Id == side))
            fighters = CombatSystem.Foes(ctx, side);
        else
            return null;
        return fighters.Select(f => f with { Id = $"{prefix}.{f.Id}" }).ToList();
    }

    public static ArenaFight Fight(ContentDb content, IReadOnlyList<Combatant> a, IReadOnlyList<Combatant> b, ulong seed)
    {
        var config = content.Get<CombatConfigDef>(CombatConfigDef.CombatId);
        var battle = Battle.Start(a, b, config, Xoshiro256StarStar.FromSeed(StableHash.Of("arena.combat", seed)));
        var ai = Xoshiro256StarStar.FromSeed(StableHash.Of("arena.ai", seed));
        bool timedOut = false;
        while (!battle.IsOver)
            timedOut |= BattleAi.PlayTurn(battle, ai).Any(e => e is BattleTimedOut);

        var sideA = battle.Fighters.Where(f => f.Side == 0).ToList();
        var loser = battle.Fighters.Where(f => f.Side != battle.Winner).ToList();
        string end = timedOut ? "timeout" : loser.All(f => f.Down) ? "down" : "fled";
        var injuries = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (var (scale, amount) in sideA.SelectMany(f => f.Injuries))
            injuries[scale] = Math.Round(injuries.GetValueOrDefault(scale) + amount, 3);
        return new ArenaFight(
            seed,
            timedOut ? "none" : battle.Winner == 0 ? "a" : "b",
            end,
            battle.Turns,
            Math.Round(sideA.Sum(f => f.Health) / sideA.Sum(f => f.Def.MaxHealth), 4),
            injuries);
    }

    /// <summary>Fights seeds <paramref name="seedFrom"/> on, in parallel; results stay in seed order.</summary>
    public static ArenaFight[] FightMany(ContentDb content, IReadOnlyList<Combatant> a, IReadOnlyList<Combatant> b, ulong seedFrom, int fights)
    {
        var results = new ArenaFight[fights];
        Parallel.For(0, fights, i => results[i] = Fight(content, a, b, seedFrom + (ulong)i));
        return results;
    }

    public static string ToJsonLine(ArenaFight fight) => JsonSerializer.Serialize(fight, BotRunner.Json);

    /// <summary>The summary tech 6.1 asks for: win shares and how fights ended, length, and what side A carried away.</summary>
    public static IEnumerable<string> Report(IReadOnlyList<ArenaFight> fights)
    {
        double Share(Func<ArenaFight, bool> which) => 100.0 * fights.Count(which) / fights.Count;
        string Pct(double value) => value.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        var turns = fights.Select(f => f.Turns).Order().ToList();

        yield return $"fights: {fights.Count}";
        yield return $"A wins: {Pct(Share(f => f.Winner == "a"))} (B down {Pct(Share(f => f.Winner == "a" && f.End == "down"))}, B fled {Pct(Share(f => f.Winner == "a" && f.End == "fled"))})";
        yield return $"B wins: {Pct(Share(f => f.Winner == "b"))} (A down {Pct(Share(f => f.Winner == "b" && f.End == "down"))}, A fled {Pct(Share(f => f.Winner == "b" && f.End == "fled"))})";
        yield return $"timeouts: {Pct(Share(f => f.End == "timeout"))}";
        yield return $"turns: mean {Num(turns.Average())}, p50 {turns[turns.Count / 2]}, p90 {turns[turns.Count * 9 / 10]}";
        yield return $"A health left: mean {Num(fights.Average(f => f.AHealthShare) * 100)}%";
        var scales = fights.SelectMany(f => f.AInjuries.Keys).Distinct().Order(StringComparer.Ordinal);
        yield return "A injuries (mean): " + string.Join(", ", scales.Select(s => $"{s} {Num(fights.Average(f => f.AInjuries.GetValueOrDefault(s)))}"));
    }
}
