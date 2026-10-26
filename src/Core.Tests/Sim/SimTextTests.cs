using Core.Loc;
using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

public sealed class SimTextTests
{
    private static readonly Localizer Loc = Localizer.FromContent(TestContent.Repo);

    private static IEnumerable<string> ViewText(Simulation sim) =>
        SimText.ViewLines(sim.View, TestContent.Repo).Select(Loc.Resolve);

    [Fact]
    public void ViewLines_ResolveToUkrainianText()
    {
        var sim = Simulation.NewGame(TestContent.Repo, 1);
        sim.Execute(TestContent.Wait(55));

        Assert.Equal(
            [
                "Тиждень забігу: 55",
                "Вік: 13 р. 3 тиж.",
                "Очікувана тривалість життя: 60 р. 0 тиж.",
                "Режим: Світ",
                "Заняття: немає",
                "Місцевість: Поселення Біля Підніжжя, небезпека 0",
                "Місце медитації: Тихий двір, щільність 1, резерв ці 8 / 8",
                "Міць: 10 (Живучість 10, Стійкість 10)",
                "Витривалість: 10 (Запас 10, Віддача 10)",
                "Швидкість: 10 (Реакція 10, Верткість 10)",
                "Сила меридіан: 10 (Ємність 10, Потік 10)",
                "Сприйняття: 0",
                "Чистота: 0",
                "Здоров'я: 100 / 100",
                "Енергія тіла: 100 / 100",
                "Пул ці: 100 / 100",
                "Бойова ці: 50 / 50",
                "Ці: Перший етап ці, прогрес 0",
            ],
            ViewText(sim));
    }

    [Fact]
    public void ViewLines_RoundComputedValuesToOneDecimal()
    {
        var sim = Simulation.NewGame(TestContent.Repo, 1);
        ModifierSystem.Apply(sim.State.Player, new Modifier { TargetId = "stat.speed", Op = ModifierOp.Mul, Value = 0.123, SourceId = "s" });

        Assert.Contains("Швидкість: 11.2 (Реакція 11.2, Верткість 11.2)", ViewText(sim));
    }

    [Fact]
    public void ViewLines_ShowActivityOrRunEnd()
    {
        var sim = Simulation.NewGame(TestContent.Repo, 1);

        sim.State.Activity = new ActiveActivity { Activity = new WaitActivity(), WeeksLeft = 4 };
        Assert.Contains("Заняття: Очікування, лишилось тижнів: 4", ViewText(sim));

        sim.State.Activity.WeeksLeft = null;
        Assert.Contains("Заняття: Очікування, без обмеження часу", ViewText(sim));

        sim.State.Mode = GameMode.RunEnd;
        sim.State.EndCause = RunEndCause.OldAge;
        Assert.Contains("Забіг завершено: смерть від старості", ViewText(sim));
    }

    [Fact]
    public void Describe_ReportsNotableEventsOnly()
    {
        Assert.Null(SimText.Describe(new WeekPassed(1, 625)));
        Assert.Equal("Заняття завершено: Очікування.", Loc.Resolve(SimText.Describe(new ActivityFinished(new WaitActivity()))!));
        Assert.Equal("Заняття скасовано: Очікування.", Loc.Resolve(SimText.Describe(new ActivityCancelled(new WaitActivity()))!));
        Assert.Equal("До очікуваного кінця життя лишилось років: 5.", Loc.Resolve(SimText.Describe(new LifeEndApproaching(5))!));
        Assert.Equal("Забіг завершено: смерть від старості.", Loc.Resolve(SimText.Describe(new RunEnded(RunEndCause.OldAge))!));
        Assert.Equal("Прибуття: Туманні пагорби.", Loc.Resolve(SimText.Describe(new ZoneEntered("zone.misty_hills"))!));
        Assert.Equal("Ці місця майже вичерпано: Тихий двір.", Loc.Resolve(SimText.Describe(new PlaceQiLow("place.hub.quiet_courtyard"))!));
        var meditation = new MeditateActivity("meditation.purification", "place.hub.quiet_courtyard");
        Assert.Equal("Заняття завершено: Медитація (Очищення), Тихий двір.", Loc.Resolve(SimText.Describe(new ActivityFinished(meditation))!));
        var travel = new TravelActivity([new TravelLeg("zone.bamboo_grove", 1)]);
        Assert.Equal("Заняття завершено: Подорож, мета — Бамбуковий гай.", Loc.Resolve(SimText.Describe(new ActivityFinished(travel))!));
    }
}
