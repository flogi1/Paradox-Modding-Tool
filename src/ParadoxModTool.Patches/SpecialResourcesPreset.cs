namespace ParadoxModTool.Patches;

/// <summary>
/// Vorlage "CK III: Ressourcen in Special Buildings": Special Buildings bekommen die Ressourcen-Effekte in on_start und on_cancelled, egal ob sie einen Ort
/// in der History haben. Eine Regel "Block bearbeiten" schreibt direkt in die bestehenden Gebäude-Dateien, ohne eigene Datei und ohne Umwandlung;
/// Zeilen kommen aus "Specials aus Vanilla einlesen". Gedacht für Specials, die Special bleiben; was "Special Buildings in normale Gebäude" umwandelt, bekommt die Effekte dort.
/// </summary>
public static class SpecialResourcesPreset
{
    public const string Id = "ck3-special-resources";

    public static PatchRecipe Create(string name, IEnumerable<string> existingIds)
    {
        var recipe = RecipeEditing.Create(name, "ck3", existingIds);
        recipe.Preset = Id;
        recipe.Rules.Add(new BlockEditRule
        {
            Id = "resources",
            Name = "Ressourcen in on_start und on_cancelled",
            Files = SpecialBuildingsPreset.BuildingsFolder + "/*.txt",
            BlockKey = "{key}",
            FollowNext = true,
            CreateCopy = true,
            Edits =
            {
                new BlockEdit { Kind = BlockEditKind.InsertInto, Key = "on_start", Text = SpecialBuildingsPreset.ResourceEffects("subtract") },
                new BlockEdit { Kind = BlockEditKind.InsertInto, Key = "on_cancelled", Text = SpecialBuildingsPreset.ResourceEffects("add") },
            },
        });
        return recipe;
    }
}
