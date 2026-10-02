using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

/// <summary>
/// Meldungen beim Start, bevor ein Fenster offen ist (Keys <c>startup.*</c> in <c>locales/*.json</c>). Die Sprache ist dann schon aus den
/// globalen Einstellungen geladen (<see cref="GlobalUiLanguage"/>). Der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
internal static class StartupTexts
{
    public static string NoGameProfile(string gameId) => F("startup.noGameProfile", "Für '{0}' gibt es kein Spielprofil (Ordner 'profiles' neben der Programmdatei).", gameId);

    public static string GameAlreadyOpen(string gameName) => F("startup.gameAlreadyOpen", "{0} ist schon in einem anderen Fenster geöffnet.", gameName);

    public static string AllGamesAlreadyOpen => T("startup.allGamesAlreadyOpen", "Alle Spiele sind schon in einem anderen Fenster geöffnet.");

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
