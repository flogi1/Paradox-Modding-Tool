using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Templates;

/// <summary>Ein gelerntes Paar aus Präfix und Endung, mit dem Lokalisierungsschlüssel von Objekten eines Ordners gebildet werden (<c>Präfix + Name + Endung</c>).</summary>
public sealed class LocalizationObjectPattern
{
    public string Prefix { get; set; } = string.Empty;

    public string Suffix { get; set; } = string.Empty;

    /// <summary>Anteil der Objekte mit Lokalisierung, die dieses Paar haben (0 bis 1).</summary>
    public double Share { get; set; }

    /// <summary>Ab 90 % Anteil: im Vorschlag standardmäßig eingeschaltet.</summary>
    public bool Preselected { get; set; }
}

/// <summary>Ein gelerntes Feld, dessen Wert ein Lokalisierungsschlüssel ist (<c>text = "KEY"</c>).</summary>
public sealed class LocalizationFieldPattern
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Wie oft der Wert des Feldes ein Loc-Schlüssel war (ohne Verweise auf Objekte).</summary>
    public int Hits { get; set; }

    public double Share { get; set; }

    /// <summary>Ab 80 % Anteil: standardmäßig eingeschaltet.</summary>
    public bool Preselected { get; set; }
}

/// <summary>Das Profil eines Ordners, z. B. <c>common/building_types</c> oder <c>gui</c>.</summary>
public sealed class LocalizationFolderProfile
{
    /// <summary>Ordner relativ zum Spielordner mit <c>/</c>, ohne <c>in_game</c>.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Zahl der Objekte des Ordners, für die eine Lokalisierung gefunden wurde (Nenner der Anteile); 0 bei reinen Feld-Profilen.</summary>
    public int ObjectCount { get; set; }

    public List<LocalizationObjectPattern> Objects { get; set; } = new();

    public List<LocalizationFieldPattern> Fields { get; set; } = new();
}

/// <summary>Alle gelernten Profile eines Spiels (<c>games\&lt;id&gt;\localization\profiles.json</c>).</summary>
public sealed class LocalizationProfiles
{
    public const int CurrentVersion = 1;

    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Fingerabdruck der Skript- und Loc-Dateien, aus denen gelernt wurde.</summary>
    public string Fingerprint { get; set; } = string.Empty;

    public DateTime LearnedUtc { get; set; }

    public int FilesScanned { get; set; }

    public List<LocalizationFolderProfile> Profiles { get; set; } = new();

    /// <summary>Das Profil mit dem längsten passenden Pfad-Präfix; <see langword="null"/>, wenn keines passt.</summary>
    public LocalizationFolderProfile? Find(string? relativeFolder)
    {
        if (string.IsNullOrWhiteSpace(relativeFolder))
        {
            return null;
        }

        var folder = relativeFolder.Replace('\\', '/').Trim('/');
        return Profiles
            .Where(p => folder.Equals(p.Path, StringComparison.OrdinalIgnoreCase) || folder.StartsWith(p.Path + "/", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Path.Length)
            .FirstOrDefault();
    }
}

/// <summary>Liest und schreibt <c>profiles.json</c>. Eine kaputte Datei wird als <c>.bad</c> gesichert und gilt als nicht vorhanden (dann wird neu gelernt).</summary>
public sealed class LocalizationProfileStore
{
    private readonly string _path;

    public LocalizationProfileStore(string path)
    {
        _path = path;
    }

    public LocalizationProfiles? TryLoad()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var profiles = JsonSerializer.Deserialize<LocalizationProfiles>(File.ReadAllText(_path), LocalizationProfiles.JsonOptions);
            return profiles is { Version: LocalizationProfiles.CurrentVersion } ? Sanitize(profiles) : null;
        }
        catch (JsonException)
        {
            Backup();
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(LocalizationProfiles profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        SharedFile.WriteAllTextAtomic(_path, JsonSerializer.Serialize(profiles, LocalizationProfiles.JsonOptions));
    }

    private void Backup()
    {
        try
        {
            File.Copy(_path, _path + ".bad", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Die Kopie ist nur ein Versuch; die Datei gilt trotzdem als nicht lesbar.
        }
    }

    private static LocalizationProfiles Sanitize(LocalizationProfiles profiles)
    {
        profiles.Profiles = (profiles.Profiles ?? new List<LocalizationFolderProfile>()).Where(p => p is not null).ToList();
        foreach (var profile in profiles.Profiles)
        {
            profile.Path ??= string.Empty;
            profile.Objects = (profile.Objects ?? new List<LocalizationObjectPattern>()).Where(o => o is not null).ToList();
            profile.Fields = (profile.Fields ?? new List<LocalizationFieldPattern>()).Where(f => f is not null).ToList();
            foreach (var pattern in profile.Objects)
            {
                pattern.Prefix ??= string.Empty;
                pattern.Suffix ??= string.Empty;
            }
        }

        return profiles;
    }
}
