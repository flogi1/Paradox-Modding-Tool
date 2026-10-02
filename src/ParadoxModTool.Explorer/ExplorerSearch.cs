using System.IO;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Explorer;

/// <summary>
/// Namensmuster für die Suche in der Verzeichnis-Übersicht (Nutzer-Feedback: "Suche nach Datei- und
/// Ordnernamen"). Ohne Platzhalter wird der Text als Teilstring gesucht, mit <c>*</c> (beliebig viele
/// Zeichen) oder <c>?</c> (genau ein Zeichen) muss das ganze Namensmuster passen. Immer ohne
/// Beachtung der Groß-/Kleinschreibung.
/// </summary>
public sealed class NamePattern
{
    private readonly string _text;
    private readonly Regex? _regex;

    private NamePattern(string text, Regex? regex)
    {
        _text = text;
        _regex = regex;
    }

    /// <summary>Erzeugt ein Muster; <see langword="null"/> bei leerem/nur aus Leerzeichen bestehendem Text.</summary>
    public static NamePattern? Parse(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.IndexOfAny(new[] { '*', '?' }) < 0)
        {
            return new NamePattern(trimmed, null);
        }

        var regexText = "^" + Regex.Escape(trimmed).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return new NamePattern(trimmed, new Regex(regexText, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline));
    }

    public bool IsMatch(string name) =>
        _regex is null
            ? name.Contains(_text, StringComparison.OrdinalIgnoreCase)
            : _regex.IsMatch(name);
}

/// <summary>Ein Treffer der Namenssuche.</summary>
public sealed class ExplorerSearchHit
{
    public required string FullPath { get; init; }

    public required string Name { get; init; }

    public required bool IsDirectory { get; init; }

    /// <summary>Lesbarer Speicherort: Name der Wurzel plus relativer Pfad des übergeordneten Ordners.</summary>
    public required string Location { get; init; }

    public string Icon => IsDirectory ? "📁" : "📄";
}

/// <summary>Ergebnis von <see cref="ExplorerSearch.Run"/>; <see cref="Truncated"/> = es gab mehr Treffer als das Limit.</summary>
public sealed class ExplorerSearchResult
{
    public required IReadOnlyList<ExplorerSearchHit> Hits { get; init; }

    public required bool Truncated { get; init; }
}

/// <summary>
/// Rekursive Namenssuche über alle Wurzeln der Verzeichnis-Übersicht. Läuft breitenorientiert (flache
/// Treffer zuerst), bricht bei Erreichen des Limits bzw. auf Abbruchwunsch sofort ab und übergeht
/// nicht lesbare Ordner sowie Verzeichnisverknüpfungen (Schutz vor Endlosschleifen) - dieselbe
/// Robustheit wie <see cref="FolderScanner"/>. Reine Dateisystem-Logik ohne UI, damit testbar.
/// </summary>
public static class ExplorerSearch
{
    public const int DefaultLimit = 500;

    /// <param name="roots">Wurzeln als (Pfad, Anzeigename).</param>
    /// <param name="foldersOnly">Nur Ordner als Treffer (Dateien werden nicht geprüft).</param>
    /// <param name="hideUgcReferences">Workshop-Verweise (<see cref="UgcReference"/>) nie als Treffer.</param>
    public static ExplorerSearchResult Run(
        IEnumerable<(string Path, string DisplayName)> roots,
        NamePattern pattern,
        int limit,
        CancellationToken cancellationToken,
        bool foldersOnly = false,
        bool hideUgcReferences = false)
    {
        var hits = new List<ExplorerSearchHit>();

        foreach (var (rootPath, displayName) in roots)
        {
            if (!Directory.Exists(rootPath))
            {
                continue;
            }

            var queue = new Queue<string>();
            queue.Enqueue(rootPath);

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var directory = queue.Dequeue();
                var location = LocationOf(rootPath, displayName, directory);

                foreach (var entry in Enumerate(directory))
                {
                    var name = System.IO.Path.GetFileName(entry.Path);

                    if ((entry.IsDirectory || (!foldersOnly && !(hideUgcReferences && UgcReference.IsMatch(name)))) && pattern.IsMatch(name))
                    {
                        if (hits.Count >= limit)
                        {
                            return new ExplorerSearchResult { Hits = hits, Truncated = true };
                        }

                        hits.Add(new ExplorerSearchHit
                        {
                            FullPath = entry.Path,
                            Name = name,
                            IsDirectory = entry.IsDirectory,
                            Location = location,
                        });
                    }

                    if (entry.IsDirectory)
                    {
                        queue.Enqueue(entry.Path);
                    }
                }
            }
        }

        return new ExplorerSearchResult { Hits = hits, Truncated = false };
    }

    private static string LocationOf(string rootPath, string rootName, string directory)
    {
        var relative = System.IO.Path.GetRelativePath(rootPath, directory);
        return relative == "." ? rootName : System.IO.Path.Combine(rootName, relative);
    }

    /// <summary>Unterordner (ohne Verknüpfungen) zuerst, dann Dateien; nicht lesbare Ordner liefern nichts.</summary>
    private static IEnumerable<(string Path, bool IsDirectory)> Enumerate(string directory)
    {
        var result = new List<(string, bool)>();

        try
        {
            foreach (var subDirectory in Directory.EnumerateDirectories(directory))
            {
                if ((File.GetAttributes(subDirectory) & FileAttributes.ReparsePoint) == 0)
                {
                    result.Add((subDirectory, true));
                }
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                result.Add((file, false));
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PathTooLongException)
        {
            // Ordner nicht lesbar - bis hierhin gesammelte Einträge bleiben erhalten.
        }

        return result;
    }
}
