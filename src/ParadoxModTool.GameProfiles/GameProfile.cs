namespace ParadoxModTool.GameProfiles;

/// <summary>
/// Eine Ordnerkategorie innerhalb der Mod-/Spielstruktur (z. B. "common", "gui"),
/// inklusive Anzeigefarbe für die Tab-Kategorie-Anzeige im Editor
/// (siehe Architektur-Spezifikation, Abschnitt 5).
/// </summary>
public sealed class FolderCategory
{
    private string _label = string.Empty;
    private string? _description;

    public string Path { get; set; } = string.Empty;

    /// <summary>Optionaler Key in <c>locales/*.json</c> für <see cref="Label"/>; ohne Key gilt der Text aus dem Profil.</summary>
    public string? LabelKey { get; set; }

    /// <summary>Anzeigename in der gewählten Sprache (Rückfall: Text aus dem Profil).</summary>
    public string Label
    {
        get => GameProfileTexts.Resolve(LabelKey, _label) ?? string.Empty;
        set => _label = value;
    }

    public string DefaultColor { get; set; } = "#808080";

    /// <summary>Optionaler Key in <c>locales/*.json</c> für <see cref="Description"/>.</summary>
    public string? DescriptionKey { get; set; }

    /// <summary>Beschreibung in der gewählten Sprache; <see langword="null"/>, wenn das Profil keine hat.</summary>
    public string? Description
    {
        get => GameProfileTexts.Resolve(DescriptionKey, _description);
        set => _description = value;
    }
}

/// <summary>Ordnet eine Dateiendung ihrem Parser/Highlighter-Bezeichner zu.</summary>
public sealed class FileTypeMapping
{
    public string Extension { get; set; } = string.Empty;
    public string Parser { get; set; } = string.Empty;
    public string? DefaultEncoding { get; set; }
}

/// <summary>Ein Eintrag im Spielstart-Untermenü der Werkzeugleiste.</summary>
public sealed class LaunchOption
{
    private string _label = string.Empty;

    /// <summary>Optionaler Key in <c>locales/*.json</c> für <see cref="Label"/>; ohne Key gilt der Text aus dem Profil.</summary>
    public string? LabelKey { get; set; }

    /// <summary>Anzeigename in der gewählten Sprache (Rückfall: Text aus dem Profil).</summary>
    public string Label
    {
        get => GameProfileTexts.Resolve(LabelKey, _label) ?? string.Empty;
        set => _label = value;
    }

    public string Args { get; set; } = string.Empty;
}

/// <summary>Ein Eintrag der Überschreib-Regeltabelle eines Spielprofils (siehe <c>ParadoxModTool.ModCompat</c>).</summary>
public sealed class OverrideRuleEntry
{
    public string Folder { get; set; } = string.Empty;
    public string Behavior { get; set; } = "PerKey";
    public bool Certain { get; set; } = true;

    /// <summary>
    /// Hüllen-Blöcke, deren Einträge einzeln zählen (EU IV: <c>country_decisions</c>, <c>spriteTypes</c>). Der Schlüssel lautet dann <c>Hülle &gt; Name</c>.
    /// <c>hülle/*</c> = zwei Ebenen (HOI IV <c>ideas/*</c>: <c>ideas &gt; country &gt; GER_x</c>), <c>*</c> = jeder Block der obersten Ebene ist eine Hülle
    /// (HOI IV-Entscheidungen in Kategorien); in diesen beiden Formen zählen Werte in der Hülle nicht.
    /// </summary>
    public List<string>? Unwrap { get; set; }

    /// <summary>Feld im Block, das den Schlüssel liefert (EU IV: <c>id</c> bei Events, <c>name</c> bei GUI-Typen). Leer = der Blockname.</summary>
    public string? KeyField { get; set; }

    /// <summary>
    /// Präfixe, die in diesem Ordner wirken (Victoria 3: INJECT/REPLACE nur in den Ordnern der Wiki-Liste, in vier Ordnern nur REPLACE).
    /// Null = die Präfixe des Spiels (<see cref="GameProfile.KeyOperators"/>), leer = keine.
    /// </summary>
    public List<string>? Operators { get; set; }
}

/// <summary>
/// Datengetriebenes Profil eines Paradox-Spiels — der zentrale Baustein der
/// Mehrspiel-Fähigkeit des Tools (siehe Architektur-Spezifikation, Abschnitt 4).
/// Wird aus einer JSON-Datei unter profiles/ geladen, ohne dass dafür Code
/// geändert werden muss.
/// </summary>
public sealed class GameProfile
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Kurzname für enge Stellen wie das Menü "Meine {Kürzel} Mods" (z. B. "EU V"); ohne Angabe gilt <see cref="DisplayName"/>.</summary>
    public string? ShortName { get; set; }

    public string EffectiveShortName => string.IsNullOrWhiteSpace(ShortName) ? DisplayName : ShortName;

    /// <summary>Erscheinungsdatum des Spiels (ISO, z. B. <c>2013-08-13</c>); bestimmt die Reihenfolge in der Spielauswahl.</summary>
    public DateOnly? ReleaseDate { get; set; }

    public string EngineFamily { get; set; } = string.Empty;
    public string DefaultModRootTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Pfad der Mod-Metadaten-Datei relativ zum Mod-Ordner (Launcher-v2-JSON-Format, z. B. <c>.metadata/metadata.json</c>),
    /// oder <c>ParadoxModTool.Mods.ModMetadata.ClassicSiblingMetadataFile</c> ("*.mod") für das klassische Format
    /// (Geschwisterdatei &lt;Ordnername&gt;.mod NEBEN dem Mod-Ordner, z. B. EU IV).
    /// </summary>
    public string MetadataFile { get; set; } = string.Empty;

    /// <summary>
    /// Name der Lade-Datei im Spielordner, die das Spiel beim direkten Start liest und die der Paradox-Launcher sonst schreibt (<c>dlc_load.json</c>,
    /// <c>content_load.json</c>); null = das Spiel liest die Playsets selbst (EU V, playsets.json).
    /// </summary>
    public string? PlaysetLoadFile { get; set; }

    public string LoadOrderRule { get; set; } = string.Empty;
    public List<FolderCategory> FolderCategories { get; set; } = new();
    public List<FileTypeMapping> FileTypes { get; set; } = new();
    public List<LaunchOption> LaunchOptions { get; set; } = new();
    /// <summary>
    /// Ordnername der Vanilla-Historie im Speicherort für Snapshots (z. B. "EU V Vanilla History"). Ohne Angabe
    /// "&lt;Anzeigename&gt; Vanilla History". Je Spiel verschieden, damit zwei Spiele mit demselben Oberordner ihre Historien nicht mischen.
    /// </summary>
    public string? VanillaHistoryFolderName { get; set; }

    public string EffectiveVanillaHistoryFolderName =>
        string.IsNullOrWhiteSpace(VanillaHistoryFolderName) ? DisplayName + " Vanilla History" : VanillaHistoryFolderName;

    /// <summary>Pfad der Fehlerkatalog-Datei für das error.log, relativ zum profiles-Ordner (z.B. "errorlog/eu5.json"); null = kein Katalog.</summary>
    public string? ErrorLogRules { get; set; }
    /// <summary>Pfad der Desync-Regeln für die OOS-Analyse, relativ zum profiles-Ordner (z.B. "desync/eu5.json"); null = keine Desync-Analyse.</summary>
    public string? DesyncRules { get; set; }
    public string? Notes { get; set; }

    /// <summary>Überschreib-Verhalten je Ordner (Mod-Kompatibilität): Ordnermuster mit <c>*</c> für ein Segment, Verhalten als Name von <c>OverrideBehavior</c>, "Certain" = laut Wiki gesichert.</summary>
    public List<OverrideRuleEntry> OverrideRules { get; set; } = new();

    /// <summary>
    /// Präfixe vor Einträgen der obersten Ebene, die das Spiel kennt (Namen ohne Doppelpunkt, z. B. "INJECT", "REPLACE"). Leer = keine
    /// (EU IV). Quelle ist das offizielle Wiki des Spiels; unbestätigt bleibt die Liste leer.
    /// </summary>
    public List<string> KeyOperators { get; set; } = new();

    /// <summary>
    /// INJECT hängt die Einträge hinten an den vorhandenen Block an (Victoria 3, laut Wiki), statt vorhandene Teilschlüssel an ihrer Stelle
    /// zu überschreiben (EU V, gemessen 2026-10-02). Ohne Angabe gilt das Verhalten von EU V.
    /// </summary>
    public bool InjectAppends { get; set; }

    /// <summary>
    /// Bei "zuerst geladen gewinnt" (GUI-Typen, Events) zählen Vanilla und Mods gemeinsam in Ladereihenfolge (<see cref="GameLoadOrder"/>), die
    /// Playset-Reihenfolge nicht (Victoria 3: für GUI gemessen 2026-10-11, für Events laut Wiki). Ohne Angabe: Playset-Rang, dann Dateiname (bisheriges Verhalten).
    /// </summary>
    public bool FirstWinsInLoadOrder { get; set; }

    /// <summary>
    /// Bei "zuletzt geladen gewinnt" zählen Vanilla und Mods gemeinsam in Ladereihenfolge der Dateien (<see cref="GameLoadOrder"/>), die Playset-Reihenfolge
    /// nur bei gleichem Pfad (Stellaris, laut Wiki "Modding": Dateien laden nach ihrem Namen; eine Mod-Datei <c>00_x.txt</c> verliert gegen Vanilla in
    /// <c>zz_y.txt</c>). Ohne Angabe: Playset-Rang, dann Dateiname (bisheriges Verhalten).
    /// </summary>
    public bool LastWinsInLoadOrder { get; set; }

    /// <summary>
    /// Die Ladereihenfolge der Dateinamen vergleicht ASCII mit Groß-/Kleinschreibung (Großbuchstaben vor "_" vor Kleinbuchstaben; Stellaris, gemessen
    /// 2026-10-16). Ohne Angabe: ASCII in Kleinschreibung (Victoria 3), siehe <see cref="GameLoadOrder"/>.
    /// </summary>
    public bool LoadOrderCaseSensitive { get; set; }

    /// <summary>
    /// <c>supported_version</c> im Mod-Deskriptor gilt nur als genaue Version (<c>v4.5.2</c>) oder Hauptversion.Nebenversion.* (<c>v4.5.*</c>);
    /// <c>*</c>, <c>v4.*</c> und <c>v4.*.*</c> meldet das Spiel als "Invalid supported_version" (Stellaris, gemessen 2026-10-16). Ein neuer
    /// Kompatch nimmt dann die installierte Version, siehe <c>NewCompatch.SupportedVersion</c>.
    /// </summary>
    public bool SupportedVersionMajorMinor { get; set; }

    /// <summary>Steam-AppID des Spiels (für den Workshop-Upload); ohne Angabe ist das Veröffentlichen auf Steam für dieses Spiel gesperrt.</summary>
    public string? SteamAppId { get; set; }

    /// <summary>Woher die Script-Datenbank dieses Spiels stammt: "log" (Standard, aus docs/*.log) oder "wiki" (siehe ScriptDocsWikiFile).</summary>
    public string ScriptDocsSourceKind { get; set; } = "log";

    /// <summary>
    /// Ordner der Doku-Logs (modifiers.log, triggers.log …), relativ zum Spieldatenordner des Nutzers (dem Ordner über dem Mod-Ordner).
    /// EU V schreibt sie nach "docs", CK III mit dem Konsolenbefehl <c>script_docs</c> nach "logs".
    /// </summary>
    public string ScriptDocsFolder { get; set; } = "docs";

    /// <summary>
    /// Worauf sich <see cref="ScriptDocsFolder"/> bezieht: "user" (Standard, Spieldatenordner des Nutzers) oder "install" (Installationsordner des
    /// Spiels). Hearts of Iron IV liefert seine Doku als Markdown im Installationsordner (<c>documentation/*.md</c>).
    /// </summary>
    public string ScriptDocsBase { get; set; } = "user";

    /// <summary>
    /// Abweichende Namen der Doku-Logs je Eintragsart (Schlüssel = Name der Art, z. B. <c>EventTarget</c>). Stellaris schreibt die Scope-Wechsel nach
    /// <c>scopes.log</c>. Ohne Angabe gelten die üblichen Namen (<c>modifiers.log</c>, <c>event_targets.log</c> …).
    /// </summary>
    public Dictionary<string, string>? ScriptDocsFiles { get; set; }

    /// <summary>
    /// Art der Events ohne Feld <c>type</c> für die Scope-Verfolgung (CK III laut <c>game/events/_events.info</c>: <c>character_event</c>).
    /// <see langword="null"/>, wenn das Spiel <c>type</c> verlangt (EU V) oder es nicht geprüft ist.
    /// </summary>
    public string? DefaultEventType { get; set; }

    /// <summary>
    /// Events, deren Art im Blocknamen steht statt im Feld <c>type</c> (Stellaris: <c>country_event = { id = x.1 … }</c>), für die Scope-Verfolgung:
    /// Blockname auf Root-Scope, "none" für Events ohne Root-Scope (Stellaris: <c>event</c>), "learned" für einen gelernten Root-Scope (Hearts of Iron IV,
    /// EU IV). <see langword="null"/> bei Spielen mit <c>type</c>.
    /// </summary>
    public Dictionary<string, string>? EventBlockScopes { get; set; }

    /// <summary>
    /// Zahlen als Blockschlüssel wechseln den Scope (HOI IV: Staats-ID <c>763 = { … }</c>, EU IV: Provinz-ID), außer in <c>random_list</c>. Die
    /// Scope-Verfolgung kennt den Scope dahinter nicht und warnt dort nicht. Ohne Angabe gelten Zahlen als Gewichte (bisheriges Verhalten).
    /// </summary>
    public bool NumericKeysChangeScope { get; set; }

    /// <summary>
    /// <c>replace_path</c> einer Mod blendet auch die Dateien der Mods aus, die im Playset vor ihr laden, nicht nur Vanilla (Crusader Kings III,
    /// Europa Universalis V, gemessen 2026-09-30, und Victoria 3, gemessen 2026-10-11, je mit zwei Test-Mods). <see langword="false"/> = nur Vanilla (Europa Universalis IV, gemessen) oder
    /// nicht gemessen.
    /// </summary>
    public bool ReplacePathHidesEarlierMods { get; set; }

    /// <summary>
    /// Wurzelordner, die <c>replace_path</c> nicht mitnennt (EU V: <c>in_game</c>, <c>main_menu</c>, <c>loading_screen</c>; <c>common/x</c> blendet
    /// <c>in_game/common/x</c> aus, <c>in_game/common/x</c> wirkt nicht, gemessen 2026-09-30). Leer = Pfade gelten ab dem Mod-Ordner.
    /// </summary>
    public List<string> ReplacePathRoots { get; set; } = new();

    /// <summary>
    /// Ordner, in deren Unterordnern ein <c>replace_path</c> das Spiel beim Start abstürzen lässt, sobald dort .gui-Dateien liegen (EU IV:
    /// <c>interface</c>; das Spiel sucht die Dateien ohne den Unterordner, gemessen 2026-09-30). Die Mod-Kompatibilität warnt davor.
    /// </summary>
    public List<string> ReplacePathGuiCrashRoots { get; set; } = new();

    /// <summary>
    /// Sprachen der Lokalisierung des Spiels (Ordnernamen unter <c>localization</c>, z. B. <c>english</c>, <c>braz_por</c>), in der Reihenfolge des Spiels.
    /// Die Generator-Pakete schreiben ihre Lokalisierung in diese Sprachen. Leer = nur Englisch.
    /// </summary>
    public List<string> LocalizationLanguages { get; set; } = new();

    /// <summary>Bei ScriptDocsSourceKind="wiki": Pfad relativ zum profiles-Ordner (z. B. "scriptdocs/eu4.json").</summary>
    public string? ScriptDocsWikiFile { get; set; }

    /// <summary>GUI-Einordnung für den GUI-Editor. Null bei Spielen ohne GUI-Unterstützung.</summary>
    public GuiProfileSettings? Gui { get; set; }

    /// <summary>Spielstand-Explorer: Ordner, Textmarke, Objektarten und Werttypen der Spielstände. Null = Explorer für dieses Spiel nicht verfügbar.</summary>
    public SaveGameSettings? SaveGames { get; set; }

    /// <summary>Woher die Anzeigeformate der Modifier-Werte kommen (Hover "Im Spiel: +10 %"). Null = keine Anzeige.</summary>
    public ModifierFormatSettings? ModifierFormats { get; set; }

    /// <summary>Profiler-Fakten für den Performance-Check (Konsolenbefehle, Dump-Datei, Monatszeile). Null = Spiel ohne Profiler-Unterstützung.</summary>
    public PerformanceSettings? Performance { get; set; }
}

/// <summary>
/// Anzeigeformate der Modifier-Werte (siehe docs/superpowers/specs/2026-09-26-modifier-values-design.md, Abschnitt "Profil").
/// </summary>
public sealed class ModifierFormatSettings
{
    /// <summary>"definitions" (Definitionsdateien des Spiels), "wiki" (Script-Datenbank aus der Wiki, geschätzt), "suffix" (aus der Namensendung, geschätzt) oder "none".</summary>
    public string Source { get; set; } = "none";

    /// <summary>Ordner der Definitionsdateien, relativ zum Spielinhalt (<c>game/</c>) und genauso relativ zum Mod-Ordner.</summary>
    public List<string> Folders { get; set; } = new();

    /// <summary>Nachkommastellen, wenn ein Eintrag kein <c>decimals</c> hat.</summary>
    public int DefaultDecimals { get; set; } = 2;

    /// <summary>Nullen am Ende der Nachkommastellen weglassen ("+12.5 %" statt "+12.50 %").</summary>
    public bool TrimTrailingZeros { get; set; } = true;

    /// <summary>Umrechnung je Wert von <c>game_data.format</c>; ein Format ohne Eintrag zählt als Faktor 1.</summary>
    public Dictionary<string, ModifierFormatFactor> Formats { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Ordner mit den Lokalisierungsdateien der Modifier-Namen, relativ zum Spielinhalt und zum Mod-Ordner; gelesen werden alle <c>*_l_&lt;sprache&gt;.yml</c> darunter.</summary>
    public List<string> NameLocFolders { get; set; } = new();

    /// <summary>Lokalisierungsschlüssel des Modifier-Namens in Rangfolge; <c>{name}</c> steht für den Modifier (Groß-/Kleinschreibung egal), z. B. <c>MOD_{name}</c>.</summary>
    public List<string> NameKeys { get; set; } = new();

    /// <summary>Lokalisierungsschlüssel eines Begriffs oder Verweises im Namen (<c>[levy|E]</c>, <c>$nobles$</c>) in Rangfolge; <c>{id}</c> steht für den Begriff, z. B. <c>game_concept_{id}</c>.</summary>
    public List<string> NameRefKeys { get; set; } = new() { "{id}" };

    /// <summary>
    /// Bei <c>source = suffix</c>: Namensendung → "percent" oder "number" (Stellaris: <c>_mult</c> Prozent, <c>_add</c> Zahl). Die längste passende Endung gilt;
    /// Modifier ohne passende Endung bekommen kein Format.
    /// </summary>
    public Dictionary<string, string> Suffixes { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Faktor, mit dem der Wert eines Sonderformats multipliziert wird (z. B. 1000 für Werte, die das Spiel in Tausend speichert).</summary>
public sealed class ModifierFormatFactor
{
    public decimal Factor { get; set; } = 1;
}

/// <summary>
/// Alles Spielabhängige des Spielstand-Explorers (siehe docs/superpowers/specs/2026-09-26-save-explorer-design.md, Abschnitt "Spielprofil").
/// Der Code kennt nur die festen Namensquellen und Darstellungen; das Profil wählt aus.
/// </summary>
public sealed class SaveGameSettings
{
    /// <summary>Ordnername der Spielstände unter dem Spieldatenordner (EU V: "save games").</summary>
    public string Folder { get; set; } = "save games";

    /// <summary>Dateiendung mit Punkt (EU V: ".eu5").</summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>Woran ein lesbarer Text-Stand erkannt wird.</summary>
    public SaveTextMarker TextMarker { get; set; } = new();

    /// <summary>Name des Metadaten-Blocks nach der Kopfzeile (EU V: "metadata", Crusader Kings III: "meta_data"); leer = Werte auf oberster Ebene vor dem ersten Block (Hearts of Iron IV).</summary>
    public string MetadataBlock { get; set; } = "metadata";

    /// <summary>Feld mit dem Spieldatum im Metadaten-Block (EU V: "date", Crusader Kings III: "meta_date").</summary>
    public string DateField { get; set; } = "date";

    /// <summary>Objektarten in Anzeigereihenfolge; Besitzer ohne passende Art erscheinen unter "Sonstige".</summary>
    public List<SaveObjectKindSetting> ObjectKinds { get; set; } = new();

    /// <summary>Werttyp (Feld <c>type</c> im Spielstand) auf Darstellung; Typen ohne Eintrag zeigen den Rohwert.</summary>
    public Dictionary<string, SaveValueTypeSetting> ValueTypes { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Erste Zeile beginnt mit <see cref="Prefix"/>, und ab <see cref="Offset"/> (0-basiert) steht <see cref="Value"/>.</summary>
public sealed class SaveTextMarker
{
    public string Prefix { get; set; } = "SAV";
    public int Offset { get; set; }
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Wert an derselben Stelle für einen Text-Stand, dessen Inhalt als ZIP-Eintrag <c>gamestate</c> hinter dem Metadaten-Block liegt
    /// (Crusader Kings III: "02"); <see langword="null"/> = solche Stände gelten als nicht lesbar (EU V).
    /// </summary>
    public string? CompressedValue { get; set; }
}

/// <summary>
/// Eine Objektart: Pfad-Muster der Besitzer (<c>*</c> = genau ein Segment), Text der Anzeige und Namensquelle
/// (<c>table</c>: Wert aus <see cref="NameTable"/> unter der Id, <c>field</c>: Feld <see cref="NameField"/> des Objekts,
/// <c>pathSegment</c>: letztes Pfadsegment, <c>id</c>: die Id).
/// </summary>
public sealed class SaveObjectKindSetting
{
    public string Path { get; set; } = string.Empty;
    public string LabelKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string NameSource { get; set; } = "id";
    public string? NameTable { get; set; }
    public string? NameField { get; set; }

    /// <summary>Feld des Objekts mit der Id seines Landes (EU V Charaktere: <c>country</c>); zusammen mit <see cref="TagTable"/> ergibt es den Land-Tag.</summary>
    public string? TagField { get; set; }

    /// <summary>Tabelle Id auf Tag (EU V: <c>countries/tags</c>).</summary>
    public string? TagTable { get; set; }

    /// <summary>Verweis im Skript mit <c>{0}</c> für die Id (EU V Charaktere: <c>character:{0}</c>); ohne Angabe gibt es keinen Skript-Verweis.</summary>
    public string? ScriptReference { get; set; }

    /// <summary>
    /// Unterpfad vom Objekt zu seinem <c>variables</c>-Block, wenn die Variablen nicht direkt im Objekt liegen (Crusader Kings III Charaktere:
    /// <c>alive_data</c>); <see langword="null"/> = direkt im Objekt.
    /// </summary>
    public string? VariablesPath { get; set; }
}

/// <summary>
/// Darstellung eines Werttyps: <c>fixedPoint</c> (Rohwert / <see cref="Factor"/>), <c>tickDate</c> (<see cref="TicksPerDay"/> ab
/// <see cref="EpochYear"/>.1.1, 365 Tage je Jahr), <c>objectName</c> (Name des Objekts <see cref="ObjectPath"/>/Rohwert), <c>yesNo</c>,
/// <c>flagName</c>, <c>raw</c>.
/// </summary>
public sealed class SaveValueTypeSetting
{
    public string Format { get; set; } = "raw";
    public long Factor { get; set; } = 1;
    public string? ObjectPath { get; set; }
    public int TicksPerDay { get; set; } = 1;
    public int EpochYear { get; set; }
}

/// <summary>Einordnung von GUI-Blöcken für den GUI-Editor (siehe docs/superpowers/specs/2026-09-22-gui-editor-design.md).</summary>
public sealed class GuiProfileSettings
{
    /// <summary>Blöcke mit Schlüssel-Wert-Inhalt, die keine Widgets sind (z.B. modify_texture, glow, state).</summary>
    public List<string> StructuredBlocks { get; set; } = new();

    /// <summary>Blöcke, deren Inhalt Widgets sind, die aber selbst kein Widget sind (z.B. tooltipwidget, item).</summary>
    public List<string> WidgetHolders { get; set; } = new();

    /// <summary>Widget-Halter, deren Inhalt im Layout des Elternelements liegt (z.B. item in Rastern, scrollwidget in scrollarea). Andere Halter (Tooltips, Kontextmenüs) erscheinen nicht im Layout.</summary>
    public List<string> LayoutHolders { get; set; } = new();

    /// <summary>Kontextordner der .gui-Dateien; <see langword="null"/> = EU V (<c>in_game</c>, <c>main_menu</c>, <c>loading_screen</c>), leer = flaches <c>gui/</c> (CK III).</summary>
    public List<string>? Contexts { get; set; }

    /// <summary>Welche Definition eines Typs oder Templates gilt: <c>lastWins</c> (EU V, Standard) oder <c>firstWins</c> (CK III, zuerst geladene Datei).</summary>
    public string TypeOverride { get; set; } = "lastWins";

    /// <summary>
    /// Bei <c>firstWins</c>: Mods gehen vor Vanilla (CK III, gemessen 2026-09-30, Standard). <see langword="false"/> = Vanilla und Mods zählen gemeinsam
    /// in Ladereihenfolge (Victoria 3, gemessen 2026-10-11: ein Mod-Typ in gui/shared/zz_… verliert gegen Vanilla in gui/shared/button_icons.gui).
    /// </summary>
    public bool ModsBeforeVanilla { get; set; } = true;

    /// <summary>Format der .gui-Dateien: <c>jomini</c> (Standard: CK III, EU V, Victoria 3) oder <c>classic</c> (HOI IV, EU IV: guiTypes, containerWindowType).</summary>
    public string Dialect { get; set; } = "jomini";

    /// <summary>Nur klassisch, welche Sprite-Definition gilt: <c>lastWins</c> oder <c>firstWins</c>; <see langword="null"/> = lastWins. <see cref="TypeOverride"/> gilt dort für Fenster.</summary>
    public string? SpriteOverride { get; set; }

    /// <summary>
    /// Nur klassisch: Ordner relativ zur Installation, deren <c>interface/</c> Vanilla ist. <c>""</c> = die Installation selbst, ein <c>/*</c> am Ende = jeder
    /// direkte Unterordner (z. B. <c>dlc/*</c>). Leer = nicht angegeben.
    /// </summary>
    public List<string> VanillaRoots { get; set; } = new();
}

/// <summary>
/// Fakten des eingebauten Skript-Profilers eines Spiels (siehe docs/superpowers/specs/2026-10-20-performance-checker-design.md, Abschnitt "Spiele und Fakten").
/// </summary>
public sealed class PerformanceSettings
{
    /// <summary>Konsolenbefehl, der den Profiler zurücksetzt und neu startet (z. B. <c>Script.Profiling.Restart</c>).</summary>
    public string RestartCommand { get; set; } = string.Empty;

    /// <summary>Konsolenbefehl, der den Profiler-Dump schreibt (z. B. <c>Script.Profiling.Dump</c>).</summary>
    public string DumpCommand { get; set; } = string.Empty;

    /// <summary>Dateimuster des Dumps im Logs-Ordner des Spiels (z. B. <c>profiling*.csv</c>); null = noch nicht gemessen, die Funktion bleibt für dieses Spiel gesperrt.</summary>
    public string? DumpFilePattern { get; set; }

    /// <summary>Datei im Spieldatenordner, die den letzten Spielstand für den Direktstart nennt.</summary>
    public string ContinueGameFile { get; set; } = "continue_game.json";

    /// <summary>Startargument, das den letzten Spielstand lädt (z. B. <c>-continuelastsave</c>); null = unbekannt.</summary>
    public string? ContinueSaveArgs { get; set; }

    /// <summary>Vollständiger Zeilenschlüssel der Zeile, die genau einmal im Monat läuft; Grundlage, um Aufrufzahlen auf Monate umzurechnen.</summary>
    public string? MonthlyPulseRow { get; set; }

    /// <summary>Namen der täglichen On-Actions (für die Aufrufzahl pro Tag).</summary>
    public List<string> DailyOnActions { get; set; } = new();

    /// <summary>Die Funktion ist für das Spiel nutzbar, sobald Dump-Befehl und Dateimuster gemessen sind.</summary>
    public bool IsUsable => !string.IsNullOrEmpty(DumpFilePattern) && !string.IsNullOrEmpty(DumpCommand);
}
