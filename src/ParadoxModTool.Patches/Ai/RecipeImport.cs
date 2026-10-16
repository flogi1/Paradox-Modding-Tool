namespace ParadoxModTool.Patches.Ai;

/// <summary>
/// Vorbereiteter Import: das Rezept mit gesetztem Spiel, Id und Vorlage, ob die Id schon vergeben ist (<see cref="IdConflict"/>) oder auf die des
/// offenen Rezepts zurückgesetzt wurde (<see cref="IdReset"/>), die entfallenen Regeln des offenen Rezepts mit Ausgabe in der Mod und die Prüfmeldungen.
/// </summary>
public sealed record RecipeImportPlan(PatchRecipe Recipe, bool IdConflict, bool IdReset, IReadOnlyList<string> DroppedRules, IReadOnlyList<RecipeIssue> Issues)
{
    public bool HasErrors => Issues.Any(i => i.Severity == CheckSeverity.Error);
}

/// <summary>
/// Bereitet die Antwort einer KI für den Import vor (Spec 2026-10-12-patch-recipes-ai-guide, Abschnitt 2 "Ergebnis"). Ändert die Antwort nicht,
/// liest nur und schreibt nichts; Speichern, Rückfragen und Vorschau macht das Fenster.
/// </summary>
public static class RecipeImport
{
    /// <summary>
    /// Kopie der Antwort mit <paramref name="gameId"/>; mit offenem Rezept bekommt sie dessen Id (bei Abweichung <c>IdReset</c>) und Vorlage, ohne
    /// offenes Rezept wird die Vorlage gelöscht (die KI soll sie nicht setzen). <paramref name="gameRoot"/> ist der Vanilla-Ordner, den auch das
    /// Rezepte-Fenster benutzt. Entfallene Regeln stehen in <c>DroppedRules</c>, nicht in den Prüfmeldungen.
    /// </summary>
    public static RecipeImportPlan Prepare(PatchRecipe answer, string gameId, IReadOnlyCollection<string> existingIds,
        PatchRecipe? openRecipe, PatchMemory? openMemory, string? modRoot, string? gameRoot, IReadOnlyList<string>? unknownProperties = null)
    {
        var recipe = PatchRecipeStore.Clone(answer);
        recipe.GameId = gameId;
        var idReset = false;
        if (openRecipe is not null && !string.Equals(recipe.Id, openRecipe.Id, StringComparison.Ordinal))
        {
            recipe.Id = openRecipe.Id;
            idReset = true;
        }

        recipe.Preset = openRecipe?.Preset;

        // Ohne Namen hieße das Rezept in der Liste leer: dann trägt es seine (endgültige) Id als Namen.
        if (string.IsNullOrWhiteSpace(recipe.Name))
        {
            recipe.Name = recipe.Id;
        }

        var idConflict = existingIds.Contains(recipe.Id, StringComparer.OrdinalIgnoreCase);
        var dropped = openRecipe is null
            ? Array.Empty<string>()
            : RecipeChecker.DroppedRulesWithOutput(recipe, openRecipe, openMemory, modRoot);
        var issues = RecipeChecker.Check(recipe, new RecipeCheckOptions(gameRoot, modRoot, UnknownProperties: unknownProperties));
        return new RecipeImportPlan(recipe, idConflict, idReset, dropped, issues);
    }

    /// <summary>
    /// Welches Rezept beim Übernehmen als mitgegeben gilt (Ruling 12), wenn ein Rezept mitgegeben wird (<paramref name="selected"/>: das im Rezepte-Fenster
    /// gewählte Rezept derselben Mod, sonst null). Trägt die Antwort die Id eines anderen Rezepts dieser Mod (<paramref name="findRecipe"/>), ist es dieses:
    /// der Auftrag wurde mit ihm gebaut, die Auswahl hat sich seitdem geändert. Sonst das gewählte, dessen Id die Antwort dann bekommt.
    /// </summary>
    public static PatchRecipe? HandedOver(PatchRecipe answer, PatchRecipe? selected, Func<string, PatchRecipe?> findRecipe)
    {
        if (selected is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(answer.Id) ? selected : findRecipe(answer.Id) ?? selected;
    }

    /// <summary>Kopie unter einer freien Id, abgeleitet vom Namen ("Speichern unter neuer Id").</summary>
    public static PatchRecipe WithNewId(PatchRecipe recipe, IEnumerable<string> existingIds)
    {
        var copy = PatchRecipeStore.Clone(recipe);
        copy.Id = RecipeEditing.NewId(copy.Name, existingIds);
        return copy;
    }
}
