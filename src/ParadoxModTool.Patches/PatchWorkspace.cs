namespace ParadoxModTool.Patches;

/// <summary>
/// Arbeitskopie der Mod-Dateien im Speicher: Regeln lesen und ändern hier, geschrieben wird erst über den Plan.
/// Pfade sind relativ zur Mod- bzw. Spielwurzel und nutzen <c>/</c>.
/// </summary>
public sealed class PatchWorkspace
{
    private readonly string _modRoot;
    private readonly string _vanillaRoot;
    private readonly Dictionary<string, TextFileContent?> _disk = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _current = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _created = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextFileContent> _createdFormat = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _touched = new();
    private readonly Dictionary<string, IReadOnlyList<string>> _vanillaGlobs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _notes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _conflicts = new(StringComparer.OrdinalIgnoreCase);

    public PatchWorkspace(string modRoot, string vanillaRoot)
    {
        _modRoot = modRoot;
        _vanillaRoot = vanillaRoot;
    }

    public string ModRoot => _modRoot;

    public string VanillaRoot => _vanillaRoot;

    /// <summary>Geänderte oder neu angelegte Dateien in der Reihenfolge ihrer ersten Änderung.</summary>
    public IReadOnlyCollection<string> Touched => _touched;

    /// <summary>Aktueller Text; null, wenn die Datei weder auf der Platte noch im Speicher existiert.</summary>
    public string? Get(string rel)
    {
        rel = Normalize(rel);
        return _current.TryGetValue(rel, out var text) ? text : Disk(rel)?.Text;
    }

    public void Set(string rel, string text)
    {
        rel = Normalize(rel);
        Touch(rel);
        _current[rel] = text;
    }

    /// <summary>Legt die Mod-Kopie aus Vanilla an; false, wenn die Vanilla-Datei fehlt.</summary>
    public bool CreateFromVanilla(string rel)
    {
        rel = Normalize(rel);
        var vanillaPath = FullPath(_vanillaRoot, rel);
        if (!File.Exists(vanillaPath))
        {
            return false;
        }

        var vanilla = TextFile.Read(vanillaPath);
        _created.Add(rel);
        _createdFormat[rel] = vanilla;
        Set(rel, vanilla.Text);
        return true;
    }

    public bool IsCreatedFromVanilla(string rel) => _created.Contains(Normalize(rel));

    /// <summary>Text auf der Platte; null bei neuen Dateien.</summary>
    public string? Original(string rel) => Disk(Normalize(rel))?.Text;

    public string NewLineOf(string rel) => Format(rel)?.NewLine ?? "\r\n";

    /// <summary>Neue Dateien bekommen ein BOM; Kopien aus Vanilla übernehmen Zeilenende, BOM und Kodierung der Vanilla-Datei.</summary>
    public bool HasBomOf(string rel) => Format(rel)?.HasBom ?? true;

    public string EncodingNameOf(string rel) => Format(rel)?.EncodingName ?? TextFile.Utf8;

    private TextFileContent? Format(string rel)
    {
        rel = Normalize(rel);
        return Disk(rel) ?? (_createdFormat.TryGetValue(rel, out var created) ? created : null);
    }

    public IEnumerable<string> ModFiles(string glob) =>
        GlobMatcher.Enumerate(_modRoot, glob)
            .Concat(_current.Keys.Where(rel => GlobMatcher.IsMatch(glob, rel)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(rel => Get(rel) is not null)
            .OrderBy(rel => rel, StringComparer.Ordinal)
            .ToList();

    /// <summary>Vanilla ändert sich während eines Laufs nicht; die Ergebnisse werden je Muster gemerkt.</summary>
    public IEnumerable<string> VanillaFiles(string glob)
    {
        if (!_vanillaGlobs.TryGetValue(glob, out var files))
        {
            files = GlobMatcher.Enumerate(_vanillaRoot, glob).ToList();
            _vanillaGlobs[glob] = files;
        }

        return files;
    }

    public string? ReadVanilla(string rel)
    {
        var path = FullPath(_vanillaRoot, Normalize(rel));
        return File.Exists(path) ? TextFile.Read(path).Text : null;
    }

    public void AddNote(string rel, string note) => ListFor(_notes, Normalize(rel)).Add(note);

    public IReadOnlyList<string> NotesOf(string rel) => _notes.TryGetValue(Normalize(rel), out var list) ? list : Array.Empty<string>();

    public void AddConflict(string rel, string conflict) => ListFor(_conflicts, Normalize(rel)).Add(conflict);

    public IReadOnlyList<string> ConflictsOf(string rel) => _conflicts.TryGetValue(Normalize(rel), out var list) ? list : Array.Empty<string>();

    /// <summary>Geänderte Dateien plus Dateien mit Notizen oder Konflikten, auch wenn ihr Text gleich blieb (z. B. von Hand gelöschte Bausteine).</summary>
    public IEnumerable<string> ReportedFiles =>
        _touched.Concat(_notes.Keys).Concat(_conflicts.Keys).Distinct(StringComparer.OrdinalIgnoreCase);

    private TextFileContent? Disk(string rel)
    {
        if (!_disk.TryGetValue(rel, out var content))
        {
            var path = FullPath(_modRoot, rel);
            content = File.Exists(path) ? TextFile.Read(path) : null;
            _disk[rel] = content;
        }

        return content;
    }

    private void Touch(string rel)
    {
        if (!_touched.Contains(rel, StringComparer.OrdinalIgnoreCase))
        {
            _touched.Add(rel);
        }
    }

    private static List<string> ListFor(Dictionary<string, List<string>> map, string rel)
    {
        if (!map.TryGetValue(rel, out var list))
        {
            list = new List<string>();
            map[rel] = list;
        }

        return list;
    }

    /// <summary>Ein führender Schrägstrich (z. B. im Ziel einer Regel) zählt nicht: sonst gäbe es dieselbe Datei zweimal im Plan.</summary>
    private static string Normalize(string rel)
    {
        rel = (rel ?? string.Empty).Replace('\\', '/').TrimStart('/');
        // Rückhalt: die Regeln werden schon vor dem Lauf geprüft (RecipePaths); hier kommt nichts durch, was Mod- oder Spielordner verlässt.
        return PathSafety.IsSafeRelative(rel) ? rel : throw new UnsafePathException(rel);
    }

    private static string FullPath(string root, string rel) => Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
}
