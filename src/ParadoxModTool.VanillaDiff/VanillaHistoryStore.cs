namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Optionale Versionshistorie der von einem Mod betroffenen Vanilla-Dateien über
/// Spiel-Patches hinweg (Abschnitt 7, zweiter Punkt). Die Historie wird — wie der
/// Mod-Versionsmanager (Abschnitt 6) — im Mod-Ordner selbst abgelegt, unter
/// ".pmt-vanilla-history/&lt;Dateipfad&gt;/&lt;Spielversion&gt;.txt", da sie sich auf die
/// jeweils betrachtete Mod-Installation bezieht (welche Vanilla-Dateien relevant sind, hängt
/// vom Mod ab) und nicht in den — meist ohnehin nicht beschreibbaren — Spielordner geschrieben
/// werden soll (z. B. Steam-Bibliothek).
/// </summary>
public sealed class VanillaHistoryStore
{
    private const string HistoryFolderName = ".pmt-vanilla-history";

    private readonly string _historyRoot;

    public VanillaHistoryStore(string modRoot)
    {
        _historyRoot = Path.Combine(modRoot, HistoryFolderName);
    }

    private VanillaHistoryStore(string historyRoot, bool _)
    {
        _historyRoot = historyRoot;
    }

    /// <summary>
    /// Legt die Historie direkt unter <paramref name="historyRoot"/> ab (z. B. "EU V Vanilla History"
    /// außerhalb des Mod-Ordners), statt fest in ".pmt-vanilla-history" im Mod-Ordner.
    /// </summary>
    public static VanillaHistoryStore FromHistoryRoot(string historyRoot) => new(historyRoot, false);

    /// <summary>Speichert den aktuellen Inhalt einer Vanilla-Datei als Snapshot für die angegebene Spielversion.</summary>
    public void RecordSnapshot(string relativePath, string gameVersion, string vanillaFileFullPath)
    {
        if (!File.Exists(vanillaFileFullPath))
        {
            throw new FileNotFoundException(VanillaDiffTexts.VanillaFileMissing, vanillaFileFullPath);
        }

        var targetDir = Path.Combine(_historyRoot, SanitizeRelativePathForFolder(relativePath));
        Directory.CreateDirectory(targetDir);

        var targetFile = Path.Combine(targetDir, SanitizeForFileName(gameVersion) + ".txt");
        File.Copy(vanillaFileFullPath, targetFile, overwrite: true);
    }

    /// <summary>Listet alle für eine Datei gespeicherten Spielversionen auf (alphabetisch sortiert).</summary>
    public IReadOnlyList<string> ListRecordedVersions(string relativePath)
    {
        var dir = Path.Combine(_historyRoot, SanitizeRelativePathForFolder(relativePath));

        if (!Directory.Exists(dir))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(dir, "*.txt")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Liest den gespeicherten Snapshot einer Datei für eine bestimmte Spielversion.</summary>
    public string ReadSnapshot(string relativePath, string gameVersion)
    {
        var file = Path.Combine(_historyRoot, SanitizeRelativePathForFolder(relativePath), SanitizeForFileName(gameVersion) + ".txt");

        if (!File.Exists(file))
        {
            throw new FileNotFoundException(VanillaDiffTexts.NoSnapshot(relativePath, gameVersion), file);
        }

        return File.ReadAllText(file);
    }

    /// <summary>Datei des Snapshots (zum Kopieren ohne Umkodieren); ob sie existiert, sagt <see cref="HasSnapshot"/>.</summary>
    public string SnapshotPath(string relativePath, string gameVersion) =>
        Path.Combine(_historyRoot, SanitizeRelativePathForFolder(relativePath), SanitizeForFileName(gameVersion) + ".txt");

    public bool HasSnapshot(string relativePath, string gameVersion) =>
        File.Exists(Path.Combine(_historyRoot, SanitizeRelativePathForFolder(relativePath), SanitizeForFileName(gameVersion) + ".txt"));

    private static string SanitizeRelativePathForFolder(string relativePath) =>
        relativePath.Replace('\\', '/').Replace('/', '_');

    private static string SanitizeForFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = input.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars).Trim().Replace(' ', '-');
    }
}
