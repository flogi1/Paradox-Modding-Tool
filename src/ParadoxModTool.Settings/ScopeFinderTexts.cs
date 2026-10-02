using ParadoxModTool.Localization;

namespace ParadoxModTool.Settings;

/// <summary>
/// Texte des Scope Finders (Dialog "Pfade vorschlagen" mit Kettenbaukasten) und seiner Startoptionen. Die Keys stehen in <c>locales/de.json</c> und
/// <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen (Test <c>ScopeFinderTextsTests</c>).
/// </summary>
public static class ScopeFinderTexts
{
    public static string Title => T("scopeFinder.title", "Scope Finder");

    public static string Search => T("scopeFinder.search", "Suche:");

    public static string SearchFromTip => T("scopeFinder.searchFrom.tip", "Zeigt unter \"Von\" nur Startpunkte, die diesen Text enthalten, und wählt den ersten Treffer");

    public static string SearchToTip => T("scopeFinder.searchTo.tip", "Zeigt unter \"Nach\" nur Scopes, die diesen Text enthalten, und wählt den ersten Treffer");

    public static string SearchAppendTip => T("scopeFinder.searchAppend.tip", "Zeigt unter \"Glied anhängen\" nur Glieder, die diesen Text enthalten; Enter hängt den ersten Treffer an");

    public static string SearchValueTip => T("scopeFinder.searchValue.tip", "Zeigt unter \"Wert am Ende\" nur Werte, die diesen Text enthalten; Enter übernimmt den ersten Treffer");

    public static string From => T("scopeFinder.from", "Von:");

    public static string FromTip => T("scopeFinder.from.tip", "Wo der Weg beginnt: im aktuellen Scope, bei root, bei einem gespeicherten Scope (scope:actor usw.) oder bei einem frei gewählten Scope");

    public static string To => T("scopeFinder.to", "Nach:");

    public static string ToTip => T("scopeFinder.to.tip", "Der Scope, zu dem der Weg führen soll. Tippen springt in der Liste zum passenden Namen.");

    public static string Steps => T("scopeFinder.steps", "Schritte:");

    public static string StepsTip => T("scopeFinder.steps.tip", "Höchstzahl der Schritte; bei vier wird die Liste sehr lang");

    public static string Iterator => T("scopeFinder.iterator", "Iterator:");

    public static string IteratorTip => T("scopeFinder.iterator.tip", "Welche Form der Iteratoren geschrieben wird: any_ für Trigger, every_/random_/ordered_ für Effects. Kennt das Spiel die Form nicht, wird die nächstliegende benutzt.");

    public static string FormAny => T("scopeFinder.formAny", "any_ (Trigger)");

    public static string FormEvery => T("scopeFinder.formEvery", "every_ (Effect)");

    public static string FormRandom => T("scopeFinder.formRandom", "random_ (Effect)");

    public static string FormOrdered => T("scopeFinder.formOrdered", "ordered_ (Effect)");

    public static string GlobalLinks => T("scopeFinder.globalLinks", "Globale Verweise (location:KEY usw.) anbieten");

    public static string GlobalLinksTip => T("scopeFinder.globalLinks.tip", "Verweise, die von überall funktionieren, aber einen Schlüssel brauchen; KEY ersetzt du nach dem Einfügen");

    public static string Filter => T("scopeFinder.filter", "Filter:");

    public static string FilterTip => T("scopeFinder.filter.tip", "Nur Wege zeigen, deren Text dies enthält");

    public static string Count(int count) => F("scopeFinder.count", "{0} Wege", count);

    public static string CountFiltered(int count, int total) => F("scopeFinder.countFiltered", "{0} von {1} Wegen", count, total);

    public static string HintChooseGoal => T("scopeFinder.hintChooseGoal", "Wähle den Ziel-Scope (\"Nach\").");

    public static string HintSameScope => T("scopeFinder.hintSameScope", "Start und Ziel sind derselbe Scope: hier ist kein Weg nötig. Die Kette lässt sich trotzdem vom Start aus bauen.");

    public static string HintNoPath(string start, string goal, int steps) =>
        F("scopeFinder.hintNoPath", "Kein Weg von {0} nach {1} in {2} Schritt(en) gefunden. Mehr Schritte oder globale Verweise versuchen.", start, goal, steps);

    public static string HintPick => T("scopeFinder.hintPick", "Die Auswahl übernimmt den Weg in die Kette; die Vorschau unten zeigt ihn. Bearbeiten lässt er sich im Tab \"Kette\".");

    public static string TabChainTip => T("scopeFinder.tabChain.tip", "Die Kette vom Start aus Glied für Glied bauen: Bedingungen, limit, Wert am Ende, Vorschau");

    public static string TabSearch => T("scopeFinder.tabSearch", "Weg suchen");

    public static string TabSearchTip => T("scopeFinder.tabSearch.tip", "Wege vom Start zu einem Ziel-Scope suchen lassen; ein gefundener Weg wird in die Kette übernommen");

    public static string HintTruncated => T("scopeFinder.hintTruncated", "Liste gekürzt: zu viele Wege. Filter oder weniger Schritte helfen.");

    public static string KindIterator => T("scopeFinder.kindIterator", "Iterator");

    public static string KindGlobal => T("scopeFinder.kindGlobal", "globaler Verweis (KEY ersetzen)");

    public static string KindDataLink => T("scopeFinder.kindDataLink", "Verweis mit Schlüssel (KEY ersetzen)");

    public static string KindTargetAny => T("scopeFinder.kindTargetAny", "Event-Target (allgemein)");

    public static string KindTarget => T("scopeFinder.kindTarget", "Event-Target");

    public static string Chain => T("scopeFinder.chain", "Kette");

    public static string Start => T("scopeFinder.start", "Start");

    public static string Condition => T("scopeFinder.condition", "Bedingung (if)");

    public static string ConditionTip => T("scopeFinder.condition.tip", "Setzt hier if = { limit = { } } ein; der Rest des Weges steht darin. In der any_-Form heißt es trigger_if.");

    public static string Limit => T("scopeFinder.limit", "Limit");

    public static string LimitTip => T("scopeFinder.limit.tip", "Setzt limit = { } in den Iterator");

    public static string LimitAnyTip => T("scopeFinder.limitAny.tip", "In any_-Blöcken ist der Inhalt schon die Bedingung, ein limit gibt es dort nicht");

    public static string RemoveFrom => T("scopeFinder.removeFrom", "Ab hier entfernen");

    public static string RemoveFromTip => T("scopeFinder.removeFrom.tip", "Entfernt dieses Glied und alle dahinter");

    public static string Append => T("scopeFinder.append", "Glied anhängen:");

    public static string AppendTip => T("scopeFinder.append.tip", "Mögliche nächste Glieder vom Scope am Ende der Kette");

    public static string Value => T("scopeFinder.value", "Wert am Ende:");

    public static string ValueTip => T("scopeFinder.value.tip", "Wert, mit dem die Kette endet, z. B. country_tax_base, modifier:KEY oder ein Script-Value; freier Text ist erlaubt");

    public static string ValueDisabledTip => T("scopeFinder.valueDisabled.tip", "Nur bei einer reinen Kette ohne Iteratoren und Bedingungen");

    public static string Preview => T("scopeFinder.preview", "Vorschau:");

    public static string Insert => T("scopeFinder.insert", "Einfügen");

    public static string InsertTip => T("scopeFinder.insert.tip", "Fügt die Kette als verschachtelte Blöcke an der Cursorstelle ein");

    public static string ChainOnly => T("scopeFinder.chainOnly", "Nur Kette");

    public static string ChainOnlyTip => T("scopeFinder.chainOnly.tip", "Fügt nur den Ausdruck ein (z. B. owner.capital); nur bei Ketten ohne Iteratoren und Bedingungen");

    public static string Close => T("scopeFinder.close", "Schließen");

    public static string StartHere(string scope) => F("scopeFinder.startHere", "Hier: {0}", scope);

    public static string StartHereUnsure(string scope) => F("scopeFinder.startHereUnsure", "Hier: {0} (unsicher)", scope);

    public static string StartRoot(string scope) => F("scopeFinder.startRoot", "root: {0}", scope);

    public static string StartSaved(string name, string scope) => F("scopeFinder.startSaved", "scope:{0}: {1}", name, scope);

    public static string StartFree(string scope) => F("scopeFinder.startFree", "{0} (frei gewählt)", scope);

    public static string DatabaseMissing => T("scopeFinder.databaseMissing", "Die Script-Datenbank ist noch nicht geladen. Sie wird beim Start aus den Doku-Logs des Spiels gelesen (Werkzeuge › Script-Datenbank).");

    private static string T(string key, string fallback) => UiText.Get(key, fallback);

    private static string F(string key, string fallback, params object?[] args) => UiText.Format(key, fallback, args);
}
