namespace Core.State;

/// <summary>
/// Doubles are rounded explicitly before they are written to state, so a save always reproduces
/// the same numbers and tiny floating-point drift never reaches the hash.
/// </summary>
public static class StateNumbers
{
    public const int Decimals = 4;

    public static double Round(double value) => Math.Round(value, Decimals, MidpointRounding.ToEven);
}
