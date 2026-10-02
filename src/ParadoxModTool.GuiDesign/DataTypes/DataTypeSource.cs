using System.Text;
using System.Text.Json;

namespace ParadoxModTool.GuiDesign.DataTypes;

/// <summary>Zustand der Datentyp-Dateien: aktuell, älter als das Spiel oder nicht vorhanden.</summary>
public enum DataTypeStatus
{
    Current,
    Stale,
    Missing,
}

/// <summary>Ergebnis des Ladens: Datenbank, Zustand, jüngste Änderungszeit der Dateien und nicht lesbare Dateien.</summary>
public sealed record DataTypeLoad(DataTypeDatabase Database, DataTypeStatus Status, DateTime? FilesUtc, IReadOnlyList<string> Unreadable);

/// <summary>
/// Findet und lädt den Datentyp-Dump eines Spiels (<c>Documents\Paradox Interactive\&lt;Spiel&gt;\logs\data_types</c>) mit Zwischenspeicher und prüft, ob er
/// älter als das Spiel ist (dann nach einem Update vermutlich veraltet).
/// </summary>
public static class DataTypeSource
{
    private static readonly JsonSerializerOptions Options = new();

    /// <summary>Der Ordner zum Mod-Pfad des Spielprofils: dessen Elternordner plus <c>logs\data_types</c>; <see langword="null"/> ohne Mod-Pfad.</summary>
    public static string? FolderFor(string defaultModRootTemplate)
    {
        if (string.IsNullOrWhiteSpace(defaultModRootTemplate))
        {
            return null;
        }

        var modRoot = Environment.ExpandEnvironmentVariables(defaultModRootTemplate).Replace('/', '\\').TrimEnd('\\');
        return Path.GetDirectoryName(modRoot) is { Length: > 0 } documents ? Path.Combine(documents, "logs", "data_types") : null;
    }

    public static DataTypeLoad Load(string? folder, string? installDirectory, string cachePath)
    {
        var files = folder is not null && Directory.Exists(folder)
            ? Directory.GetFiles(folder, DataTypeLogParser.FilePattern).Order(StringComparer.OrdinalIgnoreCase).Select(p => new FileInfo(p)).ToList()
            : new List<FileInfo>();
        if (files.Count == 0)
        {
            return new DataTypeLoad(DataTypeDatabase.Empty, DataTypeStatus.Missing, null, Array.Empty<string>());
        }

        var filesUtc = files.Max(f => f.LastWriteTimeUtc);
        var status = NewestExeUtc(installDirectory) is { } exeUtc && filesUtc < exeUtc ? DataTypeStatus.Stale : DataTypeStatus.Current;
        var fingerprint = string.Join("|", files.Select(f => $"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));

        if (TryReadCache(cachePath, fingerprint) is { } cached)
        {
            return new DataTypeLoad(new DataTypeDatabase(cached), status, filesUtc, Array.Empty<string>());
        }

        var entries = new List<DataTypeEntry>();
        var unreadable = new List<string>();
        foreach (var file in files)
        {
            try
            {
                entries.AddRange(DataTypeLogParser.Parse(File.ReadAllText(file.FullName, Encoding.UTF8), file.Name));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                unreadable.Add(file.Name);
            }
        }

        if (unreadable.Count == 0)
        {
            TryWriteCache(cachePath, fingerprint, entries);
        }

        return new DataTypeLoad(new DataTypeDatabase(entries), status, filesUtc, unreadable);
    }

    /// <summary>Jüngste Änderungszeit der <c>.exe</c>-Dateien im Installationsordner, bis zwei Ordnerebenen tief.</summary>
    private static DateTime? NewestExeUtc(string? installDirectory)
    {
        if (installDirectory is null || !Directory.Exists(installDirectory))
        {
            return null;
        }

        var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 2, IgnoreInaccessible = true };
        var times = Directory.EnumerateFiles(installDirectory, "*.exe", options).Select(File.GetLastWriteTimeUtc).ToList();
        return times.Count == 0 ? null : times.Max();
    }

    private sealed class CacheFile
    {
        public string Fingerprint { get; set; } = string.Empty;

        public List<DataTypeEntry> Entries { get; set; } = new();
    }

    private static List<DataTypeEntry>? TryReadCache(string path, string fingerprint)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var cache = JsonSerializer.Deserialize<CacheFile>(File.ReadAllText(path), Options);
            return cache is not null && cache.Fingerprint == fingerprint ? cache.Entries : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private static void TryWriteCache(string path, string fingerprint, List<DataTypeEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new CacheFile { Fingerprint = fingerprint, Entries = entries }, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ohne Zwischenspeicher wird beim nächsten Öffnen neu gelesen.
        }
    }
}
