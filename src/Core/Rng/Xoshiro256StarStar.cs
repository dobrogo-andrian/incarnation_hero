using System.Numerics;

namespace Core.Rng;

/// <summary>
/// xoshiro256** (Blackman &amp; Vigna). The only PRNG allowed in Core; never use System.Random.
/// </summary>
public sealed class Xoshiro256StarStar
{
    private const double DoubleUnit = 1.0 / (1UL << 53);

    private ulong _s0, _s1, _s2, _s3;

    private Xoshiro256StarStar(RngState state)
    {
        if (state.IsZero)
            throw new ArgumentException("xoshiro256** state must not be all zeros.", nameof(state));
        (_s0, _s1, _s2, _s3) = (state.S0, state.S1, state.S2, state.S3);
    }

    public static Xoshiro256StarStar FromSeed(ulong seed)
    {
        var sm = new SplitMix64(seed);
        // SplitMix64 never yields four zeros in a row, so the state is always valid.
        return new Xoshiro256StarStar(new RngState(sm.Next(), sm.Next(), sm.Next(), sm.Next()));
    }

    public static Xoshiro256StarStar FromState(RngState state) => new(state);

    public RngState GetState() => new(_s0, _s1, _s2, _s3);

    public ulong NextULong()
    {
        ulong result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);

        return result;
    }

    /// <summary>Uniform integer in [minInclusive, maxExclusive), without modulo bias (Lemire's method).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Range must be non-empty.");

        ulong range = (ulong)((long)maxExclusive - minInclusive);
        ulong high = Math.BigMul(NextULong(), range, out ulong low);
        if (low < range)
        {
            ulong threshold = (0UL - range) % range;
            while (low < threshold)
                high = Math.BigMul(NextULong(), range, out low);
        }
        return (int)((long)minInclusive + (long)high);
    }

    /// <summary>Uniform double in [0, 1) with 53 bits of precision.</summary>
    public double NextDouble() => (NextULong() >> 11) * DoubleUnit;

    /// <summary>True with probability <paramref name="p"/>; p &lt;= 0 never, p &gt;= 1 always.</summary>
    public bool Chance(double p) => NextDouble() < p;
}
