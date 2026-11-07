using System.Collections.ObjectModel;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.SearchReplace;

namespace ParadoxModTool.App;

/// <summary>
/// Alles, was ein <see cref="SearchReplacePanel"/> vom Gastgeber braucht (Suchfenster oder Reiter im rechten Panel). Die Namen
/// entsprechen den früheren Konstruktorparametern von <see cref="SearchReplaceWindow"/>; das Panel kennt dadurch weder EditorPane
/// noch ActionHistory, sondern arbeitet nur über diese Delegates.
/// </summary>
public sealed record SearchPanelContext
{
    /// <summary>Text des aktiven Dokuments (Modus "Aktuelles Dokument").</summary>
    public required Func<string> GetActiveDocumentText { get; init; }

    /// <summary>Markiert einen Treffer (Offset, Länge) im aktiven Dokument.</summary>
    public required Action<int, int> SelectInActiveDocument { get; init; }

    /// <summary>Ersetzt den gesamten Text des aktiven Dokuments (ein Schritt im Aktionsverlauf).</summary>
    public required Action<string> ReplaceActiveDocumentText { get; init; }

    /// <summary>Schreibt die Ersetzungspläne über das Hauptfenster (ein Schritt im Aktionsverlauf) und liefert die Zahl geänderter Dateien.</summary>
    public required Func<IReadOnlyList<FileReplacementPlan>, string, int> ExecuteFileReplacements { get; init; }

    /// <summary>Öffnet einen Datei-Treffer (Pfad, Offset, Länge).</summary>
    public required Action<string, int, int> OpenSearchResult { get; init; }

    /// <summary>Trefferliste; beim Fenster des Hauptfensters mit der Ergebnisleiste geteilt.</summary>
    public required ObservableCollection<SearchResultRow> Results { get; init; }

    /// <summary>Globaler Zustand "Beschreibung ausblenden".</summary>
    public required Func<bool> GetHideDescription { get; init; }

    public required Action<bool> SetHideDescription { get; init; }

    /// <summary>Aktives Spielprofil für die festen Schnellzugriff-Buttons (nur im vollen Modus).</summary>
    public GameProfile? ActiveProfile { get; init; }

    public required Func<string?> ResolveDefaultModRoot { get; init; }

    public required Func<string?> ResolveGameContentRoot { get; init; }

    /// <summary>Zuletzt durchsuchter Ordner (Sitzungszustand im Hauptfenster); beide null = nichts merken.</summary>
    public Func<string?>? GetLastFolder { get; init; }

    public Action<string>? SetLastFolder { get; init; }

    /// <summary>
    /// Nach "Alle finden"/"Alle ersetzen"; der Wert sagt, ob im Modus "Aktuelles Dokument" gesucht wurde (<see langword="true"/>) oder in
    /// Ordner oder Datei. Der Reiter merkt sich damit das durchsuchte Dokument; Ordner-Treffer hängen an keinem Dokument.
    /// </summary>
    public Action<bool>? SearchPerformed { get; init; }
}
