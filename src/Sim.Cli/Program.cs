using Core.Content;

return args switch
{
    ["validate", string contentDir] => Validate(contentDir),
    _ => Usage(),
};

static int Validate(string contentDir)
{
    if (!Directory.Exists(contentDir))
    {
        Console.Error.WriteLine($"Content directory not found: {contentDir}");
        return 2;
    }

    var loaded = ContentLoader.Load(ContentFiles.FromDirectory(contentDir));
    var errors = loaded.Errors.Concat(ContentValidator.Validate(loaded.Db)).ToList();

    foreach (var error in errors)
        Console.Error.WriteLine(error);
    Console.WriteLine($"{loaded.Db.Count} objects, {errors.Count} errors.");
    return errors.Count == 0 ? 0 : 1;
}

static int Usage()
{
    Console.Error.WriteLine("Usage: sim validate <contentDir>");
    return 2;
}
