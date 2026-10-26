namespace Core.Rng;

/// <summary>Serializable xoshiro256** state. Saved as-is with the game state.</summary>
public readonly record struct RngState(ulong S0, ulong S1, ulong S2, ulong S3)
{
    public bool IsZero => (S0 | S1 | S2 | S3) == 0;
}
