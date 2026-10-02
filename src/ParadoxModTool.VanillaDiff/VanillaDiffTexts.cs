using ParadoxModTool.Localization;

namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Meldungen des Vanilla-Diff-Moduls, die in der Oberfläche landen (Syntaxfehler, Merge-Konflikte, Fehler der Vanilla-Historie;
/// Keys <c>diff.*</c> in <c>locales/*.json</c>). Der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class VanillaDiffTexts
{
    public static string AtLine(string message, int line) => F("diff.parse.atLine", "{0} (Zeile {1})", message, line);

    public static string UnclosedBrace => T("diff.parse.unclosedBrace", "Nicht geschlossene öffnende Klammer \"{\"");

    public static string UnexpectedCloseBrace => T("diff.parse.unexpectedCloseBrace", "Unerwartete schließende Klammer \"}\" ohne passende \"{\"");

    public static string UnexpectedToken(string text, ScriptTokenKind kind) => F("diff.parse.unexpectedToken", "Unerwartetes Token \"{0}\" ({1})", text, kind);

    public static string UnknownOperator(string text) => F("diff.parse.unknownOperator", "Unbekannter Operator \"{0}\"", text);

    public static string UnclosedString => T("diff.parse.unclosedString", "Nicht geschlossener String-Literal");

    public static string UnexpectedCharacter(char c) => F("diff.parse.unexpectedCharacter", "Unerwartetes Zeichen '{0}'", c);

    public static string LoadRuleUnsupported(string rule, string profileId) => F("diff.loadRuleUnsupported", "Lade-Regel \"{0}\" von Spielprofil \"{1}\" wird vom Compare und Update Tool noch nicht unterstützt.", rule, profileId);

    public static string VanillaFileMissing => T("diff.history.vanillaMissing", "Vanilla-Datei nicht gefunden.");

    public static string NoSnapshot(string relativePath, string gameVersion) => F("diff.history.noSnapshot", "Kein gespeicherter Snapshot für \"{0}\" bei Spielversion \"{1}\".", relativePath, gameVersion);

    public static string ConflictRemovedByMod(string display) => F("diff.conflict.removedByMod", "Der Mod hat \"{0}\" entfernt, das Spiel-Update hat diese Stelle aber verändert. Die Fassung des Spiel-Updates wurde übernommen; \"Mod übernehmen\" entfernt die Stelle.", display);

    public static string ConflictAddedByBoth(string display) => F("diff.conflict.addedByBoth", "Sowohl der Mod als auch das Spiel-Update haben \"{0}\" neu eingeführt, mit unterschiedlichem Inhalt. Die Fassung des Spiel-Updates wurde übernommen; bitte prüfen, ob die Mod-Fassung stattdessen nötig ist.", display);

    public static string ConflictRemovedByUpdate(string display) => F("diff.conflict.removedByUpdate", "Das Spiel-Update hat \"{0}\" entfernt, der Mod hat diese Stelle aber verändert. Die Stelle fehlt in der zusammengeführten Datei; \"Mod übernehmen\" fügt die Mod-Fassung wieder ein.", display);

    public static string ConflictChangedByBoth(string display) => F("diff.conflict.changedByBoth", "\"{0}\" wurde sowohl vom Mod als auch vom Spiel-Update unterschiedlich verändert. Die Fassung des Spiel-Updates wurde übernommen; bitte die Mod-Änderung bei Bedarf über \"Mod übernehmen\" wählen.", display);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
