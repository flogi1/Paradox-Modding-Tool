using System.Collections.Concurrent;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>Fundstelle von "Wird angezeigt in (GUI)": Datei, Stelle, Zeile, Zeilentext und das umschließende Element (falls es eines gibt).</summary>
public sealed record GuiReferenceHit(GuiSourceFile File, GuiDocument Document, int Offset, int Line, string LineText, GuiNode? Element);

/// <summary>Ergebnis einer Suche. <c>Hits</c> ist auf <see cref="GuiReferenceSearch.MaxHits"/> begrenzt, <c>Total</c> zählt alle.</summary>
public sealed record GuiReferenceResult(string Word, IReadOnlyList<GuiReferenceHit> Hits, int Total, int FileCount);

/// <summary>
/// "Wird angezeigt in (GUI)": sucht einen Namen aus einer Skriptdatei (scripted_gui, Lokalisierungsschlüssel, Texturpfad, Konzept) als
/// ganzes Wort in allen .gui-Dateien des Index, ohne Kommentare (Plan-Ruling 8). Reine Textsuche: Datenbindungen lassen sich damit keiner
/// Skriptstelle zuordnen.
/// </summary>
public static class GuiReferenceSearch
{
    public const int MinimumLength = 3;

    public const int MaxHits = 1000;

    /// <summary>Zeichen eines Verweisnamens: Buchstaben, Ziffern und <c>_ . / -</c> (Loc-Keys, Texturpfade, scripted_gui-Namen).</summary>
    public static bool IsReferenceChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or '/' or '-';

    /// <summary>
    /// Das Wort an der Spalte (0-basiert; auch direkt hinter dem Wort, wie beim Cursor am Wortende), ohne <c>. / -</c> am Rand;
    /// null ohne Buchstaben.
    /// </summary>
    public static string? WordAt(string line, int column)
    {
        if (column < 0 || column > line.Length)
        {
            return null;
        }

        if (column == line.Length || !IsReferenceChar(line[column]))
        {
            if (column == 0 || !IsReferenceChar(line[column - 1]))
            {
                return null;
            }

            column--;
        }

        var start = column;
        while (start > 0 && IsReferenceChar(line[start - 1]))
        {
            start--;
        }

        var end = column;
        while (end < line.Length && IsReferenceChar(line[end]))
        {
            end++;
        }

        var word = line[start..end].Trim('.', '/', '-');
        return word.Any(char.IsLetter) ? word : null;
    }

    /// <summary>
    /// Sucht in allen Dateien parallel, sortiert (Mod-Dateien zuerst, dann nach Pfad und Stelle) und bestimmt das umschließende Element nur
    /// für die behaltenen Fundstellen (bei häufigen Wörtern wären es sonst zehntausende Auflösungen, die verworfen werden).
    /// </summary>
    public static GuiReferenceResult Find(GuiIndex index, GuiResolver resolver, string word, CancellationToken cancellation = default)
    {
        var found = new ConcurrentBag<RawHit>();
        if (word.Length >= MinimumLength)
        {
            Parallel.ForEach(
                index.Files,
                new ParallelOptions { CancellationToken = cancellation },
                file =>
                {
                    foreach (var hit in FindIn(file, index.DocumentOf(file), word))
                    {
                        found.Add(hit);
                    }
                });
        }

        var ordered = found
            .OrderByDescending(h => h.File.Layer)
            .ThenBy(h => h.File.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(h => h.Offset)
            .ToList();
        var hits = ordered
            .Take(MaxHits)
            .Select(h => new GuiReferenceHit(h.File, h.Document, h.Offset, h.Line, h.LineText, GuiElementPath.ElementAt(h.Document, h.Offset, resolver)))
            .ToList();
        return new GuiReferenceResult(word, hits, ordered.Count, ordered.Select(h => h.File).Distinct().Count());
    }

    private sealed record RawHit(GuiSourceFile File, GuiDocument Document, int Offset, int Line, string LineText);

    private static IEnumerable<RawHit> FindIn(GuiSourceFile file, GuiDocument document, string word)
    {
        var text = document.Text;
        var line = 1;
        var lineStart = 0;
        var counted = 0;
        for (var at = text.IndexOf(word, StringComparison.Ordinal); at >= 0; at = text.IndexOf(word, at + word.Length, StringComparison.Ordinal))
        {
            var end = at + word.Length;
            if ((at > 0 && IsReferenceChar(text[at - 1])) || (end < text.Length && IsReferenceChar(text[end])))
            {
                continue;
            }

            if (document.FindDeepestAt(at) is { Kind: GuiNodeKind.Comment })
            {
                continue;
            }

            for (; counted < at; counted++)
            {
                if (text[counted] == '\n')
                {
                    line++;
                    lineStart = counted + 1;
                }
            }

            var lineEnd = text.IndexOf('\n', at);
            var lineText = text[lineStart..(lineEnd < 0 ? text.Length : lineEnd)].Trim();
            yield return new RawHit(file, document, at, line, lineText.Length > 200 ? lineText[..200] : lineText);
        }
    }
}
