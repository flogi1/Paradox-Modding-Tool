using System.Text.Json;
using System.Text.Json.Serialization;
using ParadoxModTool.SteamIntegration;
using ParadoxModTool.Storage;

namespace ParadoxModTool.ModOverview;

/// <summary>Die zuletzt abgerufenen Steam-Werte einer Workshop-Seite samt Basis für den Trend "seit letztem Öffnen".</summary>
public sealed class SteamCacheEntry
{
    public string Id { get; set; } = string.Empty;

    public WorkshopPageState State { get; set; }

    public string? Title { get; set; }

    public string? PreviewUrl { get; set; }

    public long? Subscriptions { get; set; }

    public long? Favorited { get; set; }

    public long? Views { get; set; }

    public DateTimeOffset? TimeUpdated { get; set; }

    public DateTime FetchedUtc { get; set; }

    /// <summary>Abonnenten beim Abruf aus dem vorigen Öffnen des Fensters; <see langword="null"/> vor dem zweiten Öffnen.</summary>
    public long? BaselineSubscriptions { get; set; }

    /// <summary>Kennung des Fenster-Öffnens, das zuletzt geschrieben hat.</summary>
    public string? Session { get; set; }

    [JsonIgnore]
    public long? Trend => Subscriptions is { } now && BaselineSubscriptions is { } before ? now - before : null;
}

/// <summary>
/// Die Datei <c>overview\steam.json</c>. Beim ersten Abruf eines neuen Fenster-Öffnens (<c>session</c>) wird der bisher gespeicherte
/// Abonnentenstand zur Basis des Trends; weitere Abrufe im selben Fenster ändern die Basis nicht.
/// </summary>
public static class SteamCacheStore
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>Für die Anzeige: nie eine Ausnahme; fehlend, kaputt oder gerade nicht lesbar ergibt einen leeren Stand.</summary>
    public static IReadOnlyDictionary<string, SteamCacheEntry> Load(string path)
    {
        try
        {
            return LoadStrict(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new Dictionary<string, SteamCacheEntry>();
        }
    }

    /// <summary>Übernimmt <paramref name="fetched"/> unter der Sperre der Datei und gibt den neuen Gesamtstand zurück. Nicht abgerufene IDs bleiben unverändert.</summary>
    public static IReadOnlyDictionary<string, SteamCacheEntry> Apply(string path, IReadOnlyList<WorkshopDetails> fetched, string session, DateTime nowUtc) =>
        SharedFile.Locked(path, () =>
        {
            var entries = LoadStrict(path);
            foreach (var details in fetched)
            {
                entries.TryGetValue(details.Id, out var old);
                var baseline = old is null ? null
                    : old.Session != session ? old.Subscriptions
                    : old.BaselineSubscriptions;
                entries[details.Id] = new SteamCacheEntry
                {
                    Id = details.Id,
                    State = details.State,
                    Title = details.Title,
                    PreviewUrl = details.PreviewUrl,
                    Subscriptions = details.Subscriptions,
                    Favorited = details.Favorited,
                    Views = details.Views,
                    TimeUpdated = details.TimeUpdated,
                    FetchedUtc = nowUtc,
                    BaselineSubscriptions = baseline,
                    Session = session,
                };
            }

            var file = new SteamFile { Version = CurrentVersion, Items = entries.Values.OrderBy(e => e.Id, StringComparer.Ordinal).Cast<SteamCacheEntry?>().ToList() };
            SharedFile.WriteAllTextAtomic(path, JsonSerializer.Serialize(file, Options));
            return (IReadOnlyDictionary<string, SteamCacheEntry>)entries;
        });

    /// <summary>Lesefehler werfen (damit <see cref="Apply"/> nie echte Daten überschreibt); kaputtes JSON ist leer.</summary>
    private static Dictionary<string, SteamCacheEntry> LoadStrict(string path)
    {
        var result = new Dictionary<string, SteamCacheEntry>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return result;
        }

        var json = File.ReadAllText(path);
        try
        {
            foreach (var entry in JsonSerializer.Deserialize<SteamFile>(json, Options)?.Items ?? new List<SteamCacheEntry?>())
            {
                if (entry is { Id.Length: > 0 })
                {
                    result[entry.Id] = entry;
                }
            }
        }
        catch (JsonException)
        {
            result.Clear();
        }

        return result;
    }

    private sealed class SteamFile
    {
        public int Version { get; set; } = CurrentVersion;

        public List<SteamCacheEntry?>? Items { get; set; } = new();
    }
}
