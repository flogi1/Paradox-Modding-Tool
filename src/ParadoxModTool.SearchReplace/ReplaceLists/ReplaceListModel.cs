namespace ParadoxModTool.SearchReplace.ReplaceLists;

/// <summary>Wie <see cref="ReplaceRule.Search"/> gelesen wird.</summary>
public enum RuleMode
{
    /// <summary>Ganzes Wort (Buchstaben, Ziffern, _ und innen . : -), Standard und Verhalten aller älteren Listen.</summary>
    Word,

    /// <summary>Beliebiger Suchtext: überall, ohne Wortgrenzen; Leerraum im Suchtext passt auf jeden Leerraum (auch Zeilenumbruch).</summary>
    Text,

    /// <summary>Wie <see cref="Text"/>, dazu steht <c>*</c> für genau ein Wort oder einen Wert; <c>$1</c>, <c>$2</c> … in <see cref="ReplaceRule.Replace"/> geben ihn wieder.</summary>
    Pattern,
}

/// <summary>Eine Regel einer Ersetzungsliste: <see cref="Search"/> (gelesen nach <see cref="Mode"/>) wird durch beliebigen Text <see cref="Replace"/> ersetzt.</summary>
public sealed class ReplaceRule
{
    public string Search { get; set; } = string.Empty;

    public string Replace { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public RuleMode Mode { get; set; } = RuleMode.Word;

    /// <summary>
    /// Sicher auskommentieren: Beginnt <see cref="Replace"/> mit <c>#</c> und steht nach dem Treffer noch Code in der Zeile (z. B. die schließende Klammer),
    /// kommt der kommentierte Text auf eine eigene Zeile, damit der Rest aktiv bleibt.
    /// </summary>
    public bool SafeComment { get; set; }
}

/// <summary>Benannte Ersetzungsliste einer Mod (Spec 2026-09-30-replace-lists): Zielordner relativ zur Mod, Regeln in Reihenfolge.</summary>
public sealed class ReplaceList
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Relativ zum Mod-Ordner, mit "/" getrennt; leer = Mod-Ordner selbst.</summary>
    public string TargetFolder { get; set; } = string.Empty;

    public List<ReplaceRule> Rules { get; set; } = new();
}

/// <summary>Alle Ersetzungslisten einer Mod, so wie sie gespeichert werden.</summary>
public sealed class ReplaceListSet
{
    public int Version { get; set; } = 1;

    public List<ReplaceList> Lists { get; set; } = new();
}

public enum RuleProblem
{
    None,
    EmptySearch,
    NotAWord,
    Duplicate,

    /// <summary>Suchtext der Art Text oder Platzhalter über mehrere Zeilen (er bleibt einzeilig, der Leerraum darf im Treffer Zeilen umbrechen).</summary>
    Multiline,

    /// <summary>"Ersetzen durch" nennt ein <c>$n</c>, zu dem es kein <c>*</c> im Suchtext gibt.</summary>
    BadCapture,
}
