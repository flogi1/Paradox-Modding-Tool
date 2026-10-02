using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.SteamIntegration;

/// <summary>Die Steam-Einstellungen eines Spiels. Enthält nie ein Passwort oder Token: die Anmeldung verwaltet SteamCMD selbst.</summary>
public sealed class SteamCmdSettings
{
    /// <summary>Pfad der <c>steamcmd.exe</c>; <see langword="null"/> = Standardordner des Programms.</summary>
    public string? SteamCmdPath { get; set; }

    /// <summary>Der Steam-Kontoname (nicht die E-Mail-Adresse).</summary>
    public string? UserName { get; set; }

    /// <summary><see cref="UploadMethods.Client"/> oder <see cref="UploadMethods.SteamCmd"/>; <see langword="null"/> = noch nicht gewählt.</summary>
    public string? UploadMethod { get; set; }
}

/// <summary>Lädt und speichert <see cref="SteamCmdSettings"/> je Spiel in <c>games\&lt;spiel&gt;\steam.json</c> (Version 1). Eine kaputte Datei wird als <c>.bad</c> gesichert.</summary>
public sealed class SteamCmdSettingsStore
{
    public const string FileName = "steam.json";

    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _gameDirectory;

    public SteamCmdSettingsStore(string gameDirectory)
    {
        _gameDirectory = gameDirectory;
    }

    public string FilePath => Path.Combine(_gameDirectory, FileName);

    /// <summary>Hinweis, wenn die Datei beim Laden nicht lesbar war; sonst <see langword="null"/>.</summary>
    public string? LoadWarning { get; private set; }

    public SteamCmdSettings Load()
    {
        LoadWarning = null;
        if (!File.Exists(FilePath))
        {
            return new SteamCmdSettings();
        }

        try
        {
            var json = SharedFile.Locked<string>(FilePath, () => File.ReadAllText(FilePath));
            var file = JsonSerializer.Deserialize<FileModel>(json, Options) ?? throw new JsonException("Die Datei ist leer.");
            return new SteamCmdSettings { SteamCmdPath = file.SteamCmdPath, UserName = file.UserName, UploadMethod = file.UploadMethod };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LoadWarning = SteamTexts.SettingsUnreadable;
            try
            {
                File.Copy(FilePath, FilePath + ".bad", overwrite: true);
            }
            catch (Exception copyError) when (copyError is IOException or UnauthorizedAccessException)
            {
                // Die Kopie ist nur ein Versuch; die Warnung bleibt.
            }

            return new SteamCmdSettings();
        }
    }

    public void Save(SteamCmdSettings settings)
    {
        var json = JsonSerializer.Serialize(
            new FileModel { Version = CurrentVersion, SteamCmdPath = settings.SteamCmdPath, UserName = settings.UserName, UploadMethod = settings.UploadMethod },
            Options);
        SharedFile.Locked(FilePath, () => SharedFile.WriteAllTextAtomic(FilePath, json));
    }

    private sealed class FileModel
    {
        public int Version { get; set; } = CurrentVersion;

        public string? SteamCmdPath { get; set; }

        public string? UserName { get; set; }

        public string? UploadMethod { get; set; }
    }
}
