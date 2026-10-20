namespace ParadoxModTool.Patches;

/// <summary>Führt ein Rezept im Speicher aus und liefert den Plan; schreibt nie.</summary>
public static class PatchRunner
{
    /// <summary>
    /// Mit "Zeilen ganz oder gar nicht" (<see cref="PatchRecipe.WholeRows"/>) läuft das Rezept erneut, solange eine Regel eine weitere Zeile überspringt;
    /// diese Zeilen lassen dann alle Regeln aus.
    /// </summary>
    public static PatchPlan Plan(PatchContext context, PatchRecipe recipe, CancellationToken ct = default, IProgress<(int Done, int Total)>? progress = null)
    {
        var forced = new Dictionary<string, SkippedRow>(StringComparer.Ordinal);
        while (true)
        {
            var (plan, errorRows) = PlanOnce(context, recipe, forced, ct, progress);
            if (!recipe.WholeRows)
            {
                return plan;
            }

            var added = false;
            foreach (var skip in plan.Skipped.Where(s => s.RowKey != "global" && !errorRows.Contains(s.RowKey) && !forced.ContainsKey(s.RowKey)))
            {
                forced[skip.RowKey] = skip;
                added = true;
            }

            if (!added)
            {
                return plan;
            }
        }
    }

    private static void ApplyRule(PatchRule rule, RuleRunContext ctx)
    {
        switch (rule)
        {
            case LineRule line:
                ctx.Warnings.AddRange(LineRuleValidation.Warnings(line));
                LineRuleApplier.Apply(line, ctx);
                break;
            case BlockCopyRule copy:
                BlockCopyApplier.Apply(copy, ctx);
                break;
            case TemplateRule template:
                TemplateRuleApplier.Apply(template, ctx);
                break;
            case ListEntryRule list:
                ListEntryApplier.Apply(list, ctx);
                break;
            case BlockEditRule blockEdit:
                BlockEditApplier.Apply(blockEdit, ctx);
                break;
        }
    }

    private static (PatchPlan Plan, IReadOnlySet<string> ErrorRows) PlanOnce(
        PatchContext context, PatchRecipe recipe, IReadOnlyDictionary<string, SkippedRow> forced, CancellationToken ct, IProgress<(int Done, int Total)>? progress)
    {
        ct.ThrowIfCancellationRequested();
        var workspace = new PatchWorkspace(context.ModRoot, context.VanillaRoot, context.NewFileHasBom);
        var memory = PatchRecipeStore.LoadMemory(context.EffectiveRecipesFolder, recipe.Id);
        var table = ColumnResolver.Resolve(recipe, workspace);
        var ctx = new RuleRunContext(recipe, workspace, table, memory) { ForcedSkips = forced };
        ctx.Warnings.AddRange(table.Warnings);

        var rules = recipe.Rules.Where(r => r.Enabled).ToList();
        for (var i = 0; i < rules.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            // Pfade mit .. oder absolut: die Regel läuft nicht, nichts außerhalb von Mod und Spiel wird gelesen oder geschrieben.
            if (RecipePaths.UnsafePaths(rules[i]) is { Count: > 0 } unsafePaths)
            {
                ctx.Skipped.Add(new SkippedRow(rules[i].Id, "global", PatchTexts.UnsafePath(unsafePaths[0])));
            }
            else if (RecipePaths.TargetOf(rules[i]) is { } target && RecipePaths.IsNotSingleFile(target))
            {
                // "x.txt;" schriebe sonst eine Datei dieses Namens, "*" oder "?" scheiterte erst beim Schreiben.
                ctx.Skipped.Add(new SkippedRow(rules[i].Id, "global", PatchTexts.TargetNotSingleFile(target)));
            }
            else
            {
                try
                {
                    ApplyRule(rules[i], ctx);
                }
                catch (UnsafePathException ex)
                {
                    ctx.Skipped.Add(new SkippedRow(rules[i].Id, "global", PatchTexts.UnsafePath(ex.Path)));
                }
            }

            progress?.Report((i + 1, rules.Count));
        }

        var files = new List<PlannedFile>();
        var conflictUnits = new List<string>();
        var touched = workspace.Touched.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in workspace.ReportedFiles.OrderBy(r => r, StringComparer.Ordinal))
        {
            var original = workspace.Original(rel);
            var text = touched.Contains(rel) ? workspace.Get(rel) ?? string.Empty : original ?? string.Empty;
            var conflicts = workspace.ConflictsOf(rel);
            var status = conflicts.Count > 0 ? PlannedFileStatus.Conflict
                : !touched.Contains(rel) ? PlannedFileStatus.Unchanged
                : original is null ? PlannedFileStatus.New
                : original == text ? PlannedFileStatus.Unchanged
                : PlannedFileStatus.Changed;
            if (status == PlannedFileStatus.Conflict && ctx.UnitsByFile.TryGetValue(rel, out var units))
            {
                conflictUnits.AddRange(units);
            }

            files.Add(new PlannedFile(rel, status, original, text, workspace.HasBomOf(rel), workspace.IsCreatedFromVanilla(rel), workspace.NotesOf(rel), conflicts, workspace.EncodingNameOf(rel)));
        }

        var brokenMemory = PatchRecipeStore.BrokenMemoryText(context.EffectiveRecipesFolder, recipe.Id);
        if (brokenMemory is not null)
        {
            ctx.Warnings.Add(PatchTexts.MemoryBroken(Path.GetFileName(PatchRecipeStore.MemoryPath(context.EffectiveRecipesFolder, recipe.Id))));
        }

        var errorRows = table.Rows.Where(r => r.HasErrors).Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        return (new PatchPlan
        {
            RecipeId = recipe.Id,
            RecipeName = recipe.Name,
            Files = files,
            Skipped = ctx.Skipped,
            Warnings = ctx.Warnings.Distinct(StringComparer.Ordinal).ToList(),
            OldMemory = memory,
            NewMemory = ctx.NewMemory,
            ConflictUnitIds = conflictUnits,
            BrokenMemoryText = brokenMemory,
            ConflictUnits = ctx.Conflicts.ToList(),
            UnitsByFile = ctx.UnitsByFile.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value.Distinct(StringComparer.Ordinal).ToList(), StringComparer.OrdinalIgnoreCase),
            Adopted = ctx.Adopted.ToList(),
        }, errorRows);
    }
}
