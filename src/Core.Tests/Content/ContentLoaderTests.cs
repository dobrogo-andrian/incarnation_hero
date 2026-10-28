using Core.Content;

namespace Core.Tests.Content;

public sealed class ContentLoaderTests
{
    private static ContentLoadResult Load(params (string Path, string Text)[] files) =>
        ContentLoader.Load(files.Select(f => new ContentFile(f.Path, f.Text)));

    [Fact]
    public void SingleMapping_LoadsOneObject()
    {
        var result = Load(("a.yaml", """
            id: stage.qi.1
            name: { uk: "Перший" }
            """));

        Assert.True(result.Ok, string.Join("\n", result.Errors));
        Assert.Equal("Перший", result.Db.Get<CultivationStageDef>("stage.qi.1").Name.Uk);
        Assert.Equal(new ContentSource("a.yaml", 1), result.Db.SourceOf("stage.qi.1"));
    }

    [Fact]
    public void ListOfMappings_LoadsAllWithTheirTypes()
    {
        var result = Load(("a.yaml", """
            - id: vector.qi
              name: { uk: "Ці" }
              stages: [stage.qi.1, stage.qi.2]
            - id: stage.qi.1
              name: { uk: "Перший" }
            - id: stage.qi.2
              name: { uk: "Другий" }
            """));

        Assert.True(result.Ok, string.Join("\n", result.Errors));
        Assert.Equal(3, result.Db.Count);
        Assert.Equal(["stage.qi.1", "stage.qi.2"], result.Db.Get<CultivationVectorDef>("vector.qi").Stages);
        Assert.Equal(["stage.qi.1", "stage.qi.2"], result.Db.All<CultivationStageDef>().Select(s => s.Id));
        Assert.Equal(6, result.Db.SourceOf("stage.qi.2")!.Line);
    }

    [Fact]
    public void Get_WithWrongType_Throws()
    {
        var result = Load(("a.yaml", "id: stage.qi.1"));

        Assert.False(result.Db.TryGet<CultivationVectorDef>("stage.qi.1", out _));
        Assert.Throws<KeyNotFoundException>(() => result.Db.Get<CultivationVectorDef>("stage.qi.1"));
    }

    [Fact]
    public void DuplicateId_AcrossFiles_IsReportedWithFirstLocation()
    {
        var result = Load(
            ("b.yaml", "id: stage.qi.1"),
            ("a.yaml", "id: stage.qi.1"));

        var error = Assert.Single(result.Errors);
        Assert.Equal("b.yaml", error.File);
        Assert.Contains("a.yaml:1", error.Message);
    }

    [Fact]
    public void UnknownKind_IsReported()
    {
        var result = Load(("a.yaml", "id: spell.fireball"));

        var error = Assert.Single(result.Errors);
        Assert.Contains("'spell'", error.Message);
        Assert.Equal(0, result.Db.Count);
    }

    [Fact]
    public void MissingId_IsReported()
    {
        var result = Load(("a.yaml", "name: { uk: \"Без ID\" }"));

        Assert.Contains("no 'id'", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void UnknownField_IsReportedWithLine()
    {
        var result = Load(("a.yaml", """
            id: stage.qi.1
            name: { uk: "Перший" }
            breakthrough_chance: 0.5
            """));

        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.Line);
        Assert.Contains("stage.qi.1", error.Message);
        Assert.False(result.Db.Contains("stage.qi.1"));
    }

    [Fact]
    public void BrokenYaml_IsReportedWithLine_AndOtherFilesStillLoad()
    {
        var result = Load(
            ("bad.yaml", """
                id: stage.qi.1
                name: { uk: "Перший"
                """),
            ("good.yaml", "id: stage.qi.2"));

        var error = Assert.Single(result.Errors);
        Assert.Equal("bad.yaml", error.File);
        Assert.True(error.Line > 0);
        Assert.True(result.Db.Contains("stage.qi.2"));
    }

    [Fact]
    public void NonMappingItem_IsReported_AndSiblingsStillLoad()
    {
        var result = Load(("a.yaml", """
            - just a string
            - id: stage.qi.1
            """));

        Assert.Single(result.Errors);
        Assert.True(result.Db.Contains("stage.qi.1"));
    }

    [Fact]
    public void FileOrder_DoesNotChangeResult()
    {
        var a = ("a.yaml", "id: stage.qi.1");
        var b = ("b.yaml", "id: stage.qi.2");

        var forward = Load(a, b).Db.All<CultivationStageDef>().Select(s => s.Id);
        var backward = Load(b, a).Db.All<CultivationStageDef>().Select(s => s.Id);

        Assert.Equal(forward, backward);
    }

    [Fact]
    public void RepoContent_LoadsWithoutErrors()
    {
        var result = ContentLoader.Load(ContentFiles.FromDirectory(RepoPaths.Content));

        Assert.True(result.Ok, string.Join("\n", result.Errors));
        Assert.True(result.Db.TryGet<CultivationVectorDef>("vector.qi", out var qi));
        Assert.NotEmpty(qi.Stages);
    }

    [Fact]
    public void EmbeddedContent_MatchesRepoContent()
    {
        var embedded = ContentFiles.Embedded();

        Assert.NotEmpty(embedded);
        Assert.Equal(ContentFiles.FromDirectory(RepoPaths.Content), embedded);
    }
}
