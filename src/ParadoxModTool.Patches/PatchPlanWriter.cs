using System.Text;
using ParadoxModTool.UndoRedo;

namespace ParadoxModTool.Patches;

/// <summary>Macht aus einem Plan einen Rückgängig-Schritt: alle neuen und geänderten Dateien plus das Gedächtnis; Konflikt-Dateien bleiben unberührt.</summary>
public static class PatchPlanWriter
{
    public static IUndoableAction CreateAction(PatchContext context, PatchPlan plan)
    {
        var description = PatchTexts.ApplyUndo(plan.RecipeName);
        var actions = plan.Files
            .Where(f => f.Status is PlannedFileStatus.New or PlannedFileStatus.Changed)
            .Select(f => (IUndoableAction)new FileWriteUndoableAction(description, FullPath(context.ModRoot, f.RelativePath), f.NewText, TextFile.EncodingFor(f.HasBom, f.EncodingName)))
            .ToList();
        if (plan.BrokenMemoryText is { } broken)
        {
            actions.Add(new FileWriteUndoableAction(description, PatchRecipeStore.MemoryPath(context.EffectiveRecipesFolder, plan.RecipeId) + ".broken", broken, new UTF8Encoding(false)));
        }

        actions.Add(new FileWriteUndoableAction(
            description,
            PatchRecipeStore.MemoryPath(context.EffectiveRecipesFolder, plan.RecipeId),
            PatchRecipeStore.SerializeMemory(MemoryToWrite(plan)),
            new UTF8Encoding(false)));
        return new CompositeUndoableAction(description, actions);
    }

    /// <summary>Neue und geänderte Dateien, deren Stand auf der Platte nicht mehr dem der Vorschau entspricht; dann darf der Plan nicht geschrieben werden.</summary>
    public static IReadOnlyList<string> StaleFiles(PatchContext context, PatchPlan plan)
    {
        var stale = new List<string>();
        foreach (var file in plan.Files.Where(f => f.Status is PlannedFileStatus.New or PlannedFileStatus.Changed))
        {
            var path = FullPath(context.ModRoot, file.RelativePath);
            var disk = File.Exists(path) ? TextFile.Read(path).Text : null;
            if (disk != file.OriginalText)
            {
                stale.Add(file.RelativePath);
            }
        }

        return stale;
    }

    /// <summary>Legt nach dem Schreiben für jede aus Vanilla angelegte Kopie einen Vanilla-Snapshot der installierten Spielversion an.</summary>
    public static void RecordSnapshots(PatchContext context, PatchPlan plan)
    {
        if (context.History is null || string.IsNullOrWhiteSpace(context.GameVersion))
        {
            return;
        }

        foreach (var file in plan.Files.Where(f => f.CreatedFromVanilla && f.Status is PlannedFileStatus.New or PlannedFileStatus.Changed))
        {
            var vanilla = FullPath(context.VanillaRoot, file.RelativePath);
            if (File.Exists(vanilla))
            {
                context.History.RecordSnapshot(file.RelativePath, context.GameVersion, vanilla);
            }
        }
    }

    /// <summary>Das neue Gedächtnis, außer für Bausteine und Listeneinträge in Konflikt-Dateien: die behalten ihren alten Stand.</summary>
    private static PatchMemory MemoryToWrite(PatchPlan plan)
    {
        var memory = plan.NewMemory.Clone();
        foreach (var unitId in plan.ConflictUnitIds)
        {
            if (plan.OldMemory.Units.TryGetValue(unitId, out var old))
            {
                memory.Units[unitId] = old;
            }
            else
            {
                memory.Units.Remove(unitId);
            }
        }

        var conflictFiles = plan.Files.Where(f => f.Status == PlannedFileStatus.Conflict).Select(f => f.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (conflictFiles.Count > 0)
        {
            foreach (var ruleId in memory.ListEntries.Keys.Concat(plan.OldMemory.ListEntries.Keys).Distinct(StringComparer.Ordinal).ToList())
            {
                var kept = memory.ListEntries.GetValueOrDefault(ruleId, new List<ListEntryRecord>()).Where(r => !conflictFiles.Contains(r.File));
                var old = plan.OldMemory.ListEntries.GetValueOrDefault(ruleId, new List<ListEntryRecord>()).Where(r => conflictFiles.Contains(r.File));
                memory.ListEntries[ruleId] = kept.Concat(old).ToList();
            }
        }

        return memory;
    }

    private static string FullPath(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
}
