namespace Core.Combat;

/// <summary>
/// A battlefield hex in axial coordinates (tech 4.1). The third cube coordinate is derived, and
/// distances and lines are worked out in cube terms, following Red Blob Games. Whether hexes are
/// drawn pointy or flat is up to the view.
/// </summary>
public readonly record struct Hex(int Q, int R) : IComparable<Hex>
{
    public static readonly Hex Zero = new(0, 0);

    /// <summary>The six neighbour offsets, counter-clockwise from +Q. Their order is part of determinism.</summary>
    public static readonly IReadOnlyList<Hex> Directions = [new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1)];

    /// <summary>Shifts line samples off hex edges so a tie always rounds the same way.</summary>
    private const double LineNudge = 1e-6;

    public int S => -Q - R;

    public static Hex operator +(Hex a, Hex b) => new(a.Q + b.Q, a.R + b.R);
    public static Hex operator -(Hex a, Hex b) => new(a.Q - b.Q, a.R - b.R);
    public static Hex operator *(Hex a, int k) => new(a.Q * k, a.R * k);

    /// <summary>Steps from the origin.</summary>
    public int Length => (Math.Abs(Q) + Math.Abs(R) + Math.Abs(S)) / 2;

    public int DistanceTo(Hex other) => (this - other).Length;

    public IEnumerable<Hex> Neighbors()
    {
        var self = this;
        return Directions.Select(d => self + d);
    }

    /// <summary>The direction (index into <see cref="Directions"/>) and steps of <paramref name="to"/> when it lies straight along one of the six axes.</summary>
    public (int Direction, int Steps)? StraightTo(Hex to)
    {
        var offset = to - this;
        int steps = offset.Length;
        for (int d = 0; d < Directions.Count && steps > 0; d++)
        {
            if (Directions[d] * steps == offset)
                return (d, steps);
        }
        return null;
    }

    /// <summary>The hexes a straight line from here to <paramref name="to"/> crosses, both ends included.</summary>
    public IReadOnlyList<Hex> LineTo(Hex to)
    {
        int steps = DistanceTo(to);
        var line = new List<Hex>(steps + 1);
        double q0 = Q + LineNudge, r0 = R + LineNudge;
        double dq = to.Q - Q, dr = to.R - R;
        for (int i = 0; i <= steps; i++)
        {
            double t = steps == 0 ? 0 : (double)i / steps;
            line.Add(Round(q0 + dq * t, r0 + dr * t));
        }
        return line;
    }

    /// <summary>The hex that holds a fractional axial point: round all three cube coordinates and fix the one that moved most.</summary>
    public static Hex Round(double q, double r)
    {
        double s = -q - r;
        double rq = Math.Round(q), rr = Math.Round(r), rs = Math.Round(s);
        double dq = Math.Abs(rq - q), dr = Math.Abs(rr - r), ds = Math.Abs(rs - s);
        if (dq > dr && dq > ds)
            rq = -rr - rs;
        else if (dr > ds)
            rr = -rq - rs;
        return new((int)rq, (int)rr);
    }

    /// <summary>Row by row: R, then Q.</summary>
    public int CompareTo(Hex other) => R != other.R ? R.CompareTo(other.R) : Q.CompareTo(other.Q);

    public override string ToString() => $"({Q},{R})";
}

/// <summary>The battlefield (D56): every hex within <see cref="Radius"/> of the centre. Its rim is where fighters escape.</summary>
public sealed record HexField(int Radius)
{
    public int Count => 3 * Radius * (Radius + 1) + 1;

    public bool Contains(Hex hex) => hex.Length <= Radius;

    public bool IsEdge(Hex hex) => hex.Length == Radius;

    /// <summary>All hexes, row by row (<see cref="Hex.CompareTo"/> order).</summary>
    public IEnumerable<Hex> Hexes()
    {
        for (int r = -Radius; r <= Radius; r++)
            for (int q = Math.Max(-Radius, -r - Radius); q <= Math.Min(Radius, -r + Radius); q++)
                yield return new(q, r);
    }
}
