using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Notes;

/// <summary>
/// Speichert alle Meldungen in <c>%APPDATA%\ParadoxModTool\bugreports.json</c> (außerhalb der Mod-Ordner, damit nichts mit hochgeladen wird).
/// Fehlt die Datei, liest <see cref="Load"/> einmalig die alte <c>bugs.json</c> im selben Ordner, schreibt sie aber nie: ältere PMT-Builds
/// benutzen sie weiter. Geschrieben wird nur über <see cref="Update"/> (unter Sperre frisch lesen, ändern, atomar schreiben).
/// </summary>
public static class BugReportStore
{
    public const string FileName = "bugreports.json";

    public const string LegacyFileName = "bugs.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string GetDefaultPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ParadoxModTool", FileName);

    public static void Save(BugData data, string path)
    {
        data.Version = BugData.CurrentVersion;
        SharedFile.WriteAllTextAtomic(path, JsonSerializer.Serialize(data, Options));
    }

    /// <summary>Für die Anzeige: nie eine Ausnahme; ist die Datei gerade nicht lesbar, ergibt das einen leeren Stand.</summary>
    public static BugData Load(string path)
    {
        try
        {
            return LoadStrict(path);
        }
        catch (IOException)
        {
            return new BugData();
        }
        catch (UnauthorizedAccessException)
        {
            return new BugData();
        }
    }

    /// <summary>
    /// Wie <see cref="Load"/>, aber Lesefehler (Datei gesperrt, keine Rechte) werfen, damit <see cref="Update"/> nie einen leeren Stand über
    /// echte Daten schreibt. Kaputtes JSON ergibt einen leeren Stand; die kaputte Datei bleibt als <c>bugreports.broken.json</c> liegen.
    /// </summary>
    public static BugData LoadStrict(string path)
    {
        if (!File.Exists(path))
        {
            return LoadLegacy(Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, LegacyFileName));
        }

        var json = File.ReadAllText(path);
        try
        {
            return Normalize(JsonSerializer.Deserialize<BugData>(json, Options));
        }
        catch (JsonException)
        {
            KeepBrokenCopy(path);
            return new BugData();
        }
    }

    /// <summary>Wendet <paramref name="change"/> auf den frisch gelesenen Stand an und speichert ihn, alles unter der Sperre der Datei.</summary>
    public static BugData Update(string path, Action<BugData> change) =>
        SharedFile.Locked(path, () =>
        {
            var data = LoadStrict(path);
            change(data);
            Save(data, path);
            return data;
        });

    private static BugData LoadLegacy(string legacyPath)
    {
        if (!File.Exists(legacyPath))
        {
            return new BugData();
        }

        try
        {
            return Normalize(BugLegacy.Convert(File.ReadAllText(legacyPath)));
        }
        catch (JsonException)
        {
            return new BugData();
        }
    }

    private static void KeepBrokenCopy(string path)
    {
        var broken = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, Path.GetFileNameWithoutExtension(path) + ".broken.json");
        if (!File.Exists(broken) || File.GetLastWriteTimeUtc(path) > File.GetLastWriteTimeUtc(broken))
        {
            File.Copy(path, broken, overwrite: true);
        }
    }

    /// <summary>JSON darf <c>null</c> für Listen und Texte enthalten; im Modell sind sie nie <see langword="null"/>.</summary>
    private static BugData Normalize(BugData? data)
    {
        data ??= new BugData();
        data.Reports ??= new List<BugReport>();
        data.Reports.RemoveAll(r => r is null);
        foreach (var report in data.Reports)
        {
            report.Id = string.IsNullOrEmpty(report.Id) ? Guid.NewGuid().ToString("N") : report.Id;
            report.SourceKey ??= string.Empty;
            report.ExternalId ??= string.Empty;
            report.Url ??= string.Empty;
            report.Title ??= string.Empty;
            report.Body ??= string.Empty;
            report.Author ??= string.Empty;
            report.Links ??= new List<BugLink>();
            report.Attachments = (report.Attachments ?? new List<BugAttachment>())
                .Where(a => a is not null && !string.IsNullOrEmpty(a.Url))
                .Select(a => a with { FileName = a.FileName ?? string.Empty, ContentType = a.ContentType ?? string.Empty })
                .ToList();
            report.Links.RemoveAll(l => l is null || string.IsNullOrEmpty(l.Key));
        }

        return data;
    }
}
