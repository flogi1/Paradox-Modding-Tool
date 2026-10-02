using ParadoxModTool.Localization;

namespace ParadoxModTool.Templates;

/// <summary>
/// Texte der Zählregeln, der Lokalisierungs-Vorschläge und der Lokalisierungs-Profile. Die Keys stehen in <c>locales/de.json</c> und
/// <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class TemplatesTexts
{
    // ---- Ebenen ----

    public static string LayerShipped => T("rules.layer.shipped", "mitgeliefert");

    public static string LayerAllGames => T("rules.layer.allGames", "Alle Spiele");

    public static string LayerGame => T("rules.layer.game", "Spiel");

    public static string LayerTemplate => T("rules.layer.template", "Vorlage");

    public static string Reason(string rule, string layer) => F("rules.reason", "Regel „{0}“ ({1})", rule, layer);

    // ---- Regeldateien ----

    public static string NoRuleFile(string layer) => F("rules.store.noFile", "Für die Ebene {0} gibt es keine Regeldatei.", layer);

    public static string RuleFileUnreadable(string path) =>
        F("rules.store.unreadable", "Die Regeldatei \"{0}\" war nicht lesbar; eine Kopie liegt als .bad daneben, die Ebene ist leer.", path);

    public static string CopySuffix => T("rules.copySuffix", " (Kopie)");

    public static string MigratedNamePrefixRule => T("rules.migratedNamePrefix", "Namenspräfix (aus alter Vorlage)");

    // ---- Mitgelieferte Regeln ----

    public static string ShippedTextureFile => T("rules.shipped.textureFile", "Texturdatei");

    public static string ShippedTextureEntry => T("rules.shipped.textureEntry", "texture-Eintrag");

    public static string ShippedPositionEntry => T("rules.shipped.positionEntry", "position-Eintrag");

    public static string ShippedSizeEntry => T("rules.shipped.sizeEntry", "size-Eintrag");

    public static string ShippedNamePrefix => T("rules.shipped.namePrefix", "Namenspräfix");

    // ---- Prüfung einer Regel ----

    public static string ValidateNoName => T("rules.validate.noName", "Gib der Regel einen Namen.");

    public static string ValidateNoPattern => T("rules.validate.noPattern", "Mindestens ein Muster ist nötig.");

    public static string ValidateMultipleMarkers(string pattern) =>
        F("rules.validate.multipleMarkers", "Das Muster „{0}“ enthält [#] mehrfach; erlaubt ist ein [#] je Muster.", pattern);

    public static string ValidateMarkerMissing(string action, string pattern) =>
        F("rules.validate.markerMissing", "Die Aktion „{0}“ braucht in jedem Muster ein [#]; es fehlt in „{1}“.", action, pattern);

    public static string ValidateNoKey => T("rules.validate.noKey", "Gib den Schlüssel an, innerhalb dessen die Regel gilt.");

    // ---- Kurztexte für Listen und Dialoge ----

    public static string ActionIgnore => T("rules.action.ignore", "Ignorieren");

    public static string ActionOnlyMarkedCounts => T("rules.action.onlyMarkedCounts", "Nur markierte zählt");

    public static string ActionAllCount => T("rules.action.allCount", "Alle zählen");

    public static string ActionMarkedDoesNotCount => T("rules.action.markedDoesNotCount", "Markierte zählt nicht");

    public static string ActionIgnoreHelp => T("rules.action.ignore.help", "Keine Zahl des getroffenen Namens zählt.");

    public static string ActionOnlyMarkedCountsHelp =>
        T("rules.action.onlyMarkedCounts.help", "Nur die Zahl bei [#] zählt, alle anderen Zahlen des Namens nicht.");

    public static string ActionAllCountHelp =>
        T("rules.action.allCount.help", "Alle Zahlen des Namens zählen, auch Namenszahlen und einzelne Zahlen.");

    public static string ActionMarkedDoesNotCountHelp =>
        T("rules.action.markedDoesNotCount.help", "Nur die Zahl bei [#] zählt nicht; über die anderen Zahlen des Namens entscheiden die übrigen Regeln.");

    public static string WhereInsideKey(string key) => F("rules.where.insideKey", "innerhalb von {0}", key);

    public static string WhereAnywhere => T("rules.where.anywhere", "überall");

    public static string GamesAll => T("rules.games.all", "alle");

    // ---- Lokalisierungs-Vorschläge ----

    public static string OriginField(string field) => F("templates.loc.origin.field", "Feld {0}", field);

    public static string OriginPrefix(string prefix) => F("templates.loc.origin.prefix", "Präfix {0}", prefix);

    public static string OriginObjectName => T("templates.loc.origin.objectName", "Objektname");

    public static string OriginSuffix(string suffix) => F("templates.loc.origin.suffix", "Endung {0}", suffix);

    // ---- Lokalisierungs-Profile ----

    public static string ProfileFallback => T("locprofile.fallback", "Rückfall (Name + _desc)");

    public static string ProfileAutomatic(string name) => F("locprofile.automatic", "automatisch: {0}", name);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
