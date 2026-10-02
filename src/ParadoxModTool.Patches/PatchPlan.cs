using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Patches;

/// <summary>
/// Wo ein Rezept läuft: Mod- und Spielwurzel, installierte Spielversion, Vanilla-Historie für neu angelegte Kopien und der Rezeptordner
/// mit dem Gedächtnis (ohne Angabe der frühere Ort im Mod-Ordner). <see cref="HiddenVanillaFolders"/>: Ordner, deren Vanilla-Dateien die Mod per
/// <c>replace_path</c> nicht laden lässt (mit <see cref="ReplacePathRoots"/> wie im Spielprofil); der Wächter durchsucht sie nicht.
/// </summary>
public sealed record PatchContext(
    string ModRoot,
    string VanillaRoot,
    string? GameVersion,
    VanillaHistoryStore? History,
    string? RecipesFolder = null,
    IReadOnlyList<string>? HiddenVanillaFolders = null,
    IReadOnlyCollection<string>? ReplacePathRoots = null)
{
    public string EffectiveRecipesFolder => RecipesFolder ?? PatchRecipeStore.Folder(ModRoot);
}

public enum PlannedFileStatus
{
    New,
    Changed,
    Conflict,
    Unchanged,
}

/// <summary>Eine Datei im Plan mit altem und neuem Text, Notizen und Konflikten.</summary>
public sealed record PlannedFile(
    string RelativePath,
    PlannedFileStatus Status,
    string? OriginalText,
    string NewText,
    bool HasBom,
    bool CreatedFromVanilla,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> Conflicts,
    string EncodingName = TextFile.Utf8);

/// <summary>Ergebnis von <see cref="PatchRunner.Plan"/>: was ein Rezeptlauf ändern würde; geschrieben wird erst über <see cref="PatchPlanWriter"/>.</summary>
public sealed class PatchPlan
{
    public required string RecipeId { get; init; }

    public required string RecipeName { get; init; }

    public required IReadOnlyList<PlannedFile> Files { get; init; }

    public required IReadOnlyList<SkippedRow> Skipped { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    public required PatchMemory OldMemory { get; init; }

    public required PatchMemory NewMemory { get; init; }

    /// <summary>Gedächtnis-Ids der Bausteine in Konflikt-Dateien; sie behalten beim Schreiben ihren alten Stand.</summary>
    public required IReadOnlyList<string> ConflictUnitIds { get; init; }

    /// <summary>Die Bausteine mit Konflikt samt ihrer drei Fassungen, damit das Fenster sie einzeln lösen kann.</summary>
    public IReadOnlyList<ConflictUnit> ConflictUnits { get; init; } = Array.Empty<ConflictUnit>();

    /// <summary>Inhalt einer unlesbaren Gedächtnis-Datei; beim Schreiben wird er als <c>.broken</c> gesichert, bevor das neue Gedächtnis sie ersetzt.</summary>
    public string? BrokenMemoryText { get; init; }

    /// <summary>Blöcke, die "Bestehende Blöcke übernehmen" aus ihrer Datei in den erzeugten Bereich holt (Prüfung vor dem Anwenden).</summary>
    public IReadOnlyList<AdoptedBlock> Adopted { get; init; } = Array.Empty<AdoptedBlock>();

    /// <summary>Welche Bausteine (Gedächtnis-Ids) in welcher Datei stehen.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> UnitsByFile { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
}

/// <summary>
/// Ein Baustein mit Konflikt: <see cref="Base"/> ist das Gedächtnis, <see cref="Fresh"/> das neu Erzeugte, <see cref="Mine"/> der Dateistand,
/// <see cref="Merged"/> das Merge-Ergebnis (an Konfliktstellen gilt das neu Erzeugte).
/// </summary>
public sealed record ConflictUnit(string File, string RuleId, string UnitKey, string Base, string Fresh, string Mine, string Merged, IReadOnlyList<string> Messages);

/// <summary>Ein übernommener Block: Schlüssel, die Datei, aus der er entfernt wird, und die Zieldatei mit dem erzeugten Bereich.</summary>
public sealed record AdoptedBlock(string Key, string SourceFile, string TargetFile);

/// <summary>Hilfen, um den Status einer Plan-Datei zu bestimmen.</summary>
public static class PlannedFileStatuses
{
    public static PlannedFileStatus For(string? originalText, string newText, bool hasConflicts) =>
        hasConflicts ? PlannedFileStatus.Conflict
        : originalText is null ? PlannedFileStatus.New
        : originalText == newText ? PlannedFileStatus.Unchanged
        : PlannedFileStatus.Changed;
}
