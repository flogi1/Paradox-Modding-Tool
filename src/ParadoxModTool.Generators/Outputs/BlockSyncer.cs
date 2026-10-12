using System.Text.RegularExpressions;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Generators;

/// <summary>Ergebnis des Abgleichs: neuer Text und die Schlüssel der hinzugefügten, geänderten und entfernten Blöcke.</summary>
public sealed record SyncResult(string Text, IReadOnlyList<string> Added, IReadOnlyList<string> Replaced, IReadOnlyList<string> Removed);

/// <summary>
/// Ausgabe "Blöcke abgleichen" (Spec Abschnitt 2, Art 5): oberste Blöcke, deren Schlüssel zum Muster passt, mit den gerenderten Blöcken abgleichen.
/// Fehlende kommen ans Ende, nicht mehr gerenderte gehen samt der Kommentarzeilen direkt davor weg, vorhandene bekommen nur die gerenderten
/// Teilschlüssel (andere wie <c>game_data</c> bleiben). Alles außerhalb dieser Blöcke bleibt zeichengleich.
/// </summary>
public static class BlockSyncer
{
    /// <summary>
    /// Mit <paramref name="keep"/> (Spec Teil 2): ein gerenderter Block ersetzt alle Einträge außer den behaltenen Schlüsseln; hat er nur behaltene,
    /// bleibt der vorhandene Block unberührt (leeres Modifikatorfeld schützt Handgeschriebenes).
    /// </summary>
    public static SyncResult Apply(string? existing, string keyPattern, string rendered, string newLine, IReadOnlyList<string>? keep = null)
    {
        var pattern = new Regex("^" + Regex.Escape(keyPattern).Replace("\\*", "[A-Za-z0-9_]+") + "$", RegexOptions.CultureInvariant);
        var renderedBlocks = Statements(rendered).Where(s => pattern.IsMatch(s.Key)).ToList();
        if (existing is null)
        {
            return new SyncResult(TextNormalizer.Normalize(rendered, newLine), renderedBlocks.Select(s => s.Key).ToList(), Array.Empty<string>(), Array.Empty<string>());
        }

        var fileBlocks = Statements(existing).Where(s => pattern.IsMatch(s.Key)).ToList();
        var edits = new List<(int Start, int End, string Text)>();
        var added = new List<string>();
        var replaced = new List<string>();
        var removed = new List<string>();

        foreach (var block in fileBlocks)
        {
            var (start, end) = Span(block);
            var wanted = renderedBlocks.FirstOrDefault(r => r.Key == block.Key);
            if (wanted is null)
            {
                edits.Add((CommentStart(existing, start), BlockText.LineEnd(existing, end), string.Empty));
                removed.Add(block.Key);
                continue;
            }

            var original = existing[start..end];
            var merged = keep is null ? Merge(original, block.Key, wanted, rendered, newLine) : Replace(original, block.Key, wanted, rendered, newLine, keep);
            if (merged != original)
            {
                edits.Add((start, end, merged));
                replaced.Add(block.Key);
            }
        }

        var text = existing;
        foreach (var (start, end, replacement) in edits.OrderByDescending(e => e.Start))
        {
            text = text[..start] + replacement + text[end..];
        }

        foreach (var block in renderedBlocks.Where(r => fileBlocks.All(f => f.Key != r.Key)))
        {
            var (start, end) = Span(block);
            text += (text.Length > 0 && !text.EndsWith('\n') ? newLine : string.Empty) + TextNormalizer.Normalize(rendered[start..end], newLine);
            added.Add(block.Key);
        }

        return new SyncResult(text, added, replaced, removed);
    }

    /// <summary>Die gerenderten Teilschlüssel in den Block der Datei übernehmen: gleiche Schlüssel ersetzen (je erster Treffer), neue vor <c>}</c> anhängen.</summary>
    private static string Merge(string original, string key, ScriptStatement wanted, string rendered, string newLine)
    {
        var inner = Statements(original).First(s => s.Key == key);
        var existingEntries = ((ScriptBlock)inner.Value).Entries.OfType<ScriptStatement>().ToList();
        var used = new HashSet<ScriptStatement>();
        var edits = new List<(int Start, int End, string Text)>();
        var appended = new List<string>();
        foreach (var entry in ((ScriptBlock)wanted.Value).Entries.OfType<ScriptStatement>())
        {
            var (renderedStart, renderedEnd) = Span(entry);
            var replacement = rendered[renderedStart..renderedEnd].Replace("\r\n", "\n").Replace("\n", newLine);
            var target = existingEntries.FirstOrDefault(e => e.Key == entry.Key && !used.Contains(e));
            if (target is null)
            {
                appended.Add(replacement);
                continue;
            }

            used.Add(target);
            var (start, end) = Span(target);
            if (original[start..end] != replacement)
            {
                edits.Add((start, end, replacement));
            }
        }

        var text = original;
        foreach (var (start, end, replacement) in edits.OrderByDescending(e => e.Start))
        {
            text = text[..start] + replacement + text[end..];
        }

        if (appended.Count > 0)
        {
            var block = BlockText.Find(text, key);
            var indent = BlockText.EntryIndent(text, block);
            text = BlockText.InsertBeforeClose(text, block, string.Concat(appended.Select(a => indent + a + newLine)), newLine);
        }

        return text;
    }

    /// <summary>
    /// Inhalt des Blocks neu aufbauen: behaltene Einträge der Datei (ganze Zeilen, zeichengleich), dann die übrigen gerenderten Einträge (ganze Zeilen
    /// aus dem gerenderten Text). Kopfzeile und schließende Klammer bleiben; ein einzeiliger Block wird geöffnet.
    /// </summary>
    private static string Replace(string original, string key, ScriptStatement wanted, string rendered, string newLine, IReadOnlyList<string> keep)
    {
        // Der gerenderte Inhalt ohne behaltene Schlüssel als ganze Zeilen, mit Kommentaren (gleiche Sicht wie der Einleser).
        var renderedLines = BlockText.BodyLines(rendered, wanted, keep.ToList());
        if (renderedLines.Count == 0)
        {
            return original;
        }

        var inner = Statements(original).First(s => s.Key == key);
        var kept = ((ScriptBlock)inner.Value).Entries.OfType<ScriptStatement>().Where(e => keep.Contains(e.Key));
        var body = new System.Text.StringBuilder();
        foreach (var entry in kept)
        {
            var (start, end) = Span(entry);
            body.Append(Lines(original, start, end, newLine));
        }

        foreach (var line in renderedLines)
        {
            body.Append(line).Append(newLine);
        }

        var block = BlockText.Find(original, key);
        var head = BlockText.LineEnd(original, block.Open);
        var closeLine = BlockText.LineStart(original, block.Close);
        if (head <= block.Close && closeLine >= head && original[closeLine..block.Close].Trim().Length == 0)
        {
            return original[..head] + body + original[closeLine..];
        }

        var indent = BlockText.LeadingWhitespace(original[BlockText.LineStart(original, block.StatementStart)..]);
        return original[..(block.Open + 1)] + newLine + body + indent + original[block.Close..];
    }

    /// <summary>Die ganzen Zeilen eines Eintrags (mit Einrückung), Zeilenenden auf <paramref name="newLine"/>.</summary>
    private static string Lines(string text, int start, int end, string newLine)
    {
        var lineStart = BlockText.LineStart(text, start);
        var prefix = text[lineStart..start].Trim().Length == 0 ? text[lineStart..start] : "\t";
        var line = prefix + text[start..end].Replace("\r\n", "\n").Replace("\n", newLine);
        return line.TrimEnd(' ', '\t') + newLine;
    }

    private static IReadOnlyList<ScriptStatement> Statements(string text)
    {
        try
        {
            return ScriptDocument.Parse(text).Entries.OfType<ScriptStatement>().Where(s => s.Value is ScriptBlock).ToList();
        }
        catch (Exception ex) when (ex is not GeneratorOutputException)
        {
            throw new GeneratorOutputException(GeneratorsTexts.ParseFailed(ex.Message));
        }
    }

    private static (int Start, int End) Span(ScriptEntry entry) =>
        (entry.SourceStart ?? throw new GeneratorOutputException(GeneratorsTexts.ParseFailed("span")), entry.SourceEnd ?? entry.SourceStart.Value);

    /// <summary>Beginn der Kommentarzeilen direkt über einer Anweisung (ohne Leerzeile dazwischen), sonst der Zeilenanfang der Anweisung.</summary>
    private static int CommentStart(string text, int statementStart)
    {
        var start = BlockText.LineStart(text, statementStart);
        while (start > 0)
        {
            var previous = BlockText.LineStart(text, start - 1);
            if (!text[previous..start].TrimStart().StartsWith('#'))
            {
                break;
            }

            start = previous;
        }

        return start;
    }
}
