namespace Core.Rng;

/// <summary>
/// Independent RNG streams. Append new streams at the end only: the order defines how
/// stream seeds are derived from the master seed, so reordering changes every run.
/// </summary>
public enum RngStream
{
    Worldgen,
    Events,
    Rumors,
    Npc,
    Loot,
    Combat,
    Ai,
    Life,
    Breakthrough,
}

/// <summary>One xoshiro256** generator per <see cref="RngStream"/>, all derived from a master seed.</summary>
public sealed class RngStreams
{
    private static readonly RngStream[] AllStreams = Enum.GetValues<RngStream>();

    private readonly Xoshiro256StarStar[] _streams;

    private RngStreams(Xoshiro256StarStar[] streams) => _streams = streams;

    public static int Count => AllStreams.Length;

    public static RngStreams FromSeed(ulong masterSeed)
    {
        var sm = new SplitMix64(masterSeed);
        var streams = new Xoshiro256StarStar[AllStreams.Length];
        for (int i = 0; i < streams.Length; i++)
            streams[i] = Xoshiro256StarStar.FromSeed(sm.Next());
        return new RngStreams(streams);
    }

    /// <summary>Restores streams from saved states, indexed by <see cref="RngStream"/> value.</summary>
    public static RngStreams FromStates(IReadOnlyList<RngState> states)
    {
        if (states.Count != AllStreams.Length)
            throw new ArgumentException($"Expected {AllStreams.Length} stream states, got {states.Count}.", nameof(states));
        var streams = new Xoshiro256StarStar[states.Count];
        for (int i = 0; i < streams.Length; i++)
            streams[i] = Xoshiro256StarStar.FromState(states[i]);
        return new RngStreams(streams);
    }

    /// <summary>States indexed by <see cref="RngStream"/> value, for saving.</summary>
    public RngState[] GetStates()
    {
        var states = new RngState[_streams.Length];
        for (int i = 0; i < states.Length; i++)
            states[i] = _streams[i].GetState();
        return states;
    }

    public Xoshiro256StarStar this[RngStream stream] => _streams[(int)stream];

    /// <summary>
    /// Draws a fresh seed from <paramref name="stream"/> for an isolated sub-simulation
    /// (e.g. one combat gets its own generator seeded from the Combat stream).
    /// </summary>
    public ulong NextSubseed(RngStream stream) => this[stream].NextULong();
}
