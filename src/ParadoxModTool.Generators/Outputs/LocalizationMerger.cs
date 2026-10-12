using System.Text.RegularExpressions;

namespace ParadoxModTool.Generators;

/// <summary>
/// Lokalisierung in gemeinsame Dateien (Spec 2026-10-06-generator-packages-idea-groups, Abschnitt 2): Einheiten sind die Eintragszeilen
/// (<c>key:0 "…"</c> oder <c>key: "…"</c>); passt der Schlüssel, ersetzt der gerenderte Text die Zeilen an Ort und Stelle (Läufe wie beim Abgleich
/// nach Treffer). Kopfzeile, Kommentare und alle anderen Zeilen bleiben zeichengleich.
/// </summary>
public static class LocalizationMerger
{
    private static readonly Regex Entry = new(@"^[ \t]*([^#\s][^:\s]*):", RegexOptions.CultureInvariant | RegexOptions.Multiline);

    /// <param name="existing">Bisheriger Dateitext; <see langword="null"/> = Datei fehlt.</param>
    /// <param name="rendered">Gerenderte Eintragszeilen (normalisiert); eine gerenderte Kopfzeile <c>l_&lt;sprache&gt;:</c> fällt weg.</param>
    /// <param name="insertAfter">Nur für neue Zeilen: Sie kommen hinter die letzte Eintragszeile, deren Schlüssel der Regex trifft; ohne solche Zeile ans Dateiende.</param>
    public static string Apply(string? existing, string language, Regex keys, string rendered, string newLine, Regex? insertAfter = null)
    {
        var header = "l_" + language + ":";
        var lines = rendered.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var first = lines.FindIndex(l => l.Trim().Length > 0);
        if (first >= 0 && lines[first].Trim() == header)
        {
            // samt der Leerzeilen davor
            lines.RemoveRange(0, first + 1);
        }

        var body = Body(lines.SkipWhile(l => l.Trim().Length == 0), newLine);
        // Neu eingefügt behält der Text seine führenden Leerzeilen (eine Leerzeile vor dem Abschnitt); ersetzte Zeilen bekommen sie nicht
        var inserted = Body(lines, newLine);
        if (existing is null)
        {
            return header + newLine + body;
        }

        var hits = new List<(int Start, int End)>();
        var anchor = -1;
        foreach (Match match in Entry.Matches(existing))
        {
            var key = match.Groups[1].Value;
            if (key == header.TrimEnd(':'))
            {
                continue;
            }

            var start = match.Groups[1].Index;
            var end = existing.IndexOf('\n', start);
            end = end < 0 ? existing.Length : end;
            if (insertAfter is not null && insertAfter.IsMatch(key))
            {
                anchor = end < existing.Length ? end + 1 : end;
            }

            if (keys.IsMatch(key))
            {
                hits.Add((start, existing[start..end].TrimEnd('\r').Length + start));
            }
        }

        if (hits.Count == 0)
        {
            if (inserted.Length == 0)
            {
                return existing;
            }

            if (anchor >= 0)
            {
                // Hinter der letzten Zeile ihrer Kategorie einfügen; fehlt dort das Zeilenende, kommt es dazu
                var gap = existing[anchor - 1] != '\n' ? newLine : string.Empty;
                return existing[..anchor] + gap + inserted + existing[anchor..];
            }

            return existing + (existing.Length > 0 && !existing.EndsWith('\n') ? newLine : string.Empty) + inserted;
        }

        return LineRuns.Apply(existing, hits, body, newLine).Text;
    }

    private static string Body(IEnumerable<string> lines, string newLine)
    {
        var body = string.Join(newLine, lines).TrimEnd('\r', '\n', ' ', '\t');
        return body.Length == 0 ? string.Empty : body + newLine;
    }
}
