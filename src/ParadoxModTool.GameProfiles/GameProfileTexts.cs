using ParadoxModTool.Localization;

namespace ParadoxModTool.GameProfiles;

/// <summary>
/// Oberflächentexte der Spielprofile (Keys <c>profiles.*</c> in <c>locales/*.json</c>). Der deutsche Text hier ist der Rückfall und
/// muss mit de.json übereinstimmen. Texte aus den Profil-JSONs selbst werden über ihren optionalen Key (<c>labelKey</c>,
/// <c>descriptionKey</c>) nachgeschlagen, siehe <see cref="Resolve"/>.
/// </summary>
public static class GameProfileTexts
{
    public static string EmptyResult => T("profiles.emptyResult", "Spielprofil konnte nicht gelesen werden (leeres Ergebnis).");

    /// <summary>
    /// Text aus einem Spielprofil in der gewählten Sprache: ohne Key oder ohne Eintrag in den Tabellen gilt der Text aus dem
    /// Profil (deutsch). Ein fehlender Text bleibt <see langword="null"/>.
    /// </summary>
    public static string? Resolve(string? key, string? text) =>
        string.IsNullOrEmpty(key) || text is null ? text : UiText.Get(key, text);

    private static string T(string key, string german) => UiText.Get(key, german);
}
