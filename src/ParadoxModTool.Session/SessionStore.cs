using System.IO;
using System.Text.Json;

namespace ParadoxModTool.Session;

/// <summary>
/// Speichert/lädt den Programmzustand (<see cref="AppSessionState"/>) als JSON-Datei
/// (Abschnitt 12). Rein dateibasiert und UI-unabhängig, damit dieses Projekt — wie
/// <c>ParadoxModTool.UndoRedo</c> — ohne WPF-Compiler automatisiert getestet werden kann.
/// </summary>
public static class SessionStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Speichert die Session unter <paramref name="path"/>. Fehlende übergeordnete Ordner werden
    /// automatisch angelegt (z. B. beim allerersten Start, wenn der Session-Ordner noch nicht existiert).
    /// </summary>
    public static void Save(AppSessionState state, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, Options);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Lädt die Session aus <paramref name="path"/>, oder gibt <see langword="null"/> zurück, wenn
    /// die Datei nicht existiert oder nicht lesbar/gültig ist (z. B. beim allerersten Programmstart,
    /// oder wenn die Datei von Hand beschädigt wurde) — bewusst kein Fehler, damit ein defektes
    /// Session-File niemals den Programmstart verhindert; es wird einfach ohne wiederhergestellte
    /// Session gestartet.
    /// </summary>
    public static AppSessionState? TryLoad(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSessionState>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Standard-Speicherort der Session-Datei: <c>%APPDATA%\ParadoxModTool\session.json</c> —
    /// bewusst NICHT im Installations-/Build-Ordner der App, damit Programmdateien und
    /// nutzerspezifischer Zustand getrennt bleiben (z. B. bei einer künftigen Neuinstallation
    /// oder einem Update).
    /// </summary>
    public static string GetDefaultSessionFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ParadoxModTool", "session.json");
    }
}
