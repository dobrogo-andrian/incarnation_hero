using System.Reflection;
using Core.Content;
using Core.Loc;
using Core.Sim;

namespace Core.Tests.Loc;

public sealed class LocalizerTests
{
    private static Localizer FromYaml(string yaml)
    {
        var loaded = ContentLoader.Load([new ContentFile("a.yaml", yaml)]);
        Assert.True(loaded.Ok, string.Join("\n", loaded.Errors));
        return Localizer.FromContent(loaded.Db);
    }

    private static readonly Localizer Sample = FromYaml("""
        - id: stage.qi.1
          name: { uk: "Перший етап" }
        - id: str.ui.week
          text: { uk: "Тиждень: {week}" }
        - id: str.ui.stage
          text: { uk: "Етап: {stage}, тиждень {week}" }
        """);

    [Fact]
    public void ContentField_IsKeyedByIdAndYamlFieldName()
    {
        Assert.Equal("Перший етап", Sample.Resolve(new LocKey(LocTable.ContentKey("stage.qi.1", "name"))));
    }

    [Fact]
    public void StringDef_IsKeyedByItsId_AndNumbersAreSubstituted()
    {
        Assert.Equal("Тиждень: 1040", Sample.Resolve(LocKey.Of("str.ui.week", new LocArg("week", 1040))));
    }

    [Fact]
    public void NestedKeyArgument_IsResolved()
    {
        var key = LocKey.Of("str.ui.stage",
            new LocArg("stage", new LocKey("stage.qi.1.name")),
            new LocArg("week", 3));

        Assert.Equal("Етап: Перший етап, тиждень 3", Sample.Resolve(key));
    }

    [Fact]
    public void MissingKey_ResolvesToKey_AndMissingArgStaysAsPlaceholder()
    {
        Assert.Equal("str.nope", Sample.Resolve(new LocKey("str.nope")));
        Assert.Equal("Тиждень: {week}", Sample.Resolve(new LocKey("str.ui.week")));
    }

    [Fact]
    public void EveryLocKeyDeclaredInCore_HasTextInRepoContent()
    {
        var loaded = ContentLoader.Load(ContentFiles.FromDirectory(RepoPaths.Content));
        var localizer = Localizer.FromContent(loaded.Db);

        var declared = typeof(Simulation).Assembly.GetTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(f => f.FieldType == typeof(LocKey))
            .Select(f => ((LocKey)f.GetValue(null)!).Key)
            .ToList();

        Assert.NotEmpty(declared);
        Assert.All(declared, key => Assert.True(localizer.Has(key), $"No text for '{key}'."));
    }
}
