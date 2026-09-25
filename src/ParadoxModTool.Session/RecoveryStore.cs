using System.IO;
using System.Text.Json;

namespace ParadoxModTool.Session;

/// <summary>
/// Gesicherter, noch nicht gespeicherter Stand eines geöffneten Dokuments (Nutzer-Feedback:
/// ungespeicherte Änderungen sollen über Programmende und Abstürze hinweg erhalten bleiben).
/// </summary>
public sealed class RecoveryEntry
{
    /// <summary>Eindeutige Kennung des Dokuments in der laufenden Sitzung (= Dateiname der Sicherung).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Pfad der zugehörigen Datei, oder <see langword="null"/> für ein noch nie gespeichertes "Unbenannt"-Dokument.</summary>
    public string? FilePath { get; set; }

    /// <summary>Der ungespeicherte Textinhalt.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Letzter Schreibzeitpunkt (UTC-Ticks) der Datei auf der Platte zum Zeitpunkt der Sicherung -
    /// dient beim Wiederherstellen dazu zu erkennen, ob die Datei seither außerhalb des Programms
    /// verändert wurde. <see langword="null"/> ohne Datei.
    /// </summary>
    public long? DiskWriteTimeUtcTicks { get; set; }
}

/// <summary>
/// Wiederherstellungsspeicher für ungespeicherte Dokumente: eine JSON-Datei pro Dokument unter
/// <c>%APPDATA%\ParadoxModTool\recovery\</c>. Bewusst getrennt von der Session-Datei, damit auch
/// nach einem Absturz (Session-Datei wird nur beim regulären Beenden geschrieben) der letzte
/// Autosave-Stand erhalten ist. Rein dateibasiert und UI-unabhängig, damit testbar.
/// </summary>
public sealed class RecoveryStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    private readonly string _directory;

    // Nur selbst geschriebene Sicherungen werden von Sync() wieder gelöscht - so überschreibt/
    // löscht eine zweite gleichzeitig laufende Programminstanz keine fremden Sicherungen.
    private readonly HashSet<string> _written = new(StringComparer.OrdinalIgnoreCase);

    public RecoveryStore(string directory)
    {
        _directory = directory;
    }

    public static string GetDefaultDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ParadoxModTool", "recovery");
    }

    /// <summary>
    /// Bringt den Speicher in Einklang mit den aktuell ungespeicherten Dokumenten: schreibt/aktualisiert
    /// deren Sicherungen und löscht selbst geschriebene Sicherungen von Dokumenten, die nicht mehr
    /// ungespeichert sind (gespeichert, verworfen oder geschlossen).
    /// </summary>
    public void Sync(IReadOnlyCollection<RecoveryEntry> current)
    {
        var keep = new HashSet<string>(current.Select(e => e.Id), StringComparer.OrdinalIgnoreCase);

        foreach (var stale in _written.Where(id => !keep.Contains(id)).ToList())
        {
            Delete(stale);
        }

        if (current.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(_directory);

        foreach (var entry in current)
        {
            File.WriteAllText(PathFor(entry.Id), JsonSerializer.Serialize(entry, Options));
            _written.Add(entry.Id);
        }
    }

    /// <summary>Lädt alle vorhandenen Sicherungen (auch die einer früheren, abgestürzten Sitzung). Defekte Dateien werden übersprungen.</summary>
    public IReadOnlyList<RecoveryEntry> LoadAll()
    {
        var result = new List<RecoveryEntry>();

        if (!Directory.Exists(_directory))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                var entry = JsonSerializer.Deserialize<RecoveryEntry>(File.ReadAllText(file), Options);
                if (entry is not null && !string.IsNullOrEmpty(entry.Id))
                {
                    result.Add(entry);
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                // Beschädigte/gesperrte Sicherung überspringen - darf die Wiederherstellung der übrigen nicht verhindern.
            }
        }

        return result;
    }

    /// <summary>Löscht die Sicherung mit dieser Kennung (auch eine aus einer früheren Sitzung), falls vorhanden.</summary>
    public void Delete(string id)
    {
        try
        {
            var path = PathFor(id);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nicht löschbar (gesperrt) - beim nächsten Sync erneut versucht, sofern selbst geschrieben.
            return;
        }

        _written.Remove(id);
    }

    private string PathFor(string id) => Path.Combine(_directory, id + ".json");
}
