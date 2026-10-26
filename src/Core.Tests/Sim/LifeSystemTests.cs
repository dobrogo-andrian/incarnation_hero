using Core.Rng;
using Core.Sim;

namespace Core.Tests.Sim;

public sealed class LifeSystemTests
{
    private const int Lifespan = 60 * 52;
    private const int Window = 3 * 52;

    [Theory]
    [InlineData(Lifespan - Window - 10)]
    [InlineData(Lifespan - Window)]
    public void NoNaturalDeathBeforeTheWindow(int age)
    {
        Assert.Equal(0, LifeSystem.NaturalDeathChance(age, Lifespan, Window));
    }

    [Fact]
    public void DeathChanceGrowsInsideTheWindow_AndIsCappedAtOne()
    {
        double early = LifeSystem.NaturalDeathChance(Lifespan - Window + 1, Lifespan, Window);
        double atLifespan = LifeSystem.NaturalDeathChance(Lifespan, Lifespan, Window);

        Assert.True(early > 0);
        Assert.True(atLifespan > early);
        Assert.Equal(1.0, LifeSystem.NaturalDeathChance(Lifespan + 100 * Window, Lifespan, Window));
    }

    [Fact]
    public void MeanAgeOfDeath_IsTheExpectedLifespan()
    {
        const int samples = 4000;
        var rng = Xoshiro256StarStar.FromSeed(12345);
        long total = 0;
        for (int i = 0; i < samples; i++)
        {
            int age = Lifespan - Window;
            do
                age++;
            while (!rng.Chance(LifeSystem.NaturalDeathChance(age, Lifespan, Window)));
            total += age;
        }

        // Spread is about half the window (~80 weeks), so the mean of 4000 deaths is within ~1.3 weeks.
        Assert.InRange((double)total / samples, Lifespan - 5, Lifespan + 5);
    }
}
