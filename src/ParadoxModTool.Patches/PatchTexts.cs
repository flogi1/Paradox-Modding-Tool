using ParadoxModTool.Localization;

namespace ParadoxModTool.Patches;

/// <summary>
/// Meldungen der Patch-Rezepte (Zeilenfehler, übersprungene Zeilen, Änderungsnotizen, Konflikte; Keys <c>patch.*</c> in <c>locales/*.json</c>).
/// Der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class PatchTexts
{
    public static string DuplicateKey => T("patch.row.duplicateKey", "Doppelter Schlüssel");

    public static string NotFound(string column) => F("patch.row.notFound", "{0}: nicht gefunden", column);

    public static string UnknownTitle(string title) => F("patch.row.unknownTitle", "Titel „{0}“ gibt es nicht", title);

    public static string FileSkipped(string file, string message) => F("patch.fileSkipped", "{0} übersprungen: {1}", file, message);

    public static string LinesCommented(int count) => F("patch.note.linesCommented", "{0} Zeilen auskommentiert", count);

    public static string LinesDeleted(int count) => F("patch.note.linesDeleted", "{0} Zeilen gelöscht", count);

    public static string Replaced(int count) => F("patch.note.replaced", "{0} Stellen ersetzt", count);

    public static string BlocksRemoved(int count) => F("patch.note.blocksRemoved", "{0} Blöcke entfernt", count);

    public static string BlocksEdited(int count) => F("patch.note.blocksEdited", "{0} Blöcke bearbeitet", count);

    public static string BlockNotFound(string key, string files) => F("patch.row.blockNotFound", "Block „{0}“ nicht gefunden in {1}", key, files);

    public static string CopyCreated => T("patch.note.copyCreated", "Kopie aus Vanilla angelegt");

    public static string MemoryBroken(string file) => F("patch.warn.memoryBroken", "Gedächtnis-Datei {0} ist unlesbar: Handänderungen der erzeugten Bausteine sind diesmal nicht bekannt. Beim Anwenden wird sie als {0}.broken gesichert", file);

    public static string SelfMatch(string rule) => F("patch.warn.selfMatch", "Regel „{0}“: der Ersatztext trifft das eigene Suchmuster, die Regel wäre nicht wiederholbar", rule);

    public static string RegionBroken(string where) => F("patch.regionBroken", "Bereich in {0} ist beschädigt (Endmarke fehlt oder doppelt), Regel übersprungen", where);

    public static string MarkerInUnit(string unit) => F("patch.markerInUnit", "Baustein „{0}“ enthält eine Zeile, die mit # PMT-Unit oder # PMT-Patch beginnt; bitte im Vorlagen- oder Blocktext ändern", unit);

    public static string InsideMissing(string path) => F("patch.insideMissing", "Block „{0}“ nicht gefunden", path);

    public static string UnitConflict(string unit, string detail) => F("patch.conflict.unit", "Baustein „{0}“: {1}", unit, detail);

    public static string UnitUnreadable(string unit) => F("patch.conflict.unreadable", "Baustein „{0}“ ist kein gültiges Skript, bitte von Hand prüfen", unit);

    public static string LocKeyConflict(string key) => F("patch.conflict.locKey", "Schlüssel „{0}“ wurde von Hand und im neu Erzeugten verschieden geändert", key);

    public static string RenameNoVanillaText(string key, string language) => F("patch.rename.noVanillaText", "Kein Vanilla-Text für „{0}“ ({1}): die Zeile verweist auf sich selbst und bleibt, bitte von Hand prüfen", key, language);

    public static string RenameLocDiffers(string key, string language, string file) => F("patch.rename.locDiffers", "„{0}“ ({1}) steht in {2} schon mit anderem Text; beide Zeilen bleiben", key, language, file);

    public static string RenameSelfRemoved(int count) => F("patch.rename.selfRemoved", "{0} Selbstverweise entfernt", count);

    public static string RenameInlined(int count) => F("patch.rename.inlined", "{0} Vanilla-Texte eingesetzt", count);

    public static string RenameDuplicates(int count) => F("patch.rename.duplicates", "{0} doppelte Zeilen entfernt", count);

    public static string RenameMoved(int count) => F("patch.rename.moved", "{0} Zeilen nach replace verschoben", count);

    public static string RenameTokens(int count) => F("patch.rename.tokens", "{0} Stellen umbenannt", count);

    public static string RenameOrphans(int count) => F("patch.rename.orphans", "{0} Lokalisierungs-Schlüssel alter Stufen ohne Gebäude bleiben unverändert", count);

    public static string RenamePlanName(string recipe) => F("patch.rename.planName", "Umbenennung ({0})", recipe);

    public static string UnitDeletedByHand(string unit) => F("patch.note.deletedByHand", "Baustein „{0}“ von Hand gelöscht, nicht neu eingefügt", unit);

    public static string UnitKeptEdited(string unit) => F("patch.note.keptEdited", "Baustein „{0}“ gehört zu keiner Zeile mehr, bleibt wegen Handänderungen stehen", unit);

    public static string VanillaBlockMissing(string key) => F("patch.skip.vanillaBlockMissing", "Vanilla-Block „{0}“ nicht gefunden", key);

    public static string AdoptBlockedRegion(string file, int line) => F("patch.skip.adoptBlockedRegion", "steht schon im erzeugten Bereich und zusätzlich in {0}:{1}; eine der beiden Stellen entfernen", file, line);

    public static string DefinedSeveralTimes(string key, string places) => F("patch.skip.definedSeveralTimes", "{0} ist in der Mod mehrfach definiert ({1}); nicht übernommen", key, places);

    public static string BlocksAdopted(int count) => F("patch.note.blocksAdopted", "{0} Blöcke in die Patch-Region übernommen", count);

    public static string SkippedWithRule(string rule, string reason) => F("patch.skip.withRule", "ausgelassen wie in Regel {0}: {1}", rule, reason);

    public static string AlreadyDefined(string file, int line) => F("patch.skip.alreadyDefined", "bereits definiert in {0}:{1}", file, line);

    public static string VariableMissing(string name, string file) => F("patch.warn.variableMissing", "@{0} ist in {1} nicht definiert", name, file);

    public static string UnitsWritten(int count) => F("patch.note.unitsWritten", "{0} Bausteine erzeugt", count);

    public static string UnitsMerged(int count) => F("patch.note.unitsMerged", "{0} Bausteine mit Handänderungen zusammengeführt", count);

    public static string UnitsRemoved(int count) => F("patch.note.unitsRemoved", "−{0} Bausteine entfernt", count);

    public static string ListMissing(string path, string file) => F("patch.skip.listMissing", "Liste „{0}“ nicht gefunden in {1}", path, file);

    public static string EntriesAdded(int count) => F("patch.note.entriesAdded", "+{0} Listeneinträge", count);

    public static string EntriesRemoved(int count) => F("patch.note.entriesRemoved", "−{0} Listeneinträge", count);

    public static string ApplyUndo(string recipe) => F("patch.undo.apply", "Patch-Rezept „{0}“ anwenden", recipe);

    public static string UnknownPlaceholder(string rule, string name) => F("patch.warn.unknownPlaceholder", "Regel „{0}“: „{{{1}}}“ ist keine Spalte und bleibt als Text stehen", rule, name);

    public static string ColumnNameInvalid => T("patch.column.invalid", "Nur Buchstaben, Ziffern und _ erlaubt, nicht mit einer Ziffer am Anfang");

    public static string ColumnNameReserved => T("patch.column.reserved", "„key“ ist reserviert");

    public static string ColumnNameTaken => T("patch.column.taken", "Diese Spalte gibt es schon");

    public static string KeyMissing => T("patch.row.keyMissing", "Schlüssel fehlt");

    public static string EmptyPlaceholder(string names) => F("patch.skip.emptyPlaceholder", "leerer Wert für {0}", names);

    public static string WatchVanillaEmpty => T("patch.watch.vanillaEmpty", "Im Spielordner stehen keine Skriptdateien: stimmt der Spielordner in den Einstellungen?");

    public static string WatchNoVanilla => T("patch.watch.noVanilla", "Vanilla-Ordner nicht gefunden, nur die Mod wurde durchsucht");

    public static string WatchFileSkipped(string file, string message) => F("patch.watch.fileSkipped", "{0} nicht gelesen: {1}", file, message);

    public static string WatchOutdatedFailed(string message) => F("patch.watch.outdatedFailed", "„veraltet“ konnte nicht bestimmt werden: {0}", message);

    public static string WatchPlanFailed(string message) => F("patch.watch.planFailed", "Vorschau für den Wächter fehlgeschlagen, „abgedeckt“ ist daher unsicher: {0}", message);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
