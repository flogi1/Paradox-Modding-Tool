using System.Text.Json.Serialization;

namespace ParadoxModTool.Patches;

/// <summary>Art einer eigenen Tabellenspalte.</summary>
public enum ColumnType
{
    Text,
    Number,

    /// <summary>Kommagetrennte Werte; in Pfaden und Vorlagen wird je Wert eine Instanz erzeugt.</summary>
    List,
}

/// <summary>Woher eine abgeleitete Spalte nachschlägt.</summary>
public enum LookupSource
{
    Vanilla,
    Mod,

    /// <summary>Die Mod-Datei, wo es sie gibt, sonst die Vanilla-Datei desselben Pfads.</summary>
    ModOverVanilla,
}

/// <summary>Welcher Schlüssel bei einem Treffer der Nachschlage-Regel genommen wird.</summary>
public enum LookupTake
{
    /// <summary>Schlüssel des äußersten umgebenden Blocks (z. B. die Provinz-ID einer History-Datei).</summary>
    TopLevelKey,

    /// <summary>Schlüssel des innersten umgebenden Blocks (z. B. <c>b_santiago</c>).</summary>
    EnclosingKey,
}

/// <summary>Nachschlage-Regel einer abgeleiteten Spalte: in Dateien <see cref="Files"/> die Anweisung <see cref="Match"/> suchen.</summary>
public sealed class DerivedLookup
{
    /// <summary>Dateimuster relativ zur Spielwurzel, z. B. <c>history/provinces/*.txt</c>.</summary>
    public string Files { get; set; } = string.Empty;

    public LookupSource Source { get; set; } = LookupSource.Vanilla;

    /// <summary>Anweisung der Form <c>schlüssel = wert</c> mit Platzhaltern, z. B. <c>special_building_slot = {key}</c>.</summary>
    public string Match { get; set; } = string.Empty;

    public LookupTake Take { get; set; } = LookupTake.TopLevelKey;

    /// <summary>Nicht gefunden ist ein leerer Wert statt eines Zeilenfehlers.</summary>
    public bool Optional { get; set; }

    /// <summary>Alle Treffer statt des ersten: der Wert ist eine Liste ("a, b, c") und die Spalte zählt als Listen-Spalte.</summary>
    public bool All { get; set; }
}

/// <summary>Eine Tabellenspalte: eigene Spalte mit Standardwert oder abgeleitete Spalte (<see cref="Lookup"/> gesetzt).</summary>
public sealed class PatchColumn
{
    public string Name { get; set; } = string.Empty;

    public ColumnType Type { get; set; } = ColumnType.Text;

    public string Default { get; set; } = string.Empty;

    public DerivedLookup? Lookup { get; set; }

    /// <summary>Der Wert ist ein Titel und wird gegen <c>common/landed_titles</c> geprüft.</summary>
    public bool TitleKey { get; set; }
}

/// <summary>Ein Tabelleneintrag; <see cref="Values"/> enthält nur die eigenen Spalten (leer = Standardwert).</summary>
public sealed class PatchRow
{
    public string Key { get; set; } = string.Empty;

    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);
}

public enum RuleScope
{
    PerRow,
    Global,
}

/// <summary>Gemeinsame Felder aller Regelarten.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LineRule), "lines")]
[JsonDerivedType(typeof(BlockCopyRule), "blockCopy")]
[JsonDerivedType(typeof(TemplateRule), "template")]
[JsonDerivedType(typeof(ListEntryRule), "listEntry")]
[JsonDerivedType(typeof(BlockEditRule), "blockEdit")]
public abstract class PatchRule
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public RuleScope Scope { get; set; } = RuleScope.PerRow;
}

public enum MatchKind
{
    /// <summary>Leerraum-tolerant, nur ganze Wörter.</summary>
    Text,
    Regex,
}

public enum LineAction
{
    Comment,
    Delete,
    Replace,
    RemoveBlock,
}

/// <summary>Zeilen-Regel: trifft Zeilen in Dateien und kommentiert, löscht oder ersetzt sie bzw. entfernt ganze Blöcke.</summary>
public sealed class LineRule : PatchRule
{
    public string Files { get; set; } = string.Empty;

    public string Match { get; set; } = string.Empty;

    public MatchKind MatchKind { get; set; } = MatchKind.Text;

    public LineAction Action { get; set; } = LineAction.Comment;

    public string Replacement { get; set; } = string.Empty;

    /// <summary>Bei <see cref="LineAction.RemoveBlock"/>: auch die über <c>next_building</c> erreichten Blöcke entfernen.</summary>
    public bool FollowNext { get; set; }

    /// <summary>Fehlt die Mod-Kopie einer passenden Vanilla-Datei, wird sie vorher angelegt.</summary>
    public bool CreateCopy { get; set; }

    /// <summary>Nur Mod-Dateien, die es unter demselben Pfad auch in Vanilla gibt (Mod-Kopien); eigene Dateien der Mod bleiben unberührt.</summary>
    public bool VanillaPathsOnly { get; set; }
}

public enum BlockEditKind
{
    Remove,
    InsertInto,
    InsertAfter,
    InsertAtStart,
    InsertAtEnd,
}

/// <summary>Eine Bearbeitung eines kopierten Vanilla-Blocks.</summary>
public sealed class BlockEdit
{
    /// <summary>Ausgeschaltete Bearbeitungen bleiben im Rezept stehen, laufen aber nicht (ältere Rezepte ohne dieses Feld: eingeschaltet).</summary>
    public bool Enabled { get; set; } = true;

    public BlockEditKind Kind { get; set; }

    public string Key { get; set; } = string.Empty;

    /// <summary>Bei <see cref="BlockEditKind.Remove"/>: nur Anweisungen mit diesem Wert entfernen.</summary>
    public string? Value { get; set; }

    public string Text { get; set; } = string.Empty;
}

/// <summary>Block-Kopie: kopiert den Vanilla-Block <see cref="BlockKey"/>, bearbeitet ihn und schreibt ihn in den erzeugten Bereich von <see cref="Target"/>.</summary>
public sealed class BlockCopyRule : PatchRule
{
    public string SourceFiles { get; set; } = string.Empty;

    public string BlockKey { get; set; } = "{key}";

    public bool FollowNext { get; set; }

    public List<BlockEdit> Edits { get; set; } = new();

    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Ist ein Schlüssel schon anderswo in der Mod definiert, wird dieser Block (bearbeitet) in den erzeugten Bereich übernommen und aus seiner Datei
    /// entfernt, statt die Zeile mit "bereits definiert" zu überspringen.
    /// </summary>
    public bool AdoptExisting { get; set; }
}

/// <summary>
/// Block bearbeiten: wendet <see cref="Edits"/> direkt auf den Block <see cref="BlockKey"/> in den Dateien <see cref="Files"/> an (ohne Kopie und ohne erzeugten Bereich).
/// Steht der Block nur in Vanilla, legt <see cref="CreateCopy"/> vorher die Mod-Kopie der Datei an.
/// </summary>
public sealed class BlockEditRule : PatchRule
{
    public string Files { get; set; } = string.Empty;

    public string BlockKey { get; set; } = "{key}";

    /// <summary>Auch die über <c>next_building</c> erreichten Folgestufen bearbeiten.</summary>
    public bool FollowNext { get; set; }

    /// <summary>Fehlt die Mod-Kopie der Vanilla-Datei mit dem Block, wird sie vorher angelegt.</summary>
    public bool CreateCopy { get; set; }

    public List<BlockEdit> Edits { get; set; } = new();
}

/// <summary>Vorlage pro Zeile: ein Textbaustein je Tabellenzeile im erzeugten Bereich von <see cref="Target"/>.</summary>
public sealed class TemplateRule : PatchRule
{
    public string Target { get; set; } = string.Empty;

    /// <summary>Optionaler Blockpfad (<c>a/b</c>), in dessen Ende der Bereich liegt.</summary>
    public string? Inside { get; set; }

    public string Text { get; set; } = string.Empty;
}

/// <summary>Listen-Eintrag: fügt je Zeile einen Eintrag in die Liste unter <see cref="ListPath"/> ein.</summary>
public sealed class ListEntryRule : PatchRule
{
    public string Target { get; set; } = string.Empty;

    public string ListPath { get; set; } = string.Empty;

    public string Entry { get; set; } = string.Empty;

    public bool CreateCopy { get; set; }
}

public sealed class IgnoredWatchFile
{
    public string Path { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;
}

/// <summary>Suchmuster und Ignorierliste des Wächters (Etappe 3).</summary>
public sealed class PatchWatch
{
    public List<string> Patterns { get; set; } = new();

    public List<IgnoredWatchFile> Ignored { get; set; } = new();
}

/// <summary>Ein Patch-Rezept einer Mod: Tabelle plus geordnete Regeln.</summary>
public sealed class PatchRecipe
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string GameId { get; set; } = string.Empty;

    public List<PatchColumn> Columns { get; set; } = new();

    public List<PatchRow> Rows { get; set; } = new();

    public List<PatchRule> Rules { get; set; } = new();

    public PatchWatch Watch { get; set; } = new();

    /// <summary>
    /// "Zeilen ganz oder gar nicht": überspringt eine Regel eine Zeile, lassen alle Regeln sie aus, damit keine Zeile halb umgebaut wird
    /// (z. B. Slots auskommentiert und Holdings eingetragen, aber das Gebäude nicht umgebaut).
    /// </summary>
    public bool WholeRows { get; set; }

    /// <summary>Vorlage, aus der das Rezept entstand (z. B. <c>ck3-special-buildings</c>); null bei einem leeren Rezept.</summary>
    public string? Preset { get; set; }

    /// <summary>Nur für die Einzel-Vorschau ("Ergebnis zeigen"): nur diese Zeile läuft, die übrigen gelten als übersprungen. Wird nie gespeichert.</summary>
    [JsonIgnore]
    public string? OnlyRowKey { get; set; }
}
