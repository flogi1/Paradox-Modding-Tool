using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.ErrorLog;

/// <summary>
/// Ein Scope, wie <c>debug_log_scopes</c> ihn schreibt. EU V: <c>Country 'England' (ENG)</c>, <c>Character 'Name' (97)</c>. Crusader Kings III:
/// <c>Name (Internal ID: 32769 - Historical ID 522) weak (Character - 32769)!</c>. Victoria 3: <c>Land: Belgien (25)</c>.
/// </summary>
public sealed record TraceScope(string Kind, string Name, string Id)
{
    private static readonly Regex Pattern = new(@"^(?<kind>[A-Za-z_][A-Za-z_ ]*) '(?<name>.*)' \((?<id>[^()]*)\)$", RegexOptions.CultureInvariant);

    private static readonly Regex Ck3Pattern = new(@"^(?<name>.*?)\s*\([^()]*\)\s+(?:[a-z]+\s+)?\((?<kind>[A-Za-z_]+) - (?<id>[^()]*)\)!$", RegexOptions.CultureInvariant);

    /// <summary>Victoria 3: <c>Land: Belgien (25)</c>, die Art in der Sprache der Spieloberfläche.</summary>
    private static readonly Regex Vic3Pattern = new(@"^(?<kind>[^:'()]+): (?<name>.+?) \((?<id>[^()]*)\)$", RegexOptions.CultureInvariant);

    public static TraceScope? Parse(string text)
    {
        var trimmed = text.Trim();
        var match = Pattern.Match(trimmed);
        if (match.Success)
        {
            return new TraceScope(match.Groups["kind"].Value, match.Groups["name"].Value, match.Groups["id"].Value);
        }

        match = Ck3Pattern.Match(trimmed);
        if (match.Success)
        {
            return new TraceScope(match.Groups["kind"].Value, match.Groups["name"].Value.Trim(), match.Groups["id"].Value);
        }

        match = Vic3Pattern.Match(trimmed);
        return match.Success ? new TraceScope(match.Groups["kind"].Value.Trim(), match.Groups["name"].Value.Trim(), match.Groups["id"].Value) : null;
    }

    public override string ToString() => $"{Kind} '{Name}' ({Id})";
}

/// <summary>Ein Treffer einer Scope-Spur in debug.log: Spur-Id, Uhrzeit, Datei und Zeile der Spur, Scope, Root und gespeicherte Event Targets.</summary>
public sealed record TraceHit(string Id, string Time, string File, int Line, TraceScope Scope, TraceScope? Root, IReadOnlyList<(string Name, TraceScope Scope)> Saved);

/// <summary>
/// Liest Treffer von Scope-Spuren aus debug.log (Spec Scope-Verfolgung, Teil 3). Format aus dem Probelauf 2026-09-26: Spur-Zeile
/// <c>&lt;datei&gt;:&lt;zeile&gt;: PMT_TRACE_&lt;id&gt;</c>, danach eine Meldung derselben Quelle mit dem Scope und bei
/// <c>debug_log_scopes = yes</c> den Folgezeilen <c>Root:</c> und <c>Saved event targets:</c>.
/// </summary>
public static class DebugLogReader
{
    private const string Marker = "PMT_TRACE_";

    /// <summary>So viele Hüllenzeilen nach einer Spur-Zeile darf die Scope-Meldung höchstens später kommen.</summary>
    private const int Window = 6;

    /// <summary>Spur-Zeile. EU V: <c>datei.txt:19: PMT_TRACE_0001</c>; Crusader Kings III: <c>file: datei.txt line: 9 (objekt:feld): PMT_TRACE_0001</c>.</summary>
    private static readonly Regex TraceLine = new(
        @"^(?:file: (?<file>.+?) line: (?<line>\d+) \([^)]*\)|(?<file>.+?):(?<line>\d+)): PMT_TRACE_(?<id>[0-9a-f]{4})$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// Treffer aus den Zeilen. <paramref name="scopeSource"/> ist die Quelle der Scope-Meldung, wenn sie nicht dieselbe wie die der Spur-Zeile ist
    /// (Crusader Kings III: Spur aus <c>jomini_effect_impl.cpp</c>, Scope aus <c>effectimpl.cpp</c>).
    /// </summary>
    public static IReadOnlyList<TraceHit> Hits(IReadOnlyList<LogLine> lines, string source, string? scopeSource = null)
    {
        var scopeFrom = string.IsNullOrEmpty(scopeSource) ? source : scopeSource;
        var hits = new List<TraceHit>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Source != source || TraceLine.Match(lines[i].Message) is not { Success: true } trace)
            {
                continue;
            }

            for (var j = i + 1; j < lines.Count && j <= i + Window; j++)
            {
                if (TraceLine.IsMatch(lines[j].Message))
                {
                    break;
                }

                if (lines[j].Source == scopeFrom && ScopeMessage(lines[j].Message) is var (scope, root, saved) && scope is not null)
                {
                    hits.Add(new TraceHit(
                        trace.Groups["id"].Value,
                        lines[i].Time,
                        trace.Groups["file"].Value,
                        int.Parse(trace.Groups["line"].Value, System.Globalization.CultureInfo.InvariantCulture),
                        scope,
                        root,
                        saved));
                    break;
                }
            }
        }

        return hits;
    }

    /// <summary>
    /// Liest debug.log zeilenweise und behält nur Spur-Zeilen und die Zeilen kurz danach (große Logs werden nicht ganz im Speicher gehalten).
    /// <c>End</c> ist die Position nach der letzten vollständigen Zeile, für das Weiterlesen im Live-Betrieb.
    /// </summary>
    public static (IReadOnlyList<TraceHit> Hits, LogPosition End) ReadHits(string path, string source, string? scopeSource = null)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var length = stream.Length;
        var head = new byte[Math.Min(64, length)];
        stream.ReadExactly(head);
        stream.Seek(0, SeekOrigin.Begin);

        var kept = new List<string>();
        var keptNumbers = new List<int>();
        var remaining = 0;
        var lineNumber = 0;
        long offset = 0;
        var pending = new MemoryStream();
        var buffer = new byte[1 << 20];
        long read = 0;
        while (read < length)
        {
            var count = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, length - read));
            if (count <= 0)
            {
                break;
            }

            var start = 0;
            for (var i = 0; i < count; i++)
            {
                if (buffer[i] != (byte)'\n')
                {
                    continue;
                }

                pending.Write(buffer, start, i - start);
                lineNumber++;
                var line = Decode(pending, lineNumber == 1);
                pending.SetLength(0);
                start = i + 1;
                offset = read + i + 1;

                var envelope = line.StartsWith('[');
                if (line.Contains(Marker, StringComparison.Ordinal))
                {
                    remaining = Window + 1;
                }

                if (remaining > 0)
                {
                    kept.Add(line);
                    keptNumbers.Add(lineNumber);
                    if (envelope)
                    {
                        remaining--;
                    }
                }
            }

            pending.Write(buffer, start, count - start);
            read += count;
        }

        // Nur die behaltenen Zeilen auswerten; Zeilennummern bleiben die echten (für Meldungen ohne Bedeutung, aber stimmig).
        var parsed = new List<LogLine>();
        for (var k = 0; k < kept.Count; k++)
        {
            LogReader.Append(parsed, LogReader.Parse(new[] { kept[k] }, keptNumbers[k]));
        }

        return (Hits(parsed, source, scopeSource), new LogPosition(offset, lineNumber + 1, Convert.ToHexString(head)));
    }

    private static (TraceScope? Scope, TraceScope? Root, IReadOnlyList<(string Name, TraceScope Scope)> Saved) ScopeMessage(string message)
    {
        var parts = message.Split('\n');
        var scope = TraceScope.Parse(parts[0]);
        if (scope is null)
        {
            return (null, null, Array.Empty<(string, TraceScope)>());
        }

        TraceScope? root = null;
        var saved = new List<(string Name, TraceScope Scope)>();
        var inSaved = false;
        foreach (var part in parts.Skip(1).Select(p => p.Trim()))
        {
            if (part.StartsWith("Root:", StringComparison.Ordinal))
            {
                root = TraceScope.Parse(part["Root:".Length..]);
            }
            else if (part == "Saved event targets:")
            {
                inSaved = true;
            }
            else if (inSaved && part.IndexOf(": ", StringComparison.Ordinal) is > 0 and var colon && TraceScope.Parse(part[(colon + 2)..]) is { } target)
            {
                saved.Add((part[..colon], target));
            }
        }

        return (scope, root, saved);
    }

    private static string Decode(MemoryStream bytes, bool first)
    {
        var data = bytes.GetBuffer();
        var length = (int)bytes.Length;
        var start = first && length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
        return Encoding.UTF8.GetString(data, start, length - start).TrimEnd('\r');
    }
}
