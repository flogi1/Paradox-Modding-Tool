using ParadoxModTool.Localization;

namespace ParadoxModTool.Editor;

/// <summary>
/// Texte der Hover-Tipps und der Autovervollständigung aus der Script-Datenbank (Keys <c>scripthint.*</c> in <c>locales/*.json</c>). Der
/// deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
internal static class ScriptHintTexts
{
    public static string ScopeAllowed(string scope) => F("scripthint.scope.allowed", "Hier: {0} – erlaubt", scope);

    public static string ScopeOnlyIn(string scope, string supported) => F("scripthint.scope.onlyIn", "Hier: {0} – gilt nur in {1}", scope, supported);

    public static string ScopeProbablyOnlyIn(string scope, string supported) =>
        F("scripthint.scope.probablyOnlyIn", "Hier: {0} (unsicher) – vermutlich nur in {1}", scope, supported);

    public static string MoreHints(int count) => F("scripthint.moreHints", "… und {0} weitere", count);

    /// <summary>Beschriftung der Kategorien eines Modifiers (im Tipp gefolgt von ": ").</summary>
    public static string Categories => T("scripthint.categories", "Kategorien");

    /// <summary>Beschriftung der Ausgabe-Scopes eines Event-Targets (im Tipp gefolgt von ": ").</summary>
    public static string Output => T("scripthint.output", "Ergebnis");

    /// <summary>Beschriftung der Ziele eines Triggers oder Effects (im Tipp gefolgt von ": ").</summary>
    public static string Targets => T("scripthint.targets", "Ziele");

    public static string Outdated => T("scripthint.outdated", "veraltet");

    public static string New => T("scripthint.new", "neu");

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
