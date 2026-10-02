namespace ParadoxModTool.Patches;

/// <summary>
/// Änderungen am Plan aus dem Vorschau-Fenster: abgelehnte Zeilen (bearbeiteter neuer Text) und gelöste Konflikte.
/// Beide geben einen neuen Plan zurück; der alte bleibt unverändert.
/// </summary>
public static class PatchPlanEdits
{
    /// <summary>Ersetzt den neuen Text einer Datei (z. B. nach mit U zurückgenommenen Zeilen) und bestimmt ihren Status neu.</summary>
    public static PatchPlan WithEditedFile(PatchPlan plan, string relativePath, string newText) =>
        Copy(plan, plan.Files.Select(f => Same(f.RelativePath, relativePath)
            ? f with { NewText = newText, Status = PlannedFileStatuses.For(f.OriginalText, newText, HasConflicts(plan.ConflictUnits, f.RelativePath)) }
            : f).ToList());

    /// <summary>
    /// Schreibt die Lösung eines Konflikts als Text des Bausteins in die Datei, merkt sich das neu Erzeugte als Gedächtnis
    /// (Basis = neu) und nimmt den Konflikt aus dem Plan. Ist es der letzte Konflikt der Datei, wird sie wieder geschrieben.
    /// </summary>
    public static PatchPlan ResolveConflict(PatchPlan plan, string relativePath, string ruleId, string unitKey, string resolvedText)
    {
        var conflict = plan.ConflictUnits.Single(c => Same(c.File, relativePath) && c.RuleId == ruleId && c.UnitKey == unitKey);
        var remaining = plan.ConflictUnits.Where(c => c != conflict).ToList();
        var file = plan.Files.Single(f => Same(f.RelativePath, relativePath));

        var region = GeneratedRegion.Read(file.NewText, plan.RecipeId, ruleId);
        var units = region.Units.Select(u => u.UnitKey == unitKey ? new RegionUnit(unitKey, resolvedText.Replace("\r\n", "\n")) : u).ToList();
        var newText = GeneratedRegion.Write(file.NewText, plan.RecipeId, ruleId, null, units, TextFile.NewLineOf(file.NewText));

        var messages = file.Conflicts.ToList();
        foreach (var message in conflict.Messages)
        {
            messages.Remove(message);
        }

        var stillConflicted = HasConflicts(remaining, relativePath);
        var files = plan.Files.Select(f => f == file
            ? f with { NewText = newText, Conflicts = messages, Status = PlannedFileStatuses.For(f.OriginalText, newText, stillConflicted) }
            : f).ToList();

        var memory = plan.NewMemory.Clone();
        memory.Units[PatchMemory.UnitId(ruleId, unitKey)] = conflict.Fresh;

        var conflictIds = plan.ConflictUnitIds.ToList();
        if (!stillConflicted && plan.UnitsByFile.TryGetValue(relativePath, out var ids))
        {
            conflictIds.RemoveAll(ids.Contains);
        }

        return new PatchPlan
        {
            RecipeId = plan.RecipeId,
            RecipeName = plan.RecipeName,
            Files = files,
            Skipped = plan.Skipped,
            Warnings = plan.Warnings,
            OldMemory = plan.OldMemory,
            NewMemory = memory,
            ConflictUnitIds = conflictIds,
            ConflictUnits = remaining,
            UnitsByFile = plan.UnitsByFile,
        };
    }

    private static PatchPlan Copy(PatchPlan plan, IReadOnlyList<PlannedFile> files) => new()
    {
        RecipeId = plan.RecipeId,
        RecipeName = plan.RecipeName,
        Files = files,
        Skipped = plan.Skipped,
        Warnings = plan.Warnings,
        OldMemory = plan.OldMemory,
        NewMemory = plan.NewMemory,
        ConflictUnitIds = plan.ConflictUnitIds,
        ConflictUnits = plan.ConflictUnits,
        UnitsByFile = plan.UnitsByFile,
        BrokenMemoryText = plan.BrokenMemoryText,
        Adopted = plan.Adopted,
    };

    /// <summary>
    /// Übernommene Blöcke, die nach Änderungen in der Vorschau (U) weder in ihrer Quelldatei noch im Ziel definiert wären: der Baustein wurde
    /// zurückgenommen, das Entfernen aus der Quelldatei aber nicht. Anwenden darf dann nicht schreiben.
    /// </summary>
    public static IReadOnlyList<string> LostAdoptions(PatchPlan plan)
    {
        string? Planned(string rel) => plan.Files.FirstOrDefault(f => Same(f.RelativePath, rel)) is { } file ? file.NewText : null;
        return plan.Adopted
            .Where(a => Planned(a.SourceFile) is { } source && !Defines(GeneratedRegion.MaskAll(source), a.Key)
                && !(Planned(a.TargetFile) is { } target && Defines(target, a.Key)))
            .Select(a => a.Key)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool Defines(string text, string key)
    {
        try
        {
            return ScriptSpans.FindTopLevel(ParadoxModTool.VanillaDiff.ScriptDocument.Parse(text), key) is not null;
        }
        catch (ParadoxModTool.VanillaDiff.ScriptParseException)
        {
            // Unlesbar: nicht als verloren melden, die Vorschau zeigt den Text ohnehin.
            return true;
        }
    }

    private static bool HasConflicts(IEnumerable<ConflictUnit> conflicts, string relativePath) => conflicts.Any(c => Same(c.File, relativePath));

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
