using System.IO.Compression;

namespace ParadoxModTool.VersionManager;

/// <summary>
/// Sichert den Stand eines Mod-Ordners als ZIP (Snapshot), z. B. vor dem Vanilla-Merge. Die frühere Verwaltung benannter Dateibäume
/// (<c>.pmt-versions</c>) ist durch Projekte aus mehreren Mod-Ordnern ersetzt (siehe <c>ParadoxModTool.Mods.ModProjects</c>);
/// vorhandene <c>.pmt-versions</c>-Ordner bleiben unangetastet und werden nur aus Snapshots ausgeschlossen.
/// </summary>
public sealed class ModVersionManager
{
    private const string VersionsFolderName = ".pmt-versions";
    private const string SnapshotsFolderName = "snapshots";
    private const string GitFolderName = ".git";

    private readonly string _modRoot;
    private readonly string _snapshotsDir;

    /// <param name="snapshotsDirectory">
    /// Zielordner der Snapshot-ZIPs; <see langword="null"/> = "snapshots/" im Mod-Ordner. Ein Ordner
    /// außerhalb des Mod-Ordners verhindert, dass Steam die Sicherungen mit hochlädt.
    /// </param>
    public ModVersionManager(string modRoot, string? snapshotsDirectory = null)
    {
        _modRoot = modRoot;
        _snapshotsDir = snapshotsDirectory ?? Path.Combine(modRoot, SnapshotsFolderName);
    }

    /// <summary>
    /// Sichert den aktuellen Stand des Mod-Ordners (ohne .pmt-versions/, snapshots/
    /// und .git/) als ZIP-Datei unter snapshots/&lt;Zeitstempel&gt;[_Notiz].zip.
    /// </summary>
    public string CreateSnapshot(string? note = null)
    {
        Directory.CreateDirectory(_snapshotsDir);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd_HHmmss");
        var safeNote = string.IsNullOrWhiteSpace(note) ? string.Empty : "_" + SanitizeForFileName(note);
        var zipPath = Path.Combine(_snapshotsDir, $"{timestamp}{safeNote}.zip");

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        var snapshotsFullDir = Path.GetFullPath(_snapshotsDir) + Path.DirectorySeparatorChar;

        foreach (var file in Directory.EnumerateFiles(_modRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(_modRoot, file);

            if (relative.StartsWith(VersionsFolderName, StringComparison.Ordinal) ||
                relative.StartsWith(SnapshotsFolderName, StringComparison.Ordinal) ||
                relative.StartsWith(StorageLocations.InModHistoryFolderName, StringComparison.Ordinal) ||
                relative.StartsWith(GitFolderName, StringComparison.Ordinal) ||
                Path.GetFullPath(file).StartsWith(snapshotsFullDir, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            archive.CreateEntryFromFile(file, relative.Replace('\\', '/'));
        }

        return zipPath;
    }

    private static string SanitizeForFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = input.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars).Trim().Replace(' ', '-');
    }
}
