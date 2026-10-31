using Core.Loc;
using Core.Sim;
using Core.State;

namespace Core.Tests.Sim;

public sealed class SimTextTests
{
    private static readonly Localizer Loc = Localizer.FromContent(TestContent.Calm);

    private static IEnumerable<string> ViewText(Simulation sim) =>
        SimText.ViewLines(sim.View, TestContent.Calm).Select(Loc.Resolve);

    [Fact]
    public void MeditationHere_ShowsDanger_AndTheDensityAndCoverOfThePlaceMeditationGoesTo()
    {
        var sim = Simulation.NewGame(TestContent.Calm, 1);
        string Here() => Loc.Resolve(SimText.MeditationHereText(sim.View, TestContent.Calm));

        Assert.Equal("Небезпека зони: 0  ·  Ці: щільність 1 — Тихий двір, безпечно", Here());
        sim.State.Player.ZoneId = "zone.misty_hills";
        Assert.Equal("Небезпека зони: 2  ·  Ці: щільність 2 — Печера туману, укриття", Here());
        sim.State.Player.ZoneId = "zone.river_valley";
        Assert.Equal("Небезпека зони: 1  ·  Ці: щільність 1.5 — Валун над водою, під відкритим небом", Here());
    }

    [Fact]
    public void ViewLines_ResolveToUkrainianText()
    {
        var sim = Simulation.NewGame(TestContent.Calm, 1);
        sim.Execute(TestContent.Wait(10));

        Assert.Equal(
            [
                "Тиждень забігу: 10",
                "Вік: 12 р. 10 тиж.",
                "Очікувана тривалість життя: 60 р. 0 тиж.",
                "Режим: Світ",
                "Місцевість: Поселення Біля Підніжжя, небезпека 0",
                "Місце медитації: Тихий двір, щільність 1, резерв ці 8 / 8",
                "Міць: 10 (Живучість 10, Стійкість 10)",
                "Ступінь I: шкала 10 / 12, надлишок 0 / 2",
                "Витривалість: 10 (Запас 10, Віддача 10)",
                "Ступінь I: шкала 10 / 12, надлишок 0 / 2",
                "Швидкість: 10 (Реакція 10, Верткість 10)",
                "Ступінь I: шкала 10 / 12, надлишок 0 / 2",
                "Сила меридіан: 10 (Ємність 10, Потік 10)",
                "Ступінь I: шкала 10 / 12, надлишок 0 / 2",
                "Сприйняття: 0",
                "Ступінь I: шкала 0 / 2",
                "Чистота: 0",
                "Каламутне тіло: шкала 0 / 20, надлишок 0 / 2",
                "Здоров'я: 100 / 100",
                "Енергія тіла: 15 / 15",
                "Пул ці: 100 / 100",
                "Бойова ці: 50 / 50",
                "Токсини: 0 / 20",
                "Ці: Смертний, шкала етапу 0 / 100",
                "Метод культивації: немає",
                "Фундамент: 0 (знання 0, чистота 0)",
                "Духовні камені: 30",
                "Пігулка збирання ці, кількість: 2, енергія наступної: 30",
                "Аптека хабу: Пігулка зрощення кісток, ціна в каменях: 15",
                "Аптека хабу: Пігулка закладання основи, ціна в каменях: 80",
                "Аптека хабу: Велика пігулка відновлення ці, ціна в каменях: 22",
                "Аптека хабу: Пігулка заспокоєння меридіан, ціна в каменях: 25",
                "Аптека хабу: Пігулка збирання ці, ціна в каменях: 12",
                "Аптека хабу: Пігулка відновлення ці, ціна в каменях: 10",
                "Аптека хабу: Пігулка духовної роси, ціна в каменях: 30",
                "Книгарня хабу: Фоліант методу лазурового подиху, ціна в каменях: 40",
                "Книгарня хабу: Фоліант «Відступ журавля», ціна в каменях: 20",
                "Книгарня хабу: Фоліант стихії вогню, ціна в каменях: 20",
                "Книгарня хабу: Фоліант «Пронизливий дракон», ціна в каменях: 20",
                "Книгарня хабу: Фоліант «Крок вітру», ціна в каменях: 20",
                "Репутація (Секта Лазурової Хмари): 0",
                "Репутація (Павільйон Ста Трав): 0",
                "Репутація (Школа Залізної Долоні): 0",
            ],
            ViewText(sim));
    }

    [Fact]
    public void ViewLines_RoundComputedValuesToOneDecimal()
    {
        var sim = Simulation.NewGame(TestContent.Calm, 1);
        ModifierSystem.Apply(sim.State.Player, new Modifier { TargetId = "stat.speed", Op = ModifierOp.Mul, Value = 0.123, SourceId = "s" });

        Assert.Contains("Швидкість: 11.2 (Реакція 11.2, Верткість 11.2)", ViewText(sim));
    }

    [Fact]
    public void StatTitle_ShowsARankedStatsRank_AndKeepsItsNumbersForTheScale()
    {
        var sim = Simulation.NewGame(TestContent.Calm, 1);
        TestContent.SetStat(sim, "stat.purity", 23);
        sim.State.Player.Stats.Single(s => s.StatId == "stat.purity").Deficit = 1.5;
        var purity = sim.View.Stats.Single(s => s.Stat.Id == "stat.purity");
        var perception = sim.View.Stats.Single(s => s.Stat.Id == "stat.perception");

        Assert.Equal("Чистота: Прояснена кров", Loc.Resolve(SimText.StatTitle(purity).Text));
        Assert.Equal("Сприйняття: Ступінь I", Loc.Resolve(SimText.StatTitle(perception).Text));
        Assert.Equal(["Прояснена кров: шкала 3 / 30, надлишок 0 / 4", "Дефіцит: 1.5 / 6"], SimText.StandingLines(purity).Select(Loc.Resolve));
        Assert.Equal(["Ступінь I: шкала 0 / 2"], SimText.StandingLines(perception).Select(Loc.Resolve));
    }

    [Fact]
    public void ViewLines_ShowRunEnd_OnceTheRunIsOver()
    {
        var sim = Simulation.NewGame(TestContent.Calm, 1);
        Assert.DoesNotContain(ViewText(sim), line => line.StartsWith("Забіг завершено", StringComparison.Ordinal));

        sim.State.Mode = GameMode.RunEnd;
        sim.State.EndCause = RunEndCause.OldAge;
        Assert.Contains("Забіг завершено: смерть від старості", ViewText(sim));
    }

    [Fact]
    public void Describe_ReportsNotableEventsOnly()
    {
        Assert.Null(SimText.Describe(new WeekPassed(1, 625)));
        Assert.Equal("Заняття завершено: Відпочинок.", Loc.Resolve(SimText.Describe(new ActivityFinished(new WaitActivity()))!));
        Assert.Equal("Заняття перервано: Відпочинок.", Loc.Resolve(SimText.Describe(new ActivityInterrupted(new WaitActivity()))!));
        Assert.Equal("До очікуваного кінця життя лишилось років: 5.", Loc.Resolve(SimText.Describe(new LifeEndApproaching(5))!));
        Assert.Equal("Забіг завершено: смерть від старості.", Loc.Resolve(SimText.Describe(new RunEnded(RunEndCause.OldAge))!));
        Assert.Equal("Прибуття: Туманні пагорби.", Loc.Resolve(SimText.Describe(new ZoneEntered("zone.misty_hills"))!));
        Assert.Equal("Ці місця майже вичерпано: Тихий двір.", Loc.Resolve(SimText.Describe(new PlaceQiLow("place.hub.quiet_courtyard"))!));
        var meditation = new MeditateActivity("meditation.purification", "place.hub.quiet_courtyard");
        Assert.Equal("Заняття завершено: Медитація (Очищення), Тихий двір.", Loc.Resolve(SimText.Describe(new ActivityFinished(meditation))!));
        var travel = new TravelActivity([new TravelLeg("zone.bamboo_grove", 1)]);
        Assert.Equal("Заняття завершено: Подорож, мета — Бамбуковий гай.", Loc.Resolve(SimText.Describe(new ActivityFinished(travel))!));
        var seclusion = new BreakthroughActivity("qi", "place.hub.quiet_courtyard", false);
        Assert.Equal("Заняття завершено: Усамітнення для прориву, Тихий двір.", Loc.Resolve(SimText.Describe(new ActivityFinished(seclusion))!));
        Assert.Equal(
            "Прорив вдався: Перший етап ці. Якість: Добрий. Років життя додано: 6.",
            Loc.Resolve(SimText.Describe(new BreakthroughSucceeded("qi", "stage.qi.1", "rank.good", 6))!));
        Assert.Equal(
            "Прорив не вдався (легка нестача): частину шкали втрачено, у тілі осіли токсини.",
            Loc.Resolve(SimText.Describe(new BreakthroughFailed("qi", "failure_zone.light"))!));
        Assert.Equal("Забіг завершено: загибель під час прориву.", Loc.Resolve(SimText.Describe(new RunEnded(RunEndCause.BreakthroughDeath))!));
    }

    [Fact]
    public void ViewLines_ShowTheBreakthroughForecastOnceTheScaleIsFull()
    {
        var sim = Simulation.NewGame(TestContent.Calm, 1);
        sim.State.Player.Tracks.Single().StageProgress = 100;

        // Foundation 0 of 40: the deficit is total. Quality 30 at age 12 + 4 weeks of seclusion: age −0.1.
        var text = ViewText(sim).ToList();
        int at = text.IndexOf("Прогноз прориву: шанс провалу 95% (червона зона); якість 29.9 — Звичайний");
        Assert.True(at >= 0);
        Assert.Equal(
            [
                "    · Поріг 40, фундамент 0: нестача фундаменту — шанс провалу",
                "    · Якість: база 30",
                "    · Чистота: +0",
                "    · Вік: −0.1",
                "    · Відкладання прориву: −0",
                "    · Стеля якості етапу: 45",
            ],
            text.Skip(at + 1).Take(6));
    }

    [Fact]
    public void BreakdownLines_ExplainModifiedValuesBySource_AndNothingOtherwise()
    {
        var sim = Simulation.NewGame(TestContent.Calm, 1);
        ModifierSystem.Apply(sim.State.Player, new Modifier { TargetId = "stat.speed", Op = ModifierOp.Mul, Value = -0.1, SourceId = "injury.body" });
        ModifierSystem.Apply(sim.State.Player, new Modifier { TargetId = "stat.speed", Op = ModifierOp.Add, Value = 2, SourceId = "injury.internal" });
        var speed = sim.View.Stats.Single(s => s.Stat.Id == "stat.speed").Stat;
        var might = sim.View.Stats.Single(s => s.Stat.Id == "stat.might").Stat;

        Assert.Equal(
            ["Швидкість: основа 10", "-10% — Тілесні травми", "+2 — Внутрішні травми"],
            SimText.BreakdownLines(speed).Select(Loc.Resolve));
        Assert.Empty(SimText.BreakdownLines(might));
    }

    [Fact]
    public void SummaryLines_ShowTheRunRecord_OnlyOnceTheRunIsOver()
    {
        var sim = Simulation.NewGame(TestContent.Calm, 1);
        sim.Execute(TestContent.Wait(3));
        sim.Execute(new UsePill("item.pill.qi_gathering"));
        Assert.Empty(SimText.SummaryLines(sim.View, TestContent.Calm));

        sim.State.Mode = GameMode.RunEnd;
        sim.State.EndCause = RunEndCause.OldAge;
        sim.State.Stats.Breakthroughs.Add(new BreakthroughRecord { AgeWeeks = 52 * 20 + 3, RankId = "rank.common" });
        Assert.Equal(
            [
                "Відпочинок — тижнів: 3 (100%)",
                "Прорив у віці 20 р. 3 тиж.: Звичайний",
                "Бої: перемог 0, втеч 0, поразок 0",
                "Пігулок прийнято: 1",
                "Каменів духу здобуто: 0, витрачено: 0",
                "Подій: 0, чуток почуто: 0",
            ],
            SimText.SummaryLines(sim.View, TestContent.Calm).Select(Loc.Resolve));
    }

    [Fact]
    public void ItemDetails_TellWhatAPillAndAMethodFolioDo()
    {
        var sim = Simulation.NewGame(TestContent.Calm, 1);
        sim.State.Player.Stones = 100;
        Assert.True(sim.Execute(new Buy("item.folio.azure_breath")).Accepted);
        string Details(string itemId) =>
            string.Join(" | ", SimText.ItemDetails(sim.View.Items.Single(i => i.ItemId == itemId), sim.View, TestContent.Calm).Select(Loc.Resolve));

        Assert.Equal("Кількість: 2 | Енергія для засвоєння: 30 | Кожна наступна така пігулка діє слабше. | Токсичність: 8", Details("item.pill.qi_gathering"));
        Assert.Equal(
            "Кількість: 1 | Вивчає: Метод лазурового подиху | Рівень зараз: 0, стеля фоліанта: 5 | Швидкість вивчення: × 0.8"
            + " | Метод культивації: вивчений до рівня 1, кладеться в слот методу, і через нього йде прорив.",
            Details("item.folio.azure_breath"));
    }

    [Fact]
    public void MethodDetails_FollowTheActiveMethodAndItsLevel()
    {
        var sim = Simulation.NewGame(TestContent.Calm, 1);
        string Details() => string.Join(" | ", SimText.MethodDetails(sim.View, TestContent.Calm).Select(Loc.Resolve));
        Assert.Equal("Метод культивації: немає | Стеля якості прориву: 45, чистота враховується до 20", Details());

        sim.State.Player.Knowledge.Add(new KnowledgeLevel { KnowledgeId = "method.azure_breath", Level = 2 });
        Assert.True(sim.Execute(new SetActiveMethod("method.azure_breath")).Accepted);
        Assert.Equal("Метод культивації: Метод лазурового подиху | Рівень методу: 2 | Стеля якості прориву: 66, чистота враховується до 70", Details());
    }

    [Fact]
    public void InterruptName_ResolvesForEveryCategory()
    {
        foreach (var category in Enum.GetValues<InterruptCategory>())
            Assert.DoesNotContain("str.", Loc.Resolve(SimText.InterruptName(category)));
    }
}
