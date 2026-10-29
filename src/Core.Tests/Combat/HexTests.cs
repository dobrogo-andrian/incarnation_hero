using Core.Combat;
using Core.Content;

namespace Core.Tests.Combat;

public class HexTests
{
    [Fact]
    public void Cube_coordinates_sum_to_zero()
    {
        var hex = new Hex(3, -5);
        Assert.Equal(2, hex.S);
        Assert.Equal(0, hex.Q + hex.R + hex.S);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(0, 0, 1, 0, 1)]
    [InlineData(0, 0, 2, -1, 2)]
    [InlineData(-2, 1, 3, -2, 5)]
    [InlineData(1, 1, -1, -1, 4)]
    public void Distance_counts_steps_both_ways(int q1, int r1, int q2, int r2, int expected)
    {
        var a = new Hex(q1, r1);
        var b = new Hex(q2, r2);
        Assert.Equal(expected, a.DistanceTo(b));
        Assert.Equal(expected, b.DistanceTo(a));
    }

    [Fact]
    public void Neighbors_are_six_distinct_hexes_one_step_away_in_fixed_order()
    {
        var center = new Hex(2, -1);
        var neighbors = center.Neighbors().ToList();
        Assert.Equal(6, neighbors.Distinct().Count());
        Assert.All(neighbors, n => Assert.Equal(1, center.DistanceTo(n)));
        Assert.Equal(new Hex(3, -1), neighbors[0]);
        Assert.Equal(new Hex(2, 0), neighbors[5]);
    }

    [Fact]
    public void Line_runs_end_to_end_through_adjacent_hexes()
    {
        var from = new Hex(-3, 1);
        var to = new Hex(2, -2);
        var line = from.LineTo(to);
        Assert.Equal(from.DistanceTo(to) + 1, line.Count);
        Assert.Equal(from, line[0]);
        Assert.Equal(to, line[^1]);
        for (int i = 1; i < line.Count; i++)
            Assert.Equal(1, line[i - 1].DistanceTo(line[i]));
    }

    [Fact]
    public void Line_along_an_axis_is_straight_and_ties_break_the_same_way()
    {
        Assert.Equal([new(0, 0), new(1, 0), new(2, 0)], new Hex(0, 0).LineTo(new Hex(2, 0)));
        // (0,0) → (1,1) passes exactly between (1,0) and (0,1); the nudge always picks the same one.
        Assert.Equal([new(0, 0), new(1, 0), new(1, 1)], new Hex(0, 0).LineTo(new Hex(1, 1)));
        Assert.Equal([new Hex(4, -2)], new Hex(4, -2).LineTo(new Hex(4, -2)));
    }

    [Fact]
    public void Round_snaps_a_fractional_point_to_the_nearest_hex()
    {
        Assert.Equal(new Hex(1, 0), Hex.Round(0.9, 0.1));
        Assert.Equal(new Hex(0, 1), Hex.Round(-0.2, 1.1));
        Assert.Equal(new Hex(-1, 0), Hex.Round(-0.8, 0.2));
    }

    [Fact]
    public void Field_is_a_hexagon_with_its_rim_at_the_radius()
    {
        var field = new HexField(4);
        var hexes = field.Hexes().ToList();
        Assert.Equal(61, field.Count);
        Assert.Equal(field.Count, hexes.Count);
        Assert.Equal(hexes.Count, hexes.Distinct().Count());
        Assert.All(hexes, h => Assert.True(field.Contains(h)));
        Assert.Equal(24, hexes.Count(field.IsEdge));
        Assert.Equal(hexes.Order().ToList(), hexes);
        Assert.False(field.Contains(new Hex(5, 0)));
        Assert.False(field.Contains(new Hex(3, 2)));
    }

    [Fact]
    public void Field_radius_comes_from_combat_config()
    {
        var config = TestContent.Repo.Get<CombatConfigDef>(CombatConfigDef.CombatId);
        Assert.Equal(4, config.FieldRadius);
    }
}
