using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParadoxModTool.ScriptDocs;

/// <summary>Datei-Inhalt einer generierten Wiki-Script-Docs-Quelle (siehe tools/wiki-scraper).</summary>
public sealed class WikiScriptDocFile
{
    public DateTime FetchedUtc { get; set; }

    public List<ScriptDocEntry> Entries { get; set; } = new();
}

/// <summary>
/// Liest eine vorab erzeugte Wiki-Script-Docs-Datei (siehe tools/wiki-scraper) statt der Doku-Logs des Spiels ein - für Spiele wie
/// Europa Universalis IV, die keine docs/*.log-Dateien schreiben. Nutzt dieselbe <see cref="ScriptDocDatabase.ApplyImport"/>-Mechanik
/// wie <see cref="ScriptDocImporter"/>: die Datei selbst zählt als "eine Quelle", ihre Schreibzeit als deren Tick.
/// </summary>
public static class WikiScriptDocImporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private const string SourceKey = "wiki-scriptdocs";

    /// <summary>
    /// Alle Arten, die die Wiki-Quelle grundsätzlich abdeckt (siehe tools/wiki-scraper): anders als bei den Doku-Logs (eine Datei je Art)
    /// steckt hier alles in EINER Datei, die der Scraper immer als Ganzes neu erzeugt - fehlt eine Art in einem gelesenen Import völlig,
    /// gilt sie trotzdem als "gelesen", damit ein wirklich aus der Wiki verschwundener Eintrag korrekt veraltet, statt für immer aktuell
    /// zu bleiben (siehe <see cref="ScriptDocDatabase.IsOutdated"/>).
    /// </summary>
    private static readonly ScriptDocKind[] WikiKinds =
        { ScriptDocKind.Modifier, ScriptDocKind.Trigger, ScriptDocKind.Effect, ScriptDocKind.OnAction };

    public static long? ReadWriteTicks(string wikiFilePath) =>
        File.Exists(wikiFilePath) ? File.GetLastWriteTimeUtc(wikiFilePath).Ticks : null;

    /// <summary>
    /// Liest nur den "Stand vom"-Zeitstempel der Wiki-Datei, für die Anzeige (z. B. <see cref="ScriptDocsRefreshResult"/> im App-Projekt).
    /// <see langword="null"/> bei fehlender oder unlesbarer Datei - nutzt dieselben <see cref="Options"/> wie <see cref="Import"/>, damit
    /// dieselbe Datei hier nicht ohne den Enum-Converter erneut (und fehlerhaft) deserialisiert wird.
    /// </summary>
    public static DateTime? ReadFetchedUtc(string wikiFilePath)
    {
        if (!File.Exists(wikiFilePath))
        {
            return null;
        }

        try
        {
            var file = JsonSerializer.Deserialize<WikiScriptDocFile>(File.ReadAllText(wikiFilePath), Options);
            return file?.FetchedUtc;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Braucht es einen (Re-)Import? Ja, wenn die Datei fehlt in der DB, oder ihre Schreibzeit sich geändert hat.</summary>
    public static bool NeedsImport(ScriptDocDatabase database, string wikiFilePath)
    {
        var ticks = ReadWriteTicks(wikiFilePath);
        if (ticks is null)
        {
            return false;
        }

        var lastImport = database.Imports.OrderByDescending(i => i.Id).FirstOrDefault();
        return lastImport is null
            || !lastImport.SourceWriteTicks.TryGetValue(SourceKey, out var known)
            || known != ticks.Value;
    }

    /// <summary>
    /// Liest die Wiki-Datei und übernimmt ihre Einträge in <paramref name="database"/>. Eine fehlende oder leere Datei (keine Einträge)
    /// zählt nicht als gelesen - sonst würden alle Einträge fälschlich als veraltet gelten. Gibt <see langword="null"/> zurück, wenn
    /// nichts gelesen werden konnte.
    /// </summary>
    public static ImportRecord? Import(ScriptDocDatabase database, string wikiFilePath, DateTime nowUtc)
    {
        if (!File.Exists(wikiFilePath))
        {
            return null;
        }

        WikiScriptDocFile? file;
        long writeTicks;
        try
        {
            writeTicks = File.GetLastWriteTimeUtc(wikiFilePath).Ticks;
            file = JsonSerializer.Deserialize<WikiScriptDocFile>(File.ReadAllText(wikiFilePath), Options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }

        if (file is null || file.Entries.Count == 0)
        {
            return null;
        }

        var merged = ScriptDocImporter.MergeDuplicates(file.Entries);
        return database.ApplyImport(merged, WikiKinds, nowUtc, new Dictionary<string, long> { [SourceKey] = writeTicks });
    }
}
