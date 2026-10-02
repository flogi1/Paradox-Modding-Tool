using System.Globalization;
using System.Text.RegularExpressions;

namespace ParadoxModTool.ErrorLog;

/// <summary>
/// Ordnet Log-Zeilen dem Katalog zu. Zeilen mit <see cref="ErrorKind.AttachToPrevious"/> hängen ihren Kandidaten an den vorigen Eintrag.
/// Zwei aufeinanderfolgende GUI-Meldungen (Code "GUI-…") zu derselben Datei und Zeile werden zu einem Eintrag zusammengeführt: Das Spiel
/// meldet denselben GUI-Fehler oft aus zwei Quellen (pdx_gui_widget_desc.h und pdx_gui_factory.cpp).
/// </summary>
public static class ErrorClassifier
{
    public static List<ErrorEntry> Classify(IEnumerable<LogLine> lines, ErrorCatalog catalog)
    {
        var result = new List<ErrorEntry>();
        foreach (var line in lines)
        {
            var kind = catalog.Match(line.Message, out var match);
            if (kind is { AttachToPrevious: true } && result.Count > 0)
            {
                var candidate = Group(match!, "candidate") ?? Group(match!, "name");
                if (candidate is not null && !result[^1].Candidates.Contains(candidate))
                {
                    result[^1].Candidates.Add(candidate);
                }

                continue;
            }

            var lineText = match is null ? null : Group(match, "line");
            var entry = new ErrorEntry
            {
                Kind = kind,
                Line = line,
                Message = line.Message,
                Name = match is null ? null : Group(match, "name"),
                FilePath = match is null ? null : Group(match, "file")?.Trim(),
                FileLine = lineText is null ? null : int.Parse(lineText, CultureInfo.InvariantCulture),
                Detail = match is null ? null : Group(match, "detail"),
            };
            entry.Lines.Add(line);

            if (result.Count > 0 && IsSameGuiError(result[^1], entry))
            {
                var previous = result[^1];
                if (!previous.Message.Contains(entry.Message, StringComparison.Ordinal))
                {
                    previous.Message = previous.Message + " / " + entry.Message;
                }

                previous.Lines.Add(line);
                continue;
            }

            result.Add(entry);
        }

        return result;
    }

    private static bool IsSameGuiError(ErrorEntry previous, ErrorEntry current) =>
        previous.Code.StartsWith("GUI-", StringComparison.Ordinal)
        && current.Code.StartsWith("GUI-", StringComparison.Ordinal)
        && previous.FilePath is not null
        && previous.FilePath == current.FilePath
        && previous.FileLine == current.FileLine;

    private static string? Group(Match match, string name)
    {
        var group = match.Groups[name];
        return group.Success && group.Value.Length > 0 ? group.Value : null;
    }
}
