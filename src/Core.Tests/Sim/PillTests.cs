using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

/// <summary>Repo pills: qi gathering gives 30 energy and 8 toxins, ×0.8 energy per repeat; tolerance starts at 20.</summary>
public sealed class PillTests
{
    private const string QiGathering = "item.pill.qi_gathering";
    private const string SpiritDew = "item.pill.spirit_dew";

    private static Simulation NewGame()
    {
        var sim = Simulation.NewGame(TestContent.Repo, 1);
        sim.Execute(new SetInterruptStops([]));
        return sim;
    }

    private static double Toxins(Simulation sim) =>
        sim.State.Player.Resources.Single(r => r.ResourceId == "resource.toxins").Current;

    private static double? PillEnergy(Simulation sim) =>
        sim.State.Player.Buffs.Find(b => b.Kind == BuffKind.PillEnergy)?.Points;

    [Fact]
    public void NewRun_StartsWithTheConfiguredPurseAndNoToxins()
    {
        var player = NewGame().State.Player;

        Assert.Equal(30, player.Stones);
        Assert.Equal(2, ItemSystem.Count(player.Items, QiGathering));
        Assert.Equal(0, Toxins(NewGame()));
    }

    [Fact]
    public void RepeatedPillsOfOneType_GiveLessEnergy_ButTheSameToxins()
    {
        var sim = NewGame();

        var first = sim.Execute(new UsePill(QiGathering));
        var second = sim.Execute(new UsePill(QiGathering));

        Assert.Equal([new PillTaken(QiGathering, 30)], first.Events);
        Assert.Equal([new PillTaken(QiGathering, 24)], second.Events);
        Assert.Equal(54, PillEnergy(sim));
        Assert.Equal(16, Toxins(sim));
        Assert.Equal(0, ItemSystem.Count(sim.State.Player.Items, QiGathering));
        Assert.Equal(0, sim.State.Week);
    }

    [Fact]
    public void RepeatCount_IsPerPillType()
    {
        var sim = NewGame();
        sim.Execute(new UsePill(QiGathering));
        ItemSystem.Change(sim.State.Player.Items, SpiritDew, 1);

        Assert.Equal(24, sim.View.Items.Single(i => i.ItemId == QiGathering).NextPillEnergy);
        Assert.Equal(60, sim.View.Items.Single(i => i.ItemId == SpiritDew).NextPillEnergy);
    }

    [Fact]
    public void Pill_OverTheToxinTolerance_IsRejected_AndPurityRaisesTheTolerance()
    {
        var sim = NewGame();
        ItemSystem.Change(sim.State.Player.Items, QiGathering, 1);
        sim.Execute(new UsePill(QiGathering));
        sim.Execute(new UsePill(QiGathering));

        Assert.Equal(RejectReasons.ToxinsTooHigh, sim.Execute(new UsePill(QiGathering)).RejectReason);   // 16 + 8 > 20

        sim.State.Player.Stats.Single(s => s.StatId == "stat.purity").Base = 4;
        Assert.True(sim.Execute(new UsePill(QiGathering)).Accepted);   // 16 + 8 ≤ 24
    }

    [Fact]
    public void Pill_NotCarried_IsRejected()
    {
        var sim = NewGame();

        Assert.Equal(RejectReasons.NoSuchItem, sim.Execute(new UsePill(SpiritDew)).RejectReason);
        Assert.Equal(RejectReasons.NoSuchItem, sim.Execute(new UsePill("item.nothing")).RejectReason);
    }

    [Fact]
    public void Purification_FlushesToxinsFirst_ThenRaisesPurity()
    {
        var sim = NewGame();
        sim.Execute(new UsePill(QiGathering));   // 8 toxins

        // Quiet courtyard: density 1, so 0.1 purity a week, or 1 toxin at the purge ratio of 10.
        var meditate = new StartActivity(new MeditateActivity("meditation.purification", "place.hub.quiet_courtyard"), 9);
        sim.Execute(meditate);

        Assert.Equal(0, Toxins(sim));
        Assert.Equal(0.1, sim.State.Player.Stats.Single(s => s.StatId == "stat.purity").Base, 9);
    }
}
