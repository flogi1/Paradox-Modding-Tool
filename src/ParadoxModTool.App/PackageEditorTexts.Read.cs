namespace ParadoxModTool.App;

/// <summary>Texte der Maske "Einlesen", des Vorschlag-Dialogs und des Probelaufs (Teil 5b, Keys <c>packageEditor.read.*</c>).</summary>
internal static partial class PackageEditorTexts
{
    public static string ReadEnable => T("packageEditor.read.enable", "Paket kann Sätze aus einer Mod einlesen");

    public static string ReadEnableTip => T("packageEditor.read.enable.tip",
        "Schaltet \"Aus Mod einlesen\", \"Tags aus Mod übernehmen\" und \"Mod prüfen\" im Generator-Fenster ein. Die Muster unten beschreiben, wo PMT die Werte eines Satzes in der Mod findet.");

    public static string ReadDisableConfirm => T("packageEditor.read.disableConfirm", "Den Abschnitt \"Einlesen\" mit allen Mustern entfernen?");

    public static string ReadOffNote => T("packageEditor.read.offNote",
        "Ohne diesen Abschnitt kann der Generator Sätze nur erzeugen, nicht aus einer Mod zurücklesen.");

    public static string ReadKeysHeading => T("packageEditor.read.keysHeading", "Schlüssel-Dateien");

    public static string ReadKeys => T("packageEditor.read.keys", "Muster");

    public static string ReadKeysTip => T("packageEditor.read.keys.tip",
        "Regulärer Ausdruck auf die Pfade der Mod (mit /). Jede Datei, die passt, nennt mit der Gruppe (?<key>...) einen Satz. Beispiel: ^main_menu/common/static_modifiers/02_IV2_(?<key>[A-Z0-9]{2,5})\\.txt$");

    public static string ReadKeysFrom => T("packageEditor.read.keysFrom", "Schlüssel aus");

    public static string ReadKeysFromTip => T("packageEditor.read.keysFrom.tip",
        "Dateipfad: je Satz eine eigene Datei, der Pfad nennt den Satz. Blöcken in Dateien: die Sätze stehen in gemeinsamen Dateien, die Blockschlüssel nennen sie.");

    public static string ReadKeysFromPath => T("packageEditor.read.keysFrom.path", "Dateipfad");

    public static string ReadKeysFromBlocks => T("packageEditor.read.keysFrom.blocks", "Blöcken in Dateien");

    public static string ReadKeysFile => T("packageEditor.read.keysFile", "Dateien");

    public static string ReadKeysFileTip => T("packageEditor.read.keysFile.tip",
        "Regulärer Ausdruck auf die Pfade der Mod (mit /): in diesen Dateien werden die Blockschlüssel gelesen. Beispiel: ^main_menu/common/static_modifiers/01_IV2_ideagroup_modifiers_(adm|dip|army|navy)\\.txt$");

    public static string ReadKeysBlock => T("packageEditor.read.keysBlock", "Block-Muster");

    public static string ReadKeysBlockTip => T("packageEditor.read.keysBlock.tip",
        "Regulärer Ausdruck auf die obersten Blockschlüssel. Die Gruppe (?<key>...) oder je Schlüsselfeld eine Gruppe mit dessen Namen nennt den Satz. Beispiel: ideagroup_modifier_(?<category>adm|dip|army|navy)_(?<group>\\d+)_1");

    public static string ReadLocOutput => T("packageEditor.read.locOutput", "Aus Ausgabe");

    public static string ReadLocOutputTip => T("packageEditor.read.locOutput.tip", "Die Lokalisierungs-Ausgabe, aus deren Dateien gelesen wird.");

    public static string ReadLocOutputFirst => T("packageEditor.read.locOutput.first", "(die erste)");

    public static string ReadLocEntryOutput => T("packageEditor.read.locEntryOutput", "Ausgabe");

    public static string ReadLocEntryOutputTip => T("packageEditor.read.locEntryOutput.tip",
        "Lokalisierungs-Ausgabe, aus deren Dateien dieser Eintrag liest. Standard: die oben gewählte Ausgabe.");

    public static string ReadLocEntryOutputDefault => T("packageEditor.read.locEntryOutput.default", "(Standard)");

    public static string ReadValuesHeading => T("packageEditor.read.valuesHeading", "Werte sammeln");

    public static string ReadValuesNote => T("packageEditor.read.valuesNote",
        "Sammelt Treffer aus einem Block in ein Textfeld, z. B. alle Religionen aus religion = religion:x. Doppelte fallen weg.");

    public static string ReadValueFileTip => T("packageEditor.read.valueFile.tip", "Datei der Mod mit dem Block, mit Scriban für den Schlüssel.");

    public static string ReadValueBlockTip => T("packageEditor.read.valueBlock.tip",
        "Muster auf den obersten Blockschlüssel. Beispiel: iv2_ga_select_ideagroup_{{ category }}_{{ group }}");

    public static string ReadValuePattern => T("packageEditor.read.valuePattern", "Treffer");

    public static string ReadValuePatternTip => T("packageEditor.read.valuePattern.tip",
        "Regulärer Ausdruck mit (?<value>...); jeder Treffer im Block liefert einen Wert. Beispiel: religion = religion:(?<value>\\w+)");

    public static string ReadFlagPattern => T("packageEditor.read.flagPattern", "Oder Muster");

    public static string ReadFlagPatternTip => T("packageEditor.read.flagPattern.tip",
        "Regulärer Ausdruck mit Scriban statt des festen Texts, z. B. mit Zahlgrenze: iv2_ga_select_ideagroup_{{ category }}_{{ group }}(?![0-9]).");

    public static string ReadValueBody => T("packageEditor.read.valueBody", "Unterblock");

    public static string ReadValueBodyTip => T("packageEditor.read.valueBody.tip",
        "Statt Treffer zu sammeln den ganzen Inhalt eines Unterblocks lesen, als Pfad aus Schlüsseln. Beispiel: allow (die Bedingungen einer Aktion).");

    public static string ReadValueFieldTip => T("packageEditor.read.valueField.tip", "Textfeld, das die Werte bekommt: ein Kopf-Feld oder Ebene.Feld.");

    public static string ReadValueJoin => T("packageEditor.read.valueJoin", "Verbinden mit");

    public static string ReadValueJoinTip => T("packageEditor.read.valueJoin.tip", "Zeichen zwischen den Werten; leer oder \\n = Zeilenumbruch.");

    public static string ReadElementsHeading => T("packageEditor.read.elementsHeading", "Elemente");

    public static string ReadElementsFile => T("packageEditor.read.elementsFile", "Datei");

    public static string ReadElementsFileTip => T("packageEditor.read.elementsFile.tip",
        "Datei der Mod mit einem Block je Element, mit {{ tag }} für den Schlüssel. Beispiel: main_menu/common/static_modifiers/02_IV2_{{ tag }}.txt");

    public static string ReadElementsBlock => T("packageEditor.read.elementsBlock", "Block-Muster");

    public static string ReadElementsBlockTip => T("packageEditor.read.elementsBlock.tip",
        "Muster auf die Schlüssel der obersten Blöcke dieser Datei. (?<number>...) ist die Nummer des Elements, (?<suffix>...) wählt das Element der tieferen Ebene. Beispiel: national_idea_modifier_{{ tag }}_(?<number>\\d+)(?<suffix>_\\d)?");

    public static string ReadElementsLevel => T("packageEditor.read.elementsLevel", "Ebene");

    public static string ReadElementsLevelTip => T("packageEditor.read.elementsLevel.tip", "Ebene, deren Elemente aus den Blöcken entstehen.");

    public static string ReadBody => T("packageEditor.read.body", "Blockinhalt übernehmen");

    public static string ReadBodyTip => T("packageEditor.read.body.tip",
        "Das Feld bekommt die Zeilen des Blocks (Kommentare und Unterblöcke eingeschlossen, gemeinsame Einrückung entfernt).");

    public static string ReadExcept => T("packageEditor.read.except", "Ausnahmen");

    public static string ReadExceptTip => T("packageEditor.read.except.tip", "Schlüssel, deren Zeilen nicht übernommen werden, durch Komma getrennt. Beispiel: game_data");

    public static string ReadLocalizationHeading => T("packageEditor.read.localizationHeading", "Lokalisierung");

    public static string ReadLocalizationNote => T("packageEditor.read.localizationNote",
        "PMT liest die Datei der Lokalisierungs-Ausgabe in jeder Sprache des Spielprofils. Englisch füllt das Feld, andere Sprachen werden Übersetzungen.");

    public static string ReadLocKey => T("packageEditor.read.locKey", "Schlüssel-Muster");

    public static string ReadLocKeyTip => T("packageEditor.read.locKey.tip",
        "Muster auf Lokalisierungsschlüssel mit (?<number>...). Beispiel: iv2_historic_idea_{{ tag }}_(?<number>\\d+)");

    public static string ReadField => T("packageEditor.read.field", "Feld");

    public static string ReadLocFieldTip => T("packageEditor.read.locField.tip", "Mehrsprachiges Textfeld, das den Text bekommt: ein Kopf-Feld oder Ebene.Feld.");

    public static string ReadFlagsHeading => T("packageEditor.read.flagsHeading", "Ja/Nein-Merkmale");

    public static string ReadFlagFileTip => T("packageEditor.read.flagFile.tip",
        "Datei der Mod mit den Blöcken, die das Merkmal tragen. Beispiel: in_game/common/generic_actions/iv2_GA_ideas_{{ tag }}.txt");

    public static string ReadFlagBlockTip => T("packageEditor.read.flagBlock.tip", "Muster auf die Blockschlüssel dieser Datei, mit (?<number>...) und bei Bedarf (?<suffix>...).");

    public static string ReadContains => T("packageEditor.read.contains", "Enthält");

    public static string ReadContainsTip => T("packageEditor.read.contains.tip",
        "Steht dieser Text im Block, wird das Feld \"Ja\", sonst \"Nein\". Beispiel: for_nat_idea_expensive_");

    public static string ReadFlagFieldTip => T("packageEditor.read.flagField.tip", "Ja/Nein-Feld als Ebene.Feld.");

    public static string ReadAssetsHeading => T("packageEditor.read.assetsHeading", "Bilder");

    public static string ReadAssetFieldTip => T("packageEditor.read.assetField.tip",
        "Bildfeld des Kopfs. PMT sucht in dessen Asset-Bibliothek den Eintrag mit denselben Bytes wie die Datei der Mod.");

    public static string ReadAssetFileTip => T("packageEditor.read.assetFile.tip", "Bilddatei der Mod. Beispiel: main_menu/gfx/interface/coa/{{ tag }}.dds");

    public static string ReadAddRow => T("packageEditor.read.addRow", "Zeile hinzufügen");

    public static string ReadAddRowTip => T("packageEditor.read.addRow.tip", "Hängt eine leere Zeile an.");

    public static string ReadRowTip => T("packageEditor.read.row.tip", "Wirkt auf diese Zeile.");

    public static string Suggest => T("packageEditor.read.suggest", "Vorschlag");

    public static string SuggestTip => T("packageEditor.read.suggest.tip",
        "Baut das Muster aus einem Beispiel der Mod: Datei, Block oder Lokalisierungsschlüssel anklicken, PMT ersetzt den Schlüssel und die Nummer.");

    public static string SuggestReadOnly => T("packageEditor.read.suggest.readOnly",
        "Mitgelieferte Pakete bleiben unverändert. Das vorgeschlagene Muster zum Kopieren:");

    public static string Probe => T("packageEditor.read.probe", "Probelauf");

    public static string ProbeTip => T("packageEditor.read.probe.tip",
        "Liest einen Satz aus einer Mod mit den aktuellen Mustern und zeigt die Treffer je Muster und das Ergebnis. Schreibt nichts.");

    public static string ChooseMod => T("packageEditor.read.chooseMod", "Mod wählen");

    public static string ChooseModMessage => T("packageEditor.read.chooseMod.message", "Aus welcher Mod sollen die Beispiele kommen? Die aktive Mod steht oben.");

    public static string ActiveModMark(string name) => F("packageEditor.read.activeMod", "{0} (aktiv)", name);

    public static string NoMods => T("packageEditor.read.noMods", "Es ist keine Mod mit vorhandenem Ordner bekannt.");

    public static string ChooseKey => T("packageEditor.read.chooseKey", "Schlüssel wählen");

    public static string ChooseKeyMessage => T("packageEditor.read.chooseKey.message", "Welchen Satz soll der Probelauf einlesen?");

    public static string ProbeNoKeys => T("packageEditor.read.probeNoKeys", "Das Muster der Schlüssel-Dateien findet in dieser Mod keinen Satz.");

    public static string SuggestTitle => T("packageEditor.suggest.title", "Muster vorschlagen");

    public static string SuggestKeyValue => T("packageEditor.suggest.keyValue", "Schlüssel im Beispiel");

    public static string SuggestKeyValueTip => T("packageEditor.suggest.keyValue.tip", "Der Wert des Schlüsselfelds, der im Beispiel vorkommt, z. B. BYZ. Er wird zu {{ tag }} bzw. (?<key>...).");

    public static string SuggestFile => T("packageEditor.suggest.file", "Datei");

    public static string SuggestFileTip => T("packageEditor.suggest.file.tip", "Datei der Mod, deren oberste Blöcke unten stehen.");

    public static string SuggestChooseFile => T("packageEditor.suggest.chooseFile", "Datei wählen");

    public static string SuggestExamples => T("packageEditor.suggest.examples", "Beispiel anklicken");

    public static string SuggestResult => T("packageEditor.suggest.result", "Vorschlag");

    public static string SuggestKeyMissing => T("packageEditor.suggest.keyMissing", "Der Schlüssel kommt im Beispiel nicht als eigenes Wort vor.");

    public static string SuggestNumberMissing => T("packageEditor.suggest.numberMissing", "Hinter dem Schlüssel steht keine Nummer.");

    public static string SuggestNone => T("packageEditor.suggest.none", "Noch kein Beispiel gewählt.");

    public static string ProbeTitle(string key) => F("packageEditor.probe.title", "Probelauf: {0}", key);

    public static string ProbeMatches => T("packageEditor.probe.matches", "Treffer");

    public static string ProbeResult => T("packageEditor.probe.result", "Ergebnis");

    public static string ProbeNotes => T("packageEditor.probe.notes", "Hinweise");

    public static string ProbeNoNotes => T("packageEditor.probe.noNotes", "Keine Hinweise.");

    public static string ProbeLine(string label, int count) => F("packageEditor.probe.line", "{0}: {1} Treffer", label, count);

    public static string ProbeNoMatch(string label) => F("packageEditor.probe.noMatch", "{0}: keine Treffer", label);

    public static string ProbePatternKeys => T("packageEditor.probe.keys", "Schlüssel-Dateien");

    public static string ProbePatternElements => T("packageEditor.probe.elements", "Elemente");

    public static string ProbePatternLocalization(int row) => F("packageEditor.probe.localization", "Lokalisierung, Zeile {0}", row);

    public static string ProbePatternFlag(int row) => F("packageEditor.probe.flag", "Ja/Nein-Merkmal, Zeile {0}", row);

    public static string ProbePatternAsset(int row) => F("packageEditor.probe.asset", "Bild, Zeile {0}", row);

    public static string ProbeClose => T("packageEditor.probe.close", "Schließen");
}
