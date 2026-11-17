using System.Text.Json.Nodes;

namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Bestmögliche Erkennung der aktuell installierten Spielversion aus "launcher-settings.json"
/// im Spielordner (Abschnitt 7, Versionshistorie). Der exakte Feldname ist NICHT gegen eine
/// echte Installation oder die Paradox-Wiki verifiziert (gleiche Kategorie wie die bereits
/// bekannte unverifizierte Annahme zu "isActive" in playsets.json) - deshalb werden mehrere
/// plausible Feldnamen versucht, und ein <see langword="null"/>-Ergebnis wird von der aufrufenden
/// UI als "manuelle Eingabe nötig" behandelt, nie als Fehler.
/// </summary>
public static class GameVersionReader
{
    private static readonly string[] CandidateFieldNames = { "rawVersion", "version", "gameVersion" };

    public static string? TryReadVersion(string gameRoot)
    {
        return TryReadFromFile(Path.Combine(gameRoot, "launcher-settings.json"))
            ?? TryReadFromFile(Path.Combine(gameRoot, "binaries", "launcher-settings.json"))
            ?? TryReadFromFile(Path.Combine(gameRoot, "launcher", "launcher-settings.json"))
            ?? TryReadFromFile(Path.Combine(gameRoot, "..", "launcher", "launcher-settings.json")) // CK III: game/ und launcher/ nebeneinander
            ?? TryReadFromExecutable(Path.Combine(gameRoot, "..", "binaries")); // EU V: game/ und binaries/ nebeneinander
    }

    private static readonly byte[] ReleaseMarker = System.Text.Encoding.ASCII.GetBytes("/release/");
    private static readonly Dictionary<(string Path, DateTime Written, long Length), string?> ExeCache = new();

    /// <summary>
    /// EU V hat kein launcher-settings.json: Die Version steht als Zweigname "…/release/1.3.11" in der Exe (geprüft an EU V 1.3.11,
    /// 2026-09-30). Das Ergebnis wird je Datei, Stand und Größe gemerkt, weil die Exe über 100 MB groß ist.
    /// </summary>
    private static string? TryReadFromExecutable(string binariesFolder)
    {
        if (!Directory.Exists(binariesFolder))
        {
            return null;
        }

        foreach (var exe in Directory.EnumerateFiles(binariesFolder, "*.exe").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var info = new FileInfo(exe);
                var key = (info.FullName, info.LastWriteTimeUtc, info.Length);
                string? version;
                lock (ExeCache)
                {
                    if (ExeCache.TryGetValue(key, out version))
                    {
                        if (version is not null)
                        {
                            return version;
                        }

                        continue;
                    }
                }

                version = FindReleaseVersion(exe) ?? FindVersionBeforeGitMarker(exe);
                lock (ExeCache)
                {
                    ExeCache[key] = version;
                }

                if (version is not null)
                {
                    return version;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Nicht lesbar: nächste Exe.
            }
        }

        return null;
    }

    /// <summary>Erste Stelle "/release/" gefolgt von Ziffern und Punkten (mindestens ein Punkt); liest in Blöcken mit Überlappung.</summary>
    private static string? FindReleaseVersion(string path)
    {
        const int Overlap = 64;
        var buffer = new byte[4 * 1024 * 1024 + Overlap];
        using var stream = File.OpenRead(path);
        var carried = 0;
        int read;
        while ((read = stream.Read(buffer, carried, buffer.Length - carried)) > 0)
        {
            var length = carried + read;
            var span = buffer.AsSpan(0, length);
            for (var at = span.IndexOf(ReleaseMarker); at >= 0;)
            {
                var start = at + ReleaseMarker.Length;
                var end = start;
                while (end < length && (char.IsAsciiDigit((char)span[end]) || span[end] == (byte)'.'))
                {
                    end++;
                }

                var text = System.Text.Encoding.ASCII.GetString(span[start..end]).Trim('.');
                if (end < length && text.Contains('.') && char.IsAsciiDigit(text[0]))
                {
                    return text;
                }

                var next = span[(at + 1)..].IndexOf(ReleaseMarker);
                at = next < 0 ? -1 : at + 1 + next;
            }

            carried = Math.Min(Overlap, length);
            span[(length - carried)..].CopyTo(buffer);
        }

        return null;
    }

    private static readonly byte[] GitMarker = System.Text.Encoding.ASCII.GetBytes("\0Exe Git Version");

    /// <summary>
    /// Ab EU V 1.4.7 heißt der Zweig "…/release/next" ohne Nummer (geprüft 2026-11-16). Die Version steht dann als
    /// "1.4.7\0Río Salado\0Exe Git Version: {}" in der Exe: Versionsnummer, Versionsname, dann der Marker.
    /// </summary>
    private static string? FindVersionBeforeGitMarker(string path)
    {
        const int Overlap = 256;
        var buffer = new byte[4 * 1024 * 1024 + Overlap];
        using var stream = File.OpenRead(path);
        var carried = 0;
        int read;
        while ((read = stream.Read(buffer, carried, buffer.Length - carried)) > 0)
        {
            var length = carried + read;
            var span = buffer.AsSpan(0, length);
            for (var from = 0; from < length;)
            {
                var found = span[from..].IndexOf(GitMarker);
                if (found < 0)
                {
                    break;
                }

                var at = from + found;
                from = at + 1;
                var nameEnd = at; // Ende des Versionsnamens (der NUL des Markers)
                var nameStart = nameEnd;
                while (nameStart > 0 && span[nameStart - 1] != 0 && nameEnd - nameStart < 64)
                {
                    nameStart--;
                }

                if (nameStart < 2 || span[nameStart - 1] != 0)
                {
                    continue;
                }

                var verEnd = nameStart - 1;
                var verStart = verEnd;
                while (verStart > 0 && (char.IsAsciiDigit((char)span[verStart - 1]) || span[verStart - 1] == (byte)'.'))
                {
                    verStart--;
                }

                var text = System.Text.Encoding.ASCII.GetString(span[verStart..verEnd]);
                if (text.Contains('.') && char.IsAsciiDigit(text[0]) && (verStart == 0 || span[verStart - 1] == 0))
                {
                    return text;
                }
            }

            carried = Math.Min(Overlap, length);
            span[(length - carried)..].CopyTo(buffer);
        }

        return null;
    }

    private static string? TryReadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var node = JsonNode.Parse(json);

            foreach (var fieldName in CandidateFieldNames)
            {
                var value = node?[fieldName]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or InvalidOperationException)
        {
            return null;
        }
    }
}
