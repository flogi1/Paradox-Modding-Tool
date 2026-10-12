using System.Text.RegularExpressions;

namespace ParadoxModTool.Mods;

/// <summary>
/// Liest und ändert die Felder <c>version</c> und <c>remote_file_id</c> einer klassischen <c>.mod</c>-Datei (EU IV, CK III; siehe <see cref="ModMetadata.ClassicSiblingMetadataFile"/>), ohne den übrigen
/// Text anzufassen: gearbeitet wird zeilenweise, Zeilenenden (LF oder CRLF) und ein fehlender Zeilenumbruch am Ende bleiben. Es zählt nur ein Feld auf oberster
/// Ebene außerhalb von Kommentaren (<c>#</c>) und Anführungszeichen. Das Gegenstück für <c>metadata.json</c> ist <see cref="ModMetadataEditor"/>.
/// </summary>
public static class ClassicModEditor
{
    private const string VersionKey = "version";

    private const string RemoteFileIdKey = "remote_file_id";

    private static readonly Regex VersionLine = KeyLine(VersionKey);

    private static readonly Regex RemoteFileIdLine = KeyLine(RemoteFileIdKey);

    private static readonly Regex TagsStart = new(@"^[ \t]*tags[ \t]*=", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PictureOrSupported = new(@"^[ \t]*(picture|supported_version)[ \t]*=", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Eine Zeile ohne Zeilenende; <see cref="Start"/> ist die Position im Text, <see cref="TopLevel"/> heißt: sie beginnt außerhalb jedes Blocks und jedes Texts in Anführungszeichen.</summary>
    private sealed record Line(int Start, string Text, bool TopLevel);

    /// <summary>Der Wert von <c>version</c> auf oberster Ebene (ohne Anführungszeichen, getrimmt); <see langword="null"/>, wenn die Zeile fehlt.</summary>
    public static string? ReadVersion(string text)
    {
        return ReadField(text, VersionLine);
    }

    /// <summary>Der Wert von <c>remote_file_id</c> auf oberster Ebene (ohne Anführungszeichen, getrimmt); <see langword="null"/>, wenn die Zeile fehlt.</summary>
    public static string? ReadRemoteFileId(string text) => ReadField(text, RemoteFileIdLine);

    /// <summary>
    /// Setzt <c>remote_file_id</c> auf oberster Ebene, wie es der Launcher nach dem Hochladen tut. Eine vorhandene Zeile wird an Ort und Stelle geändert, sonst kommt
    /// <c>remote_file_id="X"</c> ans Ende. Wirft <see cref="ArgumentException"/>, wenn die ID nicht nur aus Ziffern besteht, und <see cref="InvalidOperationException"/>,
    /// wenn <c>remote_file_id</c> zwar auf oberster Ebene steht, aber nicht in der Zeilenform.
    /// </summary>
    public static string SetRemoteFileId(string text, string workshopId)
    {
        var id = workshopId?.Trim() ?? string.Empty;
        if (id.Length == 0 || !id.All(char.IsAsciiDigit))
        {
            throw new ArgumentException(ModsTexts.ClassicModInvalidRemoteFileId, nameof(workshopId));
        }

        if (ReplaceValue(text, RemoteFileIdLine, id) is { } replaced)
        {
            return replaced;
        }

        if (HasTopLevelKey(text, RemoteFileIdKey))
        {
            throw new InvalidOperationException(ModsTexts.ClassicModRemoteFileIdNotOnOwnLine);
        }

        return AppendLine(text, RemoteFileIdKey + "=\"" + id + "\"");
    }

    /// <summary>
    /// Setzt <c>version</c> auf oberster Ebene. Eine vorhandene Zeile wird an Ort und Stelle geändert (der Wert steht danach in Anführungszeichen). Fehlt sie, kommt
    /// <c>version="X"</c> wie bei den Launcher-Dateien direkt hinter den <c>tags</c>-Block, ohne ihn vor die erste <c>picture</c>- oder <c>supported_version</c>-Zeile,
    /// sonst ans Ende. Wirft <see cref="ArgumentException"/> bei leerer Version und <see cref="InvalidOperationException"/>, wenn <c>version</c> zwar auf oberster
    /// Ebene steht, aber nicht in der Zeilenform (z. B. <c>name="x" version="1.0"</c> in einer Zeile): ein Einfügen ergäbe den Schlüssel doppelt.
    /// </summary>
    public static string SetVersion(string text, string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException(ModsTexts.MetadataEmptyVersion, nameof(version));
        }

        var line = "version=\"" + version.Trim() + "\"";
        if (ReplaceValue(text, VersionLine, version.Trim()) is { } replaced)
        {
            return replaced;
        }

        if (HasTopLevelKey(text, VersionKey))
        {
            throw new InvalidOperationException(ModsTexts.ClassicModVersionNotOnOwnLine);
        }

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = Lines(text);

        // Hinter den tags-Block: nach dem Zeilenende der Zeile mit der schließenden Klammer.
        if (lines.FirstOrDefault(l => l.TopLevel && TagsStart.IsMatch(l.Text)) is { } tags && BlockEnd(text, tags.Start) is int close)
        {
            var lineEnd = text.IndexOf('\n', close);
            return lineEnd < 0
                ? text + newline + line
                : text[..(lineEnd + 1)] + line + newline + text[(lineEnd + 1)..];
        }

        if (lines.FirstOrDefault(l => l.TopLevel && PictureOrSupported.IsMatch(l.Text)) is { } before)
        {
            return text[..before.Start] + line + newline + text[before.Start..];
        }

        return AppendLine(text, line);
    }

    /// <summary>Hängt <paramref name="line"/> als letzte Zeile an; die Zeilenenden folgen dem Text, ein fehlender Zeilenumbruch am Ende bleibt.</summary>
    private static string AppendLine(string text, string line)
    {
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        if (text.Length == 0)
        {
            return line + newline;
        }

        return text.EndsWith('\n') ? text + line + newline : text + newline + line;
    }

    /// <summary>
    /// Leichte Prüfung vor dem Speichern: Klammern ausgeglichen, jedes Anführungszeichen geschlossen (Kommentare zählen nicht). Gibt eine Fehlermeldung mit Zeile zurück,
    /// sonst <see langword="null"/>.
    /// </summary>
    public static string? Validate(string text)
    {
        var open = new Stack<int>();
        var lineNumber = 1;
        var quoteLine = 0;
        var inQuote = false;
        var inComment = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\n')
            {
                lineNumber++;
                inComment = false;
                continue;
            }

            if (inComment)
            {
                continue;
            }

            if (inQuote)
            {
                inQuote = c != '"';
                continue;
            }

            switch (c)
            {
                case '#':
                    inComment = true;
                    break;
                case '"':
                    inQuote = true;
                    quoteLine = lineNumber;
                    break;
                case '{':
                    open.Push(lineNumber);
                    break;
                case '}':
                    if (open.Count == 0)
                    {
                        return ModsTexts.ClassicModUnexpectedBrace(lineNumber);
                    }

                    open.Pop();
                    break;
            }
        }

        if (inQuote)
        {
            return ModsTexts.ClassicModUnclosedQuote(quoteLine);
        }

        return open.Count > 0 ? ModsTexts.ClassicModUnclosedBrace(open.Peek()) : null;
    }

    /// <summary>Erkennt eine Zeile <c>key = Wert</c> (Wert in Anführungszeichen oder ein Wort).</summary>
    private static Regex KeyLine(string key) => new(
        @"^(?<pre>[ \t]*" + key + @"[ \t]*=[ \t]*)(?<value>""[^""\r\n]*""|[^\s#""{}]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string? ReadField(string text, Regex keyLine)
    {
        var match = FindField(text, keyLine, out _);
        if (match is null)
        {
            return null;
        }

        var value = match.Groups["value"].Value;
        return (value.StartsWith('"') ? value[1..^1] : value).Trim();
    }

    /// <summary>Ersetzt den Wert der ersten passenden Zeile auf oberster Ebene durch <c>"value"</c>; <see langword="null"/>, wenn es keine gibt.</summary>
    private static string? ReplaceValue(string text, Regex keyLine, string value)
    {
        var match = FindField(text, keyLine, out var start);
        if (match is null)
        {
            return null;
        }

        var group = match.Groups["value"];
        return text[..(start + group.Index)] + "\"" + value + "\"" + text[(start + group.Index + group.Length)..];
    }

    /// <summary>Die erste Zeile auf oberster Ebene, die <paramref name="keyLine"/> trifft; <paramref name="lineStart"/> ist ihre Position im Text.</summary>
    private static Match? FindField(string text, Regex keyLine, out int lineStart)
    {
        foreach (var line in Lines(text))
        {
            if (line.TopLevel && keyLine.Match(line.Text) is { Success: true } match)
            {
                lineStart = line.Start;
                return match;
            }
        }

        lineStart = -1;
        return null;
    }

    /// <summary>Zerlegt den Text in Zeilen (ohne <c>\r\n</c>/<c>\n</c>) und merkt je Zeile, ob sie auf oberster Ebene beginnt.</summary>
    private static List<Line> Lines(string text)
    {
        var lines = new List<Line>();
        var depth = 0;
        var inQuote = false;
        var inComment = false;
        var start = 0;
        var topLevel = true;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i == text.Length || text[i] == '\n')
            {
                var end = i > start && text[i - 1] == '\r' ? i - 1 : i;
                lines.Add(new Line(start, text[start..end], topLevel));
                start = i + 1;
                inComment = false;
                topLevel = depth == 0 && !inQuote;
                continue;
            }

            Step(text, ref i, ref depth, ref inQuote, ref inComment);
        }

        return lines;
    }

    /// <summary>Die Position der Klammer, die den ersten Block ab <paramref name="from"/> schließt; <see langword="null"/>, wenn es keinen oder keinen geschlossenen gibt.</summary>
    private static int? BlockEnd(string text, int from)
    {
        var depth = 0;
        var inQuote = false;
        var inComment = false;
        var opened = false;
        for (var i = from; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                inComment = false;
                continue;
            }

            var before = depth;
            Step(text, ref i, ref depth, ref inQuote, ref inComment);
            if (!opened && inQuote)
            {
                // tags = "…" ohne Block: kein Einfügepunkt.
                return null;
            }

            opened |= depth > before;
            if (opened && depth == 0 && before == 1)
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>
    /// Steht irgendwo auf oberster Ebene (außerhalb von Blöcken, Kommentaren und Anführungszeichen) der Schlüssel <paramref name="key"/> mit <c>=</c> dahinter?
    /// Längere Schlüssel wie <c>supported_version</c> oder <c>myversion</c> zählen nicht.
    /// </summary>
    private static bool HasTopLevelKey(string text, string key)
    {
        var depth = 0;
        var inQuote = false;
        var inComment = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                inComment = false;
                continue;
            }

            if (depth == 0 && !inQuote && !inComment
                && string.Compare(text, i, key, 0, key.Length, StringComparison.OrdinalIgnoreCase) == 0
                && (i == 0 || !IsKeyChar(text[i - 1])))
            {
                var after = i + key.Length;
                while (after < text.Length && text[after] is ' ' or '\t')
                {
                    after++;
                }

                if (after < text.Length && text[after] == '=')
                {
                    return true;
                }
            }

            Step(text, ref i, ref depth, ref inQuote, ref inComment);
        }

        return false;
    }

    private static bool IsKeyChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or ':' or '@' or '-';

    /// <summary>
    /// Verarbeitet ein Zeichen (kein Zeilenumbruch): Kommentar, Anführungszeichen, Klammertiefe. Ein Backslash ist kein Escape-Zeichen: Paradox-Dateien nutzen keins,
    /// und ein Wert wie <c>path="C:\mod\"</c> endet am letzten Anführungszeichen.
    /// </summary>
    private static void Step(string text, ref int i, ref int depth, ref bool inQuote, ref bool inComment)
    {
        var c = text[i];
        if (inComment)
        {
            return;
        }

        if (inQuote)
        {
            inQuote = c != '"';
            return;
        }

        switch (c)
        {
            case '#':
                inComment = true;
                break;
            case '"':
                inQuote = true;
                break;
            case '{':
                depth++;
                break;
            case '}':
                depth = Math.Max(0, depth - 1);
                break;
        }
    }
}
