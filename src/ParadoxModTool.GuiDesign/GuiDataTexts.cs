using ParadoxModTool.Localization;

namespace ParadoxModTool.GuiDesign;

/// <summary>
/// Texte des GUI-Daten-Helfers (Datentypen nachschlagen, Ausdruck bauen). Die Schlüssel stehen in <c>locales/de.json</c> und <c>locales/en.json</c>; der
/// deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen (Test <c>GuiDataTextsTests</c>).
/// </summary>
public static class GuiDataTexts
{
    public static string Title => T("guiData.title", "GUI-Daten-Helfer");

    public static string Start => T("guiData.start", "Start:");

    public static string StartTip => T("guiData.start.tip", "Der Datenkontext, von dem der Ausdruck ausgeht: ein Typ (Country) oder ein globaler Promote (GetPlayer)");

    public static string Search => T("guiData.search", "Suche:");

    public static string SearchStartTip => T("guiData.searchStart.tip", "Zeigt unter \"Start\" nur Typen und globale Promotes, die diesen Text enthalten, und wählt den ersten Treffer");

    public static string FilesDate(string date) => F("guiData.filesDate", "Datentypen vom {0}", date);

    public static string Missing => T("guiData.missing", "Keine Datentyp-Dateien gefunden.");

    public static string Stale => T("guiData.stale", "Die Datentyp-Dateien sind älter als das Spiel und vermutlich veraltet.");

    public static string HowTo => T("guiData.howTo", "So erzeugst du sie neu: Spiel mit -debug_mode starten, Konsole öffnen (Taste ^), DumpDataTypes eingeben, danach dieses Fenster neu öffnen.");

    public static string Unreadable(string file) => F("guiData.unreadable", "Nicht lesbar: {0}", file);

    public static string TabLookup => T("guiData.tabLookup", "Nachschlagen");

    public static string TabLookupTip => T("guiData.tabLookup.tip", "Promotes und Funktionen per Stichwort finden, mit Beschreibung und Beispielen aus Vanilla");

    public static string TabChain => T("guiData.tabChain", "Kette");

    public static string TabChainTip => T("guiData.tabChain.tip", "Den Ausdruck vom Start aus Glied für Glied bauen");

    public static string Query => T("guiData.query", "Suchen:");

    public static string QueryTip => T("guiData.query.tip", "Stichwort in Namen, Typen und Beschreibungen, z. B. capital oder tax; auch Wortteile wie Capital in GetCapital");

    public static string ColumnName => T("guiData.columnName", "Name");

    public static string ColumnOwner => T("guiData.columnOwner", "Typ");

    public static string ColumnKind => T("guiData.columnKind", "Art");

    public static string ColumnReturn => T("guiData.columnReturn", "Rückgabe");

    public static string KindType => T("guiData.kindType", "Typ");

    public static string KindPromote => T("guiData.kindPromote", "Promote");

    public static string KindFunction => T("guiData.kindFunction", "Funktion");

    public static string KindGlobalPromote => T("guiData.kindGlobalPromote", "globaler Promote");

    public static string KindGlobalFunction => T("guiData.kindGlobalFunction", "globale Funktion");

    public static string KindGlobalMacro => T("guiData.kindGlobalMacro", "Makro");

    public static string Examples => T("guiData.examples", "Beispiele aus Vanilla:");

    public static string NoExamples => T("guiData.noExamples", "Keine Beispiele in Vanilla.");

    public static string Append => T("guiData.append", "Anhängen");

    public static string AppendTip => T("guiData.append.tip", "Hängt den Eintrag an die Kette an, bei Bedarf mit dem Weg dorthin");

    public static string AppendNoPath => T("guiData.appendNoPath", "Kein Weg vom Ende der Kette zu diesem Typ (höchstens 3 Promotes)");

    public static string AppendClosed => T("guiData.appendClosed", "Die Kette endet mit einer Funktion; entferne sie zuerst");

    public static string AppendAsStart => T("guiData.appendAsStart", "Globale Einträge und Typen setzen den Start neu; das geht nur bei einer Kette ohne Glieder");

    public static string AppendLink => T("guiData.appendLink", "Glied anhängen:");

    public static string AppendLinkTip => T("guiData.appendLink.tip", "Promotes und Funktionen des Typs am Ende der Kette; Enter im Suchfeld hängt den ersten Treffer an");

    public static string RemoveFrom => T("guiData.removeFrom", "Ab hier entfernen");

    public static string Preview => T("guiData.preview", "Vorschau:");

    public static string Insert => T("guiData.insert", "Einfügen");

    public static string InsertTip => T("guiData.insert.tip", "Fügt den Ausdruck an der Cursorstelle bzw. ins Feld ein");

    public static string Close => T("guiData.close", "Schließen");

    public static string OpenHelper => T("guiData.openHelper", "Daten-Helfer");

    public static string OpenHelperTip => T("guiData.openHelper.tip", "GUI-Daten-Helfer öffnen: Ausdruck vom Datenkontext dieses Elements aus bauen");

    public static string MenuTip => T("guiData.menu.tip", "GUI-Datentypen nachschlagen und einen Ausdruck wie [Country.GetCapital.GetName] an der Cursorstelle einfügen");

    public static string Key => T("guiData.key", "Strg+Umschalt+D");

    public static string ChainEmpty => T("guiData.chainEmpty", "Wähle einen Start und hänge Glieder an.");

    // Bedingungs-Helfer (Teil 2): Klartext, Etiketten, Hinweise.
    public static string CondAnd => T("guiData.cond.and", "und");

    public static string CondOr => T("guiData.cond.or", "oder");

    public static string CondNot => T("guiData.cond.not", "nicht");

    public static string CondAll => T("guiData.cond.all", "alle:");

    public static string CondAny => T("guiData.cond.any", "eins von:");

    public static string CondNotBlock => T("guiData.cond.notBlock", "nicht:");

    public static string CondEmpty => T("guiData.cond.empty", "(leer)");

    public static string LabelAnd => T("guiData.cond.labelAnd", "UND");

    public static string LabelOr => T("guiData.cond.labelOr", "ODER");

    public static string LabelNot => T("guiData.cond.labelNot", "NICHT");

    public static string LabelValue => T("guiData.cond.labelValue", "Wert");

    public static string LabelRaw => T("guiData.cond.labelRaw", "Rohtext");

    public static string LabelBetween => T("guiData.cond.labelBetween", "zwischen");

    public static string LabelOdd => T("guiData.cond.labelOdd", "ungerade");

    public static string LabelEven => T("guiData.cond.labelEven", "gerade");

    public static string Between(string value, string low, string high, string bounds) =>
        F("guiData.cond.between", "{0} zwischen {1} und {2} ({3})", value, low, high, bounds);

    public static string BoundsNone => T("guiData.cond.boundsNone", "ohne Grenzen");

    public static string BoundsBoth => T("guiData.cond.boundsBoth", "mit Grenzen");

    public static string BoundsMin => T("guiData.cond.boundsMin", "mit Untergrenze");

    public static string BoundsMax => T("guiData.cond.boundsMax", "mit Obergrenze");

    public static string IsOdd(string value) => F("guiData.cond.isOdd", "{0} ist ungerade", value);

    public static string IsEven(string value) => F("guiData.cond.isEven", "{0} ist gerade", value);

    public static string KindEqualTo => T("guiData.cond.kindEqualTo", "gleich");

    public static string KindNotEqualTo => T("guiData.cond.kindNotEqualTo", "ungleich");

    public static string KindLessThan => T("guiData.cond.kindLessThan", "kleiner als");

    public static string KindLessThanOrEqualTo => T("guiData.cond.kindLessThanOrEqualTo", "kleiner oder gleich");

    public static string KindGreaterThan => T("guiData.cond.kindGreaterThan", "größer als");

    public static string KindGreaterThanOrEqualTo => T("guiData.cond.kindGreaterThanOrEqualTo", "größer oder gleich");

    public static string KindBetween => T("guiData.cond.kindBetween", "zwischen (ohne Grenzen)");

    public static string KindBetweenInclusive => T("guiData.cond.kindBetweenInclusive", "zwischen (mit Grenzen)");

    public static string KindBetweenInclusiveOfMin => T("guiData.cond.kindBetweenInclusiveOfMin", "zwischen (mit Untergrenze)");

    public static string KindBetweenInclusiveOfMax => T("guiData.cond.kindBetweenInclusiveOfMax", "zwischen (mit Obergrenze)");

    public static string KindIsOdd => T("guiData.cond.kindIsOdd", "ist ungerade");

    public static string KindIsEven => T("guiData.cond.kindIsEven", "ist gerade");

    public static string CheckIsDataModelEmpty(string value) => F("guiData.cond.checkIsDataModelEmpty", "{0} ist leer", value);

    public static string CheckDataModelHasItems(string value) => F("guiData.cond.checkDataModelHasItems", "{0} hat Einträge", value);

    public static string CheckObjectsEqual(string a, string b) => F("guiData.cond.checkObjectsEqual", "{0} ist gleich {1}", a, b);

    public static string CheckStringIsEmpty(string value) => F("guiData.cond.checkStringIsEmpty", "{0} ist leerer Text", value);

    public static string CheckStringContains(string a, string b) => F("guiData.cond.checkStringContains", "{0} enthält {1}", a, b);

    public static string CheckStartsWith(string a, string b) => F("guiData.cond.checkStartsWith", "{0} beginnt mit {1}", a, b);

    public static string HintUnknownName(string detail) => F("guiData.cond.hintUnknownName", "Unbekannter Name: {0}", detail);

    public static string HintTypeMismatch(string detail) => F("guiData.cond.hintTypeMismatch", "Verschiedene Typen: {0}", detail);

    public static string HintNotBool(string detail) => F("guiData.cond.hintNotBool", "Kein Wahr/Falsch-Wert (Typ {0})", detail);

    public static string HintArgumentCount(string detail) => F("guiData.cond.hintArgumentCount", "Falsche Anzahl Argumente: {0}", detail);

    public static string HintEmptyGroup => T("guiData.cond.hintEmptyGroup", "Leere Gruppe");

    public static string HintNoVariant(string detail) => F("guiData.cond.hintNoVariant", "Variante wählen: {0}", detail);

    public static string HintMissingValue => T("guiData.cond.hintMissingValue", "Wert fehlt");

    public static string HintQuoteInText(string detail) => F("guiData.cond.hintQuoteInText", "Hochkomma im Text beendet die Konstante: {0}", detail);

    public static string HintRawText(string detail) => F("guiData.cond.hintRawText", "Rohtext, nicht geprüft: {0}", detail);

    // Tab "Bedingung".
    public static string TabCondition => T("guiData.cond.tab", "Bedingung");

    public static string TabConditionTip => T("guiData.cond.tab.tip", "Bedingung für visible, enabled, down oder checked als Baum bauen und bearbeiten");

    public static string CondAdd => T("guiData.cond.add", "+ Bedingung");

    public static string CondAddTip => T("guiData.cond.add.tip", "Neue Bedingung in die markierte Gruppe oder neben den markierten Knoten setzen");

    public static string CondAddValue => T("guiData.cond.addValue", "Wert");

    public static string CondAddCompare => T("guiData.cond.addCompare", "Vergleich");

    public static string CondAddCheck => T("guiData.cond.addCheck", "Prüfung");

    public static string CondAddAnd => T("guiData.cond.addAnd", "UND-Gruppe");

    public static string CondAddOr => T("guiData.cond.addOr", "ODER-Gruppe");

    public static string CondAddRaw => T("guiData.cond.addRaw", "Rohtext");

    public static string CondWrapAnd => T("guiData.cond.wrapAnd", "Mit UND umschließen");

    public static string CondWrapOr => T("guiData.cond.wrapOr", "Mit ODER umschließen");

    public static string CondToggleNot => T("guiData.cond.toggleNot", "NICHT an/aus");

    public static string CondMoveUp => T("guiData.cond.moveUp", "Hoch");

    public static string CondMoveDown => T("guiData.cond.moveDown", "Runter");

    public static string CondRemove => T("guiData.cond.remove", "Entfernen");

    public static string CondMerge => T("guiData.cond.merge", "Zusammenfassen");

    public static string CondMergeTip => T("guiData.cond.merge.tip", "Fasst verschachtelte UND in UND bzw. ODER in ODER zu einer Gruppe zusammen");

    public static string CondUndo => T("guiData.cond.undo", "Rückgängig");

    public static string CondRedo => T("guiData.cond.redo", "Wiederholen");

    public static string CondPlain => T("guiData.cond.plain", "Klartext:");

    public static string CondHints => T("guiData.cond.hints", "Hinweise:");

    public static string CondNoHints => T("guiData.cond.noHints", "Keine Hinweise.");

    public static string CondEmptyTree => T("guiData.cond.emptyTree", "Noch keine Bedingung. Füge mit \"+ Bedingung\" die erste hinzu.");

    public static string CondBroken(int position, string what) => F("guiData.cond.broken", "Der Ausdruck ist unvollständig (Zeichen {0}: {1}).", position, what);

    public static string CondErrMissingClose => T("guiData.cond.errMissingClose", "schließende Klammer fehlt");

    public static string CondErrUnclosedQuote => T("guiData.cond.errUnclosedQuote", "Hochkomma nicht geschlossen");

    public static string CondErrUnexpected => T("guiData.cond.errUnexpected", "Klammer zu viel");

    public static string CondStartEmpty => T("guiData.cond.startEmpty", "Leer beginnen");

    public static string CondTakeAsRaw => T("guiData.cond.takeAsRaw", "Als Rohtext übernehmen");

    public static string CondKind => T("guiData.cond.kind", "Art:");

    public static string CondVariant => T("guiData.cond.variant", "Variante:");

    public static string CondChooseVariant => T("guiData.cond.chooseVariant", "Variante wählen");

    public static string CondLeft => T("guiData.cond.left", "Links:");

    public static string CondRight => T("guiData.cond.right", "Rechts:");

    public static string CondRight2 => T("guiData.cond.right2", "Bis:");

    public static string CondArgument(int number) => F("guiData.cond.argument", "Argument {0}:", number);

    public static string CondChainMode => T("guiData.cond.chainMode", "Kette");

    public static string CondConstantMode => T("guiData.cond.constantMode", "Konstante");

    public static string CondType(string type) => F("guiData.cond.type", "Typ: {0}", type);

    public static string CondTypeUnknown => T("guiData.cond.typeUnknown", "Typ unbekannt");

    public static string CondTextType => T("guiData.cond.textType", "Text");

    public static string CondBuildChain => T("guiData.cond.buildChain", "Kette bauen");

    public static string CondBuildChainTip => T("guiData.cond.buildChain.tip", "Öffnet den Tab \"Kette\" mit dieser Kette; dort übernimmt \"In Bedingung übernehmen\" das Ergebnis");

    public static string CondTakeChain => T("guiData.cond.takeChain", "In Bedingung übernehmen");

    public static string CondCheckSearch => T("guiData.cond.checkSearch", "Prüfung suchen:");

    public static string CondApply => T("guiData.cond.apply", "Übernehmen");

    public static string CondApplyTip => T("guiData.cond.apply.tip", "Übernimmt den Text (Enter); gültiger Text wird zu Knoten");

    public static string CondGroupInfo(string label, int count) => F("guiData.cond.groupInfo", "{0}-Gruppe mit {1} Gliedern", label, count);

    public static string CondNotInfo => T("guiData.cond.notInfo", "Kehrt den Inhalt darunter um.");

    public static string CondValueInfo => T("guiData.cond.valueInfo", "Kette oder Konstante, die wahr oder falsch ist.");

    private static string T(string key, string fallback) => UiText.Get(key, fallback);

    private static string F(string key, string fallback, params object?[] args) => UiText.Format(key, fallback, args);
}
