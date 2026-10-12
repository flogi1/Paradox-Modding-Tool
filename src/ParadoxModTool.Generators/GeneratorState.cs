namespace ParadoxModTool.Generators;

/// <summary>
/// Ein Element des Formulars. <see cref="Values"/>: Wert je Feld-Id (englischer bzw. einziger Text; Ja/Nein als <c>yes</c>/<c>no</c>);
/// <see cref="Translations"/>: Feld-Id → Sprache → Text (ohne Englisch); <see cref="Children"/>: Ebenen-Id → Elemente.
/// </summary>
public sealed class FormNode
{
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, Dictionary<string, string>> Translations { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, List<FormNode>> Children { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Ein Element einer Ebene mit den Vorgabewerten und je Kind-Ebene <see cref="LevelDef.Initial"/> (sonst <see cref="LevelDef.Min"/>) leeren Elementen.</summary>
    public static FormNode Create(IReadOnlyList<FieldDef> fields, LevelDef? child, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> constants)
    {
        var node = new FormNode();
        foreach (var field in fields.Where(f => f.Default is not null))
        {
            node.Values[field.Id] = field.Default!;
        }

        if (child is not null)
        {
            node.Children[child.Id] = CreateLevel(child, constants);
        }

        return node;
    }

    private static List<FormNode> CreateLevel(LevelDef level, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> constants)
    {
        var count = level.ConstantsTable is { } table ? constants.TryGetValue(table, out var rows) ? rows.Count : 0 : level.Initial ?? level.Min;
        return Enumerable.Range(0, count).Select(_ => Create(level.Fields, level.Child, constants)).ToList();
    }
}

/// <summary>Ein Arbeitsstand: die Eingaben eines Formulars für einen Schlüssel (bei IV2 der Tag), dazu Ziel und Sprachregeln.</summary>
public sealed class GeneratorState
{
    public string PackageId { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    /// <summary>Kopf-Felder in <see cref="FormNode.Values"/>, die oberste Ebene in <see cref="FormNode.Children"/>.</summary>
    public FormNode Head { get; set; } = new();

    /// <summary>
    /// Die Mod dieses Arbeitsstands (Ordner): Grundlage der Basisdateien und Ziel bei "In Mod schreiben". Fest je Stand gewählt, damit nicht die gerade
    /// im Editor offene Datei bestimmt, wohin geschrieben wird.
    /// </summary>
    public string? ModRoot { get; set; }

    /// <summary><c>mod</c> oder <c>output</c>.</summary>
    public string Target { get; set; } = "mod";

    public string? OutputFolder { get; set; }

    /// <summary>Die gewählten Sprachen (aus dem Spielprofil; ohne Liste nur Englisch).</summary>
    public List<string> Languages { get; set; } = new();

    public LanguageDefaults LanguageRules { get; set; } = new();

    public static GeneratorState CreateEmpty(GeneratorPackage package, IReadOnlyList<string> profileLanguages) => new()
    {
        PackageId = package.Id,
        Head = FormNode.Create(package.Head, package.Root, package.Constants),
        Target = package.Targets.FirstOrDefault() ?? "mod",
        Languages = profileLanguages.Count > 0 ? profileLanguages.ToList() : new List<string> { "english" },
        LanguageRules = package.Languages,
    };
}
