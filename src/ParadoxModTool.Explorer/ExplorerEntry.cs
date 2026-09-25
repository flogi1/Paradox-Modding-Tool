using System.ComponentModel;

namespace ParadoxModTool.Explorer;

/// <summary>Art einer obersten Wurzel in der Verzeichnis-Übersicht (Nutzer-Feedback Runde 9/10:
/// "eine Verzeichnis Übersicht wie bei Notepad++ ... Ordnerstruktur der Spieldateien und der
/// Dateien, die man gerade moddet ... weitere Ordnerstrukturen ... bei Bedarf hinzufügen").</summary>
public enum ExplorerRootKind
{
    /// <summary>Automatisch anhand von <c>GameProfile.DefaultModRootTemplate</c> vorgeschlagene
    /// Mod-Wurzel (enthält alle lokal installierten Mods, nicht nur den gerade bearbeiteten).</summary>
    ModFolder,

    /// <summary>Vom Nutzer manuell hinzugefügte Spieldateien-Wurzel.</summary>
    GameFiles,

    /// <summary>Jede weitere vom Nutzer frei hinzugefügte Ordnerstruktur.</summary>
    Custom,
}

/// <summary>
/// Ein Knoten in der Verzeichnis-Übersicht — entweder eine der obersten Wurzeln
/// (<see cref="IsRoot"/> = <see langword="true"/>) oder ein Ordner/eine Datei darunter. Bewusst
/// EIN gemeinsamer Knotentyp (statt getrennter Ordner-/Datei-/Wurzel-Klassen), damit ein
/// einziges <c>HierarchicalDataTemplate</c> in <c>ExplorerPanel.xaml</c> für alle Ebenen reicht.
/// Implementiert <see cref="INotifyPropertyChanged"/> nach demselben Muster wie
/// <c>ParadoxModTool.Editor.OpenDocument</c>, da sich Farbe/Favorit-Status eines bereits
/// angezeigten Knotens zur Laufzeit ändern können, ohne dass der Baum neu aufgebaut wird.
/// </summary>
public sealed class ExplorerEntry : INotifyPropertyChanged
{
    private string? _colorHex;
    private bool _isFavorite;
    private bool _childrenLoaded;
    private bool _isExpanded;
    private string? _lockTooltip;

    public required string FullPath { get; init; }

    public required string Name { get; set; }

    public required bool IsDirectory { get; init; }

    public bool IsRoot { get; init; }

    public ExplorerRootKind RootKind { get; init; }

    /// <summary>
    /// Kinder dieses Knotens (nur für Ordner relevant). Enthält bis zum ersten Aufklappen einen
    /// einzelnen Platzhalter-Eintrag (<see cref="IsPlaceholder"/>), damit der Aufklapp-Pfeil
    /// angezeigt wird, ohne den Ordner schon beim Erzeugen des übergeordneten Knotens
    /// einzulesen — wichtig für die Performance bei sehr vielen Dateien (siehe
    /// Architektur-Dokumentation, "Performance-Dimensionierung": ~700+ Dateien sind keine
    /// Seltenheit) UND weil eine Spieldateien-Wurzel leicht mehrere zehntausend Dateien enthält.
    /// </summary>
    public System.Collections.ObjectModel.ObservableCollection<ExplorerEntry> Children { get; } = new();

    /// <summary>Reiner Platzhalter-Knoten, der beim Aufklappen durch die echten Kinder ersetzt
    /// wird (siehe <see cref="Children"/>) — kein echter Dateisystem-Eintrag.</summary>
    public bool IsPlaceholder { get; init; }

    public string? ColorHex
    {
        get => _colorHex;
        set
        {
            if (_colorHex != value)
            {
                _colorHex = value;
                OnPropertyChanged(nameof(ColorHex));
            }
        }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                OnPropertyChanged(nameof(IsFavorite));
            }
        }
    }

    /// <summary>Wurde <see cref="Children"/> bereits per <c>FolderScanner</c> befüllt (statt nur
    /// den Platzhalter zu enthalten)? Von <c>ExplorerPanel</c> beim Aufklappen geprüft.</summary>
    public bool ChildrenLoaded
    {
        get => _childrenLoaded;
        set => _childrenLoaded = value;
    }

    /// <summary>Ist der Knoten im Baum aufgeklappt? Zweiseitig an den <c>TreeViewItem</c> gebunden, damit gemerkte Ordner nach einem Neustart aufgeklappt erscheinen.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }
    }

    /// <summary>Tooltip des Schloss-Symbols; <see langword="null"/> = kein Symbol. Der Explorer kennt keine Mods: das Hauptfenster liefert den Text (siehe <c>ExplorerPanel.LockMarkProvider</c>).</summary>
    public string? LockTooltip
    {
        get => _lockTooltip;
        set
        {
            if (_lockTooltip != value)
            {
                _lockTooltip = value;
                OnPropertyChanged(nameof(LockTooltip));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
