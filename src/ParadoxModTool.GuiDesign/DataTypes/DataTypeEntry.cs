namespace ParadoxModTool.GuiDesign.DataTypes;

/// <summary>Art eines Eintrags im Datentyp-Dump des Spiels (<c>Definition type:</c>).</summary>
public enum DataTypeKind
{
    Type,
    Promote,
    Function,
    GlobalPromote,
    GlobalFunction,
    GlobalMacro,
}

/// <summary>
/// Ein Eintrag aus <c>logs/data_types/data_types_*.txt</c>: ein Typ (<c>Country</c>), ein Promote oder eine Funktion eines Typs
/// (<c>Country.GetCapital</c>) oder ein globaler Eintrag (<c>GetPlayer</c>, <c>Not( Arg0 )</c>).
/// </summary>
/// <param name="Name">Name ohne Besitzer und Argumente (<c>GetCapital</c>); bei Typen der Typname.</param>
/// <param name="Owner">Besitzer-Typ bei Promotes und Funktionen, sonst leer.</param>
/// <param name="Arguments">Argumentnamen aus der Klammer (<c>Arg0</c>, <c>Index</c>).</param>
/// <param name="ReturnType">Rückgabetyp; leer bei Typen und Makros, <c>[unregistered]</c> bleibt als Text.</param>
/// <param name="Source">Dateiname, aus dem der Eintrag stammt.</param>
public sealed record DataTypeEntry(
    string Name,
    string Owner,
    DataTypeKind Kind,
    IReadOnlyList<string> Arguments,
    string ReturnType,
    string Description,
    string Source)
{
    public const string Unregistered = "[unregistered]";

    public bool IsGlobal => Owner.Length == 0 && Kind != DataTypeKind.Type;

    /// <summary><c>Owner.Name</c>, bei globalen Einträgen und Typen nur der Name.</summary>
    public string FullName => Owner.Length == 0 ? Name : Owner + "." + Name;

    /// <summary>Der Rückgabetyp ist bekannt (nicht leer und nicht <c>[unregistered]</c>).</summary>
    public bool HasRegisteredReturn => ReturnType.Length > 0 && ReturnType != Unregistered;
}
