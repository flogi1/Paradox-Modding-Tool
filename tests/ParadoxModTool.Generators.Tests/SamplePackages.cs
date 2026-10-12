namespace ParadoxModTool.Generators.Tests;

/// <summary>Kleine Pakete im Code: Kopf "tag", Kategorien adm (Basis 0) und dip (Basis 10), Ideen mit Name, Stufen mit Modifikatoren.</summary>
internal static class SamplePackages
{
    public static GeneratorPackage Ideas(int ideaMin = 0, string functions = "", string folder = "", IReadOnlyList<OutputDef>? outputs = null, IReadOnlyList<WarningDef>? warnings = null, IReadOnlyList<WarningDef>? errors = null, int? ideaInitial = null, int? levelInitial = null, ReadDef? read = null)
    {
        var levels = new LevelDef("levels", "Stufen", null, 1, 4, null, new[]
        {
            new FieldDef("modifiers", "Modifikatoren", FieldType.Modifiers),
            new FieldDef("expensive", "teuer", FieldType.Bool, Default: "no"),
        }, null, levelInitial);
        var ideas = new LevelDef("ideas", "Ideen", null, ideaMin, 9, "base", new[]
        {
            new FieldDef("name", "Name", FieldType.Text, Required: true, Multilingual: true),
            new FieldDef("desc", "Beschreibung", FieldType.MultilineText, Multilingual: true),
        }, levels, ideaInitial);
        var categories = new LevelDef("categories", "Kategorien", "categories", 0, int.MaxValue, null, Array.Empty<FieldDef>(), ideas);
        var constants = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
        {
            ["categories"] = new[]
            {
                (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["key"] = "adm", ["base"] = "0" },
                new Dictionary<string, string> { ["key"] = "dip", ["base"] = "10" },
            },
        };
        return new GeneratorPackage(
            "iv2",
            "IV2",
            "eu5",
            folder,
            new[] { new FieldDef("tag", "Tag", FieldType.Text, Required: true, Pattern: "^[A-Z0-9]{2,5}$") },
            "tag",
            categories,
            constants,
            functions,
            outputs ?? Array.Empty<OutputDef>(),
            new[] { "mod", "output" },
            new LanguageDefaults(),
            warnings ?? Array.Empty<WarningDef>(),
            new Dictionary<string, string>(),
            errors ?? Array.Empty<WarningDef>(),
            folder,
            false,
            read);
    }
}
