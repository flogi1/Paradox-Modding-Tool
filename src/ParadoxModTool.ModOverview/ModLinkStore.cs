using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.ModOverview;

/// <summary>Selbst eingetragene Links einer Mod (Forum, Discord-Einladung, eigener GitHub-Link), je Spiel und Mod-ID.</summary>
public sealed class ModLinkEntry
{
    public string GameId { get; set; } = string.Empty;

    public string ModId { get; set; } = string.Empty;

    public string? ForumUrl { get; set; }

    public string? DiscordUrl { get; set; }

    public string? GitHubUrl { get; set; }

    /// <summary>In der Übersicht übernommene Workshop-ID (nur Ziffern); gilt, wenn weder Register noch Metadaten-Datei eine haben.</summary>
    public string? WorkshopId { get; set; }
}

/// <summary>
/// Die Datei <c>overview\links.json</c>. Lesen verträgt eine fehlende oder kaputte Datei (leer); Schreiben liest unter der Sperre frisch, ändert und
/// schreibt atomar, damit zwei offene PMT-Fenster sich nichts überschreiben.
/// </summary>
public static class ModLinkStore
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Für die Anzeige: nie eine Ausnahme; eine fehlende, kaputte oder gerade nicht lesbare Datei ergibt keine Links.</summary>
    public static IReadOnlyList<ModLinkEntry> Load(string path)
    {
        try
        {
            return LoadStrict(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<ModLinkEntry>();
        }
    }

    /// <summary>Wie <see cref="Load"/>, aber Lesefehler werfen, damit <see cref="Set"/> nie einen leeren Stand über echte Daten schreibt. Kaputtes JSON ist leer.</summary>
    private static IReadOnlyList<ModLinkEntry> LoadStrict(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<ModLinkEntry>();
        }

        var json = File.ReadAllText(path);
        try
        {
            var file = JsonSerializer.Deserialize<LinksFile>(json, Options);
            return (file?.Links ?? new List<ModLinkEntry?>())
                .Where(e => e is not null && e.GameId.Length > 0 && e.ModId.Length > 0)
                .Select(e => e!)
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<ModLinkEntry>();
        }
    }

    public static ModLinkEntry? Find(IReadOnlyList<ModLinkEntry> all, string gameId, string modId) =>
        all.FirstOrDefault(e => e.GameId == gameId && e.ModId == modId);

    /// <summary>Nur absolute http- und https-Adressen.</summary>
    public static bool IsValidUrl(string? text) =>
        Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Setzt die drei Links einer Mod; ein leerer Wert entfernt den Link, ohne Links fällt der Eintrag weg. Eine ungültige Adresse wirft
    /// <see cref="ArgumentException"/>, bevor etwas geschrieben wird.
    /// </summary>
    public static void Set(string path, string gameId, string modId, string? forum, string? discord, string? gitHub)
    {
        var values = new[] { Clean(forum), Clean(discord), Clean(gitHub) };
        if (values.FirstOrDefault(v => v is not null && !IsValidUrl(v)) is { } invalid)
        {
            throw new ArgumentException(invalid);
        }

        Update(path, gameId, modId, entry =>
        {
            entry.ForumUrl = values[0];
            entry.DiscordUrl = values[1];
            entry.GitHubUrl = values[2];
        });
    }

    /// <summary>
    /// Merkt eine Workshop-ID, die die Übersicht auf der Steam-Profilseite gefunden und der Nutzer übernommen hat (für Mods ohne ID im Register und in
    /// der Metadaten-Datei). <see langword="null"/> oder leer entfernt sie; eine ID aus anderem als Ziffern wirft <see cref="ArgumentException"/>.
    /// </summary>
    public static void SetWorkshopId(string path, string gameId, string modId, string? workshopId)
    {
        var id = Clean(workshopId);
        if (id is not null && !id.All(char.IsAsciiDigit))
        {
            throw new ArgumentException(id);
        }

        Update(path, gameId, modId, entry => entry.WorkshopId = id);
    }

    /// <summary>Ändert den Eintrag einer Mod unter der Sperre der Datei; ein Eintrag ohne Werte fällt weg.</summary>
    private static void Update(string path, string gameId, string modId, Action<ModLinkEntry> change) =>
        SharedFile.Locked(path, () =>
        {
            var entries = LoadStrict(path).ToList();
            var entry = entries.FirstOrDefault(e => e.GameId == gameId && e.ModId == modId) ?? new ModLinkEntry { GameId = gameId, ModId = modId };
            entries.Remove(entry);
            change(entry);
            if (entry.ForumUrl is not null || entry.DiscordUrl is not null || entry.GitHubUrl is not null || entry.WorkshopId is not null)
            {
                entries.Add(entry);
            }

            var file = new LinksFile { Version = CurrentVersion, Links = entries.Cast<ModLinkEntry?>().ToList() };
            SharedFile.WriteAllTextAtomic(path, JsonSerializer.Serialize(file, Options));
        });

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class LinksFile
    {
        public int Version { get; set; } = CurrentVersion;

        public List<ModLinkEntry?>? Links { get; set; } = new();
    }
}
