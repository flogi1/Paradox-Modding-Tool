namespace ParadoxModTool.Compatch;

public enum AddressKind
{
    /// <summary>Ein Schlüssel innerhalb eines Bereichs der Ordnerregel.</summary>
    Key,

    /// <summary>Alles einer Datei, was zu keinem Schlüssel gehört (Konstanten, GUI-Fenster, namespace). Nur bei gleichem Pfad in A und B.</summary>
    FileRest,

    /// <summary>Eine Datei, die nur als Ganzes gewählt werden kann (Nicht-Skript, nicht lesbar, Ordnerregel WholeFile).</summary>
    WholeFile,
}

public enum MergeSide
{
    Vanilla,
    A,
    B,
}

/// <summary>
/// Eine Stelle, die der Merge als Einheit behandelt. <see cref="Scope"/> ist bei <see cref="AddressKind.Key"/> der Bereich der Ordnerregel
/// (z. B. <c>in_game/common/building_types</c>), sonst der relative Dateipfad. Pfade immer klein, mit <c>/</c>. Schlüssel unverändert.
/// </summary>
public sealed record EntryAddress(AddressKind Kind, string Scope, string Key)
{
    public static EntryAddress ForKey(string scope, string key) => new(AddressKind.Key, Norm(scope), key);

    public static EntryAddress ForFileRest(string relativePath) => new(AddressKind.FileRest, Norm(relativePath), string.Empty);

    public static EntryAddress ForWholeFile(string relativePath) => new(AddressKind.WholeFile, Norm(relativePath), string.Empty);

    public string Id => Kind switch
    {
        AddressKind.Key => Scope + "|" + Key,
        AddressKind.FileRest => Scope + "|#rest",
        _ => Scope + "|#file",
    };

    public override string ToString() => Id;

    internal static string Norm(string path) => path.Replace('\\', '/').Trim('/').ToLowerInvariant();
}
