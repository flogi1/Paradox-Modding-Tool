using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.GitIntegration;

/// <summary>Die GitHub-Einstellungen eines Spiels. Enthält nie ein Token: die Anmeldung verwaltet <c>gh</c> selbst.</summary>
public sealed class GhSettings
{
    /// <summary>Pfad der <c>gh.exe</c>; <see langword="null"/> = suchen (PATH, Standardordner, Programmordner).</summary>
    public string? GhPath { get; set; }
}

/// <summary>Lädt und speichert <see cref="GhSettings"/> je Spiel in <c>games\&lt;spiel&gt;\github.json</c> (Version 1). Eine kaputte Datei wird als <c>.bad</c> gesichert.</summary>
public sealed class GhSettingsStore
{
    public const string FileName = "github.json";

    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _gameDirectory;

    public GhSettingsStore(string gameDirectory)
    {
        _gameDirectory = gameDirectory;
    }

    public string FilePath => Path.Combine(_gameDirectory, FileName);

    /// <summary>Hinweis, wenn die Datei beim Laden nicht lesbar war; sonst <see langword="null"/>.</summary>
    public string? LoadWarning { get; private set; }

    public GhSettings Load()
    {
        LoadWarning = null;
        if (!File.Exists(FilePath))
        {
            return new GhSettings();
        }

        try
        {
            var json = SharedFile.Locked<string>(FilePath, () => File.ReadAllText(FilePath));
            var file = JsonSerializer.Deserialize<FileModel>(json, Options) ?? throw new JsonException("Die Datei ist leer.");
            return new GhSettings { GhPath = file.GhPath };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LoadWarning = GitTexts.SettingsUnreadable;
            try
            {
                File.Copy(FilePath, FilePath + ".bad", overwrite: true);
            }
            catch (Exception copyError) when (copyError is IOException or UnauthorizedAccessException)
            {
                // Die Kopie ist nur ein Versuch; die Warnung bleibt.
            }

            return new GhSettings();
        }
    }

    public void Save(GhSettings settings)
    {
        var json = JsonSerializer.Serialize(new FileModel { Version = CurrentVersion, GhPath = settings.GhPath }, Options);
        SharedFile.Locked(FilePath, () => SharedFile.WriteAllTextAtomic(FilePath, json));
    }

    private sealed class FileModel
    {
        public int Version { get; set; } = CurrentVersion;

        public string? GhPath { get; set; }
    }
}
