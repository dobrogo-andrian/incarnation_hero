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
            """);

        Assert.Empty(errors);
    }

    [Fact]
    public void MissingReference_IsReportedWithSourceAndId()
    {
        var errors = Validate("""
            - id: stage.qi.1
              name: { uk: "Перший" }
            - id: vector.qi
              name: { uk: "Ці" }
              stages: [stage.qi.1, stage.qi.9]
            """);

        var error = Assert.Single(errors);
        Assert.Equal(new ContentSource("a.yaml", 3), new ContentSource(error.File, error.Line));
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
        var errors = Validate("id: stage.qi.1");

        Assert.Contains("'stage.qi.1'.Name has no 'uk' text", Assert.Single(errors).Message);
    }

    [Fact]
    public void RepoContent_IsValid()
    {
        var loaded = ContentLoader.Load(ContentFiles.FromDirectory(RepoPaths.Content));

        Assert.Empty(ContentValidator.Validate(loaded.Db));
    }
}
