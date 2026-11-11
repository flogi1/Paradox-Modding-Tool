using System.Collections.ObjectModel;

namespace ParadoxModTool.App;

/// <summary>
/// Reiter "Suche" im rechten Panel (Einstellung "Suche im rechten Panel anzeigen"): ein volles <see cref="SearchReplacePanel"/> (aktuelles Dokument
/// oder Ordner/Datei) mit eigener Trefferliste. Sucht über dieselben Delegates wie das Suchfenster im aktiven Arbeitsbereich (<see cref="ActivePane"/>,
/// das bei Fokus im Panel auf den zuletzt fokussierten Arbeitsbereich zurückfällt). Bei ausgeschalteter Option öffnen Strg+F, Menü und "In Pfad suchen"
/// wie bisher das Fenster.
/// </summary>
public partial class MainWindow
{
    private readonly SearchReplacePanel _searchTabPanel = new();

    /// <summary>Eigene Trefferliste des Reiters; die Ergebnisleiste im Hauptfenster (<see cref="_searchResults"/>) gehört nur zum Fenster.</summary>
    private readonly ObservableCollection<SearchResultRow> _searchTabResults = new();

    /// <summary>
    /// Aktives Dokument beim letzten "Alle finden"/"Alle ersetzen" im Modus "Aktuelles Dokument"; ein anderes aktives Dokument leert die Trefferliste.
    /// Nach einer Suche in Ordner oder Datei <see langword="null"/>: Ordner-Treffer tragen ihren Dateipfad und bleiben stehen.
    /// </summary>
    private object? _searchTabDocument;

    /// <summary>Die Treffer des Reiters stammen aus beiden Arbeitsbereichen (Option "Beide Arbeitsbereiche"); Fokuswechsel zwischen ihnen leert sie nicht.</summary>
    private bool _searchTabBoth;

    /// <summary>
    /// Einmal im Konstruktor, vor dem Laden der Einstellungen (ApplySetting setzt danach <see cref="NotesSidePanel.SearchTabEnabled"/>). Das Panel selbst
    /// wird erst in <see cref="InitializeSearchTabPanel"/> eingerichtet.
    /// </summary>
    private void InitializeSearchTab()
    {
        NotesPanelControl.SetSearchContent(_searchTabPanel);
        NotesPanelControl.SearchTabEnabled = _settings.SearchInSidePanel;

        // Dokumentwechsel: anderer Tab im Arbeitsbereich oder Fokus in den anderen Arbeitsbereich (Wechsel von _lastFocusedPane).
        // ActivePane folgt dem Tastaturfokus schon während GotKeyboardFocus, die Reihenfolge der Handler spielt daher keine Rolle.
        foreach (var pane in AllPanes)
        {
            pane.ActiveDocumentChanged += (_, _) => ClearSearchTabIfDocumentChanged();
            pane.GotKeyboardFocus += (_, _) => ClearSearchTabIfDocumentChanged();
        }
    }

    /// <summary>
    /// Richtet das Panel des Reiters ein, einmal in MainWindow_Loaded nach RestoreSession: erst dann sind das Spielprofil (feste Schnellpfad-Knöpfe)
    /// und der zuletzt durchsuchte Ordner bekannt.
    /// </summary>
    private void InitializeSearchTabPanel()
    {
        _searchTabPanel.Initialize(
            new SearchPanelContext
            {
                GetActiveDocumentText = GetActiveDocumentText,
                // ActivePane kann sich still ändern (Split-View aus); dann gehören die Treffer zu einem anderen Dokument und dürfen dort nichts markieren.
                SelectInActiveDocument = (offset, length) =>
                {
                    if (_searchTabDocument is not null && !_searchTabBoth && !ReferenceEquals(ActivePane.ActiveDocument, _searchTabDocument))
                    {
                        ClearSearchTabIfDocumentChanged();
                        return;
                    }

                    SelectInActiveDocument(offset, length);
                },
                ReplaceActiveDocumentText = ReplaceInActiveDocument,
                ExecuteFileReplacements = ExecuteFileReplacements,
                OpenSearchResult = OpenSearchResult,
                Results = _searchTabResults,
                GetHideDescription = GetHideSearchResultDescriptions,
                SetHideDescription = SetHideSearchResultDescriptions,
                ActiveProfile = _activeProfile,
                ResolveDefaultModRoot = () => ResolveDefaultModRoot(_activeProfile?.DefaultModRootTemplate),
                ResolveGameContentRoot = () => ComputeGameContentRoot(_gameExecutablePath),
                // Derselbe Merker wie beim Fenster: Reiter und Fenster suchen im zuletzt benutzten Ordner weiter.
                GetLastFolder = () => _searchLastFolder,
                SetLastFolder = folder => _searchLastFolder = folder,
                SearchPerformed = inDocument =>
                {
                    _searchTabDocument = inDocument ? ActivePane.ActiveDocument : null;
                    _searchTabBoth = false;
                },
                SearchedBothPanes = both => _searchTabBoth = both,
                GetBothDocuments = () => AllPanes
                    .Where(pane => pane.IsVisible && pane.ActiveDocument is not null)
                    .Select(pane => new SearchDocumentSource(
                        ReferenceEquals(pane, LeftEditor) ? SearchTexts.WorkspaceLeft : SearchTexts.WorkspaceRight,
                        () => pane.CurrentText,
                        pane.SelectRange))
                    .ToList(),
            });
    }

    /// <summary>Leert die Trefferliste des Reiters, wenn sie zu einem anderen als dem aktiven Dokument gehört; Suchtext und Optionen bleiben.</summary>
    private void ClearSearchTabIfDocumentChanged()
    {
        // Auch bei leerer Liste (0 Treffer, nach "Alle ersetzen"), damit der Status nicht die alte Zahl zeigt.
        if (_searchTabDocument is null || _searchTabBoth || ReferenceEquals(ActivePane.ActiveDocument, _searchTabDocument))
        {
            return;
        }

        // Steht der Reiter inzwischen auf "Ordner oder Datei" (Modus gewechselt, Ordnersuche abgebrochen oder ohne Ergebnis), passt der Hinweis
        // "Enter sucht neu" nicht: dann nur die Dokument-Treffer entfernen und den Status (z. B. "Abgebrochen.") stehen lassen.
        _searchTabPanel.ClearResults(_searchTabPanel.IsFilesMode ? null : SearchTexts.TabStale);
        // Kein geschlossenes Dokument festhalten; die nächste Suche merkt sich das dann aktive.
        _searchTabDocument = null;
    }

    /// <summary>
    /// Strg+F, Menü und Werkzeugleiste "Suchen &amp; Ersetzen": mit der Option den Reiter zeigen und den Cursor ins Suchfeld setzen
    /// (Editor-Markierung wird Suchtext), sonst wie bisher das Fenster.
    /// </summary>
    private void OpenSearch()
    {
        if (!_settings.SearchInSidePanel)
        {
            OpenSearchReplaceWindow();
            return;
        }

        _settings.NotesPanelVisible = true;
        NotesPanelControl.ShowSearchTab();

        // Läuft im Reiter gerade eine Ordner-Aufgabe, nur den Reiter zeigen: Suchtext und Modus passen sonst nicht mehr zu den Treffern.
        if (_searchTabPanel.IsBusy)
        {
            return;
        }

        // Strg+F im Reiter selbst setzt nur den Fokus neu; die Markierung im Editor würde sonst den gerade getippten Suchtext überschreiben.
        _searchTabPanel.FocusSearchBox(_searchTabPanel.IsKeyboardFocusWithin ? null : ActivePane.SelectedText);
    }

    /// <summary>
    /// "In Pfad suchen" (Lupe und Rechtsklick in der Verzeichnis-Übersicht): mit der Option der Reiter im Modus "Ordner oder Datei" mit diesem Pfad,
    /// sonst wie bisher das Fenster.
    /// </summary>
    private void OpenSearchInPath(string path)
    {
        if (!_settings.SearchInSidePanel)
        {
            OpenSearchReplaceWindow(path);
            return;
        }

        _settings.NotesPanelVisible = true;
        NotesPanelControl.ShowSearchTab();
        if (!_searchTabPanel.IsBusy)
        {
            _searchTabPanel.SearchInPath(path);
        }
    }
}
