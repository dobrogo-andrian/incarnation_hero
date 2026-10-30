using Core.Combat;
using Core.Content;
using Core.Rng;

namespace Core.Sim;

/// <summary>
/// The "test fight" (D56): a hex battle of the player's side against an enemy group,
/// outside the run. The player's side is a sandbox profile (<c>profile.*</c>, written like an enemy)
/// or a snapshot of the current hero; nothing is written back. The enemies are played by the AI (D59).
/// </summary>
public sealed class CombatSandbox
{
    public const string ProfilePrefix = "profile.";

    private readonly Xoshiro256StarStar _ai;

    private CombatSandbox(Battle battle, Xoshiro256StarStar ai)
    {
        Battle = battle;
        _ai = ai;
    }

    public Battle Battle { get; }

    /// <summary>Whether the player is to act; otherwise <see cref="PlayEnemyTurn"/> plays the next enemy.</summary>
    public bool PlayersTurn => Battle.Active is { Side: 0 };

    /// <summary>The sandbox profiles in content order.</summary>
    public static IReadOnlyList<string> Profiles(ContentDb content) =>
        content.All<EnemyDef>().Select(e => e.Id).Where(id => id.StartsWith(ProfilePrefix, StringComparison.Ordinal)).ToList();

    public static Combatant Profile(ContentDb content, string profileId) =>
        CombatSystem.FromDef(Context(content), content.Get<EnemyDef>(profileId), Combatant.PlayerId);

    /// <summary>The hero of <paramref name="sim"/> as they are now: current health, qi and known techniques.</summary>
    public static Combatant Hero(Simulation sim) => CombatSystem.Snapshot(sim.State.Player, Context(sim.Content));

    /// <summary>Starts a fight of <paramref name="hero"/> against <paramref name="groupId"/>; the seed comes from the shell.</summary>
    public static CombatSandbox Start(ContentDb content, Combatant hero, string groupId, ulong seed)
    {
        var ctx = Context(content);
        var battle = Battle.Start([hero], CombatSystem.Foes(ctx, groupId), ctx.Combat, Xoshiro256StarStar.FromSeed(StableHash.Of("sandbox.combat", seed)));
        return new CombatSandbox(battle, Xoshiro256StarStar.FromSeed(StableHash.Of("sandbox.ai", seed)));
    }

    public IReadOnlyList<SimEvent> PlayEnemyTurn() =>
        PlayersTurn || Battle.IsOver ? throw new InvalidOperationException("No enemy is to act.") : BattleAi.PlayTurn(Battle, _ai);

    // Building fighters reads rules and content only; no stream is drawn from.
    private static SimContext Context(ContentDb content) => new(content, RngStreams.FromSeed(0));
}
