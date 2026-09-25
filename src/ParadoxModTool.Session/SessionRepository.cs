using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Session;

/// <summary>Der beim Start geladene Programmzustand samt Ausgangsstand der globalen Einstellungen (für die Zusammenführung beim Speichern).</summary>
public sealed class LoadedSession
{
    internal LoadedSession(AppSessionState state, JsonObject globalBaseline)
    {
        State = state;
        GlobalBaseline = globalBaseline;
    }

    /// <summary>Gesamter Zustand: globale Einstellungen plus der Zustand des geöffneten Spiels.</summary>
    public AppSessionState State { get; }

    internal JsonObject GlobalBaseline { get; set; }
}

/// <summary>
/// Ablage des Programmzustands unter einem Wurzelordner (Standard: <c>%APPDATA%\ParadoxModTool</c>):
/// <c>settings.json</c> für alles, was für alle Spiele gilt, und <c>games\&lt;spiel&gt;\</c> mit <c>session.json</c>, <c>recovery\</c>
/// und <c>.lock</c> für den Zustand eines Spiels. Beides zusammen ergibt im Speicher weiterhin ein <see cref="AppSessionState"/>,
/// die Aufteilung regelt <see cref="SessionPartition"/>. Rein dateibasiert und ohne UI, damit testbar.
/// </summary>
public sealed class SessionRepository
{
    private const string FallbackGameId = "eu5";

    private static readonly Regex GameIdPattern = new("^[A-Za-z0-9_-]{1,40}$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public SessionRepository(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public string SettingsFilePath => Path.Combine(Root, "settings.json");

    public string LegacySessionFilePath => Path.Combine(Root, "session.json");

    public string LegacyRecoveryDirectory => Path.Combine(Root, "recovery");

    public static SessionRepository CreateDefault()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new SessionRepository(Path.Combine(appData, "ParadoxModTool"));
    }

    /// <summary>Ordner mit allen Dateien eines Spiels. Eine Spiel-Kennung darf nur Buchstaben, Ziffern, "-" und "_" enthalten.</summary>
    public string GameDirectory(string gameId) => Path.Combine(Root, "games", ValidateId(gameId));

    public string GameSessionFilePath(string gameId) => Path.Combine(GameDirectory(gameId), "session.json");

    public string GameRecoveryDirectory(string gameId) => Path.Combine(GameDirectory(gameId), "recovery");

    public string GameLockFilePath(string gameId) => Path.Combine(GameDirectory(gameId), ".lock");

    /// <summary>Sperrt das Spiel für diesen Prozess; <see langword="null"/>, wenn es schon in einem anderen Fenster geöffnet ist.</summary>
    public GameLock? TryLockGame(string gameId) => GameLock.TryAcquire(GameLockFilePath(gameId));

    /// <summary>Ist das Spiel gerade in einem anderen Prozess geöffnet?</summary>
    public bool IsGameOpen(string gameId)
    {
        var probe = TryLockGame(gameId);
        probe?.Dispose();
        return probe is null;
    }

    /// <summary>Kennung des zuletzt geöffneten Spiels, oder <see langword="null"/> vor dem allerersten Start.</summary>
    public string? ReadLastGameId()
    {
        EnsureMigrated();
        var id = ReadObject(SettingsFilePath)[nameof(AppSessionState.LastGameProfileId)]?.GetValue<string>();
        return id is not null && GameIdPattern.IsMatch(id) ? id : null;
    }

    /// <summary>Merkt das Spiel als zuletzt geöffnet - beim Öffnen eines Fensters aufgerufen, damit ein Neustart ohne Angabe es wieder wählt.</summary>
    public void RecordLastGame(string gameId)
    {
        ValidateId(gameId);
        EnsureMigrated();
        SharedFile.Locked(SettingsFilePath, () =>
        {
            var disk = ReadObject(SettingsFilePath);
            disk[nameof(AppSessionState.LastGameProfileId)] = gameId;
            WriteObject(SettingsFilePath, disk);
        });
    }

    /// <summary>
    /// Lädt globale Einstellungen und den Zustand des Spiels. Fehlende oder beschädigte Dateien ergeben den Ausgangszustand
    /// (leeres Fenster), nie einen Fehler - eine defekte Datei darf den Programmstart nicht verhindern.
    /// </summary>
    public LoadedSession Load(string gameId)
    {
        EnsureMigrated();

        var merged = SessionPartition.Pick(ReadObject(SettingsFilePath), SessionPartition.GlobalNames);
        foreach (var (name, value) in SessionPartition.Pick(ReadObject(GameSessionFilePath(gameId)), SessionPartition.GameNames))
        {
            merged[name] = value?.DeepClone();
        }

        var state = Deserialize(merged) ?? Deserialize(SessionPartition.Pick(merged, SessionPartition.GlobalNames)) ?? new AppSessionState();
        return new LoadedSession(state, ToNode(state, SessionPartition.GlobalNames));
    }

    /// <summary>
    /// Speichert den Zustand des Spiels vollständig und führt die globalen Einstellungen mit dem Stand auf der Platte zusammen
    /// (siehe <see cref="SessionPartition.MergeGlobal"/>).
    /// </summary>
    public void Save(string gameId, LoadedSession loaded)
    {
        var whole = ToNode(loaded.State, SessionPartition.GlobalNames.Concat(SessionPartition.GameNames));
        var global = SessionPartition.Pick(whole, SessionPartition.GlobalNames);

        WriteObject(GameSessionFilePath(gameId), SessionPartition.Pick(whole, SessionPartition.GameNames));

        SharedFile.Locked(SettingsFilePath, () =>
        {
            var merged = SessionPartition.MergeGlobal(ReadObject(SettingsFilePath), loaded.GlobalBaseline, global);
            WriteObject(SettingsFilePath, merged);
        });

        loaded.GlobalBaseline = global;
    }

    /// <summary>
    /// Übernimmt einmalig die Datei der Vorgängerversion (<c>session.json</c> und <c>recovery\</c> direkt im Wurzelordner): Die globalen
    /// Einstellungen werden zu <c>settings.json</c>, der Rest samt Wiederherstellungsdateien zu einem Spielordner (das dort
    /// gespeicherte aktive Spielprofil, sonst "eu5"). Die alte Datei bleibt als <c>session.json.migrated</c> erhalten.
    /// </summary>
    public void EnsureMigrated()
    {
        if (File.Exists(SettingsFilePath) || !File.Exists(LegacySessionFilePath))
        {
            return;
        }

        SharedFile.Locked(SettingsFilePath, () =>
        {
            if (File.Exists(SettingsFilePath) || !File.Exists(LegacySessionFilePath))
            {
                return;
            }

            var legacy = ReadObject(LegacySessionFilePath);
            var gameId = legacy[nameof(AppSessionState.ActiveGameProfileId)]?.GetValue<string>();
            if (gameId is null || !GameIdPattern.IsMatch(gameId))
            {
                gameId = FallbackGameId;
            }

            var game = SessionPartition.Pick(legacy, SessionPartition.GameNames);
            game[nameof(AppSessionState.ActiveGameProfileId)] = gameId;
            WriteObject(GameSessionFilePath(gameId), game);
            MoveLegacyRecoveryFiles(GameRecoveryDirectory(gameId));

            var global = SessionPartition.Pick(legacy, SessionPartition.GlobalNames);
            global[nameof(AppSessionState.LastGameProfileId)] = gameId;

            try
            {
                File.Move(LegacySessionFilePath, LegacySessionFilePath + ".migrated", overwrite: true);
            }
            catch (IOException)
            {
                // Die alte Datei bleibt liegen - unschädlich, weil settings.json ab jetzt Vorrang hat.
            }

            // Zuletzt geschrieben: erst damit gilt die Übernahme als abgeschlossen.
            WriteObject(SettingsFilePath, global);
        });
    }

    private void MoveLegacyRecoveryFiles(string targetDirectory)
    {
        if (!Directory.Exists(LegacyRecoveryDirectory))
        {
            return;
        }

        Directory.CreateDirectory(targetDirectory);
        foreach (var file in Directory.GetFiles(LegacyRecoveryDirectory, "*.json"))
        {
            try
            {
                File.Move(file, Path.Combine(targetDirectory, Path.GetFileName(file)));
            }
            catch (IOException)
            {
                // Eine einzelne Sicherung, die sich nicht verschieben lässt, bricht die Übernahme der übrigen nicht ab.
            }
        }
    }

    private static string ValidateId(string gameId)
    {
        if (!GameIdPattern.IsMatch(gameId))
        {
            throw new ArgumentException($"Ungültige Spiel-Kennung '{gameId}'.", nameof(gameId));
        }

        return gameId;
    }

    private static JsonObject ReadObject(string path)
    {
        try
        {
            if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject obj)
            {
                return obj;
            }
        }
        catch (JsonException)
        {
            // Beschädigte Datei = leerer Ausgangszustand.
        }
        catch (IOException)
        {
        }

        return new JsonObject();
    }

    private static void WriteObject(string path, JsonObject obj) =>
        SharedFile.WriteAllTextAtomic(path, obj.ToJsonString(Options));

    private static AppSessionState? Deserialize(JsonObject obj)
    {
        try
        {
            return obj.Deserialize<AppSessionState>(Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonObject ToNode(AppSessionState state, IEnumerable<string> names) =>
        SessionPartition.Pick((JsonObject)JsonSerializer.SerializeToNode(state, Options)!, names);
}
