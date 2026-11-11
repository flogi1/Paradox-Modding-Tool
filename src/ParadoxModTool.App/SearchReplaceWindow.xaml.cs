using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.Localization;
using ParadoxModTool.SearchReplace;

namespace ParadoxModTool.App;

/// <summary>
/// Vereinheitlichtes Suchen-&amp;-Ersetzen-Fenster (Abschnitt 5b): "Suchen und Ersetzen und
/// Suchen in Dateien kann in einem Funktionskasten vereinheitlicht werden [...] es wäre gut,
/// wenn es einfach ist zwischen den Modi hin und her zu wechseln." Der Inhalt samt aller Such- und
/// Ersetz-Logik liegt in <see cref="SearchReplacePanel"/> (voller Modus); das Fenster baut dafür den
/// <see cref="SearchPanelContext"/> aus den vom Hauptfenster übergebenen Delegates und kümmert sich nur noch
/// um Fensterdinge: Tab-Moduswechsel, Aktivieren, Fokus beim Öffnen und Rückgabe des Fokus an den Besitzer.
/// </summary>
public partial class SearchReplaceWindow : Window
{
    public SearchReplaceWindow(
        Func<string> getActiveDocumentText,
        Action<int, int> selectInActiveDocument,
        Action<string> replaceActiveDocumentText,
        Func<IReadOnlyList<FileReplacementPlan>, string, int> executeFileReplacements,
        Action<string, int, int> openSearchResult,
        ObservableCollection<SearchResultRow> sharedResults,
        Func<bool> getHideDescription,
        Action<bool> setHideDescription,
        GameProfile? activeProfile,
        Func<string?> resolveDefaultModRoot,
        Func<string?> resolveGameContentRoot,
        Func<string?>? getLastFolder = null,
        Action<string>? setLastFolder = null,
        Func<IReadOnlyList<SearchDocumentSource>>? getBothDocuments = null)
    {
        InitializeComponent();
        Title = SearchTexts.Title;
        Loaded += (_, _) => FocusSearchBox();
        Closing += (_, _) =>
        {
            Panel.RememberFolder();
            ReturnFocusToOwner();
        };

        Panel.Initialize(
            new SearchPanelContext
            {
                GetActiveDocumentText = getActiveDocumentText,
                SelectInActiveDocument = selectInActiveDocument,
                ReplaceActiveDocumentText = replaceActiveDocumentText,
                ExecuteFileReplacements = executeFileReplacements,
                OpenSearchResult = openSearchResult,
                Results = sharedResults,
                GetHideDescription = getHideDescription,
                SetHideDescription = setHideDescription,
                ActiveProfile = activeProfile,
                ResolveDefaultModRoot = resolveDefaultModRoot,
                ResolveGameContentRoot = resolveGameContentRoot,
                GetLastFolder = getLastFolder,
                SetLastFolder = setLastFolder,
                GetBothDocuments = getBothDocuments,
            });
    }

    /// <summary>
    /// Aktiviert das Fenster und setzt den Cursor in das Suchfeld (Nutzer-Feedback: nach Strg+F sofort tippen können). Das Aktivieren läuft wie
    /// früher über den Dispatcher und wird VOR dem Fokussieren des Panels eingereiht (gleiche Priorität, Reihenfolge bleibt erhalten).
    /// </summary>
    public void FocusSearchBox(string? selectedText = null)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => Activate());
        Panel.FocusSearchBox(selectedText);
    }

    /// <summary>Stellt die Suche auf "Ordner oder Datei" mit diesem Pfad ein und aktiviert das Fenster (siehe <see cref="SearchReplacePanel.SearchInPath"/>).</summary>
    public void SearchInPath(string path)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => Activate());
        Panel.SearchInPath(path);
    }

    /// <summary>Inhalt des Suchfelds (Reiter "Suche": "Im Fenster öffnen" übernimmt den Suchtext).</summary>
    public string SearchText
    {
        get => Panel.SearchText;
        set => Panel.SearchText = value;
    }

    /// <summary>Vom Hauptfenster aufgerufen, um den Checkbox-Zustand "Beschreibung ausblenden" zu spiegeln.</summary>
    public void SetHideDescriptionCheckboxState(bool hide) => Panel.SetHideDescriptionCheckboxState(hide);

    /// <summary>
    /// Holt beim Schließen das Besitzerfenster nach vorn (Nutzer-Feedback: beim Schließen wurde das Hauptfenster manchmal
    /// minimiert). Windows aktiviert nach dem Schließen sonst das nächste Fenster in der Z-Reihenfolge, das nach einem
    /// Ordnerdialog oder einem Klick in ein anderes Programm nicht mehr das Hauptfenster ist.
    /// </summary>
    private void ReturnFocusToOwner()
    {
        // Schließt das Hauptfenster selbst (und damit dieses Fenster), ist es schon unsichtbar: dann nichts tun.
        if (Owner is not { IsVisible: true } owner)
        {
            return;
        }

        try
        {
            if (owner.WindowState == WindowState.Minimized)
            {
                owner.WindowState = WindowState.Normal;
            }

            owner.Activate();
        }
        catch (InvalidOperationException)
        {
            // Besitzer wird gerade geschlossen.
        }
    }

    /// <summary>
    /// Tab (und Umschalt+Tab) wechselt zwischen "Aktuelles Dokument" und "Ordner oder Datei", hin und zurück. Im Suchfeld und im Ersetzen-Feld
    /// bleibt Tab ein Tab-Zeichen (eingerückten Code von Hand tippen); dort greift der Wechsel nicht.
    /// </summary>
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Tab || Panel.IsBusy || Keyboard.Modifiers is not (ModifierKeys.None or ModifierKeys.Shift) || Keyboard.FocusedElement is System.Windows.Controls.TextBox { AcceptsTab: true })
        {
            return;
        }

        e.Handled = true;
        Panel.ToggleMode(Keyboard.FocusedElement as DependencyObject);
    }
}

/// <summary>
/// Eine Zeile in der Trefferliste — sowohl für Dokument- als auch Dateisuche/-ersetzung
/// verwendet, UND von zwei Anzeigen gleichzeitig gebunden (dem modalen Suchen-Fenster sowie der
/// Ergebnisleiste im Hauptfenster, siehe <see cref="SearchReplaceWindow"/>). Implementiert
/// <see cref="INotifyPropertyChanged"/>, damit das Umschalten der "Nur gefundenen Code anzeigen"-
/// Option (Nutzer-Feedback) bereits vorhandene Zeilen live aktualisiert, statt die komplette
/// Liste neu aufbauen zu müssen.
/// </summary>
public sealed class SearchResultRow : INotifyPropertyChanged
{
    private bool _hideDescription;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? FilePath { get; private init; }

    public int LineNumber { get; private init; }

    public string LineText { get; private init; } = string.Empty;

    /// <summary>
    /// Der exakt gefundene Code (siehe <see cref="TextMatch.MatchedText"/>) — Nutzer-Feedback:
    /// "nur den Code, der tatsächlich gefunden wurde, anzeigen ... damit man es rauskopieren und
    /// verwenden kann". Bei einer reinen Ersetzungs-Zusammenfassung (<see cref="FromReplacementPlan"/>)
    /// gibt es keinen einzelnen gefundenen Text, daher dort leer.
    /// </summary>
    public string MatchedText { get; private init; } = string.Empty;

    public int Offset { get; private init; }

    public int Length { get; private init; }

    /// <summary>Bei Treffern aus "Beide Arbeitsbereiche": Name des Arbeitsbereichs; sonst leer.</summary>
    public string DocumentLabel { get; private init; } = string.Empty;

    /// <summary>Bei Treffern aus "Beide Arbeitsbereiche": springt in dem Dokument, in dem der Treffer liegt; sonst <see langword="null"/>.</summary>
    public Action<int, int>? SelectAction { get; private init; }

    /// <summary>Die beschreibende Zeile ("Datei — Zeile N: ..."), ausblendbar über <see cref="HideDescription"/>.</summary>
    public string Description => FilePath is null
        ? (DocumentLabel.Length > 0 ? DocumentLabel + " — " : string.Empty) + SearchTexts.ResultLine(LineNumber, LineText)
        : LineNumber > 0
            ? SearchTexts.ResultFileLine(Path.GetFileName(FilePath), LineNumber, LineText)
            : $"{Path.GetFileName(FilePath)} — {LineText}";

    /// <summary>Ob die Beschreibungszeile ausgeblendet ist (Nutzer-Feedback: Toggle für "nur den gefundenen Code anzeigen").</summary>
    public bool HideDescription
    {
        get => _hideDescription;
        private set
        {
            if (_hideDescription == value)
            {
                return;
            }

            _hideDescription = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HideDescription)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DescriptionVisibility)));
        }
    }

    public Visibility DescriptionVisibility => HideDescription ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Wird vom Hauptfenster auf ALLEN Zeilen der geteilten Liste aufgerufen, wenn der Toggle umgeschaltet wird.</summary>
    public void SetHideDescription(bool hide) => HideDescription = hide;

    public static SearchResultRow FromDocumentMatch(TextMatch match, bool hideDescription) => new()
    {
        FilePath = null,
        LineNumber = match.LineNumber,
        LineText = match.LineText,
        MatchedText = match.MatchedText,
        Offset = match.Offset,
        Length = match.Length,
        HideDescription = hideDescription,
    };

    public static SearchResultRow FromDocumentMatch(TextMatch match, bool hideDescription, string documentLabel, Action<int, int> select) => new()
    {
        FilePath = null,
        LineNumber = match.LineNumber,
        LineText = match.LineText,
        MatchedText = match.MatchedText,
        Offset = match.Offset,
        Length = match.Length,
        DocumentLabel = documentLabel,
        SelectAction = select,
        HideDescription = hideDescription,
    };

    public static SearchResultRow FromFileMatch(FileSearchMatch match, bool hideDescription) => new()
    {
        FilePath = match.FilePath,
        LineNumber = match.Match.LineNumber,
        LineText = match.Match.LineText,
        MatchedText = match.Match.MatchedText,
        Offset = match.Match.Offset,
        Length = match.Match.Length,
        HideDescription = hideDescription,
    };

    public static SearchResultRow FromReplacementPlan(FileReplacementPlan plan, bool hideDescription) => new()
    {
        FilePath = plan.FilePath,
        LineNumber = 0,
        LineText = SearchTexts.ResultReplacements(plan.ReplacementCount),
        MatchedText = string.Empty,
        Offset = 0,
        Length = 0,
        HideDescription = hideDescription,
    };
}
