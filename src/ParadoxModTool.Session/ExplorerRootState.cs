namespace ParadoxModTool.Session;

/// <summary>
/// Eine gespeicherte Wurzel der Verzeichnis-Übersicht (Nutzer-Feedback: "eine Verzeichnis
/// Übersicht wie bei Notepad++ ... weitere Ordnerstrukturen ... bei Bedarf hinzufügen können").
/// Bewusst ein reines Datenmodell mit einem einfachen String-Schlüssel für die Art der Wurzel
/// (statt eines Verweises auf <c>ParadoxModTool.Explorer.ExplorerRootKind</c>), damit
/// ParadoxModTool.Session — wie schon bei <see cref="AppSessionState.CategoryColorOverrides"/>
/// (Enum-Name als String-Schlüssel für ParadoxModTool.Editor.FileCategory) — NICHT auf
/// ParadoxModTool.Explorer verweisen muss. MainWindow übersetzt beim Laden/Speichern zwischen
/// diesem String und dem tatsächlichen Enum.
/// </summary>
public sealed class ExplorerRootState
{
    public string Path { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;
}
