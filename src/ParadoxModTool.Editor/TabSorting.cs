using ParadoxModTool.Categories;

namespace ParadoxModTool.Editor;

/// <summary>
/// Sortierung der Tabs eines Arbeitsbereichs (Interface-Rework 2): "Manuell" ist die Reihenfolge des Nutzers (Ziehen), sonst wird nach Name,
/// Kategorie oder Herkunft sortiert. Reine Funktionen; die Werte stehen so in der Sitzung (<c>PaneSessionState.TabSort</c>).
/// </summary>
public static class TabSorting
{
    public const string Manual = "Manual";
    public const string Name = "Name";
    public const string Category = "Category";
    public const string Origin = "Origin";

    private static readonly string[] Known = { Manual, Name, Category, Origin };

    /// <summary>Bekannter Wert (genau, Groß-/Kleinschreibung zählt), sonst <see cref="Manual"/>.</summary>
    public static string Normalize(string? sort) => Known.Contains(sort, StringComparer.Ordinal) ? sort! : Manual;

    /// <summary>Stabil sortierte Kopie; bei <see cref="Manual"/> unverändert.</summary>
    public static IReadOnlyList<OpenDocument> Sorted(IEnumerable<OpenDocument> documents, string sort)
    {
        var list = documents.ToList();
        return Normalize(sort) == Manual ? list : list.OrderBy(d => d, Comparer(sort)).ToList();
    }

    /// <summary>
    /// Position, an der <paramref name="added"/> in die (bereits sortierte) Liste gehört: vor dem ersten Dokument, das nach ihm sortiert;
    /// gleiche Schlüssel kommen dahinter. Bei <see cref="Manual"/> ans Ende.
    /// </summary>
    public static int InsertIndex(IReadOnlyList<OpenDocument> documents, OpenDocument added, string sort)
    {
        if (Normalize(sort) == Manual)
        {
            return documents.Count;
        }

        var comparer = Comparer(sort);
        for (var i = 0; i < documents.Count; i++)
        {
            if (comparer.Compare(added, documents[i]) < 0)
            {
                return i;
            }
        }

        return documents.Count;
    }

    private static IComparer<OpenDocument> Comparer(string sort) => Normalize(sort) switch
    {
        Category => System.Collections.Generic.Comparer<OpenDocument>.Create((a, b) =>
            CompareText(a.CategoryDisplayName, b.CategoryDisplayName) is var byCategory && byCategory != 0 ? byCategory : CompareText(a.DisplayName, b.DisplayName)),
        Origin => System.Collections.Generic.Comparer<OpenDocument>.Create((a, b) =>
            Rank(a.Origin).CompareTo(Rank(b.Origin)) is var byOrigin && byOrigin != 0 ? byOrigin : CompareText(a.DisplayName, b.DisplayName)),
        _ => System.Collections.Generic.Comparer<OpenDocument>.Create((a, b) => CompareText(a.DisplayName, b.DisplayName)),
    };

    private static int CompareText(string a, string b) => StringComparer.CurrentCultureIgnoreCase.Compare(a, b);

    private static int Rank(FileOrigin origin) => origin switch
    {
        FileOrigin.Vanilla => 0,
        FileOrigin.Mod => 1,
        _ => 2,
    };
}
