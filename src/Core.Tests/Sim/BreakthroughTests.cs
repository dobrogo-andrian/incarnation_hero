using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

public sealed class BreakthroughTests
{
    private const string Place = "place.hub.quiet_courtyard";
    private const int WeeksPerYear = 52;

    private static readonly AttemptBreakthrough Attempt = new("qi", Place);

    /// <summary>A mortal at the hub with a full scale and the given purity; the first breakthrough needs foundation 40.</summary>
    private static Simulation Ready(double purity, ulong seed = 1)
    {
        var sim = Simulation.NewGame(TestContent.Repo, seed);
        sim.Execute(new SetInterruptStops([]));
        Qi(sim).StageProgress = 100;
        Stat(sim, "stat.purity").Base = purity;
        return sim;
    }

    private static CultivationTrack Qi(Simulation sim) => sim.State.Player.Tracks.Single();

    private static StatValue Stat(Simulation sim, string id) => sim.State.Player.Stats.First(s => s.StatId == id);

    private static double Toxins(Simulation sim) => sim.State.Player.Resources.First(r => r.ResourceId == "resource.toxins").Current;

    private static BreakthroughEstimate Forecast(Simulation sim) => sim.View.Tracks.Single().Forecast!;

    private static void Learn(Simulation sim, string knowledgeId, int level) =>
        sim.State.Player.Knowledge.Add(new KnowledgeLevel { KnowledgeId = knowledgeId, Level = level });

    [Fact]
    public void Attempt_IsRejectedWithoutAFullScale_ElsewhereOrAtTheLastStage()
    {
        var sim = Ready(purity: 50);

        Assert.Equal(RejectReasons.PlaceNotHere, sim.Execute(Attempt with { PlaceId = "place.spirit_peak.vein" }).RejectReason);
        Assert.Equal(RejectReasons.InvalidActivity, sim.Execute(Attempt with { VectorId = "body" }).RejectReason);
        Qi(sim).StageProgress = 99;
        Assert.Equal(RejectReasons.StageScaleNotFull, sim.Execute(Attempt).RejectReason);
        Qi(sim).StageIndex = 4;
        Qi(sim).StageProgress = 1600;
        Assert.Equal(RejectReasons.NoNextStage, sim.Execute(Attempt).RejectReason);
        Assert.Equal(0, sim.State.Week);
    }

    [Fact]
    public void Forecast_AppearsOnlyWithAFullScale()
    {
        var sim = Ready(purity: 50);
        Qi(sim).StageProgress = 99;

        Assert.Null(sim.View.Tracks.Single().Forecast);
    }

    [Fact]
    public void RedZone_NeedsTheRiskAccepted()
    {
        var sim = Ready(purity: 10);

        Assert.True(Forecast(sim).Fatal);
        Assert.Equal(RejectReasons.DeathRiskNotAccepted, sim.Execute(Attempt).RejectReason);
        Assert.True(sim.Execute(Attempt with { AcceptDeathRisk = true }).Accepted);
    }

    [Fact]
    public void Success_TakesTheSeclusionWeeks_AndGivesWhatTheForecastSaid()
    {
        var sim = Ready(purity: 50);
        var forecast = Forecast(sim);
        var player = sim.State.Player;
        int lifespan = player.LifespanWeeks;

        var outcome = sim.Execute(Attempt);

        // Foundation 50 ≥ 40: no risk. Quality 30 + min(50, 20) × 0.5 − 4/52 of a year of age ≈ 39.9: common rank.
        Assert.Equal(0, forecast.FailureChance);
        Assert.Null(forecast.FailureZoneId);
        Assert.Equal(39.9231, forecast.Quality.Value);
        Assert.Equal("rank.common", forecast.RankId);
        Assert.Equal(4, sim.State.Week);
        Assert.Contains(new BreakthroughSucceeded("qi", "stage.qi.1", "rank.common", 4), outcome.Events);
        Assert.IsType<ActivityFinished>(outcome.Events[^1]);
        Assert.Equal((1, 0.0, 1), (Qi(sim).StageIndex, Qi(sim).StageProgress, Qi(sim).LastBreakthroughQuality));
        Assert.Equal(lifespan + 4 * WeeksPerYear, player.LifespanWeeks);
        Assert.Equal(12, Stat(sim, "stat.might").Base);
        Assert.Equal(12, Stat(sim, "stat.meridians").Base);
        Assert.Equal(0, Stat(sim, "stat.perception").Base);
        Assert.Equal(50, Stat(sim, "stat.purity").Base);
    }

    [Fact]
    public void Seclusion_IsNotStoppedByInterrupts_AndResolves()
    {
        var sim = Ready(purity: 50);
        sim.Execute(new SetInterruptStops([InterruptCategory.LifeEndApproaching]));
        sim.State.Player.LifespanWeeks = sim.State.Player.AgeWeeks + 5 * WeeksPerYear + 2;

        var outcome = sim.Execute(Attempt);

        // The five-year warning comes after 2 of 4 weeks, but stopping would throw the seclusion away (D44).
        Assert.Contains(new LifeEndApproaching(5), outcome.Events);
        Assert.DoesNotContain(outcome.Events, e => e is ActivityInterrupted);
        Assert.Equal(4, sim.State.Week);
        Assert.NotNull(Qi(sim).LastBreakthroughQuality);
    }

    [Fact]
    public void Quality_FallsWithToxinsAgeAndDelay_AndStopsAtTheCeiling()
    {
        var sim = Ready(purity: 200);
        var player = sim.State.Player;
        player.AgeWeeks += 10 * WeeksPerYear - 4;
        Qi(sim).ScaleFullSinceAgeWeeks = player.AgeWeeks + 4 - 3 * WeeksPerYear;
        player.Resources.First(r => r.ResourceId == "resource.toxins").Current = 10;

        var quality = Forecast(sim).Quality;

        // Purity counts up to the no-method cap of 20; the attempt resolves at 22, three years after the scale filled.
        Assert.Equal(new QualityBreakdown(30, 10, 5, 0, 10, 6, 45), quality);
        Assert.Equal(19, quality.Value);
        Assert.Equal("rank.flawed", Forecast(sim).RankId);

        player.AgeWeeks = 12 * WeeksPerYear;
        Qi(sim).ScaleFullSinceAgeWeeks = null;
        player.Resources.First(r => r.ResourceId == "resource.toxins").Current = 0;
        player.BreakthroughCeilingBonus = -20;
        Assert.Equal(25, Forecast(sim).Quality.Value);
    }

    [Fact]
    public void ActiveMethod_RaisesCeilingAndPurityCapWithItsLevel()
    {
        var sim = Ready(purity: 200);
        Learn(sim, "method.azure_breath", 2);

        Assert.True(sim.Execute(new SetActiveMethod("method.azure_breath")).Accepted);
        var quality = Forecast(sim).Quality;

        // Ceiling 50 + 2 × 8, purity cap 30 + 2 × 20.
        Assert.Equal(66, quality.Ceiling);
        Assert.Equal(35, quality.Purity);
        Assert.Equal("method.azure_breath", sim.View.ActiveMethodId);
    }

    [Fact]
    public void SetActiveMethod_NeedsAStudiedMethod_AndNullClearsIt()
    {
        var sim = Ready(purity: 0);

        Assert.Equal(RejectReasons.NotAMethod, sim.Execute(new SetActiveMethod("element.fire")).RejectReason);
        Assert.Equal(RejectReasons.NotAMethod, sim.Execute(new SetActiveMethod("method.unknown")).RejectReason);
        Assert.Equal(RejectReasons.MethodNotStudied, sim.Execute(new SetActiveMethod("method.azure_breath")).RejectReason);

        Learn(sim, "method.azure_breath", 1);
        Assert.True(sim.Execute(new SetActiveMethod("method.azure_breath")).Accepted);
        Assert.True(sim.Execute(new SetActiveMethod(null)).Accepted);
        Assert.Null(sim.State.Player.ActiveMethodId);
        Assert.Equal(45, Forecast(sim).Quality.Ceiling);
    }

    [Fact]
    public void BreakthroughPill_RaisesTheNextCeilingOnly_AndTheStrongestCounts()
    {
        var sim = Ready(purity: 50);
        sim.State.Player.Items.Add(new ItemCount { ItemId = "item.pill.foundation_building", Count = 2 });

        Assert.True(sim.Execute(new UsePill("item.pill.foundation_building")).Accepted);
        Assert.True(sim.Execute(new UsePill("item.pill.foundation_building")).Accepted);

        Assert.Equal(60, Forecast(sim).Quality.Ceiling);
        Assert.Equal(20, Toxins(sim));
        Assert.Empty(sim.State.Player.Buffs);

        sim.Execute(Attempt);
        Assert.Equal(0, sim.State.Player.BreakthroughCeilingBonus);
    }

    [Fact]
    public void Failure_InTheSeriousZone_CostsScaleToxinsPurityAndAnInternalInjury()
    {
        // Purity 26: deficit 0.35 of the threshold, mid serious zone: failure chance 0.6.
        int failures = 0, successes = 0;
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var sim = Ready(purity: 26, seed);
            Assert.Equal(("failure_zone.serious", 0.6), (Forecast(sim).FailureZoneId, Forecast(sim).FailureChance));

            var outcome = sim.Execute(Attempt);

            if (outcome.Events.Contains(new BreakthroughFailed("qi", "failure_zone.serious")))
            {
                failures++;
                Assert.Equal((0, 50.0), (Qi(sim).StageIndex, Qi(sim).StageProgress));
                Assert.Equal(20, Toxins(sim));
                Assert.Equal(22.1, Stat(sim, "stat.purity").Base);
                Assert.Equal(30, InjurySystem.Value(sim.State.Player, "injury.internal"));
                Assert.Contains(new InjuryReceived("injury.internal", 30), outcome.Events);
                Assert.Null(Qi(sim).LastBreakthroughQuality);
            }
            else
            {
                successes++;
                Assert.Equal(1, Qi(sim).StageIndex);
            }
        }
        Assert.True(failures > 0 && successes > 0);
    }

    [Fact]
    public void FatalZone_KillsOnlyAnAttemptThatAcceptedTheRisk()
    {
        int deaths = 0, survivedFailures = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var accepted = Ready(purity: 0, seed);
            var outcome = accepted.Execute(Attempt with { AcceptDeathRisk = true });
            if (accepted.State.Mode == GameMode.RunEnd)
            {
                deaths++;
                Assert.Equal(new RunEnded(RunEndCause.BreakthroughDeath), outcome.Events[^1]);
            }

            // Had the foundation fallen into the red zone during seclusion, an unaccepted attempt fails as a serious one.
            var unaccepted = Ready(purity: 0, seed);
            var events = new List<SimEvent>();
            BreakthroughSystem.Resolve(unaccepted.State, Context(unaccepted), new BreakthroughActivity("qi", Place, false), events);
            Assert.NotEqual(GameMode.RunEnd, unaccepted.State.Mode);
            if (events.Contains(new BreakthroughFailed("qi", "failure_zone.serious")))
                survivedFailures++;
        }
        Assert.True(deaths > 0);
        Assert.True(survivedFailures > 0);
    }

    private static SimContext Context(Simulation sim) =>
        new(sim.Content, Core.Rng.RngStreams.FromStates(sim.State.RngStates));
}
