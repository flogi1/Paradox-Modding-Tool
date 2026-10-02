using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Patches;

/// <summary>
/// Gemeinsamer Schritt von Block-Kopie und Vorlage: entscheidet jeden Baustein mit <see cref="MemoryMerger"/>, schreibt den
/// erzeugten Bereich neu und trägt das Ergebnis ins Gedächtnis ein.
/// </summary>
public static class UnitRegionWriter
{
    /// <param name="adopted">Übernommene Blöcke je Baustein: gelten als "meine" Fassung, solange der Bereich den Baustein noch nicht hat (Gedächtnis-Basis = frisch).</param>
    public static UnitWriteResult WriteUnits(
        RuleRunContext ctx, string ruleId, string target, string? inside, IReadOnlyList<RegionUnit> freshUnits, bool isLocalization, string? newFileHeader = null,
        IReadOnlyDictionary<string, string>? adopted = null)
    {
        var workspace = ctx.Workspace;
        var recipeId = ctx.Recipe.Id;
        var existing = workspace.Get(target);
        var text = existing ?? string.Empty;

        if (existing is not null && !isLocalization && target.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                ScriptDocument.Parse(existing);
            }
            catch (ScriptParseException ex)
            {
                ctx.Warnings.Add(PatchTexts.FileSkipped(target, ex.Message));
                return UnitWriteResult.NotWritten;
            }
        }

        var region = GeneratedRegion.Read(text, recipeId, ruleId);
        if (region.Broken)
        {
            ctx.Warnings.Add(PatchTexts.RegionBroken(target));
            return UnitWriteResult.NotWritten;
        }

        var prefix = ruleId + "|";
        var fresh = freshUnits.ToDictionary(u => u.UnitKey, u => u.Text, StringComparer.Ordinal);
        var mine = region.Units.GroupBy(u => u.UnitKey, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Text, StringComparer.Ordinal);
        foreach (var (adoptedKey, adoptedText) in adopted ?? new Dictionary<string, string>())
        {
            mine.TryAdd(adoptedKey, adoptedText);
        }
        var rememberedKeys = ctx.OldMemory.Units.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).Select(k => k[prefix.Length..]);
        var keys = freshUnits.Select(u => u.UnitKey)
            .Concat(region.Units.Select(u => u.UnitKey))
            .Concat(rememberedKeys)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Übersprungene Zeilen (Zeilenfehler, Vanilla-Block fehlt, anderswo definiert) sind nicht gelöscht: Baustein und Gedächtnis bleiben.
        var skippedRows = ctx.SkippedRowKeys(ruleId);
        bool IsSkipped(string key) => skippedRows.Any(row => key == row || key.StartsWith(row + " ", StringComparison.Ordinal));

        var output = new List<RegionUnit>();
        int written = 0, merged = 0, removed = 0;
        foreach (var key in keys)
        {
            var unitId = PatchMemory.UnitId(ruleId, key);
            if (!fresh.ContainsKey(key) && IsSkipped(key))
            {
                if (mine.TryGetValue(key, out var kept))
                {
                    output.Add(new RegionUnit(key, kept));
                }

                ctx.RecordUnit(target, unitId);
                continue;
            }

            ctx.OldMemory.Units.TryGetValue(unitId, out var remembered);
            var outcome = MemoryMerger.Decide(key, fresh.GetValueOrDefault(key), mine.GetValueOrDefault(key), remembered, isLocalization);
            if (outcome.Text is not null)
            {
                output.Add(new RegionUnit(key, outcome.Text));
            }

            switch (outcome.Kind)
            {
                case UnitOutcomeKind.Inserted:
                case UnitOutcomeKind.Replaced:
                    written++;
                    ctx.NewMemory.Units[unitId] = fresh[key];
                    break;
                case UnitOutcomeKind.Merged:
                    merged++;
                    ctx.NewMemory.Units[unitId] = fresh[key];
                    break;
                case UnitOutcomeKind.Adopted:
                    ctx.NewMemory.Units[unitId] = fresh[key];
                    break;
                case UnitOutcomeKind.Unchanged when fresh.ContainsKey(key):
                    ctx.NewMemory.Units[unitId] = fresh[key];
                    break;
                case UnitOutcomeKind.Removed:
                    removed++;
                    ctx.NewMemory.Units.Remove(unitId);
                    break;
                case UnitOutcomeKind.DeletedByHand:
                    workspace.AddNote(target, PatchTexts.UnitDeletedByHand(key));
                    break;
                case UnitOutcomeKind.KeptEditedRemoved:
                    workspace.AddNote(target, PatchTexts.UnitKeptEdited(key));
                    break;
            }

            foreach (var conflict in outcome.Conflicts)
            {
                workspace.AddConflict(target, conflict);
            }

            if (outcome.Kind == UnitOutcomeKind.Conflict)
            {
                ctx.Conflicts.Add(new ConflictUnit(target, ruleId, key, remembered ?? string.Empty, fresh[key], mine[key], outcome.Text ?? mine[key], outcome.Conflicts));
            }

            ctx.RecordUnit(target, unitId);
        }

        if (output.Count == 0 && !region.Found)
        {
            return UnitWriteResult.NotWritten;
        }

        var newLine = workspace.NewLineOf(target);
        if (existing is null && newFileHeader is not null)
        {
            text = newFileHeader + newLine;
        }

        string result;
        try
        {
            result = GeneratedRegion.Write(text, recipeId, ruleId, inside, output, newLine);
        }
        catch (PatchRegionException ex)
        {
            ctx.Warnings.Add(PatchTexts.FileSkipped(target, ex.Message));
            return UnitWriteResult.NotWritten;
        }

        if (result != existing)
        {
            workspace.Set(target, result);
        }

        if (written > 0)
        {
            workspace.AddNote(target, PatchTexts.UnitsWritten(written));
        }

        if (merged > 0)
        {
            workspace.AddNote(target, PatchTexts.UnitsMerged(merged));
        }

        if (removed > 0)
        {
            workspace.AddNote(target, PatchTexts.UnitsRemoved(removed));
        }

        // Konflikte (auch von anderen Regeln) machen die ganze Datei zur Konflikt-Datei, die "Anwenden" nicht schreibt.
        return new UnitWriteResult(true, workspace.ConflictsOf(target).Count > 0, output.Select(u => u.UnitKey).ToHashSet(StringComparer.Ordinal));
    }
}

/// <summary>Ergebnis von <see cref="UnitRegionWriter.WriteUnits"/>: geschrieben, mit Konflikten, welche Bausteine im Bereich stehen.</summary>
public sealed record UnitWriteResult(bool Written, bool HasConflicts, IReadOnlySet<string> Placed)
{
    public static UnitWriteResult NotWritten { get; } = new(false, false, new HashSet<string>());
}
