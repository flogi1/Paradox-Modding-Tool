using ParadoxModTool.Patches;

namespace ParadoxModTool.Generators;

/// <summary>
/// Wohin ein Lauf schreibt: <see cref="OutputFolder"/> <see langword="null"/> = in die Mod; <see cref="AllLanguages"/> = Sprachen des Spielprofils.
/// <see cref="ExternalPaths"/>: gewählte Pfade der externen Dateien des Pakets (Kennung → absoluter Pfad), siehe <see cref="ExternalPathStore"/>.
/// </summary>
public sealed record GeneratorTarget(string ModRoot, string? OutputFolder, IReadOnlyList<string>? AllLanguages = null, IReadOnlyDictionary<string, string>? ExternalPaths = null)
{
    /// <summary>
    /// Ob neue Textdateien mit dieser Endung (mit Punkt) ein BOM bekommen, aus dem Spielprofil (Stellaris: Skript, .gui und .gfx ohne).
    /// Ohne Angabe bekommen sie eins; Lokalisierung bekommt immer eins, bestehende Dateien behalten ihres.
    /// </summary>
    public Func<string, bool>? NewFileHasBom { get; init; }
}

/// <summary>
/// Eine Datei im Plan. Textausgaben haben <see cref="NewText"/>, Binärdateien <see cref="Bytes"/>. <see cref="OriginalText"/>/<see cref="OriginalBytes"/>
/// sind der Stand am Schreibort (<see langword="null"/> = Datei fehlt dort); daran prüft der Writer, ob sich die Datei seit der Vorschau geändert hat.
/// <see cref="ModBaseText"/>: beim Ziel "Ausgabeordner" ohne Kopie dort der Text der Mod-Datei, auf dem die Ausgabe aufbaut (nur für die Vorschau).
/// <see cref="ExternalPath"/>: voller Pfad einer externen Datei außerhalb von Mod und Ausgabeordner (dann ist <see cref="RelativePath"/> nur <c>extern/&lt;Name&gt;</c> zur Anzeige).
/// </summary>
public sealed record GeneratorPlannedFile(
    string RelativePath,
    PlannedFileStatus Status,
    string? OriginalText,
    string? NewText,
    byte[]? Bytes,
    byte[]? OriginalBytes,
    bool HasBom,
    string EncodingName,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> RemovedBlocks,
    int LegacyRemoved = 0,
    string? ModBaseText = null,
    string? ExternalPath = null);

/// <summary>Was ein Lauf schreiben würde; geschrieben wird erst über <see cref="GeneratorPlanWriter"/>.</summary>
public sealed class GeneratorPlan
{
    public required string PackageId { get; init; }

    public required string Key { get; init; }

    public required string WriteRoot { get; init; }

    public required IReadOnlyList<GeneratorPlannedFile> Files { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>Der Plan mit zusätzlichen Warnungen (etwa Hash-Kollisionen); Dateien bleiben gleich.</summary>
    public GeneratorPlan WithWarnings(IEnumerable<string> extra) => new()
    {
        PackageId = PackageId,
        Key = Key,
        WriteRoot = WriteRoot,
        Files = Files,
        Warnings = Warnings.Concat(extra).ToList(),
    };

    /// <summary>Der Plan mit geändertem Text einer Datei (Vorschau: Änderung verworfen); der Status folgt dem neuen Text.</summary>
    public GeneratorPlan WithEditedFile(string relativePath, string newText) => new()
    {
        PackageId = PackageId,
        Key = Key,
        WriteRoot = WriteRoot,
        Warnings = Warnings,
        Files = Files.Select(f => f.RelativePath != relativePath ? f : f with
        {
            NewText = newText,
            Status = f.OriginalText is null ? PlannedFileStatus.New : f.OriginalText == newText ? PlannedFileStatus.Unchanged : PlannedFileStatus.Changed,
        }).ToList(),
    };
}
