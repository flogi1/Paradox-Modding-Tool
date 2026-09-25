namespace ParadoxModTool.Explorer;

/// <summary>
/// Beschreibt eine Wurzel der Verzeichnis-Übersicht von AUSSEN (zum Hinzufügen über
/// <c>ExplorerPanel.AddRoot</c> sowie zum Wiederherstellen/Speichern der Session über
/// <c>ExplorerPanel.CaptureRoots</c>/<c>SetRoots</c>) — bewusst getrennt vom internen
/// Baum-Knotentyp <see cref="ExplorerEntry"/>, der zusätzlich Bindungs-/Anzeigezustand
/// (Kinder, Platzhalter, INotifyPropertyChanged) trägt, den die Session nicht braucht.
/// </summary>
public sealed record ExplorerRootDescriptor(string FullPath, string DisplayName, ExplorerRootKind Kind);
