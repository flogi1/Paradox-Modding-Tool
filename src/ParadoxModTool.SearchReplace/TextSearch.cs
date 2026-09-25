using System.Text;

namespace ParadoxModTool.SearchReplace;

/// <summary>
/// Kernlogik für Suchen/Ersetzen in einem einzelnen Text (Abschnitt 5b). Rein textbasiert und
/// UI-unabhängig, damit sowohl das aktuell geöffnete Dokument (Editor-Puffer) als auch einzelne
/// Dateien einer Ordnerstruktur (siehe <see cref="FileSearchService"/>/<see cref="FileReplaceService"/>)
/// dieselbe, automatisiert getestete Logik verwenden.
/// </summary>
public static class TextSearch
{
    /// <summary>
    /// Findet ALLE Treffer, einschließlich sich überlappender (z. B. Suche nach "aa" in "aaa"
    /// liefert zwei Treffer) — für die reine Trefferanzeige ("Alle finden") gewünscht. Für
    /// tatsächliche Ersetzungen <see cref="FindAllNonOverlapping"/> verwenden.
    /// </summary>
    public static IReadOnlyList<TextMatch> FindAll(string text, string query, SearchOptions options)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
        {
            return Array.Empty<TextMatch>();
        }

        // Ein mehrzeiliger Suchbegriff kommt aus einer WPF-TextBox und enthält daher IMMER
        // "\r\n" als Zeilenumbruch (siehe SearchReplaceWindow.xaml), unabhängig davon, welche
        // Konvention die durchsuchte Datei tatsächlich verwendet (Paradox-Moddateien kommen mit
        // "\n" ODER "\r\n" vor). Ohne diese Angleichung würde ein exakter Codeblock aus einer
        // "\n"-Datei nie gefunden, obwohl der sichtbare Text identisch aussieht - genau der vom
        // Nutzer gemeldete Fehler ("Programm hat Schwierigkeiten, ganze Codeblöcke zu finden").
        query = NormalizeLineEndingsToMatch(query, text);

        if (options.IgnoreFormatting)
        {
            return FindAllIgnoringFormatting(text, query, options);
        }

        if (query.Length > text.Length)
        {
            return Array.Empty<TextMatch>();
        }

        var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var lineStarts = ComputeLineStarts(text);
        var matches = new List<TextMatch>();

        var searchFrom = 0;
        while (searchFrom <= text.Length - query.Length)
        {
            var index = text.IndexOf(query, searchFrom, comparison);
            if (index < 0)
            {
                break;
            }

            if (!options.WholeWord || IsWholeWordMatch(text, index, query.Length))
            {
                var lineNumber = LineNumberAt(lineStarts, index);
                var lineText = GetLineText(text, lineStarts, lineNumber);
                var matchedText = text.Substring(index, query.Length);
                matches.Add(new TextMatch(index, query.Length, lineNumber, lineText, matchedText));
            }

            searchFrom = index + 1;
        }

        return matches;
    }

    /// <summary>
    /// Sucht wie <see cref="FindAll"/>, aber ignoriert dabei Unterschiede in Whitespace zwischen
    /// Suchbegriff und Zieltext (Nutzer-Feedback: unterschiedlich formatierte Codeblöcke sollen
    /// trotzdem beide gefunden werden). Dafür werden Suchbegriff UND Zieltext auf eine Version
    /// OHNE jegliche Whitespace-Zeichen reduziert (Paradox-Skriptsyntax kennt keinen semantisch
    /// bedeutsamen Whitespace — er dient nur der Lesbarkeit), die eigentliche Suche läuft in
    /// dieser reduzierten Fassung; JEDER Treffer wird anschließend über eine mitgeführte
    /// Positions-Zuordnung zurück auf seine tatsächliche Zeichenspanne im ORIGINALTEXT abgebildet
    /// (samt dessen tatsächlicher, ggf. abweichender Formatierung) — <see cref="TextMatch.Offset"/>/
    /// <see cref="TextMatch.Length"/>/<see cref="TextMatch.MatchedText"/> zeigen also immer auf den
    /// echten Originaltext, nicht auf die reduzierte Vergleichsfassung. Dadurch funktionieren
    /// <see cref="FindAllNonOverlapping"/> und <see cref="FindNext"/> unverändert weiter, da diese
    /// ausschließlich mit den von hier gelieferten Original-Positionen arbeiten. <see cref="ReplaceAll"/>
    /// nutzt <see cref="TextMatch.Offset"/> ebenfalls unverändert zum Einfügen, passt bei aktivem
    /// <see cref="SearchOptions.IgnoreFormatting"/> aber zusätzlich die Einrückung des jeweils
    /// eingefügten Ersetzungstextes an die tatsächliche Einrückung DIESER Fundstelle an (siehe dort,
    /// <c>ReindentReplacementToMatchContext</c>) — da zwei über diese Methode gefundene Fundstellen
    /// bewusst UNTERSCHIEDLICH tief eingerückt sein können, würde ein und derselbe, absolut
    /// eingegebene Ersetzungstext sonst nur zu einer der Fundstellen wirklich passen.
    /// </summary>
    private static IReadOnlyList<TextMatch> FindAllIgnoringFormatting(string text, string query, SearchOptions options)
    {
        var (strippedQuery, _) = StripWhitespace(query);
        if (strippedQuery.Length == 0)
        {
            return Array.Empty<TextMatch>();
        }

        var (strippedText, originalOffsets) = StripWhitespace(text);
        if (strippedQuery.Length > strippedText.Length)
        {
            return Array.Empty<TextMatch>();
        }

        var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var lineStarts = ComputeLineStarts(text);
        var matches = new List<TextMatch>();

        var searchFrom = 0;
        while (searchFrom <= strippedText.Length - strippedQuery.Length)
        {
            var strippedIndex = strippedText.IndexOf(strippedQuery, searchFrom, comparison);
            if (strippedIndex < 0)
            {
                break;
            }

            // originalOffsets[i] ist die Originaltext-Position des i-ten NICHT-Whitespace-Zeichens -
            // der Treffer beginnt dort und endet direkt NACH der Originalposition des letzten dazu
            // gehörenden Zeichens (deckt so auch eingeschlossenen Whitespace INNERHALB des Treffers
            // mit ab, z. B. die tatsächliche Einrückung zwischen zwei Codezeilen des Treffers).
            var originalStart = originalOffsets[strippedIndex];
            var lastCharIndex = strippedIndex + strippedQuery.Length - 1;
            var originalEnd = originalOffsets[lastCharIndex] + 1;
            var length = originalEnd - originalStart;

            if (!options.WholeWord || IsWholeWordMatch(text, originalStart, length))
            {
                var lineNumber = LineNumberAt(lineStarts, originalStart);
                var lineText = GetLineText(text, lineStarts, lineNumber);
                var matchedText = text.Substring(originalStart, length);
                matches.Add(new TextMatch(originalStart, length, lineNumber, lineText, matchedText));
            }

            searchFrom = strippedIndex + 1;
        }

        return matches;
    }

    /// <summary>
    /// Entfernt sämtliche Whitespace-Zeichen aus <paramref name="value"/> und liefert zusätzlich
    /// für jedes verbleibende Zeichen dessen ursprüngliche Position in <paramref name="value"/> —
    /// die Grundlage dafür, einen Treffer in der reduzierten Fassung wieder auf seine echte
    /// Zeichenspanne im Originaltext zurückzuführen (siehe <see cref="FindAllIgnoringFormatting"/>).
    /// </summary>
    private static (string Stripped, List<int> OriginalOffsets) StripWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var offsets = new List<int>(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsWhiteSpace(value[i]))
            {
                continue;
            }

            builder.Append(value[i]);
            offsets.Add(i);
        }

        return (builder.ToString(), offsets);
    }

    /// <summary>
    /// Wie <see cref="FindAll"/>, aber überlappende Folgetreffer werden übersprungen — die
    /// Grundlage für jede tatsächliche Ersetzung (<see cref="ReplaceAll"/>, sowie das Ersetzen im
    /// aktuell geöffneten Dokument über das App-Modul), da überlappende Ersetzungen sonst
    /// widersprüchliche Textbereiche erzeugen würden.
    /// </summary>
    public static IReadOnlyList<TextMatch> FindAllNonOverlapping(string text, string query, SearchOptions options)
    {
        var all = FindAll(text, query, options);
        if (all.Count == 0)
        {
            return all;
        }

        var result = new List<TextMatch>();
        var cursor = 0;
        foreach (var match in all)
        {
            if (match.Offset < cursor)
            {
                continue;
            }

            result.Add(match);
            cursor = match.Offset + match.Length;
        }

        return result;
    }

    /// <summary>
    /// Liefert den nächsten Treffer AB (einschließlich) <paramref name="fromOffset"/> — für
    /// "Weitersuchen". Gibt es danach keinen mehr, wird umlaufend wieder von vorn gesucht
    /// (wie bei Notepad++); gibt es im gesamten Text keinen Treffer, wird null zurückgegeben.
    /// </summary>
    public static TextMatch? FindNext(string text, string query, int fromOffset, SearchOptions options)
    {
        var all = FindAll(text, query, options);
        if (all.Count == 0)
        {
            return null;
        }

        foreach (var match in all)
        {
            if (match.Offset >= fromOffset)
            {
                return match;
            }
        }

        return all[0];
    }

    /// <summary>Ersetzt ALLE (nicht überlappenden) Treffer in <paramref name="text"/> und liefert den neuen Text sowie die Trefferanzahl.</summary>
    public static (string NewText, int Count) ReplaceAll(string text, string query, string replacement, SearchOptions options)
    {
        var matches = FindAllNonOverlapping(text, query, options);
        if (matches.Count == 0)
        {
            return (text, 0);
        }

        // Der Ersetzungstext kommt ebenfalls aus einer mehrzeiligen WPF-TextBox ("\r\n") - auch
        // er wird an die Zeilenenden-Konvention des Zieltextes angeglichen, damit ein eingefügter
        // mehrzeiliger Block nicht mit abweichenden Zeilenumbrüchen mitten in der Datei landet.
        replacement = NormalizeLineEndingsToMatch(replacement, text);

        var builder = new StringBuilder(text.Length);
        var cursor = 0;
        foreach (var match in matches)
        {
            builder.Append(text, cursor, match.Offset - cursor);

            // Nutzer-Feedback: "die Formatierung [ist] danach einen Tab weiter eingerückt als sie
            // sein sollte" - bei aktivem IgnoreFormatting kann DERSELBE Suchbegriff an Stellen mit
            // UNTERSCHIEDLICHER tatsächlicher Einrückungstiefe gefunden werden (das ist ja gerade
            // der Zweck der Funktion); ohne Anpassung würde jede Fundstelle den Ersetzungstext mit
            // GENAU der einen, absolut eingegebenen Einrückung erhalten, die nur zu EINER der
            // Fundstellen wirklich passt. ReindentReplacementToMatchContext gleicht die relative
            // Einrückungsstruktur des eingegebenen Ersetzungstextes stattdessen an die tatsächliche
            // Einrückung JEDER EINZELNEN Fundstelle an.
            var textToInsert = options.IgnoreFormatting
                ? ReindentReplacementToMatchContext(text, match.Offset, replacement)
                : replacement;
            builder.Append(textToInsert);
            cursor = match.Offset + match.Length;
        }

        builder.Append(text, cursor, text.Length - cursor);

        return (builder.ToString(), matches.Count);
    }

    /// <summary>
    /// Passt die Einrückung eines mehrzeiligen Ersetzungstextes an die tatsächliche Einrückung
    /// EINER bestimmten Fundstelle an (siehe <see cref="ReplaceAll"/>). Die erste Zeile des
    /// Ersetzungstextes braucht dafür keine eigene Einrückung - sie wird direkt hinter die im
    /// Originaltext bereits vorhandene, unverändert belassene Einrückung der Fundstelle eingefügt
    /// (siehe <see cref="FindAllIgnoringFormatting"/>: ein Treffer beginnt immer erst beim ersten
    /// Nicht-Whitespace-Zeichen). Für jede WEITERE Zeile wird die relative Differenz ihrer eigenen
    /// Einrückung gegenüber der ERSTEN Zeile des (unveränderten) Ersetzungstextes ermittelt und
    /// stattdessen auf die tatsächliche Einrückung DIESER Fundstelle aufgeschlagen - unabhängig
    /// davon, welche absolute Einrückung der Nutzer beim Eintippen des Ersetzungstextes zufällig
    /// verwendet hat. Enthält der Ersetzungstext keinen Zeilenumbruch, gibt es keine innere
    /// Einrückung zu korrigieren und der Text wird unverändert zurückgegeben.
    /// </summary>
    private static string ReindentReplacementToMatchContext(string text, int matchOffset, string replacement)
    {
        if (!ContainsLineBreak(replacement))
        {
            return replacement;
        }

        var newline = DetectDominantNewline(text) == "\r\n" ? "\r\n" : "\n";
        var lines = replacement.Split(new[] { newline }, StringSplitOptions.None);
        if (lines.Length <= 1)
        {
            return replacement;
        }

        var contextIndent = LeadingIndentBeforeMatch(text, matchOffset);
        var baseIndent = LeadingHorizontalWhitespace(lines[0]);

        // Die eigene Einrückung der ersten Zeile ist redundant mit der bereits im Originaltext
        // vorhandenen (unangetasteten) Einrückung vor der Fundstelle - wird sie hier nicht entfernt,
        // würde sie sich mit dieser addieren ("doppelt eingerückte" erste Zeile).
        lines[0] = lines[0].Substring(baseIndent.Length);

        for (var i = 1; i < lines.Length; i++)
        {
            var lineIndent = LeadingHorizontalWhitespace(lines[i]);
            var rest = lines[i].Substring(lineIndent.Length);

            string newIndent;
            if (lineIndent.Length >= baseIndent.Length)
            {
                var extra = lineIndent.Substring(baseIndent.Length);
                newIndent = contextIndent + extra;
            }
            else
            {
                var missing = baseIndent.Length - lineIndent.Length;
                newIndent = missing <= contextIndent.Length
                    ? contextIndent.Substring(0, contextIndent.Length - missing)
                    : string.Empty;
            }

            lines[i] = newIndent + rest;
        }

        return string.Join(newline, lines);
    }

    /// <summary>
    /// Liefert die tatsächliche Einrückung (Leerzeichen/Tabs) VOR <paramref name="matchOffset"/>,
    /// aber NUR, wenn diese unmittelbar auf einen Zeilenanfang folgt (Dateianfang oder
    /// Zeilenumbruch) - sitzt die Fundstelle mitten in einer Zeile (z. B. ein einzeiliger,
    /// verschachtelter Block wie "a = { b = { c = 1 } }"), gibt es keine sinnvolle "Zeileneinrückung",
    /// von der man ausgehen könnte, und es wird eine leere Zeichenkette geliefert.
    /// </summary>
    private static string LeadingIndentBeforeMatch(string text, int matchOffset)
    {
        var i = matchOffset;
        while (i > 0 && (text[i - 1] == ' ' || text[i - 1] == '\t'))
        {
            i--;
        }

        if (i > 0 && text[i - 1] != '\n' && text[i - 1] != '\r')
        {
            return string.Empty;
        }

        return text.Substring(i, matchOffset - i);
    }

    private static string LeadingHorizontalWhitespace(string line)
    {
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
        {
            i++;
        }

        return line.Substring(0, i);
    }

    /// <summary>
    /// Wandelt Zeilenumbrüche in <paramref name="value"/> (immer "\r\n", wenn es aus einer
    /// mehrzeiligen WPF-TextBox stammt) in die in <paramref name="referenceText"/> überwiegend
    /// verwendete Konvention um. Ohne diese Angleichung schlägt ein exakter Vergleich per
    /// <c>string.IndexOf</c> bei jedem mehrzeiligen Suchbegriff/Ersetzungstext fehl, sobald die
    /// Zieldatei eine andere Zeilenenden-Konvention verwendet als die Eingabe-TextBox.
    /// </summary>
    private static string NormalizeLineEndingsToMatch(string value, string referenceText)
    {
        if (!ContainsLineBreak(value))
        {
            return value;
        }

        var targetNewline = DetectDominantNewline(referenceText);

        // Erst einheitlich auf "\n" reduzieren (behandelt "\r\n" und einzelne "\r" gleich),
        // dann erst auf die Zielkonvention bringen - vermeidet eine doppelte Umwandlung von
        // "\r\n" über zwei nacheinander ausgeführte Replace-Aufrufe.
        var unified = value.Replace("\r\n", "\n").Replace("\r", "\n");
        return targetNewline == "\n" ? unified : unified.Replace("\n", targetNewline);
    }

    private static bool ContainsLineBreak(string value) => value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;

    /// <summary>
    /// Ermittelt, ob <paramref name="text"/> überwiegend "\r\n" oder reines "\n" als
    /// Zeilenumbruch verwendet (Paradox-Moddateien kommen in beiden Varianten vor). Enthält der
    /// Text gar keinen Zeilenumbruch, ist das Ergebnis ohnehin bedeutungslos, da ein mehrzeiliger
    /// Suchbegriff dort unter keiner Konvention einen Treffer finden kann.
    /// </summary>
    private static string DetectDominantNewline(string text)
    {
        var crlfCount = 0;
        var lfOnlyCount = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
            {
                continue;
            }

            if (i > 0 && text[i - 1] == '\r')
            {
                crlfCount++;
            }
            else
            {
                lfOnlyCount++;
            }
        }

        if (crlfCount == 0 && lfOnlyCount == 0)
        {
            return Environment.NewLine;
        }

        return crlfCount >= lfOnlyCount ? "\r\n" : "\n";
    }

    private static bool IsWholeWordMatch(string text, int index, int length)
    {
        var before = index - 1;
        var after = index + length;

        var beforeOk = before < 0 || !IsWordChar(text[before]);
        var afterOk = after >= text.Length || !IsWordChar(text[after]);
        return beforeOk && afterOk;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static List<int> ComputeLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return starts;
    }

    private static int LineNumberAt(List<int> lineStarts, int offset)
    {
        var lo = 0;
        var hi = lineStarts.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (lineStarts[mid] <= offset)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return lo + 1;
    }

    private static string GetLineText(string text, List<int> lineStarts, int lineNumber)
    {
        var start = lineStarts[lineNumber - 1];
        var end = lineNumber < lineStarts.Count ? lineStarts[lineNumber] : text.Length;

        while (end > start && (text[end - 1] == '\n' || text[end - 1] == '\r'))
        {
            end--;
        }

        return text.Substring(start, end - start);
    }
}
