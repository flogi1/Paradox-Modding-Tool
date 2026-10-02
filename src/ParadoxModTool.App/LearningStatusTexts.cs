using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

/// <summary>
/// Stand der im Hintergrund gelernten Lokalisierungs-Profile und Scope-Regeln (Keys <c>localization.*</c> und <c>scope.summary.*</c> in
/// <c>locales/*.json</c>). Der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
internal static class LearningStatusTexts
{
    public static string LocProfilesNotLearned => T("localization.profiles.notLearned", "Lokalisierungs-Profile: noch nicht gelernt, Rückfall aktiv.");

    public static string LocProfilesLearning => T("localization.profiles.learning", "Lokalisierungs-Profile werden gelernt …");

    public static string LocProfilesNoVanilla => T("localization.profiles.noVanilla", "Lokalisierungs-Profile: kein Vanilla-Ordner bekannt (Einstellungen › Spielpfad einstellen), Rückfall aktiv.");

    public static string LocProfilesLearned(DateTime learnedLocal, int folders) =>
        F("localization.profiles.learned", "Lokalisierungs-Profile gelernt am {0:g} ({1} Ordner).", learnedLocal, folders);

    public static string ScopeRulesLearning => T("scope.summary.learning", "Scope-Regeln werden noch gelernt.");

    public static string ScopeRulesMissing => T("scope.summary.missing", "Scope-Regeln fehlen: Es ist kein Vanilla-Ordner bekannt (Einstellungen › Spielpfad einstellen) oder die Script-Datenbank ist leer.");

    public static string ScopeRulesLearned(int confident, int unresolved, int discriminators, DateTime learnedLocal, int files) =>
        F("scope.summary.learned", "Scope-Regeln: {0:N0} sichere, {1:N0} offene, {2:N0} mit Unterscheider; gelernt am {3:g} aus {4:N0} Dateien.", confident, unresolved, discriminators, learnedLocal, files);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
