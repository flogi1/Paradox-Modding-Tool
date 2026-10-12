using System.Text.RegularExpressions;

namespace ParadoxModTool.Generators;

/// <summary>Ergebnis einer Ausgabe auf einen Dateitext: neuer Text und Hinweise für die Vorschau.</summary>
public sealed record SectionResult(string Text, IReadOnlyList<string> Notes, int LegacyRemoved = 0);

/// <summary>
/// Ausgabe "Abschnitt einfügen" (Spec Abschnitt 2, Art 3): Inhalt zwischen Markierungen in einer Datei oder einem benannten Block.
/// PMT-Markierungen gehen vor alten (Python-)Markierungen; gefundene Markierungszeilen bleiben, wie sie sind, nur der Inhalt dazwischen wird
/// ersetzt. Ohne Markierung kommt der Abschnitt ans Ende des Blocks bzw. der Datei. Vorher entfernt <see cref="LegacySweep"/> unmarkierte
/// Alt-Einträge (Spec Teil 2); leerer Inhalt ohne Markierung fügt nichts ein.
/// </summary>
public static class SectionInserter
{
    public static SectionResult Apply(
        string? existing,
        string? blockPath,
        string begin,
        string end,
        string? legacyBegin,
        string? legacyEnd,
        string content,
        string? skeleton,
        string newLine,
        IReadOnlyList<Regex>? legacyPatterns = null)
    {
        var text = existing ?? (skeleton is null
            ? throw new GeneratorOutputException(GeneratorsTexts.FileMissingNoSkeleton)
            : TextNormalizer.Normalize(skeleton, newLine));
        var markers = new[] { (begin, end), (legacyBegin, legacyEnd) }.Where(m => m.Item1 is not null && m.Item2 is not null).Select(m => (m.Item1!, m.Item2!)).ToList();
        var notes = new List<string>();
        var legacyRemoved = 0;

        if (legacyPatterns is { Count: > 0 })
        {
            var (scopeStart, scopeEnd) = Scope(text, blockPath);
            var skip = markers.Select(m => MarkerRange(text, scopeStart, scopeEnd, m.Item1, m.Item2)).OfType<(int Start, int End)>().ToList();
            var (swept, removed) = LegacySweep.Apply(text, scopeStart, scopeEnd, legacyPatterns, skip);
            text = swept;
            legacyRemoved = removed;
            if (removed > 0)
            {
                notes.Add(GeneratorsTexts.LegacyRemoved(removed));
            }
        }

        var block = blockPath is null ? (BlockSpan?)null : BlockText.Find(text, blockPath);
        var (start, stop) = Scope(text, block);
        foreach (var (markerBegin, markerEnd) in markers)
        {
            var lines = BlockText.Lines(text, start, stop).ToList();
            var first = lines.FindIndex(l => l.Trimmed == markerBegin);
            if (first < 0)
            {
                continue;
            }

            var last = lines.FindIndex(first + 1, l => l.Trimmed == markerEnd);
            if (last < 0)
            {
                throw new GeneratorOutputException(GeneratorsTexts.MarkerWithoutEnd(markerBegin));
            }

            var indent = BlockText.LeadingWhitespace(text[lines[first].Start..lines[first].End]);
            var replaced = text[..lines[first].End] + BlockText.Indent(content, indent, newLine) + text[lines[last].Start..];
            return new SectionResult(replaced, notes, legacyRemoved);
        }

        if (content.Trim().Length == 0)
        {
            return new SectionResult(text, notes, legacyRemoved);
        }

        if (block is { } target)
        {
            var indent = BlockText.EntryIndent(text, target);
            var section = indent + begin + newLine + BlockText.Indent(content, indent, newLine) + indent + end + newLine;
            return new SectionResult(BlockText.InsertBeforeClose(text, target, section, newLine), notes, legacyRemoved);
        }

        var prefix = text.Length > 0 && !text.EndsWith('\n') ? newLine : string.Empty;
        return new SectionResult(text + prefix + begin + newLine + BlockText.Indent(content, string.Empty, newLine) + end + newLine, notes, legacyRemoved);
    }

    private static (int Start, int End) Scope(string text, string? blockPath) =>
        Scope(text, blockPath is null ? null : BlockText.Find(text, blockPath));

    /// <summary>Bereich, in dem Markierungen gesucht werden: der Inhalt des Blocks (ab der Zeile nach <c>{</c>) oder die ganze Datei.</summary>
    private static (int Start, int End) Scope(string text, BlockSpan? block) =>
        block is { } b ? (BlockText.LineEnd(text, b.Open), b.Close) : (0, text.Length);

    /// <summary>Von der Anfangs- bis nach der End-Markierungszeile; ohne vollständiges Paar <see langword="null"/>.</summary>
    private static (int Start, int End)? MarkerRange(string text, int scopeStart, int scopeEnd, string markerBegin, string markerEnd)
    {
        var lines = BlockText.Lines(text, scopeStart, scopeEnd).ToList();
        var first = lines.FindIndex(l => l.Trimmed == markerBegin);
        var last = first < 0 ? -1 : lines.FindIndex(first + 1, l => l.Trimmed == markerEnd);
        return last < 0 ? null : (lines[first].Start, lines[last].End);
    }
}
