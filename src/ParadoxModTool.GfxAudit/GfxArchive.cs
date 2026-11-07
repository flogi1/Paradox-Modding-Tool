using System.Globalization;
using System.Text.RegularExpressions;

namespace ParadoxModTool.GfxAudit;

/// <summary>Warum eine Datei nicht ausgelagert oder zurückgeholt wurde.</summary>
public enum GfxArchiveFailureReason
{
    /// <summary>Die Datei gibt es nicht (mehr).</summary>
    Missing,

    /// <summary>Der Pfad zeigt aus dem Mod-Ordner (bzw. Archivordner) hinaus.</summary>
    OutsideFolder,

    /// <summary>Keine Bilddatei (<c>.dds</c>, <c>.png</c>, <c>.tga</c>).</summary>
    NotAnImage,

    /// <summary>Beim Zurückholen liegt am alten Ort schon eine Datei.</summary>
    TargetExists,

    /// <summary>Das Dateisystem hat die Aktion abgelehnt.</summary>
    IoError,
}

/// <summary>Eine verschobene Datei, beide Pfade mit Schrägstrichen: beim Auslagern von Mod-Ordner nach Archiv, beim Zurückholen von Archiv nach Mod-Ordner.</summary>
public sealed record GfxArchiveMove(string FromRelative, string ToRelative);

/// <summary>Eine Datei, die nicht verschoben wurde. <paramref name="Relative"/> ist der übergebene Pfad, <paramref name="Message"/> nur bei <see cref="GfxArchiveFailureReason.IoError"/> gesetzt.</summary>
public sealed record GfxArchiveFailure(string Relative, GfxArchiveFailureReason Reason, string? Message);

/// <summary>Ergebnis von <see cref="GfxArchive.MoveOut"/> und <see cref="GfxArchive.Restore"/>.</summary>
public sealed record GfxArchiveResult(IReadOnlyList<GfxArchiveMove> Moved, IReadOnlyList<GfxArchiveFailure> Failed);

/// <summary>Eine ausgelagerte Bilddatei. <paramref name="LastWriteUtc"/> ist der letzte Schreibzeitpunkt der Datei selbst (beim Verschieben bleibt er erhalten), nicht der Zeitpunkt des Auslagerns. <paramref name="OriginalExists"/>: Am alten Ort liegt inzwischen wieder eine Datei, Zurückholen wäre blockiert.</summary>
public sealed record GfxArchivedFile(string ArchiveRelative, string FullPath, string OriginalRelative, bool OriginalExists, long Size, DateTime LastWriteUtc);

/// <summary>
/// Lagert ungenutzte Bilder einer lokalen Mod in den Ordner <c>&lt;Mod&gt;_gfx_archiv</c> neben der Mod aus und wieder zurück, ohne Oberfläche.
/// Nichts wird überschrieben oder gelöscht: Bei Namensgleichheit im Archiv bekommt die Datei ein Datum angehängt, und entfernt werden nur Ordner, die leer geworden sind.
/// </summary>
public sealed class GfxArchive
{
    /// <summary>Endung des Archivordners, angehängt an den Namen des Mod-Ordners.</summary>
    public const string ArchiveSuffix = "_gfx_archiv";

    private static readonly string[] ImageExtensions = { ".dds", ".png", ".tga" };

    private static readonly Regex DateSuffix = new(@"~\d{8}(-\d+)?$", RegexOptions.Compiled);

    private readonly Func<DateTime> _now;
    private readonly Action<string, string> _move;

    public GfxArchive(string modDirectory)
        : this(modDirectory, () => DateTime.Now, (from, to) => File.Move(from, to))
    {
    }

    /// <summary>Für Tests: eigene Uhr und eigene Verschiebe-Aktion (darf <see cref="IOException"/> werfen).</summary>
    internal GfxArchive(string modDirectory, Func<DateTime> now, Action<string, string> move)
    {
        ModDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(modDirectory));
        ArchiveDirectory = Path.Combine(Path.GetDirectoryName(ModDirectory)!, Path.GetFileName(ModDirectory) + ArchiveSuffix);
        _now = now;
        _move = move;
    }

    public string ModDirectory { get; }

    public string ArchiveDirectory { get; }

    /// <summary>Ob <paramref name="modDirectory"/> echt unterhalb des Mod-Ordners <paramref name="modRoot"/> des Spiels liegt (nicht Workshop, nicht der Ordner selbst).</summary>
    public static bool IsLocalMod(string modDirectory, string? modRoot)
    {
        if (string.IsNullOrWhiteSpace(modRoot))
        {
            return false;
        }

        var mod = Path.TrimEndingDirectorySeparator(Path.GetFullPath(modDirectory));
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(modRoot));
        return mod.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ob ein Ordnername ein Archivordner ist (endet auf <see cref="ArchiveSuffix"/>, Groß-/Kleinschreibung egal); solche Ordner sind keine Mods.</summary>
    public static bool IsArchiveFolder(string folderName) => folderName.EndsWith(ArchiveSuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Der Pfad ohne das Datum, das bei Namensgleichheit an den Dateinamen kommt: <c>a~20261106-2.dds</c> wird zu <c>a.dds</c>.</summary>
    public static string OriginalName(string archiveRelative)
    {
        var extension = Path.GetExtension(archiveRelative);
        var stem = archiveRelative[..^extension.Length];
        return DateSuffix.Replace(stem, string.Empty) + extension;
    }

    /// <summary>Verschiebt die Bilder (Pfade relativ zum Mod-Ordner) ins Archiv, Unterordner bleiben erhalten. Fehler einzelner Dateien stoppen die anderen nicht.</summary>
    public GfxArchiveResult MoveOut(IReadOnlyList<string> modRelativePaths)
    {
        var moved = new List<GfxArchiveMove>();
        var failed = new List<GfxArchiveFailure>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relative in modRelativePaths)
        {
            var source = Resolve(ModDirectory, relative);
            if (source is null)
            {
                failed.Add(new GfxArchiveFailure(relative, GfxArchiveFailureReason.OutsideFolder, null));
                continue;
            }

            if (!seen.Add(source))
            {
                continue;
            }

            if (!IsImage(source))
            {
                failed.Add(new GfxArchiveFailure(relative, GfxArchiveFailureReason.NotAnImage, null));
                continue;
            }

            if (!File.Exists(source))
            {
                failed.Add(new GfxArchiveFailure(relative, GfxArchiveFailureReason.Missing, null));
                continue;
            }

            var from = Slashed(Path.GetRelativePath(ModDirectory, source));
            string? created = null;
            string? targetFolder = null;
            try
            {
                var target = FreeTarget(Path.Combine(ArchiveDirectory, from.Replace('/', Path.DirectorySeparatorChar)));
                targetFolder = Path.GetDirectoryName(target)!;
                created = CreateFolder(targetFolder);
                _move(source, target);
                moved.Add(new GfxArchiveMove(from, Slashed(Path.GetRelativePath(ArchiveDirectory, target))));
                RemoveEmptyFolders(Path.GetDirectoryName(source)!, ModDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                UndoCreatedFolders(targetFolder, created);
                failed.Add(new GfxArchiveFailure(relative, GfxArchiveFailureReason.IoError, ex.Message));
            }
        }

        return new GfxArchiveResult(moved, failed);
    }

    /// <summary>Alle ausgelagerten Bilder, nach Pfad sortiert. Leer, wenn es den Archivordner nicht gibt.</summary>
    public IReadOnlyList<GfxArchivedFile> List()
    {
        var files = new List<GfxArchivedFile>();
        if (!Directory.Exists(ArchiveDirectory))
        {
            return files;
        }

        foreach (var path in Directory.EnumerateFiles(ArchiveDirectory, "*", SearchOption.AllDirectories).Where(IsImage))
        {
            var relative = Slashed(Path.GetRelativePath(ArchiveDirectory, path));
            var original = OriginalName(relative);
            var info = new FileInfo(path);
            var originalExists = File.Exists(Path.Combine(ModDirectory, original.Replace('/', Path.DirectorySeparatorChar)));
            files.Add(new GfxArchivedFile(relative, path, original, originalExists, info.Length, info.LastWriteTimeUtc));
        }

        files.Sort((a, b) => string.Compare(a.ArchiveRelative, b.ArchiveRelative, StringComparison.OrdinalIgnoreCase));
        return files;
    }

    /// <summary>Holt Bilder (Pfade relativ zum Archivordner) an ihren alten Ort zurück. Liegt dort schon eine Datei, bleibt das Bild im Archiv.</summary>
    public GfxArchiveResult Restore(IReadOnlyList<string> archiveRelativePaths)
    {
        var moved = new List<GfxArchiveMove>();
        var failed = new List<GfxArchiveFailure>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relative in archiveRelativePaths)
        {
            var source = Resolve(ArchiveDirectory, relative);
            if (source is null)
            {
                failed.Add(new GfxArchiveFailure(relative, GfxArchiveFailureReason.OutsideFolder, null));
                continue;
            }

            if (!seen.Add(source))
            {
                continue;
            }

            if (!IsImage(source))
            {
                failed.Add(new GfxArchiveFailure(relative, GfxArchiveFailureReason.NotAnImage, null));
                continue;
            }

            if (!File.Exists(source))
            {
                failed.Add(new GfxArchiveFailure(relative, GfxArchiveFailureReason.Missing, null));
                continue;
            }

            var from = Slashed(Path.GetRelativePath(ArchiveDirectory, source));
            var original = OriginalName(from);
            var target = Resolve(ModDirectory, original);
            if (target is null)
            {
                failed.Add(new GfxArchiveFailure(relative, GfxArchiveFailureReason.OutsideFolder, null));
                continue;
            }

            if (File.Exists(target) || Directory.Exists(target))
            {
                failed.Add(new GfxArchiveFailure(relative, GfxArchiveFailureReason.TargetExists, null));
                continue;
            }

            string? created = null;
            var targetFolder = Path.GetDirectoryName(target)!;
            try
            {
                created = CreateFolder(targetFolder);
                _move(source, target);
                moved.Add(new GfxArchiveMove(from, original));
                RemoveEmptyFolders(Path.GetDirectoryName(source)!, ArchiveDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                UndoCreatedFolders(targetFolder, created);
                failed.Add(new GfxArchiveFailure(relative, GfxArchiveFailureReason.IoError, ex.Message));
            }
        }

        if (moved.Count > 0)
        {
            RemoveIfEmpty(ArchiveDirectory);
        }

        return new GfxArchiveResult(moved, failed);
    }

    /// <summary>Voller Pfad von <paramref name="relative"/> unter <paramref name="root"/>, oder null, wenn er nicht strikt darunter liegt (auch bei absoluten Pfaden und <c>..</c>).</summary>
    private static string? Resolve(string root, string relative)
    {
        try
        {
            var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static bool IsImage(string path) => ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private static string Slashed(string path) => path.Replace('\\', '/');

    /// <summary>Der Zielpfad selbst, wenn frei, sonst mit Datum (<c>a~20261106.dds</c>, dann <c>a~20261106-2.dds</c> usw.) vor der Endung.</summary>
    private string FreeTarget(string target)
    {
        if (!File.Exists(target) && !Directory.Exists(target))
        {
            return target;
        }

        var extension = Path.GetExtension(target);
        var stem = target[..^extension.Length] + "~" + _now().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        for (var n = 1; ; n++)
        {
            var candidate = stem + (n == 1 ? string.Empty : "-" + n.ToString(CultureInfo.InvariantCulture)) + extension;
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>Legt <paramref name="folder"/> an und gibt den obersten dabei neu entstandenen Ordner zurück (null, wenn schon alles da war).</summary>
    private static string? CreateFolder(string folder)
    {
        string? top = null;
        for (var current = folder; !Directory.Exists(current); current = Path.GetDirectoryName(current)!)
        {
            top = current;
        }

        Directory.CreateDirectory(folder);
        return top;
    }

    /// <summary>Räumt nach einer fehlgeschlagenen Verschiebung die dafür neu angelegten Ordner wieder weg, soweit sie leer sind.</summary>
    private static void UndoCreatedFolders(string? folder, string? created)
    {
        if (folder is not null && created is not null)
        {
            RemoveEmptyFolders(folder, Path.GetDirectoryName(created)!);
        }
    }

    /// <summary>Entfernt <paramref name="folder"/> und seine Eltern bis <paramref name="stopAt"/> (bleibt immer), solange sie leer sind.</summary>
    private static void RemoveEmptyFolders(string folder, string stopAt)
    {
        var current = Path.TrimEndingDirectorySeparator(folder);
        while (current.StartsWith(stopAt + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && RemoveIfEmpty(current))
        {
            current = Path.GetDirectoryName(current)!;
        }
    }

    private static bool RemoveIfEmpty(string folder)
    {
        try
        {
            if (!Directory.Exists(folder) || Directory.EnumerateFileSystemEntries(folder).Any())
            {
                return false;
            }

            Directory.Delete(folder, false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
