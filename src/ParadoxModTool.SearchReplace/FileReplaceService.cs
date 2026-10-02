using System.IO;
using System.Text;

namespace ParadoxModTool.SearchReplace;

/// <summary>Vorgeschlagene Ersetzung für eine einzelne Datei (Abschnitt 5b) — noch NICHT geschrieben.</summary>
public sealed class FileReplacementPlan
{
    public FileReplacementPlan(string filePath, string newContent, int replacementCount)
    {
        FilePath = filePath;
        NewContent = newContent;
        ReplacementCount = replacementCount;
    }

    public string FilePath { get; }

    public string NewContent { get; }

    public int ReplacementCount { get; }
}

/// <summary>
/// Berechnet Ersetzungen über mehrere Dateien hinweg (Abschnitt 5b: "Ersetzen in Dokumenten"),
/// OHNE sie zu schreiben — bewusst eine reine Berechnung ohne Seiteneffekte. Das tatsächliche
/// Schreiben übernimmt das aufrufende App-Modul über den programmweiten Aktionsverlauf
/// (<c>ParadoxModTool.UndoRedo</c>, <c>FileWriteUndoableAction</c>), damit die Ersetzung in
/// allen betroffenen Dateien als EIN rückgängig machbarer Schritt im Aktionsverlauf erscheint
/// (Abschnitt 5: "alle Prozesse ... mit einer Funktion rückgängig gemacht werden"). Eine
/// einzelne nicht lesbare Datei bricht die Berechnung der übrigen Dateien nicht ab.
/// </summary>
public static class FileReplaceService
{
    public static IReadOnlyList<FileReplacementPlan> ComputeReplacements(
        IEnumerable<string> filePaths,
        string query,
        string replacement,
        SearchOptions options,
        CancellationToken cancellation = default,
        IProgress<(int Done, int Total)>? progress = null)
    {
        var plans = new List<FileReplacementPlan>();
        var files = filePaths.ToList();
        var done = 0;

        foreach (var filePath in files)
        {
            cancellation.ThrowIfCancellationRequested();
            progress?.Report((done++, files.Count));
            string text;
            try
            {
                text = File.ReadAllText(filePath, Encoding.UTF8);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            var (newText, count) = TextSearch.ReplaceAll(text, query, replacement, options);
            if (count > 0)
            {
                plans.Add(new FileReplacementPlan(filePath, newText, count));
            }
        }

        return plans;
    }
}
