using ParadoxModTool.Localization;

namespace ParadoxModTool.ModCompat;

/// <summary>
/// Texte der Mod-Kompatibilitätsprüfung (Funde, Fortschritt, Bericht). Die Schlüssel stehen in <c>locales/de.json</c> und
/// <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class ModCompatTexts
{
    // Schwere, Art, Empfehlung, Überschreibverhalten (siehe CompatText).
    public static string SeverityConflict => T("modcompat.severity.conflict", "Konflikt");

    public static string SeverityHint => T("modcompat.severity.hint", "Hinweis");

    public static string KindFileOverride => T("modcompat.kind.fileOverride", "Datei ersetzt");

    public static string KindFileIdentical => T("modcompat.kind.fileIdentical", "Datei identisch");

    public static string KindKeyIdentical => T("modcompat.kind.keyIdentical", "Schlüssel identisch");

    public static string KindKeyCombinable => T("modcompat.kind.keyCombinable", "Kombinierbar");

    public static string KindKeyLoss => T("modcompat.kind.keyLoss", "Beitrag geht verloren");

    public static string KindKeyConflict => T("modcompat.kind.keyConflict", "Schlüsselkonflikt");

    public static string KindRedundantOverride => T("modcompat.kind.redundantOverride", "Unnötige Kopie");

    public static string KindTooCoarse => T("modcompat.kind.tooCoarse", "Zu grob überschrieben");

    public static string KindAdditiveOnly => T("modcompat.kind.additiveOnly", "Nur Ergänzung");

    public static string KindValueChangeOnly => T("modcompat.kind.valueChangeOnly", "Nur Werte geändert");

    public static string KindReplaceNeeded => T("modcompat.kind.replaceNeeded", "Ersetzung nötig");

    public static string KindUnreadable => T("modcompat.kind.unreadable", "Nicht lesbar");

    public static string KindMissingFolder => T("modcompat.kind.missingFolder", "Ordner fehlt");

    public static string KindReplacePathNoEffect => T("modcompat.kind.replacePathNoEffect", "replace_path wirkt nicht");

    public static string KindReplacePathGuiCrash => T("modcompat.kind.replacePathGuiCrash", "replace_path: Absturz");

    public static string SuggestionNeedsCompatch => T("modcompat.suggestion.needsCompatch", "Kompatibilitätspatch nötig");

    public static string SuggestionReviewBothSides => T("modcompat.suggestion.reviewBothSides", "Beide Seiten prüfen");

    public static string SuggestionUseInject => T("modcompat.suggestion.useInject", "INJECT: genügt");

    public static string SuggestionUseReplace => T("modcompat.suggestion.useReplace", "REPLACE: nötig");

    public static string SuggestionProvideOnlyChanged => T("modcompat.suggestion.provideOnlyChanged", "Nur Geändertes bereitstellen");

    public static string SuggestionRemoveRedundant => T("modcompat.suggestion.removeRedundant", "Kopie entfernen");

    public static string BehaviorWholeFile => T("modcompat.behavior.wholeFile", "Nur Datei");

    public static string BehaviorPerKey => T("modcompat.behavior.perKey", "Je Schlüssel (INJECT/REPLACE)");

    public static string BehaviorFirstWins => T("modcompat.behavior.firstWins", "Erster gewinnt");

    public static string BehaviorLastWins => T("modcompat.behavior.lastWins", "Letzter gewinnt");

    public static string BehaviorPartialKeys => T("modcompat.behavior.partialKeys", "Einzelne Werte");

    public static string BehaviorAppendOnly => T("modcompat.behavior.appendOnly", "Nur anhängen");

    public static string BehaviorLocalizationKeys => T("modcompat.behavior.localizationKeys", "Lokalisierung");

    // Fortschritt.
    public static string ProgressReadFiles(string mod) => F("modcompat.progress.readFiles", "Lese Dateien: {0}", mod);

    public static string ProgressCheckFiles(string mod) => F("modcompat.progress.checkFiles", "Prüfe Dateien: {0}", mod);

    public static string ProgressCompareFiles => T("modcompat.progress.compareFiles", "Vergleiche Dateien");

    public static string ProgressCompareKeys => T("modcompat.progress.compareKeys", "Vergleiche Schlüssel");

    public static string ProgressSenseCheck => T("modcompat.progress.senseCheck", "Sinnhaftigkeitscheck");

    // Funde.
    public static string Unreadable => T("modcompat.unreadable", "nicht lesbar");

    public static string FocusNotInSet => T("modcompat.error.focusNotInSet", "Mod 1 steht nicht im Set.");

    public static string NoteCompatchPresent(string compatch) => F("modcompat.note.compatchPresent", "Kompatch vorhanden: {0}", compatch);

    public static string NoteSameProject => T("modcompat.note.sameProject", "Gleiches Projekt: gewollte Abhängigkeit.");

    public static string FindingMissingFolder(string mod, string folder) => F("modcompat.finding.missingFolder", "Der Ordner der Mod \"{0}\" fehlt und wurde übersprungen ({1}).", mod, folder);

    public static string FindingReplacePathNoEffect(string mod, string entry, string root, string rest) => F("modcompat.finding.replacePathNoEffect", "Der replace_path-Eintrag {1} der Mod \"{0}\" wirkt im Spiel nicht, weil replace_path die Ordner ohne {2}/ davor nennt. Schreibe {3}.", mod, entry, root, rest);

    public static string FindingReplacePathGuiCrash(string mod, string entry, string mods) => F("modcompat.finding.replacePathGuiCrash", "Die Mod \"{0}\" nennt {1} als replace_path, und dort liegen .gui-Dateien ({2}). Das Spiel stürzt dann beim Start ab, weil es diese Dateien ohne den Unterordner sucht. Nimm den Eintrag heraus oder lege die .gui-Dateien in einen anderen Ordner.", mod, entry, mods);

    public static string FindingUnreadable(string file, string mod, string? error) => F("modcompat.finding.unreadable", "Die Datei {0} in \"{1}\" ist nicht lesbar ({2}). Sie zählt nur als Dateikonflikt.", file, mod, error);

    public static string FindingFileIdentical(string file, string names) => F("modcompat.finding.fileIdentical", "Die Datei {0} ist in {1} inhaltsgleich.", file, names);

    public static string FindingFileOverride(string file, string names, string winner) => F("modcompat.finding.fileOverride", "Die Datei {0} gibt es in {1}. Geladen wird nur die Fassung von \"{2}\", die anderen gehen vollständig verloren.", file, names, winner);

    public static string FindingKeyIdentical(string key, string names) => F("modcompat.finding.keyIdentical", "Der Schlüssel \"{0}\" ist in {1} inhaltsgleich.", key, names);

    public static string FindingFirstWins(string key, string names, string winner) => F("modcompat.finding.firstWins", "Der Schlüssel \"{0}\" ist in {1} verschieden definiert. Die zuerst geladene Fassung gewinnt: \"{2}\".", key, names, winner);

    public static string FindingLastWins(string key, string names, string winner) => F("modcompat.finding.lastWins", "Der Schlüssel \"{0}\" ist in {1} verschieden gesetzt. Es gilt die zuletzt geladene Fassung: \"{2}\".", key, names, winner);

    public static string FindingAppendOnly(string key, string names) => F("modcompat.finding.appendOnly", "\"{0}\" wird von {1} ergänzt. Das Spiel hängt an; prüfe, ob Trigger- oder Effect-Blöcke doppelt gesetzt werden.", key, names);

    public static string FindingLocalizationKey(string key, string names, string winner) => F("modcompat.finding.localizationKey", "Der Lokalisierungsschlüssel \"{0}\" ist in {1} verschieden. Es gilt die Fassung von \"{2}\".", key, names, winner);

    public static string FindingMultipleReplace(string key, string replacers, string winner) => F("modcompat.finding.multipleReplace", "Mehrere Mods ersetzen \"{0}\" ({1}). Nur die Fassung von \"{2}\" gilt.", key, replacers, winner);

    public static string FindingInjectLost(string winner, string key, string lost) => F("modcompat.finding.injectLost", "\"{0}\" ersetzt \"{1}\" (REPLACE). Der INJECT-Beitrag von {2} geht verloren, weil REPLACE laut Wiki nach INJECT verarbeitet wird.", winner, key, lost);

    public static string FindingReplacesDefinition(string winner, string key, string others) => F("modcompat.finding.replacesDefinition", "\"{0}\" ersetzt die Definition von \"{1}\" aus {2} vollständig (REPLACE).", winner, key, others);

    public static string FindingPlainConflict(string names, string key, string winner) => F("modcompat.finding.plainConflict", "{0} definieren \"{1}\" jeweils vollständig. Nur die Fassung von \"{2}\" gilt.", names, key, winner);

    public static string FindingChildOverwrite(string names, string clashes, string key, string winner) => F("modcompat.finding.childOverwrite", "{0} setzen die Teilschlüssel {1} von \"{2}\" verschieden. INJECT überschreibt vorhandene Teilschlüssel, es gilt der Wert von \"{3}\".", names, clashes, key, winner);

    public static string FindingChildCombine(string names, string key) => F("modcompat.finding.childCombine", "{0} ändern \"{1}\" an verschiedenen Teilschlüsseln. INJECT kombiniert sie.", names, key);

    public static string FindingRedundantFile(string file, string mod, string baseMod) => F("modcompat.finding.redundantFile", "Die Datei {0} in \"{1}\" ist eine unveränderte Kopie der Fassung aus \"{2}\". Sie lässt sich löschen.", file, mod, baseMod);

    public static string FindingAdditiveOnly(string key, string baseMod) => F("modcompat.finding.additiveOnly", "\"{0}\" fügt gegenüber \"{1}\" nur Teilschlüssel hinzu. Statt den ganzen Eintrag zu ersetzen, genügt INJECT:{0}.", key, baseMod);

    public static string FindingValueChangeOnly(string key, string baseMod) => F("modcompat.finding.valueChangeOnly", "\"{0}\" ändert gegenüber \"{1}\" nur einfache Werte von Teilschlüsseln (und fügt eventuell Teilschlüssel hinzu). Statt den ganzen Eintrag zu ersetzen, genügt INJECT:{0}; es überschreibt vorhandene Teilschlüssel.", key, baseMod);

    public static string FindingReplaceNeeded(string key, string baseMod) => F("modcompat.finding.replaceNeeded", "\"{0}\" ändert oder entfernt vorhandene Werte aus \"{1}\". Dafür ist REPLACE:{0} nötig (ohne die restliche Datei).", key, baseMod);

    public static string FindingFormattingOnly(string file, string mod, string baseMod) => F("modcompat.finding.formattingOnly", "Alle Einträge der Datei {0} in \"{1}\" entsprechen der Fassung aus \"{2}\" (nur Formatierung). Sie lässt sich löschen.", file, mod, baseMod);

    public static string FindingTooCoarse(int unchanged, int total, string file, string baseMod, int needed) => F("modcompat.finding.tooCoarse", "{0} von {1} Einträgen der Datei {2} sind unverändert aus \"{3}\" kopiert. Nötig sind nur {4}. Als REPLACE:/INJECT: bereitgestellt kollidiert die Mod seltener mit anderen.", unchanged, total, file, baseMod, needed);

    public static string CheckerConflict(string path, string a, string b) => F("modcompat.checker.conflict", "\"{0}\" wird von Mod A und Mod B unterschiedlich gesetzt: Mod A = {1}, Mod B = {2}. Bitte manuell entscheiden.", path, a, b);

    /// <summary>Gleicher Schlüssel wie im Kompatch-Merge (<c>CompatchTexts.DocRemoved</c>).</summary>
    public static string Removed => T("compatch.doc.removed", "(entfernt)");

    // Bericht und Detailanzeige.
    public static string ReportTitle => T("modcompat.report.title", "Kompatibilitätsbericht");

    public static string ReportCounts(int conflicts, int hints, int infos) => F("modcompat.report.counts", "Funde: {0} Konflikte, {1} Hinweise, {2} Infos", conflicts, hints, infos);

    public static string NoFindings => T("modcompat.noFindings", "Keine Funde.");

    public static string Conflicts => T("modcompat.conflicts", "Konflikte");

    public static string Hints => T("modcompat.hints", "Hinweise");

    public static string Infos => T("modcompat.infos", "Infos");

    public static string Uncertain => T("modcompat.uncertain", "unsicher");

    public static string WinnerLine(string winner) => F("modcompat.detail.winner", "Gewinner: {0}", winner);

    public static string SuggestionLine(string suggestion) => F("modcompat.detail.suggestion", "Empfehlung: {0}", suggestion);

    public static string NoteLine(string note) => F("modcompat.detail.note", "Hinweis: {0}", note);

    public static string ReportContributions => T("modcompat.report.contributions", "Beiträge:");

    // Startset.
    public static string StarterSetName => T("modcompat.starter.name", "Mod Kompatibilitätstest");

    public static string StarterSetDescription => T("modcompat.starter.description", "Füge hier die zwei oder mehr Mods hinzu, die du kompatibel machen willst, in Ladereihenfolge vom größten zum kleinsten Mod, um sie zu vergleichen.");

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
