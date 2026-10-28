using Core.Rng;
using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

/// <summary>
/// Repo injuries: body −0.5% health and −0.4% body energy per point, heals 0.5 a week and 2.5 more
/// at rest; internal −0.3 quality and −0.3 foundation per point, heals 0.2 and 0.8 more at rest.
/// A fresh character has 100 health and 100 body energy.
/// </summary>
public sealed class InjuryTests
{
    private const string Body = "injury.body";
    private const string Internal = "injury.internal";
    private const string Courtyard = "place.hub.quiet_courtyard";

    private static Simulation NewGame()
    {
        var sim = Simulation.NewGame(TestContent.Repo, 1);
        sim.Execute(new SetInterruptStops([]));
        return sim;
    }

    private static List<SimEvent> Inflict(Simulation sim, string injuryId, double amount)
    {
        var events = new List<SimEvent>();
        InjurySystem.Inflict(sim.State.Player, Context(sim), injuryId, amount, events);
        return events;
    }

    private static SimContext Context(Simulation sim) => new(sim.Content, RngStreams.FromStates(sim.State.RngStates));

    private static double Injury(Simulation sim, string injuryId) => InjurySystem.Value(sim.State.Player, injuryId);

    private static ResourceView Resource(Simulation sim, string id) => sim.View.Resources.Single(r => r.ResourceId == id);

    [Fact]
    public void Inflict_LowersCeilingsThroughModifiers_AndStopsAtTheMax()
    {
        var sim = NewGame();

        var events = Inflict(sim, Body, 40);

        Assert.Equal([new InjuryReceived(Body, 40)], events);
        var health = Resource(sim, "resource.health");
        Assert.Equal((80.0, 80.0), (health.Current, health.Max.Value));
        Assert.Equal(new Core.Character.ModifierPart(Body, ModifierOp.Mul, -0.2), Assert.Single(health.Max.Modifiers));
        Assert.Equal(84, Resource(sim, "resource.body_energy").Max.Value);
        Assert.Equal(new InjuryView(Body, 40, 100), Assert.Single(sim.View.Injuries));

        Assert.Equal([new InjuryReceived(Body, 60)], Inflict(sim, Body, 500));
        Assert.Empty(Inflict(sim, Body, 1));
        Assert.Equal(50, Resource(sim, "resource.health").Max.Value);
    }

    [Fact]
    public void Injuries_HealEveryWeek_AndFasterAtRest()
    {
        var resting = NewGame();
        var meditating = NewGame();
        Inflict(resting, Body, 10);
        Inflict(meditating, Body, 10);

        resting.Execute(TestContent.Wait(2));
        meditating.Execute(new StartActivity(new MeditateActivity("meditation.purification", Courtyard), 2));

        Assert.Equal(4, Injury(resting, Body));
        Assert.Equal(9, Injury(meditating, Body));
        // Health regenerates 10 a week in the world phase, up to the ceiling as it stood before that week's rest.
        var health = Resource(resting, "resource.health");
        Assert.Equal((96.75, 98.0), (health.Current, health.Max.Value));
    }

    [Fact]
    public void HealedScale_LosesItsModifiers_AndInterruptsRest()
    {
        var sim = Simulation.NewGame(TestContent.Repo, 1);
        Inflict(sim, Body, 1);

        var outcome = sim.Execute(TestContent.Wait(null));

        Assert.Equal(1, sim.State.Week);
        Assert.Contains(new InjuryHealed(Body), outcome.Events);
        Assert.Empty(sim.State.Player.Injuries);
        Assert.Empty(sim.State.Player.Modifiers);
        Assert.Equal(100, Resource(sim, "resource.health").Max.Value);
    }

    [Fact]
    public void HealingMeditation_HealsInternalInjuries_AndNeedsSomethingToHeal()
    {
        var sim = NewGame();
        var healing = new StartActivity(new MeditateActivity("meditation.healing", Courtyard), 2);

        Assert.Equal(RejectReasons.NothingToHeal, sim.Execute(healing).RejectReason);

        Inflict(sim, Internal, 10);
        Assert.True(sim.Execute(healing).Accepted);

        // 0.2 natural + rate 2 × density 1, for two weeks.
        Assert.Equal(5.6, Injury(sim, Internal));
    }

    [Fact]
    public void HealingMeditation_EndsItself_OnceNothingIsLeftToHeal()
    {
        var sim = NewGame();
        Inflict(sim, Internal, 3);

        var outcome = sim.Execute(new StartActivity(new MeditateActivity("meditation.healing", Courtyard), null));

        // 2.2 a week heals 3 points in the second week.
        Assert.Equal(2, sim.State.Week);
        Assert.Null(sim.State.Activity);
        Assert.Contains(outcome.Events, e => e is ActivityFinished);
    }

    [Fact]
    public void HealingPill_HealsAtOnceWithToxins_AndIsRefusedWithNothingToHeal()
    {
        var sim = NewGame();
        sim.State.Player.Items.Add(new ItemCount { ItemId = "item.pill.bone_mending", Count = 2 });

        Assert.Equal(RejectReasons.NothingToHeal, sim.Execute(new UsePill("item.pill.bone_mending")).RejectReason);

        Inflict(sim, Body, 30);
        var outcome = sim.Execute(new UsePill("item.pill.bone_mending"));

        Assert.True(outcome.Accepted);
        Assert.Equal(5, Injury(sim, Body));
        Assert.Equal(6, sim.State.Player.Resources.Single(r => r.ResourceId == "resource.toxins").Current);
        Assert.Empty(sim.State.Player.Buffs);
        Assert.Equal([new InjuryHealed(Body)], sim.Execute(new UsePill("item.pill.bone_mending")).Events.OfType<InjuryHealed>());
    }
}
