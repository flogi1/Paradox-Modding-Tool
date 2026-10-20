using ParadoxModTool.Localization;

namespace ParadoxModTool.Scopes;

/// <summary>
/// Texte der Scope-Verfolgung für Statusleiste, Hinweise und Warnungen (Keys <c>scope.*</c> in <c>locales/*.json</c>). Der deutsche Text
/// hier ist der Rückfall und muss mit de.json übereinstimmen; die Tests prüfen ihn ohne geladene Sprache.
/// </summary>
internal static class ScopeTexts
{
    public static string IssueOnlyIn(string command, string supported, string scope) => F("scope.issue.onlyIn", "{0} gilt nur in {1} – hier: {2}", command, supported, scope);

    public static string RulesNotLearned => T("scope.reason.rulesNotLearned", "Scope-Regeln noch nicht gelernt");

    public static string NotInScriptFolder => T("scope.reason.notInScriptFolder", "Datei liegt nicht in common oder events");

    public static string OutsideObject => T("scope.reason.outsideObject", "außerhalb eines Objekts");

    public static string ScopeUnknown => T("scope.reason.scopeUnknown", "Scope unbekannt");

    public static string EventWithoutType => T("scope.reason.eventWithoutType", "Ereignis ohne type");

    public static string EventWithoutRootScope(string kind) => F("scope.reason.eventWithoutRootScope", "Ereignis ohne Root-Scope ({0})", kind);

    public static string StartScopeAmbiguous(string path) => F("scope.reason.startScopeAmbiguous", "Start-Scope von {0} nicht eindeutig", path);

    public static string NoRule(string path) => F("scope.reason.noRule", "keine Regel für {0}", path);

    public static string RootUnknown => T("scope.reason.rootUnknown", "root unbekannt");

    public static string PrevUnknown => T("scope.reason.prevUnknown", "prev unbekannt");

    public static string NotDeterminable(string key) => F("scope.reason.notDeterminable", "{0} ist nicht bestimmbar", key);

    public static string SeveralTargets(string key) => F("scope.reason.severalTargets", "{0} hat mehrere Ziele", key);

    public static string ParameterBlock(string key) => F("scope.reason.parameterBlock", "Parameterblock von {0}", key);

    public static string UnknownBlock(string key) => F("scope.reason.unknownBlock", "unbekannter Block {0}", key);

    public static string SavedScopeTypeUnknown(string name) => F("scope.reason.savedScopeTypeUnknown", "Typ von scope:{0} unbekannt", name);

    public static string ChainUnresolved => T("scope.reason.chainUnresolved", "Kette nicht auflösbar");

    public static string UnknownTarget(string name) => F("scope.reason.unknownTarget", "unbekanntes Ziel {0}", name);

    public static string SeveralOutputScopes(string name) => F("scope.reason.severalOutputScopes", "{0} hat mehrere Ergebnis-Scopes", name);

    public static string VariousOutputScopes(string name) => F("scope.reason.variousOutputScopes", "{0} führt je nach Kontext zu verschiedenen Scopes", name);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
