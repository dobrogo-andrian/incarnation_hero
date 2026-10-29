using Core.Combat;
using Core.Content;
using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

/// <summary>Training activities (§5): hired work, hunting and arena bouts, on calm content (no event or rumor rolls).</summary>
public sealed class TrainingTests
{
    private static readonly ContentDb Content = TestContent.Quiet(TestContent.WithExtra("""
        - id: enemy.test.glass
          name: { uk: "Скляний" }
          stats: { stat.might: 0.5 }
          weapon_damage: 300
        - id: enemy.test.killer
          name: { uk: "Убивця" }
          stats: { stat.might: 100000, stat.speed: 100 }
          weapon_damage: 100000
        - id: grp.test.weak
          name: { uk: "Слабкі" }
          members: [ { enemy: enemy.test.glass } ]
          loot: { stones_min: 3, stones_max: 3 }
        - id: grp.test.sparring
          name: { uk: "Спаринг" }
          members: [ { enemy: enemy.test.glass } ]
        - id: grp.test.killer
          name: { uk: "Убивці" }
          members: [ { enemy: enemy.test.killer } ]
        - id: hunt.test.easy
          name: { uk: "Легке полювання" }
          zone: zone.hub
          map: { x: 1, y: 1 }
          encounter_chance: 1
          groups: { grp.test.weak: 1 }
          gains: { stat.speed: 0.5 }
        - id: arena.test
          name: { uk: "Тестова арена" }
          zone: zone.hub
          map: { x: 2, y: 2 }
          bouts:
            - { group: grp.test.sparring, fee: 4, prize: 10 }
            - { group: grp.test.killer, fee: 4, prize: 10 }
        """));

    private static Simulation NewGame() => Simulation.NewGame(Content, 1);

    private static double Health(Simulation sim) => sim.State.Player.Resources.First(r => r.ResourceId == "resource.health").Current;

    private static double Base(Simulation sim, string statId) => sim.State.Player.Stats.First(s => s.StatId == statId).Base;

    [Fact]
    public void Work_PaysItsWageEveryWeek_AndTrainsItsStats_WithoutJournalLines()
    {
        var sim = NewGame();
        int stones = sim.State.Player.Stones;
        double might = Base(sim, "stat.might");

        var outcome = sim.Execute(new StartActivity(new WorkActivity("job.hub.porter"), 4));

        Assert.True(outcome.Accepted);
        Assert.Equal(stones + 4, sim.State.Player.Stones);
        Assert.Equal(might + 4 * 0.17, Base(sim, "stat.might"), 6);
        Assert.All(outcome.Events.OfType<StonesChanged>(), s => Assert.Null(SimText.Describe(s)));
        Assert.Equal(4, sim.State.Stats.ActivityWeeks.Single(t => t.Key == "job.hub.porter").Weeks);
    }

    [Fact]
    public void Training_ElsewhereThanItsZone_IsRejected()
    {
        var sim = NewGame();
        sim.State.Player.ZoneId = "zone.bamboo_grove";

        Assert.Equal(RejectReasons.NotHere, sim.Execute(new StartActivity(new WorkActivity("job.hub.porter"), 1)).RejectReason);
        Assert.Equal(RejectReasons.NotHere, sim.Execute(new StartActivity(new ArenaActivity("arena.hub", 0), 1)).RejectReason);
        Assert.Equal(RejectReasons.NotHere, sim.Execute(new StartActivity(new HuntActivity("hunt.no_such"), 1)).RejectReason);
    }

    [Fact]
    public void Hunting_MeetsPrey_AndAWonFightGivesItsLoot()
    {
        var sim = NewGame();
        int stones = sim.State.Player.Stones;

        var outcome = sim.Execute(new StartActivity(new HuntActivity("hunt.test.easy"), 2));

        Assert.Equal(2, outcome.Events.OfType<CombatResolved>().Count(c => c.End == CombatEnd.Won));
        Assert.Equal(stones + 6, sim.State.Player.Stones);
        Assert.True(Base(sim, "stat.speed") > 10);
    }

    [Fact]
    public void HuntingAndTheArena_NeedFightingHealth()
    {
        var sim = NewGame();
        sim.State.Player.Resources.First(r => r.ResourceId == "resource.health").Current = 40;

        Assert.Equal(RejectReasons.TooWounded, sim.Execute(new StartActivity(new HuntActivity("hunt.test.easy"), 1)).RejectReason);
        Assert.Equal(RejectReasons.TooWounded, sim.Execute(new StartActivity(new ArenaActivity("arena.test", 0), 1)).RejectReason);
    }

    [Fact]
    public void ArenaBout_TakesTheFee_AndAWinPaysThePrize()
    {
        var sim = NewGame();
        int stones = sim.State.Player.Stones;

        var outcome = sim.Execute(new StartActivity(new ArenaActivity("arena.test", 0), 1));

        Assert.Equal(stones - 4 + 10, sim.State.Player.Stones);
        Assert.Contains(new StonesChanged(-4, Routine: true), outcome.Events);
        Assert.Contains(new StonesChanged(10), outcome.Events);
    }

    [Fact]
    public void ArenaBout_EndsInYielding_NeverInDeath_AndTheSpentBodyEndsTheBouts()
    {
        var sim = NewGame();

        var outcome = sim.Execute(new StartActivity(new ArenaActivity("arena.test", 1), 10));

        Assert.Equal(GameMode.Overworld, sim.State.Mode);
        Assert.Equal(CombatEnd.Yielded, outcome.Events.OfType<CombatResolved>().Single().End);
        Assert.True(Health(sim) > 0);
        Assert.Contains(outcome.Events, e => e is ActivityFinished);
        Assert.Equal(1, sim.State.Stats.CombatsFled);
    }

    [Fact]
    public void ArenaBout_NeedsTheFee()
    {
        var sim = NewGame();
        sim.State.Player.Stones = 3;

        Assert.Equal(RejectReasons.NotEnoughStones, sim.Execute(new StartActivity(new ArenaActivity("arena.test", 0), 1)).RejectReason);
        Assert.Equal(RejectReasons.InvalidActivity, sim.Execute(new StartActivity(new ArenaActivity("arena.test", 5), 1)).RejectReason);
    }

    [Fact]
    public void SteadyTraining_BreaksThroughARank()
    {
        var sim = Simulation.NewGame(TestContent.Calm, 1);

        sim.Execute(new StartActivity(new WorkActivity("job.hub.courier"), 40));

        Assert.Equal(1, sim.State.Player.Stats.First(s => s.StatId == "stat.speed").Rank);
    }

    /// <summary>
    /// A steady gain g levels off at g / decay of overflow and only approaches it, so a level equal
    /// to a rank's breakthrough shows a full bar that never breaks through. Keep every training gain clear of them.
    /// </summary>
    [Fact]
    public void TrainingGains_DoNotLevelOffAtABreakthrough()
    {
        var db = TestContent.Repo;
        var stuck = new List<string>();
        foreach (var def in db.All<TrainingDef>())
        {
            foreach (var (statId, gain) in def.Gains)
            {
                if (db.Get<StatDef>(statId).Progress is not { } progressId
                    || db.Get<StatProgressDef>(progressId) is not { OverflowDecayPerWeek: { } decay } progress)
                    continue;
                double level = gain / decay;
                var near = progress.Ranks.Select(db.Get<StatRankDef>).Where(r => r.Breakthrough is { } b && Math.Abs(level - b) < 0.1 * b);
                stuck.AddRange(near.Select(r => $"{def.Id} {statId}: {level} ~ {r.Breakthrough}"));
            }
        }

        Assert.Empty(stuck);
    }

    [Fact]
    public void SettlementHuntsAndArenas_NeedAPointOnTheLocalMap()
    {
        var db = TestContent.WithExtra("""
            - id: hunt.test.unplaced
              name: { uk: "Без точки" }
              zone: zone.hub
              encounter_chance: 0.5
              groups: { grp.beast.wild_boar: 1 }
            """);

        Assert.Contains(ContentValidator.Validate(db), e => e.Message.Contains("hunt.test.unplaced"));
    }
}
