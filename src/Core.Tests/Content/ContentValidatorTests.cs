using Core.Content;

namespace Core.Tests.Content;

public sealed class ContentValidatorTests
{
    private static IReadOnlyList<ContentError> Validate(string yaml)
    {
        var loaded = ContentLoader.Load([new ContentFile("a.yaml", yaml)]);
        Assert.True(loaded.Ok, string.Join("\n", loaded.Errors));
        return ContentValidator.Validate(loaded.Db);
    }

    [Fact]
    public void ValidContent_HasNoErrors()
    {
        var errors = Validate("""
            - id: vector.qi
              name: { uk: "Ці" }
              stages: [stage.qi.1]
            - id: stage.qi.1
              name: { uk: "Перший" }
              progress_required: 1
            """);

        Assert.Empty(errors);
    }

    [Fact]
    public void MissingReference_IsReportedWithSourceAndId()
    {
        var errors = Validate("""
            - id: stage.qi.1
              name: { uk: "Перший" }
              progress_required: 1
            - id: vector.qi
              name: { uk: "Ці" }
              stages: [stage.qi.1, stage.qi.9]
            """);

        var error = Assert.Single(errors);
        Assert.Equal(new ContentSource("a.yaml", 4), new ContentSource(error.File, error.Line));
        Assert.Contains("'vector.qi'.Stages", error.Message);
        Assert.Contains("'stage.qi.9'", error.Message);
    }

    [Fact]
    public void ReferenceToWrongType_IsReported()
    {
        var errors = Validate("""
            id: vector.qi
            name: { uk: "Ці" }
            stages: [vector.qi]
            """);

        Assert.Contains("not CultivationStageDef", Assert.Single(errors).Message);
    }

    [Fact]
    public void MissingUkrainianText_IsReported()
    {
        var errors = Validate("{ id: stage.qi.1, progress_required: 1 }");

        Assert.Contains("'stage.qi.1'.Name has no 'uk' text", Assert.Single(errors).Message);
    }

    private const string TwoAspectStat = """
        - id: stat.might
          name: { uk: "Міць" }
          aspects: [aspect.a, aspect.b]
        - id: aspect.a
          name: { uk: "А" }
        - id: aspect.b
          name: { uk: "Б" }
        """;

    [Fact]
    public void DictionaryKeys_AreCheckedAsReferences()
    {
        var errors = Validate(TwoAspectStat + """

            - id: aspect_profile.p
              name: { uk: "П" }
              shares: { aspect.a: 0.5, aspect.b: 0.4, aspect.zzz: 0.1 }
            """);

        Assert.Contains(errors, e => e.Message.Contains("'aspect_profile.p'.Shares references missing id 'aspect.zzz'"));
    }

    [Fact]
    public void AspectShares_MustAddUpToOnePerStat()
    {
        var errors = Validate(TwoAspectStat + """

            - id: aspect_profile.p
              name: { uk: "П" }
              shares: { aspect.a: 0.5, aspect.b: 0.4 }
            """);

        Assert.Contains("shares of 'stat.might' aspects add up to 0.9", Assert.Single(errors).Message);
    }

    [Fact]
    public void AspectProfile_MustCoverEveryAspect()
    {
        var errors = Validate(TwoAspectStat + """

            - id: aspect_profile.p
              name: { uk: "П" }
              shares: { aspect.a: 1 }
            """);

        Assert.Contains("'aspect_profile.p' has no share for aspect.b", Assert.Single(errors).Message);
    }

    [Fact]
    public void Stage_NeedsAPositiveScale()
    {
        var errors = Validate("""- { id: stage.qi.1, name: { uk: "Перший" }, progress_required: 0 }""");

        Assert.Contains("'stage.qi.1' needs a positive progress_required", Assert.Single(errors).Message);
    }

    [Fact]
    public void ResourceSource_MustBeStatOrAspect()
    {
        var errors = Validate("""
            - id: stage.qi.1
              name: { uk: "Перший" }
              progress_required: 1
            - id: resource.r
              name: { uk: "Р" }
              source: stage.qi.1
            """);

        Assert.Contains("source 'stage.qi.1' is neither a stat nor an aspect", Assert.Single(errors).Message);
    }

    [Fact]
    public void Route_MustJoinTwoZonesAndTakeTime()
    {
        var errors = Validate("""
            - { id: zone.a, name: { uk: "А" } }
            - { id: place.p, name: { uk: "П" }, zone: zone.a, density: 1, capacity: 1 }
            - { id: route.aa, a: zone.a, b: zone.a, weeks: 0 }
            """);

        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Message.Contains("'route.aa' connects a zone to itself"));
        Assert.Contains(errors, e => e.Message.Contains("'route.aa' must take at least one week"));
    }

    [Fact]
    public void Place_NeedsPositiveNumbers()
    {
        var errors = Validate("""
            - { id: zone.a, name: { uk: "А" } }
            - { id: place.p, name: { uk: "П" }, zone: zone.a, density: 0, capacity: 10, regen_per_week: -1 }
            """);

        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Message.Contains("positive density and capacity"));
        Assert.Contains(errors, e => e.Message.Contains("negative regeneration"));
    }

    [Fact]
    public void Zone_NeedsAMeditationPlace()
    {
        var errors = Validate("""- { id: zone.a, name: { uk: "А" } }""");

        Assert.Contains("'zone.a' has no meditation place", Assert.Single(errors).Message);
    }

    [Fact]
    public void MeditationDirection_TargetsAStatResourceOrVector()
    {
        var errors = Validate("""
            - { id: zone.a, name: { uk: "А" } }
            - { id: place.p, name: { uk: "П" }, zone: zone.a, density: 1, capacity: 1 }
            - { id: meditation.m, name: { uk: "М" }, rate: 1, target: zone.a }
            """);

        Assert.Contains("target 'zone.a' is not a stat, a resource or a cultivation vector", Assert.Single(errors).Message);
    }

    [Fact]
    public void EveryZone_MustBeReachableFromTheStartZone()
    {
        var errors = Validate("""
            - { id: zone.a, name: { uk: "А" } }
            - { id: zone.b, name: { uk: "Б" } }
            - { id: zone.c, name: { uk: "В" } }
            - { id: route.ab, a: zone.a, b: zone.b, weeks: 1 }
            - { id: config.run, start_zone: zone.a }
            """);

        // The trimmed config also lacks its other references; only the reachability error matters here.
        Assert.Contains(errors, e => e.Message.Contains("zones unreachable from 'zone.a': zone.c."));
    }

    [Fact]
    public void RepoContent_IsValid()
    {
        var loaded = ContentLoader.Load(ContentFiles.FromDirectory(RepoPaths.Content));

        Assert.Empty(ContentValidator.Validate(loaded.Db));
    }
}
