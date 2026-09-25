using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Mods;

/// <summary>Wann und unter welcher Workshop-ID eine Version auf Steam hochgeladen wurde. <see cref="PublishedAt"/> ist <c>yyyy-MM-ddTHH:mm:ss</c> (Ortszeit).</summary>
public sealed class SteamPublication
{
    public string PublishedAt { get; set; } = string.Empty;

    public string WorkshopId { get; set; } = string.Empty;
}

/// <summary>Eine erfasste Release einer Mod. <see cref="Date"/> ist <c>yyyy-MM-dd</c> oder leer (Datum unbekannt).</summary>
public sealed class ReleaseEntry
{
    public string Version { get; set; } = string.Empty;

    public string Date { get; set; } = string.Empty;

    /// <summary>Die Veröffentlichung dieser Version auf Steam; <see langword="null"/>, wenn sie dort nicht veröffentlicht wurde.</summary>
    public SteamPublication? Steam { get; set; }

    /// <summary>Die Veröffentlichungen dieser Version als GitHub-Release; <see langword="null"/>, wenn es keine gibt.</summary>
    public List<GitHubPublication>? GitHub { get; set; }
}

/// <summary>
/// Die erfassten Releases einer Mod (<c>games\&lt;spiel&gt;\mods\&lt;modId&gt;\releases.json</c>, Version 1). Sie liegen in den Programmdaten; den Text einer Release
/// liefert der Changelog im Mod-Ordner. Jede Änderung wird sofort atomar gespeichert. Die Nummer ist der Schlüssel und wird nach Zahlenwert verglichen
/// (<c>1.0</c> und <c>1.0.0</c> sind dieselbe Release).
/// </summary>
public sealed class ReleaseLog
{
    public const string FileName = "releases.json";

    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _dataDirectory;
    private List<ReleaseEntry> _releases = new();

    private ReleaseLog(string dataDirectory)
    {
        _dataDirectory = dataDirectory;
    }

    public string FilePath => Path.Combine(_dataDirectory, FileName);

    /// <summary>Hinweis, wenn die Datei beim Laden nicht lesbar war (Kopie als <c>.bad</c>); sonst <see langword="null"/>.</summary>
    public string? LoadWarning { get; private set; }

    /// <summary>Alle Releases, die neueste zuerst.</summary>
    public IReadOnlyList<ReleaseEntry> Releases => _releases
        .OrderByDescending(r => Number(r.Version))
        .ToList();

    /// <summary>Lädt das Log der Mod aus deren Datenordner. Eine fehlende Datei ergibt ein leeres Log (und legt nichts an), eine kaputte wird als <c>.bad</c> gesichert.</summary>
    public static ReleaseLog Load(string modDataDirectory)
    {
        var log = new ReleaseLog(modDataDirectory);
        var path = log.FilePath;
        if (!File.Exists(path))
        {
            return log;
        }

        try
        {
            var json = SharedFile.Locked<string>(path, () => File.ReadAllText(path));
            var file = JsonSerializer.Deserialize<LogFile>(json, Options) ?? throw new JsonException("Die Datei ist leer.");
            foreach (var entry in file.Releases ?? new List<ReleaseEntry?>())
            {
                if (entry is not null && VersionNumber.TryParse(entry.Version, out var number) && log.IndexOf(number) < 0)
                {
                    log._releases.Add(new ReleaseEntry
                    {
                        Version = number.Text,
                        Date = IsValidDate(entry.Date) ? entry.Date : string.Empty,
                        Steam = entry.Steam,
                        GitHub = KeepGitHub(entry.GitHub),
                    });
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            log.LoadWarning = "releases.json war nicht lesbar; eine Kopie liegt als releases.json.bad neben der Datei.";
            try
            {
                File.Copy(path, path + ".bad", overwrite: true);
            }
            catch (Exception copyError) when (copyError is IOException or UnauthorizedAccessException)
            {
                // Die Kopie ist nur ein Versuch; die Warnung bleibt.
            }
        }

        return log;
    }

    public ReleaseEntry? Find(string version) =>
        VersionNumber.TryParse(version, out var number) && IndexOf(number) is var index and >= 0 ? _releases[index] : null;

    /// <summary>Erfasst eine Release. Wirft <see cref="ArgumentException"/> bei ungültiger Nummer oder ungültigem Datum und <see cref="InvalidOperationException"/>, wenn es die Version schon gibt.</summary>
    public ReleaseEntry Add(string version, string date)
    {
        var number = ParseVersion(version);
        ValidateDate(date);
        if (IndexOf(number) >= 0)
        {
            throw new InvalidOperationException($"Die Version {number.Text} ist schon als Release erfasst.");
        }

        var entry = new ReleaseEntry { Version = number.Text, Date = date };
        _releases.Add(entry);
        Save();
        return entry;
    }

    public void SetDate(string version, string date)
    {
        ValidateDate(date);
        var entry = Find(version) ?? throw new InvalidOperationException($"Die Version {version} ist nicht erfasst.");
        entry.Date = date;
        Save();
    }

    /// <summary>Vermerkt die Veröffentlichung einer erfassten Version auf Steam. Wirft <see cref="InvalidOperationException"/>, wenn die Version nicht erfasst ist.</summary>
    public void SetSteam(string version, SteamPublication publication)
    {
        var entry = Find(version) ?? throw new InvalidOperationException($"Die Version {version} ist nicht erfasst.");
        entry.Steam = publication;
        Save();
    }

    private static List<GitHubPublication>? KeepGitHub(List<GitHubPublication>? list)
    {
        var kept = list?.Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Repo) && !string.IsNullOrWhiteSpace(p.Tag)).ToList();
        return kept is { Count: > 0 } ? kept : null;
    }

    /// <summary>Vermerkt GitHub-Veröffentlichungen einer erfassten Version; ein Eintrag mit gleichem Repo (Groß-/Kleinschreibung egal) und Tag wird ersetzt, sonst ergänzt.</summary>
    public void SetGitHub(string version, IEnumerable<GitHubPublication> publications)
    {
        var entry = Find(version) ?? throw new InvalidOperationException($"Die Version {version} ist nicht erfasst.");
        var list = entry.GitHub ?? new List<GitHubPublication>();
        foreach (var publication in publications)
        {
            list.RemoveAll(p => p.Repo.Equals(publication.Repo, StringComparison.OrdinalIgnoreCase) && p.Tag == publication.Tag);
            list.Add(publication);
        }

        entry.GitHub = list.Count == 0 ? null : list;
        Save();
    }

    /// <summary>Entfernt den Vermerk für Repo und Tag aus allen Versionen (nach dem Löschen des Releases). <see langword="true"/>, wenn etwas entfernt wurde.</summary>
    public bool RemoveGitHub(string repo, string tag)
    {
        var changed = false;
        foreach (var entry in _releases)
        {
            if (entry.GitHub is { } list && list.RemoveAll(p => p.Repo.Equals(repo, StringComparison.OrdinalIgnoreCase) && p.Tag == tag) > 0)
            {
                entry.GitHub = list.Count == 0 ? null : list;
                changed = true;
            }
        }

        if (changed)
        {
            Save();
        }

        return changed;
    }

    public void Remove(string version)
    {
        var entry = Find(version) ?? throw new InvalidOperationException($"Die Version {version} ist nicht erfasst.");
        _releases.Remove(entry);
        Save();
    }

    private int IndexOf(VersionNumber number) =>
        _releases.FindIndex(r => VersionNumber.TryParse(r.Version, out var other) && other.Equals(number));

    private static VersionNumber Number(string version) =>
        VersionNumber.TryParse(version, out var number) ? number : default;

    private static VersionNumber ParseVersion(string version) =>
        VersionNumber.TryParse(version, out var number) ? number : throw new ArgumentException($"\"{version}\" ist keine Versionsnummer.", nameof(version));

    private static bool IsValidDate(string? date) =>
        string.IsNullOrEmpty(date) || DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static void ValidateDate(string date)
    {
        if (!IsValidDate(date))
        {
            throw new ArgumentException($"\"{date}\" ist kein Datum (erwartet yyyy-MM-dd).", nameof(date));
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(new LogFile { Version = CurrentVersion, Releases = _releases.Cast<ReleaseEntry?>().ToList() }, Options);
        SharedFile.Locked(FilePath, () => SharedFile.WriteAllTextAtomic(FilePath, json));
    }

    private sealed class LogFile
    {
        public int Version { get; set; } = CurrentVersion;

        public List<ReleaseEntry?>? Releases { get; set; } = new();
    }
}
