using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.ErrorLog;

/// <summary>
/// Liest EU-V- und CK-III-Logs zeilenweise. Jede Zeile hat die Hülle <c>[HH:MM:SS][quelle.cpp:N]: Meldung</c> (CK III mit Level: <c>[HH:MM:SS][E][quelle.cpp:N]:</c>); Zeilen ohne Hülle (eingerückt oder nicht)
/// gehören zur vorigen Meldung. Die Datei wird geteilt geöffnet, weil das Spiel sie offen hält. Rotation (Spielstart benennt error.log in
/// error.1.log um) erkennt <see cref="ReadFrom"/> an kleinerer Größe oder geändertem Dateianfang (Plan-Ruling 6).
/// </summary>
public static class LogReader
{
    private const int HeadLength = 64;

    private static readonly Regex Envelope = new(
        @"^\[(?<time>\d{2}:\d{2}:\d{2})\](?:\[[A-Z]\])?\[(?<source>[^\]:]+)(?::(?<sline>\d+))?\]:\s*(?<msg>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static List<LogLine> Parse(IEnumerable<string> lines, int firstLineNumber = 1, int part = 0)
    {
        var result = new List<LogLine>();
        var number = firstLineNumber - 1;
        foreach (var raw in lines)
        {
            number++;
            var line = raw.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var match = Envelope.Match(line);
            if (match.Success)
            {
                result.Add(new LogLine(
                    match.Groups["time"].Value,
                    match.Groups["source"].Value,
                    match.Groups["sline"].Success ? int.Parse(match.Groups["sline"].Value, CultureInfo.InvariantCulture) : null,
                    match.Groups["msg"].Value.Trim(),
                    number,
                    Part: part));
                continue;
            }

            // Auch nicht eingerückte Zeilen ohne Hülle setzen die vorige Meldung fort (Aufrufkette nach "Script location:", mehrzeilige Lesefehler).
            if (result.Count > 0)
            {
                result[^1] = result[^1] with { Message = result[^1].Message + "\n" + line.Trim() };
            }
            else
            {
                result.Add(new LogLine(string.Empty, string.Empty, null, line.Trim(), number, IsContinuation: true, Part: part));
            }
        }

        return result;
    }

    public static List<LogLine> ReadFile(string path, int part = 0)
    {
        using var stream = OpenShared(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Parse(reader.ReadToEnd().Split('\n'), part: part);
    }

    /// <summary>Liest alle vollständigen Zeilen ab <paramref name="position"/>. Eine Zeile ohne abschließendes "\n" bleibt für den nächsten Aufruf liegen.</summary>
    public static LogChunk ReadFrom(string path, LogPosition position, int part = 0)
    {
        using var stream = OpenShared(path);
        var length = stream.Length;
        var head = ReadHead(stream);
        // Neustart: Datei kürzer als der Offset, oder der Dateianfang hat sich geändert. Eine wachsende Datei behält ihren Anfang;
        // war sie beim letzten Lesen kürzer als 64 Bytes, ist der alte Anfang ein Präfix des neuen.
        var restarted = position.Offset > length
            || (position.Head.Length > 0
                && !head.StartsWith(position.Head, StringComparison.Ordinal)
                && !position.Head.StartsWith(head, StringComparison.Ordinal));
        var offset = restarted ? 0 : position.Offset;
        var lineNumber = restarted ? 1 : position.NextLineNumber;

        stream.Seek(offset, SeekOrigin.Begin);
        var bytes = new byte[length - offset];
        stream.ReadExactly(bytes);

        var lastNewline = Array.LastIndexOf(bytes, (byte)'\n');
        if (lastNewline < 0)
        {
            return new LogChunk(Array.Empty<LogLine>(), new LogPosition(offset, lineNumber, head), restarted);
        }

        var start = offset == 0 && bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        var text = Encoding.UTF8.GetString(bytes, start, lastNewline + 1 - start);
        var lines = text.Split('\n');
        var parsed = Parse(lines.Take(lines.Length - 1), lineNumber, part);
        return new LogChunk(parsed, new LogPosition(offset + lastNewline + 1, lineNumber + lines.Length - 1, head), restarted);
    }

    /// <summary>
    /// Liest alle Dateien eines Laufs in Reihenfolge (älteste zuerst, <see cref="LogLine.Part"/> = Position im Lauf). Ältere Teile sind
    /// abgeschlossen und werden ganz gelesen; die neueste nur bis zur letzten vollständigen Zeile, damit die Live-Beobachtung dort ansetzt.
    /// </summary>
    public static RunRead ReadRun(LogRun run)
    {
        var lines = new List<LogLine>();
        var last = run.Files.Count - 1;
        for (var part = 0; part < last; part++)
        {
            Append(lines, ReadFile(run.Files[part].Path, part));
        }

        var newest = ReadFrom(run.Files[last].Path, LogPosition.Start, last);
        Append(lines, newest.Lines);
        return new RunRead(lines, newest.Next);
    }

    /// <summary>
    /// Uhrzeit der ersten und der letzten Zeile mit Hülle (<c>null</c> ohne solche Zeile). Die letzte wird von hinten in Blöcken gesucht,
    /// damit große Logs nicht ganz gelesen werden.
    /// </summary>
    public static (TimeSpan? First, TimeSpan? Last) FirstAndLastTime(string path)
    {
        using var stream = OpenShared(path);
        var first = FirstTime(stream);
        return first is null ? (null, null) : (first, LastTime(stream));
    }

    private static TimeSpan? FirstTime(FileStream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        while (reader.ReadLine() is { } line)
        {
            if (TimeOf(line) is { } time)
            {
                return time;
            }
        }

        return null;
    }

    private static TimeSpan? LastTime(FileStream stream)
    {
        const int block = 64 * 1024;
        var end = stream.Length;
        var carry = Array.Empty<byte>();
        while (end > 0)
        {
            var start = Math.Max(0, end - block);
            var buffer = new byte[end - start + carry.Length];
            stream.Seek(start, SeekOrigin.Begin);
            stream.ReadExactly(buffer, 0, (int)(end - start));
            carry.CopyTo(buffer, (int)(end - start));

            // Die erste Zeile im Block kann angeschnitten sein; sie wandert in den nächsten (davor liegenden) Block.
            var cut = start == 0 ? 0 : Array.IndexOf(buffer, (byte)'\n') + 1;
            if (start > 0 && cut == 0)
            {
                carry = buffer;
                end = start;
                continue;
            }

            var lines = Encoding.UTF8.GetString(buffer, cut, buffer.Length - cut).Split('\n');
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                if (TimeOf(lines[i].TrimEnd('\r')) is { } time)
                {
                    return time;
                }
            }

            carry = buffer[..cut];
            end = start;
        }

        return null;
    }

    private static TimeSpan? TimeOf(string line)
    {
        var match = Envelope.Match(line.TrimStart('﻿'));
        return match.Success && TimeSpan.TryParseExact(match.Groups["time"].Value, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var time)
            ? time
            : null;
    }

    /// <summary>Hängt eine Leseportion an; Folgezeilen am Anfang der Portion (<see cref="LogLine.IsContinuation"/>) gehören zur letzten vorhandenen Meldung.</summary>
    public static void Append(List<LogLine> existing, IReadOnlyList<LogLine> chunk)
    {
        foreach (var line in chunk)
        {
            if (line.IsContinuation && existing.Count > 0)
            {
                existing[^1] = existing[^1] with { Message = existing[^1].Message + "\n" + line.Message };
            }
            else
            {
                existing.Add(line with { IsContinuation = false });
            }
        }
    }

    private static FileStream OpenShared(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static string ReadHead(FileStream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        var buffer = new byte[Math.Min(HeadLength, stream.Length)];
        stream.ReadExactly(buffer);
        return Convert.ToHexString(buffer);
    }
}
