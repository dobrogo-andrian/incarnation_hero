using Core.Content;
using Core.Rng;

namespace Sim.Cli.Bots;

/// <summary>The bots Sim.Cli can run, by name. A new bot is one entry here.</summary>
public static class BotRegistry
{
    /// <summary>Salt that keeps a bot's own random stream apart from the game's streams of the same seed.</summary>
    private const ulong BotSeedSalt = 0xB07_5EED_B07_5EEDUL;

    private static readonly Dictionary<string, Func<ContentDb, Xoshiro256StarStar, IBot>> Factories = new(StringComparer.Ordinal)
    {
        ["cautious"] = (db, rng) => new RuleBot(BotProfile.Cautious, db, rng),
        ["fighter"] = (db, rng) => new RuleBot(BotProfile.Fighter, db, rng),
        ["random"] = (db, rng) => new RandomBot(db, rng),
    };

    public static IEnumerable<string> Names => Factories.Keys.Order(StringComparer.Ordinal);

    public static bool Exists(string name) => Factories.ContainsKey(name);

    /// <summary>A fresh bot for one run; its random choices are seeded from the run seed, not saved in the game state.</summary>
    public static IBot Create(string name, ContentDb content, ulong runSeed) =>
        Factories[name](content, Xoshiro256StarStar.FromSeed(runSeed ^ BotSeedSalt));
}
