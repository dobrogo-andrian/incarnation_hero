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
                "Ці: Перший етап ці, прогрес 0",
            ],
            ViewText(sim));
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
    }
}
