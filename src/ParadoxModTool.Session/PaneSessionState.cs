namespace ParadoxModTool.Session;

/// <summary>Zustand eines einzelnen Arbeitsbereichs (Abschnitt 5, Split-View) innerhalb einer Session.</summary>
public sealed class PaneSessionState
{
    public List<DocumentSessionState> Documents { get; set; } = new();

    /// <summary>
    /// Index des zuletzt aktiven Tabs in <see cref="Documents"/>, oder -1 wenn keines aktiv war
    /// bzw. der Arbeitsbereich leer war.
    /// </summary>
    public int ActiveDocumentIndex { get; set; } = -1;

    /// <summary>Sortierung der Tabs dieses Arbeitsbereichs: <c>Manual</c> (Ziehen, Standard), <c>Name</c>, <c>Category</c> oder <c>Origin</c>.</summary>
    public string TabSort { get; set; } = "Manual";
}
