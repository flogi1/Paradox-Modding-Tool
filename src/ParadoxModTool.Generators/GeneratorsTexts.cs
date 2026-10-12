using ParadoxModTool.Localization;

namespace ParadoxModTool.Generators;

/// <summary>Meldungen der Generator-Bibliothek (Keys <c>generators.*</c> in <c>locales/*.json</c>). Der deutsche Text ist der Rückfall und muss mit de.json übereinstimmen.</summary>
public static class GeneratorsTexts
{
    public static string TemplateError(string template, int line, string message) =>
        F("generators.error.template", "Vorlage {0}, Zeile {1}: {2}", template, line, message);

    public static string PackageJsonMissing(string folder) => F("generators.error.packageJsonMissing", "package.json fehlt in {0}.", folder);

    public static string PackageJsonInvalid(string message) => F("generators.error.packageJsonInvalid", "package.json ist ungültig: {0}", message);

    public static string FieldMissing(string field) => F("generators.error.fieldMissing", "package.json: \"{0}\" fehlt.", field);

    public static string KeyNotAllowed(string field) => F("generators.error.keyNotAllowed", "package.json: \"{0}\" ist bei dieser Ausgabeart nicht erlaubt.", field);

    public static string UnknownValue(string field, string value) => F("generators.error.unknownValue", "package.json: unbekannter Wert \"{1}\" bei \"{0}\".", field, value);

    public static string UnsafePath(string output, string path) =>
        F("generators.error.unsafePath", "Ausgabe {0}: Pfad \"{1}\" verlässt den Zielordner.", output, path);

    public static string ArchiveTooLarge(string entry) =>
        F("generators.archive.tooLarge", "Import abgebrochen: {0} überschreitet die Größengrenze.", entry);

    public static string ArchiveInvalid(string message) =>
        F("generators.archive.invalid", "Die Datei ist kein gültiges Generator-Paket: {0}", message);

    public static string CheckOk => T("generators.check.ok", "Keine Fehler gefunden.");

    public static string TemplateNameInvalid(string output, string name) =>
        F("generators.error.templateName", "Ausgabe {0}: Vorlagenname \"{1}\" ist ungültig; erlaubt ist nur ein Dateiname mit .sbn im Ordner templates.", output, name);

    public static string UnsafeAssetPath(string id, string path) =>
        F("generators.error.unsafeAssetPath", "Asset {0}: Pfad \"{1}\" muss relativ im Paketordner liegen.", id, path);

    public static string TemplateMissing(string template) => F("generators.error.templateMissing", "Vorlage {0} fehlt im Ordner templates.", template);

    public static string LookupUnknownSource(string field, string source) =>
        F("generators.error.lookupUnknownSource", "Feld {0}: Nachschlage-Quelle \"{1}\" gibt es nicht.", field, source);

    public static string LookupUnknownFill(string field, string target) =>
        F("generators.error.lookupUnknownFill", "Feld {0}: \"fills\" nennt das Feld \"{1}\", das es auf dieser Ebene nicht gibt.", field, target);

    public static string LookupFillsNeedName(string field) => F("generators.error.lookupFillsNeedName", "Feld {0}: \"fills\" braucht \"name\" oder \"desc\".", field);

    public static string LookupUnknownAsset(string source, string asset) =>
        F("generators.error.lookupUnknownAsset", "Nachschlage-Quelle {0}: Asset \"{1}\" fehlt in \"assets\".", source, asset);

    public static string LookupNoGameFolder(string source) => F("generators.lookup.noGameFolder", "{0}: Spielordner nicht gefunden.", source);

    public static string LookupAssetMissing(string source, string asset) =>
        F("generators.lookup.assetMissing", "{0}: Datei für \"{1}\" fehlt, \"Asset wählen\" holt sie.", source, asset);

    public static string LookupSourceFailed(string source, string message) => F("generators.lookup.sourceFailed", "{0}: {1}", source, message);

    public static string KeyFieldNotInHead(string field) => F("generators.error.keyFieldNotInHead", "keyField \"{0}\" ist kein Kopf-Feld.", field);

    public static string KeyFieldNeedsPattern(string field) =>
        F("generators.error.keyFieldNeedsPattern", "Schlüsselfeld \"{0}\" braucht ein Muster oder eine Auswahl, weil danach noch ein Schlüsselfeld folgt.", field);

    public static string KeyChoiceHasSeparator(string field, string value, string separator) =>
        F("generators.error.keyChoiceHasSeparator", "Schlüsselfeld \"{0}\": Der Auswahlwert \"{1}\" enthält das Trennzeichen \"{2}\".", field, value, separator);

    public static string AutoNumberNotAllowed(string field) =>
        F("generators.error.autoNumberNotAllowed", "Feld \"{0}\": autoNumber ist nur am letzten Schlüsselfeld vom Typ Zahl erlaubt.", field);

    public static string CandidatesNeedAutoNumber =>
        T("generators.error.candidatesNeedAutoNumber", "candidates: Das Paket braucht ein Schlüsselfeld mit autoNumber, damit neue Arbeitsstände ihre Nummer selbst bekommen.");

    public static string CandidatesUsedEmpty => T("generators.error.candidatesUsedEmpty", "candidates.used: Mindestens eine Angabe ist nötig, sonst gälte jeder Modifikator als unbenutzt.");

    public static string CandidatesPatternInvalid(string field, string pattern, string message) =>
        F("generators.error.candidatesPatternInvalid", "{0}: \"{1}\" ist kein gültiger regulärer Ausdruck ({2}).", field, pattern, message);

    public static string CandidatesGroupIdInvalid(string id) =>
        F("generators.error.candidatesGroupIdInvalid", "candidates.groups: Die Kennung \"{0}\" ist leer oder kommt doppelt vor.", id);

    public static string CandidatesAskInvalid(string field) =>
        F("generators.error.candidatesAskInvalid", "candidates.create.ask: \"{0}\" muss ein Kopf-Feld vom Typ Auswahl sein und nicht das autoNumber-Feld.", field);

    public static string CandidatesAskMissing(string field) =>
        F("generators.error.candidatesAskMissing", "candidates.create.ask: Das Schlüsselfeld \"{0}\" fehlt.", field);

    public static string CandidatesNameInvalid(string field) =>
        F("generators.error.candidatesNameInvalid", "candidates.create.name: \"{0}\" muss ein Kopf-Feld vom Typ Text sein.", field);

    public static string CandidatesModifiersInvalid(string value) =>
        F("generators.error.candidatesModifiersInvalid", "candidates.create.modifiers: \"{0}\" muss \"<Wurzel-Ebene>.<Feld>\" mit einem Feld vom Typ Modifikatoren sein.", value);

    public static string NoFreeNumber(int start, int tries) =>
        F("generators.error.noFreeNumber", "Ab Nummer {0} war in {1} Versuchen jede Nummer vergeben oder hatte eine Hash-Kollision der Lokalisierungsschlüssel.", start, tries);

    public static string HashCollision(string newKey, string existingKey) =>
        F("generators.warning.hashCollision", "Der Lokalisierungsschlüssel {0} hat denselben Hash wie {1}; das Spiel meldet das im error.log.", newKey, existingKey);

    public static string ExternalNotChosen(string label) =>
        F("generators.warning.externalNotChosen", "Externe Datei \"{0}\" ist nicht gewählt oder fehlt; ihr Eintrag wird nicht geschrieben.", label);

    public static string ExternalNameClash(string name) =>
        F("generators.error.externalNameClash", "Zwei externe Dateien heißen \"{0}\"; sie lassen sich im Plan nicht unterscheiden.", name);

    public static string JsonEntryFileInvalid(string message) =>
        F("generators.error.jsonEntryFileInvalid", "Die externe Datei ist kein gültiges JSON: {0}", message);

    public static string JsonEntryValueInvalid(string message) =>
        F("generators.error.jsonEntryValueInvalid", "Der gerenderte Wert ist kein gültiges JSON: {0}", message);

    public static string JsonEntryPathMissing(string path) =>
        F("generators.error.jsonEntryPathMissing", "In der externen Datei gibt es unter \"{0}\" kein Objekt.", path);

    public static string UnknownTarget(string target) => F("generators.error.unknownTarget", "Unbekanntes Ziel \"{0}\" (erlaubt: mod, output).", target);

    public static string FileMissingNoSkeleton => T("generators.error.fileMissingNoSkeleton", "Die Datei fehlt und das Paket hat kein Grundgerüst dafür.");

    public static string BlockMissing(string path) => F("generators.error.blockMissing", "Block \"{0}\" fehlt in der Datei.", path);

    public static string MarkerWithoutEnd(string marker) => F("generators.error.markerWithoutEnd", "Markierung \"{0}\" hat kein Ende. Bitte die Datei von Hand prüfen.", marker);

    public static string MatchInvalid(string output, string message) => F("generators.error.matchInvalid", "Ausgabe \"{0}\": Das Treffer-Muster ist kein gültiger regulärer Ausdruck ({1}).", output, message);

    public static string ParseFailed(string message) => F("generators.error.parseFailed", "Die Datei lässt sich nicht lesen: {0}", message);

    public static string AssetLibraryMissing(string path) => F("generators.error.assetLibraryMissing", "Asset-Bibliothek {0} fehlt.", path);

    public static string AssetMissing(string entry) => F("generators.error.assetMissing", "\"{0}\" fehlt in der Asset-Bibliothek.", entry);

    public static string CountOutOfRange(string level, int min, int max) => F("generators.issue.count", "{0}: {1} bis {2} erlaubt.", level, min, max);

    public static string FieldRequired(string field) => F("generators.issue.required", "{0} fehlt.", field);

    public static string FieldPattern(string field, string pattern) => F("generators.issue.pattern", "{0} passt nicht zum Muster {1}.", field, pattern);

    public static string ModifierWithoutValue(string field, string key) => F("generators.issue.modifierNoValue", "{0}: {1} hat keinen Wert.", field, key);

    public static string BlocksRemoved(string file, string keys) => F("generators.plan.blocksRemoved", "{0}: Blöcke entfallen: {1}", file, keys);

    public static string UnknownAsset(string id) => F("generators.error.unknownAsset", "Unbekannte Asset-Bibliothek \"{0}\".", id);

    public static string InFile(string file, string message) => F("generators.error.inFile", "{0}: {1}", file, message);

    public static string AssetLibraryNotProvided(string id) =>
        F("generators.note.assetLibraryNotProvided", "Asset-Bibliothek \"{0}\" ist noch nicht bereitgestellt; die Kopie daraus entfällt.", id);

    public static string ReadNumberOutside(int number) => F("generators.read.numberOutside", "Nummer {0} gehört zu keiner Gruppe; übersprungen.", number);

    public static string ReadTooMany(string element, int max) => F("generators.read.tooMany", "{0}: mehr als {1} Elemente; der Rest wurde übersprungen.", element, max);

    public static string ReadKeyMissing(string key) => F("generators.read.keyMissing", "{0}: keine Dateien in der Mod gefunden.", key);

    public static string ReadJsonNoFile(string label) =>
        F("generators.read.jsonNoFile", "Externe Datei \"{0}\" ist nicht gewählt oder fehlt; die Werte daraus wurden nicht eingelesen.", label);

    public static string ReadJsonNoEntry(string label, string entry) =>
        F("generators.read.jsonNoEntry", "In der externen Datei \"{0}\" gibt es keinen Eintrag \"{1}\".", label, entry);

    public static string ReadJsonNoProperty(string label, string entry, string property) =>
        F("generators.read.jsonNoProperty", "Im Eintrag \"{1}\" der externen Datei \"{0}\" fehlt \"{2}\".", label, entry, property);

    public static string ReadJsonInvalid(string label, string message) =>
        F("generators.read.jsonInvalid", "Externe Datei \"{0}\" konnte nicht gelesen werden: {1}", label, message);

    public static string ReadUnsafePath(string path) => F("generators.read.unsafePath", "Pfad \"{0}\" verlässt die Mod; übersprungen.", path);

    public static string LookupUnsafeFolder(string source, string folder) => F("generators.lookup.unsafeFolder", "{0}: Ordner \"{1}\" verlässt den Spielordner.", source, folder);

    public static string PatternInvalid(string message) => F("generators.pattern.invalid", "Muster ungültig: {0}", message);

    public static string PatternGroupMissing(string group) => F("generators.pattern.groupMissing", "Das Muster braucht die Gruppe (?<{0}>...).", group);

    public static string PatternNeedsGroup => T("generators.pattern.needsGroup", "Das Muster braucht eine Gruppe in runden Klammern für den Tag.");

    public static string PatternUnclosed => T("generators.pattern.unclosed", "\"{{\" und \"}}\" passen nicht zusammen.");

    public static string PatternEmpty =>T("generators.pattern.empty", "Angabe fehlt.");

    public static string PathUnsafe(string path) => F("generators.pattern.pathUnsafe", "Pfad \"{0}\" muss relativ bleiben (kein \"..\", kein Laufwerk, kein führendes /).", path);

    public static string CheckUnknownLevel(string level) => F("generators.check.unknownLevel", "Ebene \"{0}\" gibt es nicht.", level);

    public static string CheckUnknownField(string field) => F("generators.check.unknownField", "Feld \"{0}\" gibt es nicht.", field);

    public static string CheckSampleRender(string message) => F("generators.check.sampleRender", "Probe mit Beispielwerten: {0}", message);

    public static string CheckYmlNotLocalization(string output) => F("generators.check.ymlNotLocalization",
        "Ausgabe \"{0}\" schreibt eine .yml-Datei, ist aber nicht von der Art \"localization\". Nur diese Art schreibt eine Datei je Sprache mit Kopfzeile l_<sprache>:.", output);

    public static string CheckFieldWrongType(string field, string expected) => F("generators.check.fieldWrongType", "Feld \"{0}\" passt nicht: erwartet {1}.", field, expected);

    public static string ExpectedMultilingual => T("generators.check.expectedMultilingual", "ein mehrsprachiges Textfeld");

    public static string ExpectedBool => T("generators.check.expectedBool", "ein Ja/Nein-Feld");

    public static string ExpectedImage => T("generators.check.expectedImage", "ein Bildfeld des Kopfs");

    public static string ExpectedText => T("generators.check.expectedText", "ein Textfeld derselben Ebene");

    public static string ExpectedTextOrNumber => T("generators.check.expectedTextOrNumber", "ein Text- oder Zahlfeld");

    public static string CheckLookupNoSource => T("generators.check.lookupNoSource", "Mindestens eine Quelle wählen.");

    public static string ProbeFileMissing(string path) => F("generators.probe.fileMissing", "Datei \"{0}\" fehlt in der Mod.", path);

    public static string ProbeNoElementsFile(string path) =>
        F("generators.probe.noElementsFile", "Die Elemente-Datei \"{0}\" fehlt in der Mod; für diesen Schlüssel gibt es nichts einzulesen.", path);

    public static string ProbeNoLocalizationOutput =>
        T("generators.probe.noLocalizationOutput", "Das Paket hat keine Ausgabe der Art Lokalisierung; Texte werden nicht eingelesen.");

    public static string PreviewNoTxt(string folder) => F("generators.preview.noTxt", "Ordner \"{0}\" enthält keine .txt-Dateien.", folder);

    public static string PreviewNoBlocks => T("generators.preview.noBlocks", "Die .txt-Dateien enthalten keine passenden Blöcke (Tiefe und \"Nur Blöcke mit Namen\" prüfen).");

    public static string PreviewItemsEmpty(string items) => F("generators.preview.itemsEmpty", "Der Elemente-Pfad \"{0}\" trifft nichts in der Datei.", items);

    public static string AiFileNotAllowed(string path) =>
        F("generators.ai.fileNotAllowed", "Datei \"{0}\" ist nicht erlaubt (nur package.json, functions.sbn und templates/*.sbn ohne Unterordner).", path);

    public static string AiFileDuplicate(string path) => F("generators.ai.fileDuplicate", "Datei \"{0}\" kommt doppelt vor.", path);

    public static string AiBlockWithoutEnd(string path) => F("generators.ai.blockWithoutEnd", "Der Block für \"{0}\" endet nicht mit <<<END>>>.", path);

    public static string AiNoPackageJson => T("generators.ai.noPackageJson", "Die Antwort enthält kein package.json.");

    public static string AiNoFiles => T("generators.ai.noFiles", "Die Antwort enthält keine Dateiblöcke (<<<FILE pfad>>> ... <<<END>>>).");

    public static string LegacyRemoved(int count) => F("generators.note.legacyRemoved", "{0} alte Einträge ohne Markierung entfernt.", count);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object[] args) => UiText.Format(key, german, args);
}
