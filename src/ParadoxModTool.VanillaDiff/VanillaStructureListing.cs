namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Verzeichnisliste der Vanilla-Dateien und -Ordner einer Spielversion, begrenzt auf die Bereiche, die lokale Mods
/// berühren. Sie hält fest, welche Dateien und Ordner Vanilla in einer älteren Version hatte (z. B. die Datei
/// "main_menu/gui/messagetypes.txt", die ein Patch durch den Ordner "main_menu/gui/messagetypes/" ersetzt).
/// Pfade sind relativ, mit "/" als Trenner, Groß-/Kleinschreibung egal.
/// </summary>
public sealed class VanillaStructureListing
{
    private readonly HashSet<string> _files;
    private readonly HashSet<string> _folders;

    private VanillaStructureListing(HashSet<string> files, HashSet<string> folders)
    {
        _files = files;
        _folders = folders;
    }

    /// <summary>
    /// Die Bereiche einer Mod: die ersten beiden Pfadsegmente jeder Mod-Datei (z. B. "main_menu/gui"), bei Dateien direkt
    /// unter einem obersten Ordner nur dieser. Ordner mit Punkt am Anfang (".git", ".pmt-…") und Dateien im Wurzelordner
    /// zählen nicht, wie bei <see cref="VanillaFileTracker"/>.
    /// </summary>
    public static IReadOnlyCollection<string> ScopesOf(string modRoot)
    {
        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(modRoot))
        {
            return scopes;
        }

        foreach (var modFile in Directory.EnumerateFiles(modRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(modRoot, modFile).Replace('\\', '/');
            if (relative.StartsWith('.') || relative.Contains("/.", StringComparison.Ordinal))
            {
                continue;
            }

            var segments = relative.Split('/');
            if (segments.Length < 2)
            {
                continue;
            }

            scopes.Add(segments.Length >= 3 ? segments[0] + "/" + segments[1] : segments[0]);
        }

        return scopes;
    }

    /// <summary>Liest Dateien und Ordner unterhalb der angegebenen Bereiche (relativ zu <paramref name="vanillaRoot"/>, rekursiv); fehlende Bereiche bleiben leer.</summary>
    public static VanillaStructureListing Scan(string vanillaRoot, IEnumerable<string> topFolders, CancellationToken ct)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var scope in topFolders)
        {
            ct.ThrowIfCancellationRequested();
            var scopePath = scope.Replace('\\', '/').Trim('/');
            var dir = Path.Combine(vanillaRoot, scopePath.Replace('/', Path.DirectorySeparatorChar));
            if (scopePath.Length == 0 || !Directory.Exists(dir))
            {
                continue;
            }

            // Der Bereich selbst und sein Elternordner (bei zwei Segmenten) gelten als vorhanden.
            for (var i = scopePath.IndexOf('/'); i >= 0; i = scopePath.IndexOf('/', i + 1))
            {
                folders.Add(scopePath[..i]);
            }

            folders.Add(scopePath);

            // Ein Bereich mit einem Segment (z. B. "gfx") wäre sehr groß: nur seine Dateien und direkten Unterordner.
            var option = scopePath.Contains('/') ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir, "*", option))
            {
                ct.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(vanillaRoot, entry).Replace('\\', '/');
                (Directory.Exists(entry) ? folders : files).Add(relative);
            }
        }

        return new VanillaStructureListing(files, folders);
    }

    /// <summary>Lädt eine gespeicherte Liste; <c>null</c>, wenn die Datei fehlt oder nicht lesbar ist.</summary>
    public static VanillaStructureListing? Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadLines(path))
            {
                if (line.Length < 3 || line[1] != ' ')
                {
                    continue;
                }

                switch (line[0])
                {
                    case 'F':
                        files.Add(line[2..]);
                        break;
                    case 'D':
                        folders.Add(line[2..]);
                        break;
                }
            }

            return new VanillaStructureListing(files, folders);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Speichert die Liste (eine Zeile je Eintrag: "D Ordner" oder "F Datei", sortiert); legt den Ordner an.</summary>
    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var lines = _folders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).Select(f => "D " + f)
            .Concat(_files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).Select(f => "F " + f));
        // Erst in eine temporäre Datei, damit ein Abbruch keine halbe Liste hinterlässt, die als vorhanden gilt.
        var tmp = path + ".tmp";
        File.WriteAllLines(tmp, lines);
        File.Move(tmp, path, overwrite: true);
    }

    public bool HasFile(string relativePath) => _files.Contains(Normalize(relativePath));

    public bool HasFolder(string relativePath) => _folders.Contains(Normalize(relativePath));

    /// <summary>Alle Dateien mit diesem Dateinamen, an beliebiger Stelle (sortiert).</summary>
    public IReadOnlyList<string> FilesNamed(string fileName) => Named(_files, fileName);

    /// <summary>Alle Ordner mit diesem Namen, an beliebiger Stelle (sortiert).</summary>
    public IReadOnlyList<string> FoldersNamed(string folderName) => Named(_folders, folderName);

    private static IReadOnlyList<string> Named(HashSet<string> paths, string name) =>
        paths.Where(p => string.Equals(p[(p.LastIndexOf('/') + 1)..], name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string Normalize(string path) => path.Replace('\\', '/').Trim('/');
}
