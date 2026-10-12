namespace ParadoxModTool.Generators;

public enum FieldType
{
    Text,
    MultilineText,
    Number,
    Bool,
    Choice,
    Image,
    Modifiers,
}

/// <summary>
/// Ein Formularfeld. <see cref="Asset"/>: bei Bildfeldern die Kennung der Asset-Bibliothek (siehe <see cref="GeneratorPackage.Assets"/>).
/// <see cref="AutoNumber"/>: nur am letzten Schlüsselfeld vom Typ Zahl; ein neuer Arbeitsstand bekommt die nächste freie Nummer (siehe <see cref="ParadoxModTool.Generators.AutoNumber"/>).
/// </summary>
public sealed record FieldDef(
    string Id,
    string Label,
    FieldType Type,
    bool Required = false,
    string? Pattern = null,
    IReadOnlyList<string>? Choices = null,
    bool Multilingual = false,
    string? Default = null,
    string? Asset = null,
    Lookups.FieldLookupDef? Lookup = null,
    bool AutoNumber = false);

/// <summary>
/// Eine Ebene des Formulars. Mit <see cref="ConstantsTable"/> eine feste Gruppe (ein Element je Tabellenzeile, deren Werte wie Felder lesbar sind),
/// sonst eine zählbare Ebene mit <see cref="Min"/> bis <see cref="Max"/> Elementen. <see cref="NumberBaseField"/>: Feld des Elternelements, aus dem
/// sich die Nummer ergibt (Basis + Index). <see cref="Initial"/>: Anzahl der Elemente in einem neuen Arbeitsstand (ohne Angabe <see cref="Min"/>).
/// </summary>
public sealed record LevelDef(
    string Id,
    string Label,
    string? ConstantsTable,
    int Min,
    int Max,
    string? NumberBaseField,
    IReadOnlyList<FieldDef> Fields,
    LevelDef? Child,
    int? Initial = null);

public enum OutputKind
{
    OwnFile,
    Localization,
    InsertSection,
    EnsureLine,
    SyncBlocks,
    CopyBinary,
    SyncMatching,
    Check,
    JsonEntry,
}

/// <summary>
/// Eine Ausgabe. <see cref="Path"/>, <see cref="Block"/>, <see cref="LegacyBegin"/>/<see cref="LegacyEnd"/>, <see cref="Line"/>, <see cref="KeyPattern"/>
/// und <see cref="Source"/> sind Scriban-Vorlagen; <see cref="ForEach"/> nennt eine Ebene, je Element entsteht die Ausgabe einmal.
/// <see cref="LegacyPatterns"/>: Regex-Vorlagen für unmarkierte Alt-Einträge, die "Abschnitt einfügen" vorher entfernt; <see cref="Keep"/>: Teilschlüssel,
/// die "Blöcke abgleichen" stehen lässt, während es alle anderen ersetzt. <see cref="Match"/>: Regex-Vorlage für "Abgleich nach Treffer";
/// <see cref="Keys"/>: Regex-Vorlage auf Lokalisierungsschlüssel, mit der eine Lokalisierung nur ihre Zeilen in einer gemeinsamen Datei ersetzt.
/// <see cref="InsertAfter"/>: Regex-Vorlage für "Abgleich nach Treffer" und Lokalisierung mit <see cref="Keys"/>; neue Einheiten kommen hinter die letzte Einheit, auf die sie trifft.
/// <see cref="IfLimit"/>: Scriban-Vorlage für "Abgleich nach Treffer": gerendert leer = Einheit auf oberster Ebene, sonst Einheit im <c>if</c> mit dieser <c>limit</c>-Bedingung.
/// <see cref="Message"/>: Meldung einer Prüfung ("check") in der Oberflächensprache, Scriban mit <c>arg</c>.
/// <see cref="External"/>, <see cref="JsonPath"/>, <see cref="EntryKey"/>: nur "jsonEntry" — Kennung der externen Datei (siehe <see cref="GeneratorPackage.Externals"/>),
/// Punkt-Pfad zum Objekt in der JSON-Datei (leer = Wurzel) und Scriban-Vorlage des Schlüssels; <see cref="Template"/> rendert den JSON-Wert des Eintrags.
/// </summary>
public sealed record OutputDef(
    string Id,
    OutputKind Kind,
    string Path,
    string? Template = null,
    string? ForEach = null,
    string? Block = null,
    string? Skeleton = null,
    string? LegacyBegin = null,
    string? LegacyEnd = null,
    string? Line = null,
    string? KeyPattern = null,
    string? Source = null,
    string? NewLine = null,
    IReadOnlyList<string>? LegacyPatterns = null,
    IReadOnlyList<string>? Keep = null,
    bool CheckUnreferenced = false,
    string? Match = null,
    string? Keys = null,
    string? Message = null,
    string? InsertAfter = null,
    string? IfLimit = null,
    string? External = null,
    string? JsonPath = null,
    string? EntryKey = null);

/// <summary>Herkunft eines Assets: aus dem Spiel (wird nie exportiert) oder aus einer Mod bzw. vom Autor.</summary>
public enum AssetOrigin
{
    Game,
    Mod,
}

/// <summary>Ein Asset des Pakets: Pfad relativ zum Datenordner und Herkunft.</summary>
public sealed record AssetDef(string Path, AssetOrigin Origin);

/// <summary>Eine Paket-Warnung: <see cref="When"/> ist ein Scriban-Ausdruck; ist er wahr, fragt das Fenster vor dem Erzeugen nach.</summary>
public sealed record WarningDef(string When, string Message);

/// <summary>Eine externe Datei außerhalb von Mod und Ausgabeordner, in die eine "jsonEntry"-Ausgabe einen Eintrag schreibt; <see cref="Hint"/> erscheint als Tooltip.</summary>
public sealed record ExternalDef(string Label, string? Hint);

/// <summary>Regeln für mehrsprachige Texte (Spec Abschnitt 1), als Vorgabe des Pakets und je Arbeitsstand änderbar.</summary>
public sealed record LanguageDefaults(bool ExtendEnglish = false, bool CopyEnglish = false, bool EnglishForMissing = false);

/// <summary>
/// Ein Generator-Paket (Spec 2026-10-03-generator-packages-core). <see cref="Functions"/>: Text von <c>functions.sbn</c> (leer ohne Datei);
/// <see cref="Targets"/>: erlaubte Ziele <c>mod</c> und <c>output</c>; <see cref="Assets"/>: Kennung → Zip oder Ordner relativ zu <see cref="DataFolder"/>.
/// <see cref="Errors"/>: wie Warnungen, aber blockierend. <see cref="DataFolder"/>: Ordner für Arbeitsstände und Assets (bei mitgelieferten Paketen
/// im PMT-Datenordner, sonst der Paketordner); <see cref="Shipped"/>: kommt aus dem Programmordner.
/// </summary>
public sealed record GeneratorPackage(
    string Id,
    string Name,
    string GameId,
    string Folder,
    IReadOnlyList<FieldDef> Head,
    string KeyField,
    LevelDef? Root,
    IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> Constants,
    string Functions,
    IReadOnlyList<OutputDef> Outputs,
    IReadOnlyList<string> Targets,
    LanguageDefaults Languages,
    IReadOnlyList<WarningDef> Warnings,
    IReadOnlyDictionary<string, string> Assets,
    IReadOnlyList<WarningDef> Errors,
    string DataFolder,
    bool Shipped,
    ReadDef? Read = null,
    IReadOnlyList<Lookups.LookupSourceDef>? Lookups = null)
{
    private readonly IReadOnlyDictionary<string, AssetDef>? _assetDefs;
    private readonly string? _markerId;
    private readonly IReadOnlyList<string>? _keyFields;

    /// <summary>Kopf-Felder des Schlüssels (<c>keyFields</c>, sonst nur <see cref="KeyField"/>); siehe <see cref="PackageKey"/>.</summary>
    public IReadOnlyList<string> KeyFields
    {
        get => _keyFields ?? new[] { KeyField };
        init => _keyFields = value;
    }

    /// <summary>Trennzeichen zwischen den Teilen eines zusammengesetzten Schlüssels (<c>keySeparator</c>, Vorgabe <c>_</c>).</summary>
    public string KeySeparator { get; init; } = "_";

    /// <summary>
    /// Kennung in den PMT-Markierungen (<c>markerId</c> in package.json, ohne Angabe die Paket-Kennung). Eine Kopie behält die Kennung des Originals,
    /// damit sie dessen Abschnitte in der Mod wiederfindet statt sie ein zweites Mal zu schreiben.
    /// </summary>
    public string MarkerId
    {
        get => _markerId ?? Id;
        init => _markerId = value;
    }

    /// <summary>Assets mit Herkunft (Spec Teil 5a); ohne Angabe beim Erzeugen gelten alle <see cref="Assets"/> als aus dem Spiel.</summary>
    public IReadOnlyDictionary<string, AssetDef> AssetDefs
    {
        get => _assetDefs ?? Assets.ToDictionary(p => p.Key, p => new AssetDef(p.Value, AssetOrigin.Game));
        init => _assetDefs = value;
    }

    /// <summary>Externe Dateien (<c>externals</c>): Kennung → Beschriftung und Hinweis; den Pfad wählt der Nutzer (siehe <see cref="ExternalPathStore"/>).</summary>
    public IReadOnlyDictionary<string, ExternalDef> Externals { get; init; } = new Dictionary<string, ExternalDef>();

    /// <summary>Hinweis (<c>afterGenerate</c>, in der Oberflächensprache), den das Fenster nach erfolgreichem Erzeugen unter der Meldung zeigt; <see langword="null"/> ohne Angabe.</summary>
    public string? AfterGenerate { get; init; }

    /// <summary>Kandidatenliste (<c>candidates</c>); <see langword="null"/> ohne Angabe.</summary>
    public CandidatesDef? Candidates { get; init; }

    /// <summary>Text einer Vorlage aus <c>templates/</c>.</summary>
    public string TemplateText(string name) => File.ReadAllText(System.IO.Path.Combine(Folder, "templates", name));

    /// <summary>Voller Pfad einer Asset-Bibliothek (im <see cref="DataFolder"/>).</summary>
    public string AssetPath(string id) => System.IO.Path.Combine(DataFolder, Assets[id].Replace('/', System.IO.Path.DirectorySeparatorChar));
}

/// <summary>Einleser-Feld: <see cref="Body"/> = Inhalt des Blocks ohne die Schlüssel in <see cref="Except"/> als Text.</summary>
public sealed record ReadFieldDef(bool Body, IReadOnlyList<string> Except);

/// <summary>Einleser-Elemente (Spec Teil 4): jeder Block von <see cref="File"/>, dessen Schlüssel <see cref="Block"/> ganz trifft, legt ein Element von <see cref="Level"/> an.</summary>
public sealed record ReadElementsDef(string File, string Block, string Level, IReadOnlyDictionary<string, ReadFieldDef> Fields);

/// <summary>Lokalisierungsschlüssel (Regex mit <c>number</c>/<c>suffix</c>) und Ziel-Feld als <c>Ebene.Feld</c>.</summary>
public sealed record ReadLocalizationDef(string Key, string Field, string? Output = null);

/// <summary>Ja/Nein-Feld (<c>Ebene.Feld</c>): ja, wenn der Block <see cref="Contains"/> enthält.</summary>
public sealed record ReadFlagDef(string File, string Block, string Contains, string Field, string? Pattern = null);

/// <summary>Kopf-Feld eines Bilds: Eintrag der Asset-Bibliothek mit denselben Bytes wie <see cref="File"/> in der Mod.</summary>
public sealed record ReadAssetDef(string Field, string File);

/// <summary>Einleser eines Pakets (Abschnitt <c>read</c>): macht aus Mod-Dateien wieder einen Arbeitsstand. <see cref="Keys"/>: Regex auf relative Pfade mit Gruppe <c>key</c>.</summary>
/// <summary>
/// Gesammelte Werte (Spec 2026-10-06, Abschnitt 4): in dem Block von <see cref="File"/>, dessen Schlüssel <see cref="Block"/> ganz trifft, liefert jeder
/// Treffer von <see cref="Pattern"/> seine Gruppe <c>value</c>; ohne Doppelte mit <see cref="Join"/> verbunden in <see cref="Field"/> (Kopf-Feld oder <c>Ebene.Feld</c>).
/// </summary>
public sealed record ReadValueDef(string File, string Block, string Pattern, string Field, string Join, string? Body = null);

/// <summary>
/// Wert aus einer externen JSON-Datei (<c>read.json</c>): im Objekt unter <see cref="JsonPath"/> der Eintrag <see cref="EntryKey"/> (mit den Kopf-Werten
/// des Schlüssels gerendert), darin <see cref="Property"/>; ein Array wird mit <see cref="Join"/> verbunden, ein Text bleibt, wie er ist. Das Ergebnis
/// steht im Kopf-Feld <see cref="Field"/>. <see cref="External"/>: Kennung einer externen Datei des Pakets.
/// </summary>
public sealed record ReadJsonDef(string External, string JsonPath, string EntryKey, string Property, string Field, string Join);

/// <remarks>
/// <see cref="LocalizationOutput"/>: Kennung der Lokalisierungs-Ausgabe, aus deren Dateien gelesen wird (ohne Angabe die erste).
/// <see cref="KeysBlock"/>: Schlüssel aus obersten Blockschlüsseln der Dateien, auf die <see cref="Keys"/> passt (Gruppe <c>key</c> oder Gruppen nach den Schlüsselfeldern).
/// </remarks>
public sealed record ReadDef(
    string Keys,
    ReadElementsDef Elements,
    IReadOnlyList<ReadLocalizationDef> Localization,
    IReadOnlyList<ReadFlagDef> Flags,
    IReadOnlyList<ReadAssetDef> Assets,
    string? LocalizationOutput = null,
    string? KeysBlock = null,
    IReadOnlyList<ReadValueDef>? Values = null,
    IReadOnlyList<ReadJsonDef>? Json = null)
{
    /// <summary>Die Lokalisierungs-Ausgabe, aus der das Einlesen liest.</summary>
    public OutputDef? LocalizationFrom(GeneratorPackage package) =>
        package.Outputs.FirstOrDefault(o => o.Kind == OutputKind.Localization && (LocalizationOutput is null || o.Id == LocalizationOutput));

    /// <summary>Die Lokalisierungs-Ausgabe eines Eintrags: seine eigene <c>output</c>, sonst die des Abschnitts.</summary>
    public OutputDef? LocalizationFor(GeneratorPackage package, ReadLocalizationDef entry) =>
        entry.Output is { Length: > 0 } id
            ? package.Outputs.FirstOrDefault(o => o.Kind == OutputKind.Localization && o.Id == id)
            : LocalizationFrom(package);
}

/// <summary>
/// Kandidatenliste eines Pakets (Abschnitt <c>candidates</c>): Spiel-Modifikatoren der Quelle <see cref="Category"/> (Definitions-Kategorie und
/// Skriptdatenbank-Bereich), die in keinem der <see cref="Used"/>-Blöcke vorkommen, gruppiert nach <see cref="Groups"/>; <see cref="Create"/> legt daraus Arbeitsstände an.
/// </summary>
public sealed record CandidatesDef(string Category, IReadOnlyList<CandidateUsedDef> Used, IReadOnlyList<CandidateGroupDef> Groups, CandidateCreateDef Create);

/// <summary>Wo ein Modifikator schon verwendet wird: Dateien (<see cref="File"/>, Regex auf den relativen Pfad), darin oberste Blöcke (<see cref="Block"/>, Regex auf den ganzen Schlüssel); <see cref="Except"/>: Unterblöcke, die nicht zählen.</summary>
public sealed record CandidateUsedDef(string File, string Block, IReadOnlyList<string> Except);

/// <summary>Gruppe der Kandidatenliste: <see cref="Label"/> in der Oberflächensprache, <see cref="Pattern"/> (Regex) trifft den Modifikator-Namen; <see cref="Hidden"/>: standardmäßig ausgeblendet.</summary>
public sealed record CandidateGroupDef(string Id, string Label, string Pattern, bool Hidden);

/// <summary>Neue Arbeitsstände aus Kandidaten: <see cref="Ask"/> sind Kopf-Felder (Auswahl), die der Nutzer einmal wählt; <see cref="Name"/> das Kopf-Textfeld für den Namen; <see cref="Modifiers"/> = <c>Ebene.Feld</c> der Modifikatoren.</summary>
public sealed record CandidateCreateDef(IReadOnlyList<string> Ask, string Name, string Modifiers);
