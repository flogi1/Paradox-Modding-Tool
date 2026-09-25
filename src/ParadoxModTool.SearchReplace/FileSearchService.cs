using System.IO;
using System.Linq;
using System.Text;

namespace ParadoxModTool.SearchReplace;

/// <summary>Ein Suchtreffer innerhalb einer Datei (Abschnitt 5b, "Suchen in Dateien").</summary>
public sealed class FileSearchMatch
{
    public FileSearchMatch(string filePath, TextMatch match)
    {
        FilePath = filePath;
        Match = match;
    }

    public string FilePath { get; }

    public TextMatch Match { get; }
}

/// <summary>
/// Durchsucht eine Ordnerstruktur (z. B. den lokalen Mod-Ordner) nach einem Suchbegriff
/// (Abschnitt 5b: "Es muss auch eine Funktion Suchen in Dateien geben"). Rein dateibasiert und
/// UI-unabhängig, damit dieses Projekt automatisiert getestet werden kann.
/// </summary>
public static class FileSearchService
{
    /// <summary>
    /// Dateiendungen, die laut Architektur-Spezifikation in Paradox-Mods vorkommen und daher
    /// standardmäßig durchsucht werden (Abschnitt 1: ".txt, .gui, .yml, .gfx").
    /// </summary>
    public static readonly IReadOnlyCollection<string> DefaultExtensions =
        new[] { ".txt", ".gui", ".gfx", ".yml", ".yaml" };

    /// <summary>
    /// Zählt rekursiv alle durchsuchbaren Dateien unter <paramref name="rootFolder"/> auf. Läuft
    /// bewusst NICHT über <c>Directory.EnumerateFiles(..., SearchOption.AllDirectories)</c>, da
    /// dessen Aufzählung beim ersten nicht zugänglichen Unterordner komplett abbricht —
    /// stattdessen wird jeder Ordner einzeln besucht: ein einzelner nicht lesbarer Unterordner
    /// wird übersprungen, die übrigen aber trotzdem weiter durchsucht.
    /// </summary>
    public static IEnumerable<string> EnumerateSearchableFiles(string rootFolder, IReadOnlyCollection<string>? extensions = null)
    {
        var allowedExtensions = extensions ?? DefaultExtensions;
        var directories = new Stack<string>();
        directories.Push(rootFolder);

        while (directories.Count > 0)
        {
            var currentDirectory = directories.Pop();

            List<string> files;
            try
            {
                // .ToList() erzwingt die eigentliche Enumeration INNERHALB des try-Blocks -
                // sowohl der Aufruf von EnumerateFiles selbst als auch das spätere Durchlaufen
                // (foreach) können bei fehlenden Zugriffsrechten eine Ausnahme werfen.
                files = Directory.EnumerateFiles(currentDirectory).ToList();
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);
                if (allowedExtensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return file;
                }
            }

            List<string> subDirectories;
            try
            {
                subDirectories = Directory.EnumerateDirectories(currentDirectory).ToList();
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var subDirectory in subDirectories)
            {
                directories.Push(subDirectory);
            }
        }
    }

    /// <summary>
    /// Durchsucht alle Dateien unter <paramref name="rootFolder"/> nach <paramref name="query"/>.
    /// Eine einzelne nicht lesbare Datei bricht die Suche der übrigen Dateien nicht ab.
    /// </summary>
    public static IReadOnlyList<FileSearchMatch> Search(
        string rootFolder,
        string query,
        SearchOptions options,
        IReadOnlyCollection<string>? extensions = null)
    {
        var results = new List<FileSearchMatch>();

        foreach (var file in EnumerateSearchableFiles(rootFolder, extensions))
        {
            string text;
            try
            {
                text = File.ReadAllText(file, Encoding.UTF8);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var match in TextSearch.FindAll(text, query, options))
            {
                results.Add(new FileSearchMatch(file, match));
            }
        }

        return results;
    }
}
