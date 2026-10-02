namespace ParadoxModTool.Explorer;

/// <summary>
/// Verweisdateien der alten Workshop-Anbindung im Mod-Ordner der Spiele (<c>ugc_1385440355.mod</c>). Der Launcher legt sie an und
/// man kann sie nicht dauerhaft löschen; beim Modden liest oder ändert man sie nie selbst, deshalb blendet die Übersicht sie aus.
/// </summary>
public static class UgcReference
{
    /// <summary>Dateiname beginnt mit <c>ugc_</c> und endet auf <c>.mod</c> (Groß-/Kleinschreibung egal).</summary>
    public static bool IsMatch(string fileName) =>
        fileName.StartsWith("ugc_", StringComparison.OrdinalIgnoreCase)
        && fileName.EndsWith(".mod", StringComparison.OrdinalIgnoreCase);
}
