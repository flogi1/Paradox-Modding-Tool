using ParadoxModTool.Localization;

namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Meldungen beim Spielstart über <see cref="GameLauncher"/> (Keys <c>launch.*</c> in <c>locales/*.json</c>). Der deutsche Text hier ist
/// der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class LaunchTexts
{
    public static string NoExecutablePath => T("launch.noExecutablePath", "Es ist noch kein Pfad zur Spiel-.exe eingestellt.");

    public static string FileNotFound(string path) => F("launch.fileNotFound", "Die Datei \"{0}\" wurde nicht gefunden.", path);

    public static string StartFailed(string message) => F("launch.startFailed", "Das Spiel konnte nicht gestartet werden: {0}", message);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
