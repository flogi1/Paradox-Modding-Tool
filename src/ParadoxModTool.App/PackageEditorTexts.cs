using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

/// <summary>Texte des Paket-Editors (Keys <c>packageEditor.*</c> in <c>locales/*.json</c>); der deutsche Text ist der Rückfall.</summary>
internal static partial class PackageEditorTexts
{
    public static string Title => T("packageEditor.title", "Paket-Editor");

    public static string TitleWith(string name) => F("packageEditor.titleWith", "Paket-Editor: {0}", name);

    public static string Packages => T("packageEditor.packages", "Pakete");

    public static string New => T("packageEditor.new", "Neu");

    public static string NewTip => T("packageEditor.new.tip", "Legt ein leeres Paket für ein Spiel an: ein Kopf-Feld \"key\", keine Ausgaben. Beispiel: Kennung \"stadtwappen\".");

    public static string Copy => T("packageEditor.copy", "Als Kopie bearbeiten");

    public static string CopyTip => T("packageEditor.copy.tip", "Legt eine Kopie des gewählten Pakets mit neuer Kennung an. So lassen sich auch mitgelieferte Pakete wie IV2 anpassen.");

    public static string Delete => T("packageEditor.delete", "Löschen");

    public static string DeleteTip => T("packageEditor.delete.tip", "Verschiebt das gewählte eigene Paket samt Arbeitsständen und Assets in den Papierkorb.");

    public static string Export => T("packageEditor.export", "Exportieren");

    public static string ExportTip => T("packageEditor.export.tip", "Speichert das Paket als .pmtgen-Datei zum Weitergeben, wahlweise mit Arbeitsständen und Assets aus Mods.");

    public static string Import => T("packageEditor.import", "Importieren");

    public static string ImportTip => T("packageEditor.import.tip", "Liest ein Paket aus einer .pmtgen-Datei ein. Unsichere Einträge und Assets aus dem Spiel werden übersprungen.");

    public static string Save => T("packageEditor.save", "Speichern");

    public static string SaveTip => T("packageEditor.save.tip", "Schreibt package.json und die geänderten Vorlagen (Strg+S). Auch ein halb fertiges Paket lässt sich speichern.");

    public static string Discard => T("packageEditor.discard", "Verwerfen");

    public static string DiscardTip => T("packageEditor.discard.tip", "Verwirft alle ungespeicherten Änderungen und lädt das Paket neu.");

    public static string Check => T("packageEditor.check", "Prüfen");

    public static string CheckTip => T("packageEditor.check.tip", "Lädt das Paket probeweise und prüft alle Vorlagen. Fehler in Vorlagen springen per Klick an die Zeile.");

    public static string TestInGenerator => T("packageEditor.test", "Im Generator testen");

    public static string TestInGeneratorTip => T("packageEditor.test.tip", "Speichert und öffnet das Generator-Fenster auf diesem Paket. Zum Testen das Ziel \"Ausgabeordner\" wählen.");

    public static string Guide => T("packageEditor.guide", "Anleitung");

    public static string GuideTip => T("packageEditor.guide.tip", "Öffnet die Anleitung zu Generator-Paketen (F1 springt zum Kapitel der offenen Maske).");

    public static string ShippedBanner => T("packageEditor.shippedBanner", "Mitgeliefert, nur lesen. Änderungen gehen nur an einer Kopie.");

    public static string Sections => T("packageEditor.sections", "Abschnitte");

    public static string SectionBasics => T("packageEditor.section.basics", "Grunddaten");

    public static string SectionHead => T("packageEditor.section.head", "Kopf-Felder");

    public static string SectionLevels => T("packageEditor.section.levels", "Formular-Ebenen");

    public static string SectionConstants => T("packageEditor.section.constants", "Konstanten");

    public static string SectionLanguages => T("packageEditor.section.languages", "Sprachen");

    public static string SectionAssets => T("packageEditor.section.assets", "Assets");

    public static string SectionOutputs => T("packageEditor.section.outputs", "Ausgaben");

    public static string SectionTemplates => T("packageEditor.section.templates", "Vorlagen");

    public static string SectionMessages => T("packageEditor.section.messages", "Warnungen und Fehler");

    public static string SectionRead => T("packageEditor.section.read", "Einlesen");

    public static string SectionLookups => T("packageEditor.section.lookups", "Nachschlagen");

    public static string NoPackage => T("packageEditor.noPackage", "Links ein Paket wählen oder mit \"Neu\" anlegen.");

    public static string BrokenPackage(string folder) => F("packageEditor.brokenPackage", "{0} (fehlerhaft)", folder);

    public static string CannotOpen(string message) => F("packageEditor.cannotOpen", "Das Paket lässt sich nicht öffnen: {0}", message);

    public static string UnsavedAsk(string name) => F("packageEditor.unsavedAsk", "Das Paket \"{0}\" hat ungespeicherte Änderungen. Speichern?", name);

    public static string CommentsWarning => T("packageEditor.commentsWarning", "package.json enthält Kommentare. Beim Speichern gehen sie verloren, der Inhalt bleibt gleich. Trotzdem speichern?");

    public static string SaveFailed(string message) => F("packageEditor.saveFailed", "Speichern fehlgeschlagen: {0}", message);

    public static string NewTitle => T("packageEditor.newTitle", "Neues Paket");

    public static string CopyTitle => T("packageEditor.copyTitle", "Paket kopieren");

    public static string IdLabel => T("packageEditor.id", "Kennung");

    public static string IdTip => T("packageEditor.id.tip", "Ordnername und Kennung des Pakets: Kleinbuchstaben, Ziffern, _ und -, höchstens 40 Zeichen. Beispiel: stadtwappen.");

    public static string NameLabel => T("packageEditor.name", "Name");

    public static string NameTip => T("packageEditor.name.tip", "Anzeigename in der Paketliste. Beispiel: Stadtwappen.");

    public static string GameLabel => T("packageEditor.game", "Spiel");

    public static string GameTip => T("packageEditor.game.tip", "Spiel, für das das Paket Dateien erzeugt. Es erscheint nur, solange dieses Spiel aktiv ist.");

    public static string WithStates => T("packageEditor.withStates", "Arbeitsstände mitkopieren");

    public static string WithStatesTip => T("packageEditor.withStates.tip", "Kopiert die gespeicherten Formulareingaben (je Schlüssel, z. B. je Tag) in das neue Paket.");

    public static string WithAssets => T("packageEditor.withAssets", "Assets mitkopieren");

    public static string WithAssetsTip => T("packageEditor.withAssets.tip", "Kopiert die Asset-Dateien (z. B. eine Wappen-Bibliothek) in das neue Paket.");

    public static string IdInvalid => T("packageEditor.idInvalid", "Nur Kleinbuchstaben, Ziffern, _ und -, höchstens 40 Zeichen.");

    public static string IdTaken => T("packageEditor.idTaken", "Diese Kennung ist schon vergeben (auch mitgelieferte Pakete zählen).");

    public static string OtherGameNote(string game) => F("packageEditor.otherGameNote", "Das Paket gehört zu {0}. Es erscheint in der Liste, sobald dieses Spiel aktiv ist.", game);

    public static string DeleteAsk(string name, int states, int assets) =>
        F("packageEditor.deleteAsk", "Paket \"{0}\" mit {1} Arbeitsständen und {2} Asset-Dateien in den Papierkorb verschieben?", name, states, assets);

    public static string DeleteFailed(string message) => F("packageEditor.deleteFailed", "Löschen fehlgeschlagen: {0}", message);

    public static string Languages => T("packageEditor.languages", "Sprachen");

    public static string LanguagesTip => T("packageEditor.languages.tip", "Text je Sprache bearbeiten. Ohne weitere Sprachen bleibt der Text ein einfacher Text für alle Sprachen.");

    public static string LanguagesTitle(string label) => F("packageEditor.languagesTitle", "Sprachen: {0}", label);

    // Masken (Aufgabe 10)
    public static string Up => T("packageEditor.up", "Hoch");

    public static string Down => T("packageEditor.down", "Runter");

    public static string Remove => T("packageEditor.remove", "Entfernen");

    public static string Rename => T("packageEditor.rename", "Umbenennen");

    public static string Add => T("packageEditor.add", "Hinzufügen");

    public static string None => T("packageEditor.none", "(keins)");

    public static string IdReadOnlyTip => T("packageEditor.basics.id.tip", "Kennung des Pakets, gleich dem Ordnernamen. Sie lässt sich nur über \"Als Kopie bearbeiten\" ändern.");

    public static string MarkerIdLabel => T("packageEditor.basics.markerId", "Markierungs-Kennung");

    public static string MarkerIdTip => T("packageEditor.basics.markerId.tip",
        "Kennung in den Markierungen, mit denen PMT seine Abschnitte in der Mod wiederfindet (# PMT-Generator <Kennung>/...). Leer = Paket-Kennung. Eine Kopie behält die Kennung des Originals und ersetzt so dessen Abschnitte, statt sie ein zweites Mal zu schreiben. Eine eigene Kennung nur eintragen, wenn die Kopie getrennt vom Original in dieselbe Mod schreiben soll.");

    public static string MarkerIdInvalid => T("packageEditor.basics.markerId.invalid", "Nur Kleinbuchstaben, Ziffern, _ und -, höchstens 40 Zeichen. Die Änderung wird erst mit einer gültigen Kennung übernommen.");

    public static string KeyFields => T("packageEditor.basics.keyFields", "Schlüsselfelder");

    public static string KeyFieldsTip => T("packageEditor.basics.keyFields.tip",
        "Kopf-Felder, deren Werte einen Arbeitsstand benennen, in der Reihenfolge des Kopfs. Beispiel: tag (je Land ein Arbeitsstand) oder Kategorie und Nummer (adm_18). Alle außer dem letzten brauchen ein Muster oder eine Auswahl.");

    public static string KeyFieldsNeedOne => T("packageEditor.basics.keyFields.needOne", "Mindestens ein Schlüsselfeld muss gewählt sein.");

    public static string KeySeparator => T("packageEditor.basics.keySeparator", "Trennzeichen");

    public static string KeySeparatorTip => T("packageEditor.basics.keySeparator.tip", "Verbindet die Werte mehrerer Schlüsselfelder, Vorgabe _ (adm + 18 = adm_18).");

    public static string Targets => T("packageEditor.basics.targets", "Ziele");

    public static string TargetMod => T("packageEditor.basics.targetMod", "In die Mod (mod)");

    public static string TargetModTip => T("packageEditor.basics.targetMod.tip", "Erlaubt das Schreiben direkt in den Mod-Ordner.");

    public static string TargetOutput => T("packageEditor.basics.targetOutput", "In einen Ausgabeordner (output)");

    public static string TargetOutputTip => T("packageEditor.basics.targetOutput.tip", "Erlaubt das Schreiben in einen gewählten Ordner; geänderte Mod-Dateien werden vorher dorthin kopiert.");

    public static string TargetsNeedOne => T("packageEditor.basics.targetsNeedOne", "Mindestens ein Ziel muss erlaubt sein.");

    public static string FieldId => T("packageEditor.field.id", "Kennung");

    public static string FieldIdTip => T("packageEditor.field.id.tip", "Name des Felds in den Vorlagen: Buchstaben, Ziffern und _, nicht mit einer Ziffer am Anfang. Beispiel: name.");

    public static string FieldLabel => T("packageEditor.field.label", "Beschriftung");

    public static string FieldLabelTip => T("packageEditor.field.label.tip", "Text vor dem Feld im Formular. Beispiel: Name der Idee.");

    public static string FieldType => T("packageEditor.field.type", "Art");

    public static string FieldTypeTip => T("packageEditor.field.type.tip", "Eingabeart: Text, mehrzeiliger Text, Zahl, Ja/Nein, Auswahl, Bild aus einer Asset-Bibliothek oder Modifikatoren.");

    public static string FieldRequired => T("packageEditor.field.required", "Pflichtfeld");

    public static string FieldRequiredTip => T("packageEditor.field.required.tip", "Ohne Wert verweigert der Generator das Erzeugen.");

    public static string FieldPattern => T("packageEditor.field.pattern", "Muster");

    public static string FieldPatternTip => T("packageEditor.field.pattern.tip", "Regulärer Ausdruck, den der Wert erfüllen muss. Beispiel: ^[A-Z0-9]{2,5}$ für einen Tag.");

    public static string FieldPatternInvalid(string message) => F("packageEditor.field.patternInvalid", "Ungültiger regulärer Ausdruck: {0}", message);

    public static string FieldDefault => T("packageEditor.field.default", "Vorgabe");

    public static string FieldDefaultTip => T("packageEditor.field.default.tip", "Wert in einem neuen Arbeitsstand. Beispiel: no bei einem Ja/Nein-Feld.");

    public static string FieldMultilingual => T("packageEditor.field.multilingual", "Mehrsprachig");

    public static string FieldMultilingualTip => T("packageEditor.field.multilingual.tip", "Das Feld bekommt im Formular einen Übersetzungen-Dialog; Lokalisierungs-Ausgaben schreiben je Sprache den passenden Text.");

    public static string FieldChoices => T("packageEditor.field.choices", "Auswahl");

    public static string FieldChoicesTip => T("packageEditor.field.choices.tip", "Werte der Auswahl, einer je Zeile. Beispiel: adm, dip, mil.");

    public static string FieldAsset => T("packageEditor.field.asset", "Asset");

    public static string FieldAssetTip => T("packageEditor.field.asset.tip", "Asset-Bibliothek, aus der das Bild gewählt wird (Abschnitt Assets). Beispiel: coa.");

    public static string IdInvalidName => T("packageEditor.idInvalidName", "Nur Buchstaben, Ziffern und _, nicht mit einer Ziffer am Anfang.");

    public static string IdDuplicate => T("packageEditor.idDuplicate", "Diese Kennung gibt es hier schon.");

    public static string NewFieldTip => T("packageEditor.field.new.tip", "Hängt ein neues Textfeld an die Liste an.");

    public static string RemoveFieldTip => T("packageEditor.field.remove.tip", "Entfernt das gewählte Feld. Vorlagen, die es nutzen, melden danach einen Fehler.");

    public static string MoveTip => T("packageEditor.move.tip", "Verschiebt den gewählten Eintrag; die Reihenfolge ist auch die im Formular bzw. beim Erzeugen.");

    public static string FieldsHeader => T("packageEditor.fields", "Felder");

    public static string TypeName(Generators.FieldType type) => type switch
    {
        Generators.FieldType.Text => T("packageEditor.type.text", "Text"),
        Generators.FieldType.MultilineText => T("packageEditor.type.multilineText", "Mehrzeiliger Text"),
        Generators.FieldType.Number => T("packageEditor.type.number", "Zahl"),
        Generators.FieldType.Bool => T("packageEditor.type.bool", "Ja/Nein"),
        Generators.FieldType.Choice => T("packageEditor.type.choice", "Auswahl"),
        Generators.FieldType.Image => T("packageEditor.type.image", "Bild"),
        _ => T("packageEditor.type.modifiers", "Modifikatoren"),
    };

    public static string AddRootLevel => T("packageEditor.level.addRoot", "Wurzel-Ebene anlegen");

    public static string AddChildLevel => T("packageEditor.level.addChild", "Kind-Ebene anlegen");

    public static string AddLevelTip => T("packageEditor.level.add.tip", "Legt unter der tiefsten Ebene eine neue Ebene an. Beispiel: unter \"ideas\" die Ebene \"levels\".");

    public static string DeleteLevel => T("packageEditor.level.delete", "Ebene löschen");

    public static string DeleteLevelTip => T("packageEditor.level.delete.tip", "Löscht diese Ebene samt allen tieferen Ebenen und ihren Feldern.");

    public static string DeleteLevelAsk(string id) => F("packageEditor.level.deleteAsk", "Ebene \"{0}\" samt allen tieferen Ebenen löschen?", id);

    public static string LevelsNote => T("packageEditor.level.note", "Das Formular ist eine Kette von Ebenen: jede Ebene hat Felder und höchstens eine Kind-Ebene. Beispiel IV2: Kategorien → Ideen → Stufen.");

    public static string LevelIdTip => T("packageEditor.level.id.tip", "Name der Ebene in den Vorlagen (eine Liste). Beispiel: ideas, in Vorlagen: for i in c.ideas.");

    public static string LevelLabelTip => T("packageEditor.level.label.tip", "Überschrift der Ebene im Formular. Beispiel: Ideen.");

    public static string LevelKind => T("packageEditor.level.kind", "Art");

    public static string LevelFixed => T("packageEditor.level.fixed", "fest (Konstanten-Tabelle)");

    public static string LevelFixedTip => T("packageEditor.level.fixed.tip", "Ein Element je Zeile einer Konstanten-Tabelle; die Spalten lassen sich in Vorlagen wie Felder lesen.");

    public static string LevelCountable => T("packageEditor.level.countable", "zählbar");

    public static string LevelCountableTip => T("packageEditor.level.countable.tip", "Der Nutzer legt zwischen Min und Max Elemente an.");

    public static string LevelTable => T("packageEditor.level.table", "Tabelle");

    public static string LevelTableTip => T("packageEditor.level.table.tip", "Konstanten-Tabelle, deren Zeilen die festen Elemente sind. Beispiel: categories.");

    public static string LevelMin => T("packageEditor.level.min", "Min");

    public static string LevelMinTip => T("packageEditor.level.min.tip", "Mindestzahl der Elemente. Beispiel: 0.");

    public static string LevelMax => T("packageEditor.level.max", "Max");

    public static string LevelMaxTip => T("packageEditor.level.max.tip", "Höchstzahl der Elemente; leer = unbegrenzt. Beispiel: 9.");

    public static string LevelInitial => T("packageEditor.level.initial", "Anfangszahl");

    public static string LevelInitialTip => T("packageEditor.level.initial.tip", "Elemente in einem neuen Arbeitsstand; leer = Min. Beispiel: 3.");

    public static string LevelNumberBase => T("packageEditor.level.numberBase", "Nummer-Basisfeld");

    public static string LevelNumberBaseTip => T("packageEditor.level.numberBase.tip", "Feld oder Tabellenspalte der Elternebene; die Nummer eines Elements ist dieser Wert plus sein Index. Beispiel: base (adm 0, dip 10).");

    public static string LevelCountsInvalid => T("packageEditor.level.countsInvalid", "Min darf nicht größer als Max sein, die Anfangszahl muss dazwischen liegen.");

    public static string NumberInvalid => T("packageEditor.numberInvalid", "Bitte eine ganze Zahl eingeben.");

    public static string Tables => T("packageEditor.constants.tables", "Tabellen");

    public static string NewTableTip => T("packageEditor.constants.newTable.tip", "Legt eine leere Konstanten-Tabelle an. Beispiel: categories.");

    public static string RenameTableTip => T("packageEditor.constants.renameTable.tip", "Benennt die Tabelle um; Ebenen, die sie nutzen, werden angepasst.");

    public static string DeleteTableTip => T("packageEditor.constants.deleteTable.tip", "Löscht die Tabelle samt ihren Zeilen.");

    public static string TableName => T("packageEditor.constants.tableName", "Name der Tabelle:");

    public static string ColumnName => T("packageEditor.constants.columnName", "Name der Spalte:");

    public static string DeleteTableAsk(string table, string levels) =>
        F("packageEditor.constants.deleteTableAsk", "Tabelle \"{0}\" löschen? Sie wird noch von diesen Ebenen genutzt: {1}", table, levels);

    public static string AddColumn => T("packageEditor.constants.addColumn", "Spalte hinzufügen");

    public static string AddColumnTip => T("packageEditor.constants.addColumn.tip", "Neue Spalte für alle Zeilen. Beispiel: base.");

    public static string RenameColumn => T("packageEditor.constants.renameColumn", "Spalte umbenennen");

    public static string RemoveColumn => T("packageEditor.constants.removeColumn", "Spalte entfernen");

    public static string ColumnTip => T("packageEditor.constants.column.tip", "Wirkt auf die Spalte der gewählten Zelle.");

    public static string AddRow => T("packageEditor.constants.addRow", "Zeile hinzufügen");

    public static string RemoveRow => T("packageEditor.constants.removeRow", "Zeile entfernen");

    public static string RowTip => T("packageEditor.constants.row.tip", "Wirkt auf die Zeile der gewählten Zelle.");

    public static string CellLanguages => T("packageEditor.constants.cellLanguages", "Sprachen der Zelle");

    public static string CellLanguagesTip => T("packageEditor.constants.cellLanguages.tip", "Text der gewählten Zelle je Sprache, z. B. für die Spalte label.");

    public static string NoTable => T("packageEditor.constants.noTable", "Noch keine Tabelle. Mit \"Neu\" eine anlegen.");

    public static string ExtendEnglish => T("packageEditor.languages.extendEnglish", "Englisch erweitern");

    public static string ExtendEnglishTip => T("packageEditor.languages.extendEnglish.tip", "Schreibt die Lokalisierung für alle Sprachen des Spielprofils; wo keine Übersetzung da ist, mit dem englischen Text.");

    public static string CopyEnglish => T("packageEditor.languages.copyEnglish", "Englisch kopieren");

    public static string CopyEnglishTip => T("packageEditor.languages.copyEnglish.tip", "Leere Sprachen bekommen beim Erzeugen den englischen Text.");

    public static string EnglishForMissing => T("packageEditor.languages.englishForMissing", "Englisch für fehlende");

    public static string EnglishForMissingTip => T("packageEditor.languages.englishForMissing.tip", "Hat ein Eintrag Übersetzungen, bekommen seine fehlenden Sprachen den englischen Text.");

    public static string LanguagesNote => T("packageEditor.languages.note", "Vorgaben für mehrsprachige Felder; jeder Arbeitsstand kann sie ändern.");

    public static string AssetId => T("packageEditor.asset.id", "Kennung");

    public static string AssetIdTip => T("packageEditor.asset.id.tip", "Name, mit dem Bildfelder und copyBinary-Ausgaben das Asset nennen. Beispiel: coa.");

    public static string AssetPath => T("packageEditor.asset.path", "Pfad");

    public static string AssetPathTip => T("packageEditor.asset.path.tip", "Zip-Datei oder Ordner relativ zum Datenordner des Pakets. Beispiel: assets/coa.zip.");

    public static string AssetPathInvalid => T("packageEditor.asset.pathInvalid", "Der Pfad muss relativ sein und darf den Paketordner nicht verlassen.");

    public static string AssetOrigin => T("packageEditor.asset.origin", "Herkunft");

    public static string AssetOriginTip => T("packageEditor.asset.origin.tip", "Spiel: aus den Spieldateien, wird nie exportiert. Mod/eigene: aus einer Mod oder selbst erstellt, darf mit exportiert werden.");

    public static string OriginGame => T("packageEditor.asset.originGame", "Spiel");

    public static string OriginMod => T("packageEditor.asset.originMod", "Mod/eigene");

    public static string AssetPresent => T("packageEditor.asset.present", "vorhanden");

    public static string AssetMissing => T("packageEditor.asset.missing", "fehlt");

    public static string ChooseFile => T("packageEditor.asset.chooseFile", "Datei wählen");

    public static string ChooseFileTip => T("packageEditor.asset.chooseFile.tip", "Kopiert eine Datei in den Ordner assets des Pakets und setzt den Pfad.");

    public static string AddAssetTip => T("packageEditor.asset.add.tip", "Neue Zeile für eine Asset-Bibliothek.");

    public static string RemoveAssetTip => T("packageEditor.asset.remove.tip", "Entfernt die Angabe; die Datei bleibt liegen.");

    public static string Warnings => T("packageEditor.messages.warnings", "Warnungen (fragen vor dem Erzeugen)");

    public static string Errors => T("packageEditor.messages.errors", "Fehler (verhindern das Erzeugen)");

    public static string Condition => T("packageEditor.messages.condition", "Bedingung");

    public static string ConditionTip => T("packageEditor.messages.condition.tip", "Scriban-Ausdruck; ist er wahr, erscheint die Meldung. Beispiel: total_ideas < 10.");

    public static string MessageTip => T("packageEditor.messages.message.tip", "Text der Meldung. Beispiel: Weniger als 10 Ideen.");

    public static string AddMessageTip => T("packageEditor.messages.add.tip", "Neue Bedingung mit Meldung.");

    // Externe Dateien, Hinweis nach dem Erzeugen und autoNumber (dynamische Ideen)
    public static string SectionExternals => T("packageEditor.section.externals", "Externe Dateien");

    public static string SectionAfterGenerate => T("packageEditor.section.afterGenerate", "Hinweis nach dem Erzeugen");

    public static string ExternalsNote => T("packageEditor.externals.note",
        "Dateien außerhalb der Mod, in die Ausgaben der Art \"JSON-Eintrag\" (jsonEntry) einen Eintrag schreiben. Den Pfad wählt man im Generator-Fenster; er steht nicht im Paket.");

    public static string ExternalId => T("packageEditor.externals.id", "Kennung");

    public static string ExternalIdTip => T("packageEditor.externals.id.tip", "Name der Datei im Paket; die Ausgabe nennt ihn unter \"Externe Datei\". Beispiel: tags.");

    public static string ExternalLabel => T("packageEditor.externals.label", "Beschriftung");

    public static string ExternalLabelTip => T("packageEditor.externals.label.tip", "So heißt die Datei im Generator-Fenster, beim Knopf zur Dateiwahl. Beispiel: Tag-Liste.");

    public static string ExternalHint => T("packageEditor.externals.hint", "Hinweis");

    public static string ExternalHintTip => T("packageEditor.externals.hint.tip", "Erscheint als Tooltip an der Dateiwahl im Generator-Fenster. Beispiel: Die Datei tags.json im Ordner des Hilfsskripts.");

    public static string AddExternalTip => T("packageEditor.externals.add.tip", "Neue externe Datei.");

    public static string RemoveExternalTip => T("packageEditor.externals.remove.tip", "Entfernt die Angabe; Ausgaben, die sie nennen, melden das beim Prüfen.");

    public static string AfterGenerateNote => T("packageEditor.afterGenerate.note",
        "Dieser Text erscheint im Generator-Fenster unter der Meldung, wenn das Erzeugen gelungen ist, z. B. als Erinnerung an einen Schritt, der danach außerhalb von PMT folgt.");

    public static string AfterGenerateLabel => T("packageEditor.afterGenerate.label", "Hinweis");

    public static string AfterGenerateTip => T("packageEditor.afterGenerate.tip", "Text je Sprache, ohne Scriban. Leer = kein Hinweis. Beispiel: Danach das Hilfsskript build_chooser.py starten.");

    // Modifier ohne Idee (candidates)
    public static string SectionCandidates => T("packageEditor.section.candidates", "Modifier ohne Idee");

    public static string CandidatesNote => T("packageEditor.candidates.note",
        "Für den Knopf \"Modifier ohne Idee…\" im Generator-Fenster: woran man erkennt, dass ein Modifier schon benutzt wird, wie die Liste in Gruppen geteilt wird und wie aus einem Modifier ein Arbeitsstand entsteht. Die Regeln dieses Abschnitts prüft \"Prüfen\".");

    public static string CandidatesAdd => T("packageEditor.candidates.add", "Abschnitt anlegen");

    public static string CandidatesAddTip => T("packageEditor.candidates.add.tip", "Legt den Abschnitt leer an; \"Prüfen\" meldet, was noch fehlt.");

    public static string CandidatesCategory => T("packageEditor.candidates.category", "Kategorie");

    public static string CandidatesCategoryTip => T("packageEditor.candidates.category.tip",
        "Woher die Modifier kommen: die Kategorie in den Modifier-Definitionen des Spiels und der Geltungsbereich in der Script-Datenbank. Leer und \"all\" zählen nicht. Beispiel: country.");

    public static string CandidatesUsedHeading => T("packageEditor.candidates.used.heading", "Wo ein Modifier benutzt wird");

    public static string CandidatesUsedNote => T("packageEditor.candidates.used.note",
        "Ein Modifier gilt als benutzt, wenn er direkt in einem passenden Block einer passenden Datei der Mod steht. Mindestens ein Eintrag ist nötig.");

    public static string CandidatesUsedFile => T("packageEditor.candidates.used.file", "Datei");

    public static string CandidatesUsedFileTip => T("packageEditor.candidates.used.file.tip", "Regulärer Ausdruck auf den relativen Pfad der Datei in der Mod. Beispiel: ^common/static_modifiers/.*[.]txt$");

    public static string CandidatesUsedBlock => T("packageEditor.candidates.used.block", "Block");

    public static string CandidatesUsedBlockTip => T("packageEditor.candidates.used.block.tip", "Regulärer Ausdruck auf den ganzen Schlüssel des obersten Blocks. Beispiel: ^iv2_.*$");

    public static string CandidatesUsedExcept => T("packageEditor.candidates.used.except", "Ausgenommen");

    public static string CandidatesUsedExceptTip => T("packageEditor.candidates.used.except.tip", "Unterblöcke, die nicht zählen, durch Komma getrennt. Beispiel: game_data");

    public static string CandidatesAddUsedTip => T("packageEditor.candidates.used.add.tip", "Neuer Eintrag: Datei und Block, in denen ein Modifier als benutzt gilt.");

    public static string CandidatesRemoveUsedTip => T("packageEditor.candidates.used.remove.tip", "Entfernt diesen Eintrag.");

    public static string CandidatesGroupsHeading => T("packageEditor.candidates.groups.heading", "Gruppen");

    public static string CandidatesGroupsNote => T("packageEditor.candidates.groups.note",
        "Das Fenster teilt die Modifier nach ihrem Namen in Gruppen. Die Reihenfolge zählt: die erste Gruppe, deren Muster trifft, gewinnt; was in keine passt, steht unter \"Allgemein\". Muster mit Namensgrenzen schreiben, z. B. (^|_)(wort|...)(_|$), nicht als bloße Teilzeichenkette.");

    public static string CandidatesGroupId => T("packageEditor.candidates.group.id", "Kennung");

    public static string CandidatesGroupIdTip => T("packageEditor.candidates.group.id.tip", "Eindeutiger Name der Gruppe; das Fenster merkt sich darunter, ob sie ausgeblendet ist. Beispiel: estates.");

    public static string CandidatesGroupLabel => T("packageEditor.candidates.group.label", "Beschriftung");

    public static string CandidatesGroupLabelTip => T("packageEditor.candidates.group.label.tip", "So heißt die Gruppe in der Liste des Fensters, je Sprache. Leer = die Kennung. Beispiel: Stände.");

    public static string CandidatesGroupPattern => T("packageEditor.candidates.group.pattern", "Muster");

    public static string CandidatesGroupPatternTip => T("packageEditor.candidates.group.pattern.tip", "Regulärer Ausdruck auf den Namen des Modifiers. Beispiel: (^|_)(nobles|clergy|burghers)(_|$)");

    public static string CandidatesGroupHidden => T("packageEditor.candidates.group.hidden", "Standardmäßig ausgeblendet");

    public static string CandidatesGroupHiddenTip => T("packageEditor.candidates.group.hidden.tip", "Das Fenster zeigt die Gruppe zunächst grau; der Nutzer kann sie einblenden.");

    public static string CandidatesAddGroupTip => T("packageEditor.candidates.group.add.tip", "Neue Gruppe am Ende der Liste; mit Hoch und Runter an die richtige Stelle schieben.");

    public static string CandidatesRemoveGroupTip => T("packageEditor.candidates.group.remove.tip", "Entfernt diese Gruppe; ihre Modifier fallen in die nächste passende oder nach \"Allgemein\".");

    public static string CandidatesCreateHeading => T("packageEditor.candidates.create.heading", "Arbeitsstände anlegen");

    public static string CandidatesAsk => T("packageEditor.candidates.ask", "Einmal fragen");

    public static string CandidatesAskTip => T("packageEditor.candidates.ask.tip",
        "Kopf-Felder vom Typ Auswahl, die der Nutzer einmal für alle neuen Arbeitsstände wählt, durch Komma getrennt. Alle Schlüsselfelder außer dem mit automatischer Nummer müssen darin stehen. Beispiel: category, subTab");

    public static string CandidatesName => T("packageEditor.candidates.name", "Name-Feld");

    public static string CandidatesNameTip => T("packageEditor.candidates.name.tip", "Kopf-Feld vom Typ Text, das den offiziellen Namen des Modifiers bekommt. Beispiel: name");

    public static string CandidatesModifiers => T("packageEditor.candidates.modifiers", "Modifier-Feld");

    public static string CandidatesModifiersTip => T("packageEditor.candidates.modifiers.tip", "Feld vom Typ Modifier einer Ebene, als Ebene.Feld; der Modifier kommt auf alle Stufen. Beispiel: levels.modifiers");

    public static string CandidatesRemove => T("packageEditor.candidates.remove", "Abschnitt entfernen");

    public static string CandidatesRemoveTip => T("packageEditor.candidates.remove.tip", "Entfernt den ganzen Abschnitt candidates; das Paket hat dann keinen Knopf \"Modifier ohne Idee…\" mehr.");

    public static string FieldAutoNumber => T("packageEditor.field.autoNumber", "Nummer automatisch");

    public static string FieldAutoNumberTip => T("packageEditor.field.autoNumber.tip",
        "Ein neuer Arbeitsstand bekommt die nächste freie Nummer in diesem Feld; Lokalisierungsschlüssel mit gleichem Hash rücken weiter. Nur am letzten Schlüsselfeld vom Typ Zahl im Kopf.");

    public static string FieldAutoNumberNotLast => T("packageEditor.field.autoNumberNotLast", "autoNumber ist nur am letzten Schlüsselfeld erlaubt; dieses Feld ist keins (Grunddaten, Schlüsselfelder).");

    // Ausgaben und Vorlagen (Aufgabe 11)
    public static string KindName(Generators.OutputKind kind) => kind switch
    {
        Generators.OutputKind.OwnFile => T("packageEditor.kind.ownFile", "Eigene Datei (ownFile)"),
        Generators.OutputKind.Localization => T("packageEditor.kind.localization", "Lokalisierung (localization)"),
        Generators.OutputKind.InsertSection => T("packageEditor.kind.insertSection", "Abschnitt einfügen (insertSection)"),
        Generators.OutputKind.EnsureLine => T("packageEditor.kind.ensureLine", "Zeile sicherstellen (ensureLine)"),
        Generators.OutputKind.SyncBlocks => T("packageEditor.kind.syncBlocks", "Blöcke abgleichen (syncBlocks)"),
        Generators.OutputKind.SyncMatching => T("packageEditor.kind.syncMatching", "Abgleich nach Treffer (syncMatching)"),
        Generators.OutputKind.Check => T("packageEditor.kind.check", "Prüfung (check)"),
        Generators.OutputKind.JsonEntry => T("packageEditor.kind.jsonEntry", "JSON-Eintrag in externe Datei (jsonEntry)"),
        _ => T("packageEditor.kind.copyBinary", "Datei kopieren (copyBinary)"),
    };

    public static string NewOutputTip => T("packageEditor.output.new.tip", "Hängt eine neue Ausgabe der Art \"Eigene Datei\" an.");

    public static string Duplicate => T("packageEditor.duplicate", "Duplizieren");

    public static string DuplicateTip => T("packageEditor.output.duplicate.tip", "Kopiert die gewählte Ausgabe direkt dahinter (Kennung mit _copy).");

    public static string RemoveOutputTip => T("packageEditor.output.remove.tip", "Entfernt die gewählte Ausgabe; ihre Vorlage bleibt.");

    public static string OutputIdTip => T("packageEditor.output.id.tip", "Name der Ausgabe; erscheint in den Markierungen in der Mod. Beispiel: staticModifiers.");

    public static string OutputKind => T("packageEditor.output.kind", "Art");

    public static string OutputKindTip => T("packageEditor.output.kind.tip", "Was die Ausgabe tut: eigene Datei schreiben, Lokalisierung je Sprache, Abschnitt in eine Datei einfügen, Zeile sicherstellen, Blöcke abgleichen oder eine Datei aus einem Asset kopieren.");

    public static string KindChangeDropped(string keys) => F("packageEditor.output.kindDropped", "Diese Angaben kennt die neue Art nicht, sie wurden entfernt: {0}", keys);

    public static string OutputPath => T("packageEditor.output.path", "Pfad");

    public static string OutputPathTip => T("packageEditor.output.path.tip", "Zieldatei relativ zur Mod, eine Scriban-Vorlage. Beispiel: in_game/common/x_{{ tag }}.txt.");

    public static string LangMissing => T("packageEditor.output.langMissing", "Der Pfad einer Lokalisierung braucht {{ lang }}, sonst schreiben alle Sprachen in dieselbe Datei.");

    public static string OutputTemplate => T("packageEditor.output.template", "Vorlage");

    public static string OutputTemplateTip => T("packageEditor.output.template.tip", "Scriban-Vorlage aus dem Ordner templates, die den Text der Ausgabe erzeugt.");

    public static string Open => T("packageEditor.open", "Öffnen");

    public static string OpenTemplateTip => T("packageEditor.output.openTemplate.tip", "Springt zu dieser Vorlage im Abschnitt Vorlagen.");

    public static string OutputSkeleton => T("packageEditor.output.skeleton", "Grundgerüst");

    public static string OutputSkeletonTip => T("packageEditor.output.skeleton.tip", "Vorlage für die Datei, wenn sie noch nicht existiert. Leer = Fehler bei fehlender Datei.");

    public static string OutputBlock => T("packageEditor.output.block", "Block");

    public static string OutputBlockTip => T("packageEditor.output.block.tip", "Pfad des Blocks in der Datei, Teile mit Punkt getrennt; auch GUI-Köpfe wie type name. Leer = Dateiende. Beispiel: types iv2_types.type iv2_shell.");

    public static string OutputForEach => T("packageEditor.output.forEach", "Je Element");

    public static string OutputForEachTip => T("packageEditor.output.forEach.tip", "Ebene, für deren Elemente die Ausgabe je einmal entsteht; in der Vorlage heißt das Element item. Leer = einmal.");

    public static string OutputLegacyBegin => T("packageEditor.output.legacyBegin", "Alt-Anfang");

    public static string OutputLegacyBeginTip => T("packageEditor.output.legacyBegin.tip", "Anfangsmarkierung eines älteren Werkzeugs, die PMT erkennt und behält. Beispiel: # === IV2Generator:{{ tag }}:START ===.");

    public static string OutputLegacyEnd => T("packageEditor.output.legacyEnd", "Alt-Ende");

    public static string OutputLegacyEndTip => T("packageEditor.output.legacyEnd.tip", "Endmarkierung des älteren Werkzeugs. Beispiel: # === IV2Generator:{{ tag }}:END ===.");

    public static string OutputLegacyPatterns => T("packageEditor.output.legacyPatterns", "Alt-Muster");

    public static string OutputLegacyPatternsTip => T("packageEditor.output.legacyPatterns.tip", "Reguläre Ausdrücke für unmarkierte Alt-Einträge, einer je Zeile; PMT entfernt sie vor dem Einfügen. Beispiel: ^\\s*iv2_idea_{{ tag }}_\\d+.");

    public static string OutputLine => T("packageEditor.output.line", "Zeile");

    public static string OutputLineTip => T("packageEditor.output.line.tip", "Zeile, die im Block stehen muss; fehlt sie, fügt PMT sie ein. Beispiel: has_or_had_tag = {{ tag }}.");

    public static string OutputMatch => T("packageEditor.output.match", "Treffer-Muster");

    public static string OutputMatchTip => T("packageEditor.output.match.tip",
        "Regulärer Ausdruck (mit Scriban): Jeder Eintrag des Blocks, in dessen Text er trifft, gehört zu diesem Satz und wird an Ort und Stelle ersetzt. Grenzen selbst setzen, z. B. iv2_ig_{{ category }}_{{ group }}(?![0-9]).");

    public static string OutputKeys => T("packageEditor.output.keys", "Schlüssel-Muster (gemeinsame Datei)");

    public static string OutputKeysTip => T("packageEditor.output.keys.tip",
        "Leer: Die Lokalisierung schreibt die ganze Datei. Gefüllt (regulärer Ausdruck mit Scriban auf den Schlüssel): Die Datei ist gemeinsam, ersetzt werden nur die passenden Zeilen, z. B. ^iv2_x_{{ group }}(_|$).");

    public static string OutputInsertAfter => T("packageEditor.output.insertAfter", "Einfügen nach");

    public static string OutputInsertAfterTip => T("packageEditor.output.insertAfter.tip",
        "Regulärer Ausdruck (mit Scriban): Neue Einträge kommen direkt hinter den letzten Eintrag, auf dessen Text (bei Lokalisierung auf dessen Schlüssel) er trifft; vorhandene behalten ihren Platz. Ohne Treffer gilt die Standardstelle. Bei Lokalisierung nur mit Schlüssel-Muster. Beispiel: ^iv2_ga_ideagroup_{{ category }}_1.");

    public static string OutputIfLimit => T("packageEditor.output.ifLimit", "Bedingung (if/limit)");

    public static string OutputIfLimitTip => T("packageEditor.output.ifLimit.tip",
        "Scriban-Vorlage mit dem Text einer Bedingung. Leer gerendert: die Einträge stehen auf oberster Ebene. Sonst stehen sie in einem if = { limit = { ... } } mit dieser Bedingung, die Vorlage rückt dann eine Ebene tiefer ein. Eine Zeile je Bedingung. Beispiel: has_variable = iv2_flag_{{ category }}.");

    public static string OutputExternal => T("packageEditor.output.external", "Externe Datei");

    public static string OutputExternalTip => T("packageEditor.output.external.tip", "Kennung einer Datei aus \"Externe Dateien\", in die der Eintrag geschrieben wird. Den Pfad wählt man im Generator-Fenster.");

    public static string OutputJsonPath => T("packageEditor.output.jsonPath", "JSON-Pfad");

    public static string OutputJsonPathTip => T("packageEditor.output.jsonPath.tip", "Punkt-Pfad der Objekte in der Datei, in dem der Eintrag steht (Scriban). Leer = oberstes Objekt. Beispiel: entries.");

    public static string OutputEntryKey => T("packageEditor.output.entryKey", "Eintragsschlüssel");

    public static string OutputEntryKeyTip => T("packageEditor.output.entryKey.tip",
        "Schlüssel des Eintrags im Objekt (Scriban). Ein vorhandener Schlüssel wird an Ort und Stelle ersetzt, ein neuer ans Ende gehängt. Beispiel: {{ tag }}.");

    public static string OutputJsonTemplateTip => T("packageEditor.output.jsonTemplate.tip", "Scriban-Vorlage aus dem Ordner templates, die den Wert des Eintrags als JSON erzeugt.");

    public static string JsonEntryNote => T("packageEditor.output.jsonEntryNote", "Diese Art schreibt in eine externe Datei außerhalb der Mod und braucht keinen Pfad.");

    public static string OutputMessage => T("packageEditor.output.message", "Meldung");

    public static string OutputMessageTip => T("packageEditor.output.message.tip",
        "Warnung in der Vorschau, wenn ein Muster der Prüfung nicht trifft. Scriban; {{ arg }} ist das Argument hinter => in der Zeile der Vorlage.");

    public static string OutputKeyPattern => T("packageEditor.output.keyPattern", "Schlüsselmuster");

    public static string OutputKeyPatternTip => T("packageEditor.output.keyPattern.tip", "Muster der Blockschlüssel, die abgeglichen werden; * steht für Namenszeichen. Beispiel: national_idea_modifier_{{ tag }}_*.");

    public static string OutputKeep => T("packageEditor.output.keep", "Behalten");

    public static string OutputKeepTip => T("packageEditor.output.keep.tip", "Teilschlüssel, die beim Abgleich stehen bleiben, einer je Zeile. Beispiel: game_data.");

    public static string OutputCheckUnreferenced => T("packageEditor.output.checkUnreferenced", "Ungenutzte prüfen");

    public static string OutputCheckUnreferencedTip => T("packageEditor.output.checkUnreferenced.tip", "\"Mod prüfen\" listet Blöcke dieser Ausgabe, die keine andere Datei nennt.");

    public static string OutputSource => T("packageEditor.output.source", "Quelle");

    public static string OutputSourceTip => T("packageEditor.output.source.tip", "Asset und Eintrag darin, eine Scriban-Vorlage. Beispiel: coa und {{ coa }}.");

    public static string OutputSourceEntryTip => T("packageEditor.output.sourceEntry.tip", "Eintrag im Asset (Datei im Zip oder Ordner). Leer = die Ausgabe wird übersprungen.");

    public static string OutputNewLine => T("packageEditor.output.newLine", "Zeilenende");

    public static string OutputNewLineTip => T("packageEditor.output.newLine.tip", "Automatisch: CRLF für .txt und .gui, LF für .yml. Bestehende Dateien behalten ihr Zeilenende.");

    public static string NewLineAuto => T("packageEditor.output.newLineAuto", "automatisch");

    public static string NoOutputs => T("packageEditor.output.none", "Noch keine Ausgabe. Mit \"Neu\" eine anlegen.");

    public static string Templates => T("packageEditor.templates", "Dateien");

    public static string NewTemplateTip => T("packageEditor.template.new.tip", "Legt eine neue, leere Vorlage an. Beispiel: modifiers.sbn.");

    public static string RenameTemplateTip => T("packageEditor.template.rename.tip", "Benennt die Vorlage um; speichert das Paket.");

    public static string DeleteTemplateTip => T("packageEditor.template.delete.tip", "Löscht die Vorlage; speichert das Paket.");

    public static string TemplateName => T("packageEditor.template.name", "Name der Vorlage (endet auf .sbn):");

    public static string TemplateNameInvalid => T("packageEditor.template.nameInvalid", "Der Name braucht die Endung .sbn und darf nur Buchstaben, Ziffern, _, - und . enthalten.");

    public static string TemplateExists => T("packageEditor.template.exists", "Eine Vorlage mit diesem Namen gibt es schon.");

    public static string RenameReferencesAsk(string outputs) => F("packageEditor.template.renameRefsAsk", "Diese Ausgaben nutzen die Vorlage: {0}. Die Verweise mitändern?", outputs);

    public static string DeleteTemplateAsk(string name, string outputs) =>
        F("packageEditor.template.deleteAsk", "Vorlage \"{0}\" löschen? Sie wird noch genutzt von: {1}", name, outputs);

    public static string DeleteTemplateAskPlain(string name) => F("packageEditor.template.deleteAskPlain", "Vorlage \"{0}\" löschen?", name);

    public static string SaveFirst => T("packageEditor.template.saveFirst", "Dafür speichert PMT zuerst das Paket. Fortfahren?");

    public static string Names => T("packageEditor.names", "Verfügbare Namen");

    public static string NamesTip => T("packageEditor.names.tip", "Namen, die Vorlagen kennen. Doppelklick fügt den Namen an der Schreibmarke ein.");

    public static string NamesHead => T("packageEditor.names.head", "Kopf-Felder");

    public static string NamesLevels => T("packageEditor.names.levels", "Ebenen und Felder");

    public static string NamesElement => T("packageEditor.names.element", "Je Element");

    public static string NamesGlobal => T("packageEditor.names.global", "Allgemein");

    public static string NamesFunctions => T("packageEditor.names.functions", "Funktionen");

    public static string TemplateEditorTip => T("packageEditor.template.editor.tip", "Scriban-Vorlage. Änderungen schreibt erst \"Speichern\".");

    // Prüfen, Testen, Export und Import (Aufgabe 12)
    public static string CheckLine(string where, int? line, string message) =>
        line is { } l ? F("packageEditor.checkLineAt", "{0}, Zeile {1}: {2}", where, l, message) : F("packageEditor.checkLine", "{0}: {1}", where, message);

    public static string CheckResultsTip => T("packageEditor.checkResults.tip", "Ergebnis von \"Prüfen\". Ein Klick auf einen Vorlagenfehler springt zur Zeile.");

    public static string ExportTitle => T("packageEditor.exportTitle", "Paket exportieren");

    public static string ExportStates => T("packageEditor.exportStates", "Arbeitsstände mitnehmen");

    public static string ExportStatesTip => T("packageEditor.exportStates.tip", "Legt die gespeicherten Formulareingaben (je Schlüssel) mit in die Datei.");

    public static string ExportAssets => T("packageEditor.exportAssets", "Assets mitnehmen");

    public static string ExportAssetTip => T("packageEditor.exportAsset.tip", "Nur Assets aus Mods oder eigene dürfen weitergegeben werden.");

    public static string ExportAssetGame => T("packageEditor.exportAssetGame", "aus dem Spiel, wird nicht exportiert");

    public static string ExportAssetMissing => T("packageEditor.exportAssetMissing", "fehlt");

    public static string ExportFilter => T("packageEditor.exportFilter", "Generator-Paket (*.pmtgen)|*.pmtgen");

    public static string Exported(string file) => F("packageEditor.exported", "Exportiert nach {0}.", file);

    public static string ExportFailed(string message) => F("packageEditor.exportFailed", "Export fehlgeschlagen: {0}", message);

    public static string ImportSummary(string name, string game, int templates, int states, string assets, string skipped) =>
        F("packageEditor.importSummary", "Paket \"{0}\" für {1}\nVorlagen: {2}, Arbeitsstände: {3}\nAssets: {4}\nÜbersprungen: {5}\n\nImportieren?", name, game, templates, states, assets, skipped);

    public static string ImportNothing => T("packageEditor.importNothing", "keine");

    public static string ImportUnknownGame(string game) => F("packageEditor.importUnknownGame", "Das Paket gehört zum Spiel \"{0}\", das PMT nicht kennt. Import abgebrochen.", game);

    public static string ImportShippedId(string id) => F("packageEditor.importShippedId", "Die Kennung \"{0}\" gehört einem mitgelieferten Paket. Neue Kennung:", id);

    public static string ImportNewId => T("packageEditor.importNewId", "Neue Kennung:");

    public static string ImportOwnExists(string id) =>
        F("packageEditor.importOwnExists", "Ein eigenes Paket \"{0}\" gibt es schon.\n\nJa: ersetzen (Arbeitsstände mit gleichem Namen nach Rückfrage)\nNein: unter neuer Kennung importieren\nAbbrechen: nichts tun", id);

    public static string ImportOverwriteStates(string names) => F("packageEditor.importOverwriteStates", "Diese Arbeitsstände gibt es schon und würden überschrieben: {0}. Überschreiben?", names);

    public static string ImportLoadFailed(string message) =>
        F("packageEditor.importLoadFailed", "Das Paket lässt sich nicht laden: {0}\n\nTrotzdem importieren, um es im Editor zu reparieren?", message);

    public static string ImportFailed(string message) => F("packageEditor.importFailed", "Import fehlgeschlagen: {0}", message);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object[] args) => UiText.Format(key, german, args);
}
