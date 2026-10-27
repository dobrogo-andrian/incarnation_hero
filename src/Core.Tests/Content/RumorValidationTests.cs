using Core.Content;

namespace Core.Tests.Content;

public sealed class RumorValidationTests
{
    private static IReadOnlyList<string> Errors(string yaml)
    {
        var loaded = ContentLoader.Load([new ContentFile("a.yaml", yaml)]);
        Assert.True(loaded.Ok, string.Join("\n", loaded.Errors));
        return ContentValidator.Validate(loaded.Db).Select(e => e.Message).ToList();
    }

    [Fact]
    public void RepoRumors_AreLoaded()
    {
        Assert.Equal(8, TestContent.Repo.All<RumorDef>().Count());
        Assert.NotEmpty(TestContent.Repo.All<RumorSourceDef>());
    }

    [Fact]
    public void BrokenWindowNodesAndSources_AreReported()
    {
        var errors = Errors("""
            - id: zone.a
              name: { uk: "А" }
            - id: place.a
              name: { uk: "А" }
              zone: zone.a
              density: 1
              capacity: 1
            - id: source.a
              name: { uk: "А" }
              reliability: 2
            - id: rumor.a
              name: { uk: "А" }
              text: { uk: "А" }
              zone: zone.a
              window_weeks: { min: 5, max: 2.5 }
              appeal: 1
              weight: 1
              truth: real
              falsehoods: { real: 1, lonely_fake: 0 }
              nodes:
                real: { text: { uk: "x" } }
                stray: { text: { uk: "x" } }
            - id: evt.a
              contexts: [rest]
              weight: 1
              nodes:
                start:
                  text: { uk: "x" }
                  options:
                    - text: { uk: "x" }
                      effects: [ { add_rumor: { id: rumor.missing, source: source.a } } ]
            """);

        Assert.Contains("'source.a' reliability must be from 0 to 1.", errors);
        Assert.Contains("'rumor.a' window_weeks needs whole min and max, 1 <= min <= max.", errors);
        Assert.Contains("'rumor.a' truth and falsehoods must be different nodes, none of them 'taken'.", errors);
        Assert.Contains("'rumor.a' falsehood weights must be positive.", errors);
        Assert.Contains("'rumor.a' needs a 'lonely_fake' node.", errors);
        Assert.Contains("'rumor.a' needs a 'taken' node.", errors);
        Assert.Contains(errors, e => e.Contains("rumor.missing"));
    }

    [Fact]
    public void UnreachableRumorNode_IsReported()
    {
        var errors = Errors("""
            - id: zone.a
              name: { uk: "А" }
            - id: place.a
              name: { uk: "А" }
              zone: zone.a
              density: 1
              capacity: 1
            - id: rumor.a
              name: { uk: "А" }
              text: { uk: "А" }
              zone: zone.a
              window_weeks: { min: 1, max: 2 }
              appeal: 1
              weight: 1
              truth: real
              nodes:
                real: { text: { uk: "x" } }
                taken: { text: { uk: "x" } }
                stray: { text: { uk: "x" } }
            """);

        Assert.Contains(errors, e => e.Contains("nodes unreachable from 'real', 'taken': stray"));
    }
}
