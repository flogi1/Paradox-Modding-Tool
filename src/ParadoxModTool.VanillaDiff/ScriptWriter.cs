using System.Text;

namespace ParadoxModTool.VanillaDiff;

/// <summary>Formatierungsoptionen für <see cref="ScriptWriter"/>.</summary>
public sealed class ScriptWriteOptions
{
    /// <summary>Anzahl Leerzeichen (bzw. Tabs, siehe <see cref="UseTabs"/>) pro Einrückungsebene.</summary>
    public int IndentSize { get; init; } = 4;

    public bool UseTabs { get; init; }

    /// <summary>
    /// Wörtlich übernommenen Quelltext auf diese Einrückungsart und die Zieltiefe umrücken (Folgezeilen relativ zur ersten Zeile). Für Text,
    /// der in eine bestehende Datei kommt, aber aus anderer Tiefe oder anderer Quelle stammt (Blocktext mit Leerzeichen in einer Tab-Datei).
    /// </summary>
    public bool ReindentVerbatim { get; init; }

    internal string IndentString(int level)
    {
        if (level <= 0)
        {
            return string.Empty;
        }

        return UseTabs
            ? new string('\t', level)
            : new string(' ', level * IndentSize);
    }
}

/// <summary>
/// Serialisiert einen <see cref="ScriptBlock"/>-AST zurück in Paradox-Skript-Text. Wird von
/// der Merge-Funktion (Abschnitt 7) verwendet, um das Zusammenführungsergebnis zu schreiben,
/// sowie vom späteren Formatierungs-Werkzeug (Abschnitt 13, Code hübscher darstellen).
/// Diese erste Fassung schreibt immer einheitlich neu formatiert (eine Anweisung pro Zeile,
/// konfigurierbare Einrückung) — sie reproduziert nicht die exakte Original-Whitespace-
/// Formatierung des Quelltexts. Das entspricht der Grundregel aus Abschnitt 7, dass beim
/// Merge im Zweifel eine einheitliche (Vanilla-artige) Formatierung verwendet wird.
/// </summary>
public static class ScriptWriter
{
    public static string Write(ScriptBlock root, ScriptWriteOptions? options = null) =>
        Write(root, preferSourceVerbatim: false, options);

    /// <summary>
    /// Wie <see cref="Write(ScriptBlock, ScriptWriteOptions?)"/>, aber wenn
    /// <paramref name="preferSourceVerbatim"/> gesetzt ist, wird für jeden Eintrag, der noch
    /// einen Original-Quelltext-Span trägt (<see cref="ScriptEntry.SourceText"/>, siehe
    /// <see cref="ScriptParser"/>), dieser Rohtext unverändert übernommen statt neu
    /// serialisiert - nur synthetische Einträge ohne Span (siehe <see cref="MergeEngine"/>,
    /// z. B. neu zusammengesetzte verschachtelte Blöcke) werden weiterhin normalisiert
    /// geschrieben. Das ist die Grundlage für "Mod-Formatierung beibehalten": unveränderte
    /// Mod- UND unveränderte Vanilla-Anteile behalten jeweils ihr eigenes Original-Layout,
    /// nur wirklich neu zusammengeführte Stellen werden vereinheitlicht.
    /// </summary>
    public static string Write(ScriptBlock root, bool preferSourceVerbatim, ScriptWriteOptions? options = null) =>
        Write(root, preferSourceVerbatim, options, 0);

    /// <summary>Wie oben, aber alle Zeilen um <paramref name="indent"/> Ebenen eingerückt (Einfügen in einen verschachtelten Block).</summary>
    public static string Write(ScriptBlock root, bool preferSourceVerbatim, ScriptWriteOptions? options, int indent)
    {
        options ??= new ScriptWriteOptions();
        var sb = new StringBuilder();
        WriteEntries(sb, root.Entries, indent, options, preferSourceVerbatim);
        return sb.ToString();
    }

    private static void WriteEntries(StringBuilder sb, IReadOnlyList<ScriptEntry> entries, int indent, ScriptWriteOptions options, bool preferSourceVerbatim)
    {
        var indentString = options.IndentString(indent);

        // "template name { ... }": Kopfzeilen-Wörter und Rumpf gehören auf EINE Zeile (siehe EntryUnit).
        var headerByBody = new Dictionary<ScriptEntry, IReadOnlyList<ScriptEntry>>(ReferenceEqualityComparer.Instance);
        var headerEntries = new HashSet<ScriptEntry>(ReferenceEqualityComparer.Instance);
        foreach (var unit in EntryUnits.Build(entries))
        {
            if (unit.Header.Count > 0)
            {
                headerByBody[unit.Entry] = unit.Header;
                foreach (var header in unit.Header)
                {
                    headerEntries.Add(header);
                }
            }
        }

        var written = 0;
        foreach (var entry in entries)
        {
            if (entry is ScriptCommentEntry)
            {
                foreach (var comment in entry.LeadingComments)
                {
                    sb.Append(indentString).Append(CommentLine(comment)).Append('\n');
                }

                continue;
            }

            if (headerEntries.Contains(entry))
            {
                continue;
            }

            IReadOnlyList<ScriptEntry>? header = headerByBody.TryGetValue(entry, out var foundHeader) ? foundHeader : null;

            // Leerzeilen zwischen Geschwistern aus dem Quelltext übernehmen, wenn ein Elternblock neu zusammengesetzt wird.
            if (preferSourceVerbatim && written > 0 && BlankLineBefore(header is { Count: > 0 } ? header[0] : entry))
            {
                sb.Append('\n');
            }

            written++;

            if (header is not null)
            {
                foreach (var headerEntry in header)
                {
                    foreach (var comment in headerEntry.LeadingComments)
                    {
                        sb.Append(indentString).Append(CommentLine(comment)).Append('\n');
                    }
                }
            }

            foreach (var comment in entry.LeadingComments)
            {
                sb.Append(indentString).Append(CommentLine(comment)).Append('\n');
            }

            sb.Append(indentString);

            if (header is not null && preferSourceVerbatim && TryGetContiguousSource(header, entry, out var contiguous))
            {
                sb.Append(Verbatim(contiguous, header[0], indent, options));
            }
            else if (header is not null)
            {
                foreach (var headerEntry in header)
                {
                    sb.Append(HeaderPartText(headerEntry)).Append(' ');
                }

                WriteValue(sb, ((ScriptListItem)entry).Value, indent, options, preferSourceVerbatim);
            }
            else if (preferSourceVerbatim && entry.SourceText is not null && entry.SourceStart is not null && entry.SourceEnd is not null)
            {
                sb.Append(Verbatim(entry.SourceText.Substring(entry.SourceStart.Value, entry.SourceEnd.Value - entry.SourceStart.Value), entry, indent, options));
            }
            else
            {
                switch (entry)
                {
                    case ScriptStatement stmt:
                        sb.Append(stmt.Key).Append(' ').Append(OperatorText(stmt.Operator)).Append(' ');
                        WriteValue(sb, stmt.Value, indent, options, preferSourceVerbatim);
                        break;
                    case ScriptListItem item:
                        WriteValue(sb, item.Value, indent, options, preferSourceVerbatim);
                        break;
                }
            }

            if (!string.IsNullOrEmpty(entry.TrailingComment))
            {
                sb.Append(" # ").Append(entry.TrailingComment);
            }

            sb.Append('\n');
        }
    }

    /// <summary>
    /// Wörtlicher Text eines Eintrags; mit <see cref="ScriptWriteOptions.ReindentVerbatim"/> werden die Folgezeilen von der Tiefe der ersten
    /// Zeile im Quelltext auf <paramref name="indent"/> verschoben (Tab = eine Ebene, vier Leerzeichen = eine Ebene). Leerzeilen bleiben.
    /// </summary>
    private static string Verbatim(string text, ScriptEntry first, int indent, ScriptWriteOptions options)
    {
        if (!options.ReindentVerbatim || !text.Contains('\n') || first.SourceText is not { } source || first.SourceStart is not { } start)
        {
            return text;
        }

        var lineStart = start == 0 ? 0 : source.LastIndexOf('\n', start - 1) + 1;
        var lead = source[lineStart..start];
        if (lead.Any(c => c is not (' ' or '\t')))
        {
            return text;
        }

        var from = Levels(lead, out _);
        var lines = text.Split('\n');
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            var n = 0;
            while (n < line.Length && line[n] is ' ' or '\t')
            {
                n++;
            }

            if (line[n..].Trim().Length == 0)
            {
                continue;
            }

            var level = Levels(line[..n], out var rest);
            lines[i] = options.IndentString(Math.Max(0, level - from + indent)) + new string(' ', rest) + line[n..];
        }

        return string.Join('\n', lines);
    }

    private static int Levels(string lead, out int rest)
    {
        var tabs = lead.Count(c => c == '\t');
        var spaces = lead.Length - tabs;
        rest = spaces % 4;
        return tabs + spaces / 4;
    }

    /// <summary>Steht im Quelltext direkt über dem Eintrag (bzw. über seinen Kommentarzeilen) eine Leerzeile?</summary>
    private static bool BlankLineBefore(ScriptEntry entry)
    {
        if (entry.SourceText is not { } source || entry.SourceStart is not { } start || start > source.Length)
        {
            return false;
        }

        static int LineStart(string text, int position) => position <= 0 ? 0 : text.LastIndexOf('\n', position - 1) + 1;

        var line = LineStart(source, start);
        for (var i = 0; i < entry.LeadingComments.Count && line > 0; i++)
        {
            line = LineStart(source, line - 1);
        }

        if (line == 0)
        {
            return false;
        }

        var previous = LineStart(source, line - 1);
        return source.AsSpan(previous, line - 1 - previous).Trim().IsEmpty;
    }

    /// <summary>Kommentarzeile ohne das Rautezeichen (Tokenizer) wieder mit Raute; "###"-Linien bleiben "###" statt "# ##".</summary>
    private static string CommentLine(string comment) => comment.StartsWith('#') ? "#" + comment : "# " + comment;

    private static string HeaderPartText(ScriptEntry entry) => entry switch
    {
        ScriptStatement { Value: ScriptScalar v } s => $"{s.Key} {OperatorText(s.Operator)} {(v.IsQuoted ? $"\"{v.Text}\"" : v.Text)}",
        ScriptListItem { Value: ScriptScalar v } => v.IsQuoted ? $"\"{v.Text}\"" : v.Text,
        _ => string.Empty
    };

    /// <summary>
    /// Liefert den zusammenhängenden Original-Text von der ersten Kopfzeile bis zum Ende des Rumpfs,
    /// wenn alle Teile aus derselben Quelle stammen und noch ihren Span tragen (dann bleibt die
    /// Originalformatierung samt Zeilenumbrüchen erhalten).
    /// </summary>
    private static bool TryGetContiguousSource(IReadOnlyList<ScriptEntry> header, ScriptEntry body, out string text)
    {
        text = string.Empty;
        var parts = header.Append(body).ToList();
        var source = parts[0].SourceText;

        if (source is null || parts.Any(p => !ReferenceEquals(p.SourceText, source) || p.SourceStart is null || p.SourceEnd is null))
        {
            return false;
        }

        var start = parts[0].SourceStart!.Value;
        var end = parts[^1].SourceEnd!.Value;
        if (end < start)
        {
            return false;
        }

        text = source.Substring(start, end - start);
        return true;
    }

    private static void WriteValue(StringBuilder sb, ScriptValue value, int indent, ScriptWriteOptions options, bool preferSourceVerbatim)
    {
        switch (value)
        {
            case ScriptScalar scalar:
                sb.Append(scalar.IsQuoted ? $"\"{scalar.Text}\"" : scalar.Text);
                break;
            case ScriptBlock block:
                if (block.Entries.Count == 0)
                {
                    sb.Append("{ }");
                    break;
                }

                sb.Append("{\n");
                WriteEntries(sb, block.Entries, indent + 1, options, preferSourceVerbatim);
                sb.Append(options.IndentString(indent)).Append('}');
                break;
        }
    }

    private static string OperatorText(ScriptOperator op) => op switch
    {
        ScriptOperator.Equals => "=",
        ScriptOperator.LessThan => "<",
        ScriptOperator.LessOrEqual => "<=",
        ScriptOperator.GreaterThan => ">",
        ScriptOperator.GreaterOrEqual => ">=",
        ScriptOperator.EqualsIfExists => "?=",
        ScriptOperator.NotEquals => "!=",
        _ => "="
    };
}
