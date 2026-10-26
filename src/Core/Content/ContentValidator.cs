using System.Reflection;

namespace Core.Content;

/// <summary>
/// Cross-object checks on loaded content (tech 3.6), run at game start and by
/// <c>Sim.Cli validate</c>. Every <see cref="ContentRefAttribute"/> reference points to an existing
/// object of the right type, every <see cref="LocalizedText"/> has Ukrainian text, and every
/// <see cref="IValidatedDef"/> passes its own rules. Nested blocks (a pill's settings, an event's
/// nodes and options) are walked by <see cref="ContentWalker"/> and checked the same way. Errors
/// come in ID order, then property name order.
/// </summary>
public static class ContentValidator
{
    public static IReadOnlyList<ContentError> Validate(ContentDb db)
    {
        var errors = new List<ContentError>();
        foreach (var def in db.All<ContentDef>())
        {
            var source = db.SourceOf(def.Id) ?? new ContentSource("?", 0);
            var root = ContentPath.Root(def.Id);
            ContentWalker.Walk(
                def,
                root,
                (path, property, value) => CheckProperty(db, path, property, value, source, errors),
                (path, block) => CheckRules(db, block, path, source, errors));
            CheckRules(db, def, root, source, errors);
        }
        return errors;
    }

    private static void CheckProperty(
        ContentDb db, ContentPath path, PropertyInfo property, object? value, ContentSource source, List<ContentError> errors)
    {
        if (value is LocalizedText text && string.IsNullOrWhiteSpace(text.Uk))
            errors.Add(new ContentError(source.File, source.Line, $"{path.Code} has no 'uk' text."));

        if (property.GetCustomAttribute<ContentRefAttribute>() is { } reference)
        {
            foreach (string id in ReferencedIds(value))
                CheckReference(db, reference.Target, id, path.Code, source, errors);
        }
    }

    private static void CheckRules(ContentDb db, object obj, ContentPath path, ContentSource source, List<ContentError> errors)
    {
        if (obj is not IValidatedDef validated)
            return;
        foreach (string message in validated.Validate(db))
            errors.Add(new ContentError(source.File, source.Line, $"{path.Code} {message}"));
    }

    private static void CheckReference(
        ContentDb db, Type target, string id, string field, ContentSource source, List<ContentError> errors)
    {
        if (!db.TryGet<ContentDef>(id, out var found))
            errors.Add(new ContentError(source.File, source.Line, $"{field} references missing id '{id}'."));
        else if (!target.IsInstanceOfType(found))
            errors.Add(new ContentError(source.File, source.Line,
                $"{field} references '{id}', which is {found.GetType().Name}, not {target.Name}."));
    }

    private static IEnumerable<string> ReferencedIds(object? value) => value switch
    {
        string id => new[] { id },
        IEnumerable<string> ids => ids,
        System.Collections.IDictionary map => map.Keys.Cast<string>().Order(StringComparer.Ordinal),
        null => Array.Empty<string>(),
        _ => throw new InvalidOperationException($"[ContentRef] is not supported on {value.GetType().Name}."),
    };
}
