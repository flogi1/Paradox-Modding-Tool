// Lesson Learned Punkt 2 der Architektur-Dokumentation: WPF-Projekte (UseWPF=true) bringen
// TROTZ ImplicitUsings=enable NICHT automatisch System.IO/System.Linq mit wie ein gewöhnliches
// Microsoft.NET.Sdk-Klassenbibliotheksprojekt (z. B. ParadoxModTool.GameProfiles) - explizit
// importiert, da ParadoxModTool.Explorer als Ganzes net8.0-windows+UseWPF ist (wegen
// ExplorerPanel.xaml).
using System.IO;
using System.Linq;

namespace ParadoxModTool.Explorer;

/// <summary>
/// Liest die UNMITTELBAREN Kinder eines Ordners (nicht rekursiv — siehe <see cref="ExplorerEntry.Children"/>
/// zum Grund: Lazy-Loading beim Aufklappen statt vollständigem Einlesen im Voraus).
/// Robust gegen einzelne nicht lesbare Unterordner/Dateien, nach demselben Muster wie
/// <c>ParadoxModTool.SearchReplace.FileSearchService</c> — ein einzelner Zugriffsfehler
/// (Berechtigungen, kaputter Symlink, zu langer Pfad) bricht das Einlesen der übrigen Einträge
/// nicht ab.
/// </summary>
public static class FolderScanner
{
    /// <summary>
    /// Liefert die Kinder von <paramref name="folderPath"/>: zuerst alle Unterordner
    /// (alphabetisch, ohne Beachtung von Groß-/Kleinschreibung), danach alle Dateien
    /// (ebenso alphabetisch) — dieselbe Reihenfolge wie in den meisten Datei-Explorern üblich.
    /// Liefert eine leere Liste (statt einer Ausnahme), wenn der Ordner nicht (mehr) existiert
    /// oder nicht lesbar ist. Mit <paramref name="hideUgcReferences"/> fehlen Workshop-Verweise (<see cref="UgcReference"/>).
    /// </summary>
    public static IReadOnlyList<ExplorerEntry> GetChildren(string folderPath, bool hideUgcReferences = false)
    {
        var result = new List<ExplorerEntry>();

        IReadOnlyList<string> directories;
        try
        {
            directories = Directory.Exists(folderPath)
                ? Directory.GetDirectories(folderPath)
                : Array.Empty<string>();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PathTooLongException)
        {
            directories = Array.Empty<string>();
        }

        foreach (var dir in directories.OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
        {
            result.Add(new ExplorerEntry
            {
                FullPath = dir,
                Name = Path.GetFileName(dir),
                IsDirectory = true,
            });
        }

        IReadOnlyList<string> files;
        try
        {
            files = Directory.Exists(folderPath)
                ? Directory.GetFiles(folderPath)
                : Array.Empty<string>();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PathTooLongException)
        {
            files = Array.Empty<string>();
        }

        foreach (var file in files.Where(f => !(hideUgcReferences && UgcReference.IsMatch(Path.GetFileName(f)))).OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
        {
            result.Add(new ExplorerEntry
            {
                FullPath = file,
                Name = Path.GetFileName(file),
                IsDirectory = false,
            });
        }

        return result;
    }
}
