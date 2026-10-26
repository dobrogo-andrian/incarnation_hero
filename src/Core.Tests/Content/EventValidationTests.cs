using Core.Content;

namespace Core.Tests.Content;

public sealed class EventValidationTests
{
    private static IReadOnlyList<string> Errors(string yaml)
    {
        var loaded = ContentLoader.Load([new ContentFile("a.yaml", yaml)]);
        Assert.True(loaded.Ok, string.Join("\n", loaded.Errors));
        return ContentValidator.Validate(loaded.Db).Select(e => e.Message).ToList();
    }

    [Fact]
    public void RepoEvents_AreValid()
    {
        Assert.NotEmpty(TestContent.Repo.All<EventDef>());
        Assert.Empty(ContentValidator.Validate(TestContent.Repo));
    }

    [Fact]
    public void MissingStartNode_IsReported()
    {
        var errors = Errors("""
            - id: evt.a
              contexts: [rest]
              weight: 1
              nodes: { other: { text: { uk: "x" } } }
            """);

        Assert.Contains("'evt.a' needs a 'start' node.", errors);
    }

    [Fact]
    public void BrokenGraphConditionsAndEffects_AreReported()
    {
        var errors = Errors("""
            - id: zone.open
              name: { uk: "Відкрита" }
              danger: 0
            - id: evt.a
              contexts: [rest, dungeon]
              weight: 1
              nodes:
                start:
                  text: { uk: "x" }
                  options:
                    - text: { uk: "a" }
                      conditions: [ { stones: { min: 1 }, zone_danger: { max: 1 } } ]
                      next: nowhere
                    - text: { uk: "b" }
                      effects: [ { start_combat: { group: grp.none, win: start, loss: start } }, { stones: 1 } ]
                    - text: { uk: "c" }
                      effects: [ { reveal: zone.open }, { stones: 1, time: 2 } ]
                    - text: { uk: "d" }
                      outcomes: [ { weight: 1, weight_per_stat: { zone.open: 1 } } ]
                lonely: { text: { uk: "y" } }
            """);

        Assert.Contains(errors, e => e.Contains("'evt.a' needs contexts out of"));
        Assert.Contains(errors, e => e.Contains("node 'start' option 0: goes to missing node 'nowhere'"));
        Assert.Contains(errors, e => e.Contains("'evt.a'.Nodes.start.Options.0.Conditions.0 condition must set exactly one field"));
        Assert.Contains(errors, e => e.Contains("node 'start' option 1: start_combat must be the last effect"));
        Assert.Contains(errors, e => e.Contains("StartCombat.Group references missing id 'grp.none'"));
        Assert.Contains(errors, e => e.Contains("reveal needs a hidden zone or place, not 'zone.open'"));
        Assert.Contains(errors, e => e.Contains("'evt.a'.Nodes.start.Options.2.Effects.1 effect must set exactly one field"));
        Assert.Contains(errors, e => e.Contains("weight_per_stat references 'zone.open', which is ZoneDef"));
        Assert.Contains(errors, e => e.Contains("nodes unreachable from 'start': lonely"));
    }
}
