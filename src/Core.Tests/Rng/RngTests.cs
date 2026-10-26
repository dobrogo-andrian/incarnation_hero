using Core.Rng;

namespace Core.Tests.Rng;

public class RngTests
{
    [Fact]
    public void SplitMix64_MatchesReferenceOutput()
    {
        var sm = new SplitMix64(0);
        Assert.Equal(0xE220A8397B1DCDAFUL, sm.Next());
        Assert.Equal(0x6E789E6AA1B965F4UL, sm.Next());
        Assert.Equal(0x06C45D188009454FUL, sm.Next());
    }

    [Fact]
    public void Xoshiro_MatchesReferenceOutput()
    {
        var rng = Xoshiro256StarStar.FromState(new RngState(1, 2, 3, 4));
        Assert.Equal(11520UL, rng.NextULong());
        Assert.Equal(0UL, rng.NextULong());
        Assert.Equal(1509978240UL, rng.NextULong());
        Assert.Equal(1215971899390074240UL, rng.NextULong());
    }

    [Fact]
    public void Xoshiro_RejectsZeroState()
    {
        Assert.Throws<ArgumentException>(() => Xoshiro256StarStar.FromState(new RngState(0, 0, 0, 0)));
    }

    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = Xoshiro256StarStar.FromSeed(42);
        var b = Xoshiro256StarStar.FromSeed(42);
        for (int i = 0; i < 1000; i++)
            Assert.Equal(a.NextULong(), b.NextULong());
    }

    [Fact]
    public void StateRoundTrip_ContinuesSequence()
    {
        var a = Xoshiro256StarStar.FromSeed(7);
        for (int i = 0; i < 10; i++) a.NextULong();

        var b = Xoshiro256StarStar.FromState(a.GetState());
        for (int i = 0; i < 100; i++)
            Assert.Equal(a.NextULong(), b.NextULong());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 6)]
    [InlineData(-5, 5)]
    [InlineData(int.MinValue, int.MaxValue)]
    public void NextInt_StaysInRange(int min, int max)
    {
        var rng = Xoshiro256StarStar.FromSeed(1);
        for (int i = 0; i < 10_000; i++)
        {
            int v = rng.NextInt(min, max);
            Assert.InRange(v, min, max - 1);
        }
    }

    [Fact]
    public void NextInt_IsRoughlyUniform()
    {
        var rng = Xoshiro256StarStar.FromSeed(123);
        var counts = new int[6];
        const int n = 60_000;
        for (int i = 0; i < n; i++) counts[rng.NextInt(0, 6)]++;
        foreach (int c in counts)
            Assert.InRange(c, 9_500, 10_500);
    }

    [Fact]
    public void NextInt_RejectsEmptyRange()
    {
        var rng = Xoshiro256StarStar.FromSeed(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(3, 3));
    }

    [Fact]
    public void NextDouble_IsInUnitInterval()
    {
        var rng = Xoshiro256StarStar.FromSeed(5);
        for (int i = 0; i < 10_000; i++)
            Assert.InRange(rng.NextDouble(), 0.0, 0.9999999999999999);
    }

    [Fact]
    public void Chance_RespectsBounds()
    {
        var rng = Xoshiro256StarStar.FromSeed(9);
        for (int i = 0; i < 1000; i++)
        {
            Assert.False(rng.Chance(0));
            Assert.True(rng.Chance(1));
        }
    }

    [Fact]
    public void Streams_AreIndependent()
    {
        var a = RngStreams.FromSeed(99);
        var b = RngStreams.FromSeed(99);

        // Drawing from one stream must not shift any other stream.
        for (int i = 0; i < 50; i++) a[RngStream.Combat].NextULong();

        Assert.Equal(a[RngStream.Events].NextULong(), b[RngStream.Events].NextULong());
        Assert.Equal(a[RngStream.Loot].NextULong(), b[RngStream.Loot].NextULong());
        Assert.NotEqual(a[RngStream.Events].NextULong(), a[RngStream.Loot].NextULong());
    }

    [Fact]
    public void Streams_StateRoundTrip()
    {
        var a = RngStreams.FromSeed(2024);
        a[RngStream.Npc].NextULong();
        a[RngStream.Ai].NextULong();

        var b = RngStreams.FromStates(a.GetStates());
        foreach (var s in Enum.GetValues<RngStream>())
            Assert.Equal(a[s].NextULong(), b[s].NextULong());
    }

    [Fact]
    public void Streams_FromStates_RejectsWrongCount()
    {
        Assert.Throws<ArgumentException>(() => RngStreams.FromStates(new RngState[RngStreams.Count - 1]));
    }

    [Fact]
    public void NextSubseed_IsDeterministic()
    {
        var a = RngStreams.FromSeed(5);
        var b = RngStreams.FromSeed(5);
        Assert.Equal(a.NextSubseed(RngStream.Combat), b.NextSubseed(RngStream.Combat));
        Assert.NotEqual(a.NextSubseed(RngStream.Combat), a.NextSubseed(RngStream.Combat));
    }
}
