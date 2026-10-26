using System.Reflection;

namespace Core.Content;

/// <summary>
/// Cross-object checks on loaded content (tech 3.6), run at game start and by
/// <c>Sim.Cli validate</c>. Every <see cref="ContentRefAttribute"/> reference points to an existing
/// object of the right type, every <see cref="LocalizedText"/> has Ukrainian text, and every
/// <see cref="IValidatedDef"/> passes its own rules. Nested blocks (a pill's or folio's settings)
/// are checked the same way. Errors come in ID order, then property name order.
/// </summary>
public static class ContentValidator
{
    public static IReadOnlyList<ContentError> Validate(ContentDb db)
    {
        var errors = new List<ContentError>();
        foreach (var def in db.All<ContentDef>())
        {
            var source = db.SourceOf(def.Id) ?? new ContentSource("?", 0);
            CheckObject(db, def, $"'{def.Id}'", source, errors);

            if (def is IValidatedDef validated)
            {
                foreach (string message in validated.Validate(db))
                    errors.Add(new ContentError(source.File, source.Line, $"'{def.Id}' {message}"));
            }
        }
        return errors;
    }

    private static void CheckObject(ContentDb db, object obj, string path, ContentSource source, List<ContentError> errors)
    {
        foreach (var property in PropertiesOf(obj.GetType()))
        {
            object? value = property.GetValue(obj);
            string field = $"{path}.{property.Name}";

            if (value is LocalizedText text && string.IsNullOrWhiteSpace(text.Uk))
                errors.Add(new ContentError(source.File, source.Line, $"{field} has no 'uk' text."));

            if (property.GetCustomAttribute<ContentRefAttribute>() is { } reference)
            {
                foreach (string id in ReferencedIds(value))
                    CheckReference(db, reference.Target, id, field, source, errors);
            }
            else if (value is not null && IsNestedBlock(value.GetType()))
            {
                CheckObject(db, value, field, source, errors);
            }
        }
    }

    /// <summary>A content class used inside a content object, such as <see cref="PillDef"/>.</summary>
    private static bool IsNestedBlock(Type type) =>
        type.IsClass && type.Namespace == typeof(ContentDef).Namespace
        && !typeof(ContentDef).IsAssignableFrom(type) && type != typeof(LocalizedText);

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

    private static IEnumerable<PropertyInfo> PropertiesOf(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.Name, StringComparer.Ordinal);
}
