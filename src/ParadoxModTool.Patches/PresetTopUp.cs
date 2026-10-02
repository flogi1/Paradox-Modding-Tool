namespace ParadoxModTool.Patches;

/// <summary>Was „Fehlendes aus der Vorlage ergänzen“ einem Rezept hinzufügt (Namen für die Rückfrage); <see cref="Edits"/> als „Regel: Eigenschaft“.</summary>
public sealed record TopUpResult(IReadOnlyList<string> Rules, IReadOnlyList<string> Columns, IReadOnlyList<string> Edits, IReadOnlyList<string> Patterns)
{
    public static TopUpResult None { get; } = new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    public int Count => Rules.Count + Columns.Count + Edits.Count + Patterns.Count;
}

/// <summary>
/// Ergänzt ein Rezept aus einer Vorlage um das, was ihm fehlt: Regeln (nach Id), Spalten (nach Name), Bearbeitungen in vorhandenen Regeln
/// (nach Art und Eigenschaft) und Wächter-Suchmuster. Vorhandenes wird nie geändert; Handänderungen und ausgeschaltete Regeln bleiben.
/// </summary>
public static class PresetTopUp
{
    /// <summary>Frische Vorlage zum Preset des Rezepts, null bei Rezepten ohne Vorlage.</summary>
    public static PatchRecipe? Template(PatchRecipe recipe) => recipe.Preset switch
    {
        SpecialBuildingsPreset.Id => SpecialBuildingsPreset.Create(recipe.Name, Array.Empty<string>()),
        SpecialResourcesPreset.Id => SpecialResourcesPreset.Create(recipe.Name, Array.Empty<string>()),
        _ => null,
    };

    /// <summary>Was <see cref="Apply"/> ergänzen würde, ohne das Rezept zu ändern.</summary>
    public static TopUpResult Missing(PatchRecipe recipe) => Apply(PatchRecipeStore.Clone(recipe));

    public static TopUpResult Apply(PatchRecipe recipe)
    {
        if (Template(recipe) is not { } template)
        {
            return TopUpResult.None;
        }

        var columns = new List<string>();
        foreach (var column in template.Columns.Where(c => recipe.Columns.All(own => own.Name != c.Name)))
        {
            recipe.Columns.Add(column);
            columns.Add(column.Name);
        }

        var rules = new List<string>();
        var edits = new List<string>();
        for (var i = 0; i < template.Rules.Count; i++)
        {
            var wanted = template.Rules[i];
            var own = recipe.Rules.FirstOrDefault(r => r.Id == wanted.Id);
            if (own is null)
            {
                // Hinter die vorhandene Regel, die in der Vorlage davor steht; sonst an den Anfang.
                var index = 0;
                for (var before = i - 1; before >= 0; before--)
                {
                    var found = recipe.Rules.FindIndex(r => r.Id == template.Rules[before].Id);
                    if (found >= 0)
                    {
                        index = found + 1;
                        break;
                    }
                }

                recipe.Rules.Insert(index, wanted);
                rules.Add(Label(wanted));
            }
            else if (TemplateEdits(wanted) is { } wantedEdits && TemplateEdits(own) is { } ownEdits && own.GetType() == wanted.GetType())
            {
                foreach (var edit in wantedEdits.Where(w => ownEdits.All(o => !Same(o, w))))
                {
                    ownEdits.Add(edit);
                    edits.Add(Label(own) + ": " + (edit.Key.Length > 0 ? edit.Key : edit.Kind.ToString()));
                }
            }
        }

        var patterns = template.Watch.Patterns.Where(p => !recipe.Watch.Patterns.Contains(p, StringComparer.Ordinal)).ToList();
        recipe.Watch.Patterns.AddRange(patterns);
        return new TopUpResult(rules, columns, edits, patterns);
    }

    private static string Label(PatchRule rule) => rule.Name.Length > 0 ? rule.Name : rule.Id;

    private static List<BlockEdit>? TemplateEdits(PatchRule rule) => rule switch
    {
        BlockCopyRule copy => copy.Edits,
        BlockEditRule edit => edit.Edits,
        _ => null,
    };

    /// <summary>Gleiche Bearbeitung: Art und Eigenschaft; beim Entfernen zählt auch der Wert (type = special ≠ type = x).</summary>
    private static bool Same(BlockEdit a, BlockEdit b) =>
        a.Kind == b.Kind && a.Key == b.Key && (a.Kind != BlockEditKind.Remove || a.Value == b.Value);
}
