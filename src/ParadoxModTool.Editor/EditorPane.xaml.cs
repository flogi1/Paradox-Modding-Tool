using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using ParadoxModTool.Categories;
using ParadoxModTool.Scopes;
using ParadoxModTool.Settings;

namespace ParadoxModTool.Editor;

/// <summary>
/// Ein unabhängiger Arbeitsbereich (Architektur-Spezifikation, Abschnitt 5, Split-View:
/// "zwei unabhängige Arbeitsbereiche, Dokumente per Drag&amp;Drop oder Tastenkombination
/// verschiebbar"). Verwaltet seine eigene Liste offener Dokumente (Tabs) und ein einzelnes
/// AvalonEdit-Steuerelement, dessen Inhalt beim Tabwechsel umgeschaltet wird. Das eigentliche
/// Öffnen/Speichern (Dateidialoge, gemeinsamer Undo-Verlauf) übernimmt bewusst NICHT dieser
/// Arbeitsbereich selbst, sondern das Hauptfenster über die "Requested"-Events — damit beide
/// Arbeitsbereiche denselben Undo-Stack und dasselbe aktive Spielprofil verwenden.
/// Das Verschieben eines Dokuments zwischen den beiden Arbeitsbereichen geschieht in dieser
/// ersten Fassung über den "⇄"-Button (nicht per Drag&amp;Drop — die Spezifikation erlaubt
/// ausdrücklich "Drag&amp;Drop ODER Tastenkombination", ein expliziter Klick erfüllt denselben
/// Zweck mit deutlich geringerem Umsetzungsrisiko).
/// </summary>
public partial class EditorPane : UserControl
{
    private readonly ObservableCollection<OpenDocument> _documents = new();

    // Hover-Tipps und Autovervollständigung über der Script-Datenbank (siehe ScriptHintSettings).
    private readonly ScriptHintController _scriptHints;
    private readonly ScopeStatusController _scopeStatus;
    private readonly BracketController _brackets;
    private readonly ScopeWarningController _warnings;

    public EditorPane()
    {
        InitializeComponent();
        ApplyToolbarTexts();
        Tabs.ItemsSource = _documents;
        _scriptHints = new ScriptHintController(Editor, () => ActiveDocument?.FilePath);
        _scopeStatus = new ScopeStatusController(
            Editor,
            () => ActiveDocument?.FilePath,
            new ScopeStatusController.Target(ScopeStatusBar, ScopeStatusText, () => ScopeSettings.StatusBarEnabled),
            new ScopeStatusController.Target(ScopeStatusBarTop, ScopeStatusTextTop, () => ScopeSettings.StatusBarTopEnabled));
        _brackets = new BracketController(Editor, () => ActiveDocument?.FilePath);
        _warnings = new ScopeWarningController(Editor, () => ActiveDocument?.FilePath);
        _scriptHints.WarningAt = _warnings.MessageAt;
        _scopeStatus.ContextChanged += (_, _) => ScopeInsightChanged?.Invoke(this, EventArgs.Empty);
        _warnings.IssuesChanged += (_, _) => ScopeInsightChanged?.Invoke(this, EventArgs.Empty);

        // Farbe der Textauswahl aus dem Theme (Token EditorSelection); folgt einem Themenwechsel.
        Editor.TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty, "Theme.EditorSelection");

        // Kategorie-Filter (Nutzer-Feedback) über die Standard-CollectionView der Tab-Liste -
        // WPF wendet einen auf CollectionViewSource.GetDefaultView gesetzten Filter automatisch
        // auf die ListBox an, ohne dass ItemsSource selbst ausgetauscht werden müsste.
        CollectionViewSource.GetDefaultView(_documents).Filter = FilterByCategory;
        RebuildCategoryFilter();

        // Ausgeblendete Filter-Häkchen gelten für alle Arbeitsbereiche; die Leiste baut sich bei jeder Änderung neu auf.
        FilterBarSettings.Changed += (_, _) => RebuildCategoryFilter();
        FolderFilterSettings.Changed += (_, _) =>
        {
            // Das Ereignis ist prozessweit; ein Arbeitsbereich eines anderen (beendeten) UI-Threads baut nichts um.
            if (Dispatcher.CheckAccess())
            {
                RebuildCategoryFilter();
            }
        };

        // Symbol-Einstellungen gelten für alle Arbeitsbereiche; Tabs zeichnen ihre Symbole bei jeder Änderung neu.
        TabIconSettings.Changed += (_, _) =>
        {
            foreach (var document in _documents)
            {
                document.NotifyIconSettingsChanged();
            }
        };

        // Ändert sich bei einem Dokument HasNote/HasBookmark, muss ein aktiver Eigenschaftsfilter neu bewertet werden;
        // ändert sich Name, Kategorie oder Herkunft, rückt der Tab an seine sortierte Stelle (EditorPane.Sort.cs).
        _documents.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (OpenDocument old in e.OldItems)
                {
                    old.PropertyChanged -= Document_PropertyChangedForFilter;
                    old.PropertyChanged -= Document_PropertyChangedForSort;
                }
            }

            if (e.NewItems is not null)
            {
                foreach (OpenDocument added in e.NewItems)
                {
                    added.PropertyChanged += Document_PropertyChangedForFilter;
                    added.PropertyChanged += Document_PropertyChangedForSort;
                }
            }
        };

        InitializeGuiDesigner();
    }

    private void Document_PropertyChangedForFilter(object? sender, PropertyChangedEventArgs e)
    {
        if ((e.PropertyName is nameof(OpenDocument.HasNote) or nameof(OpenDocument.HasBookmark) or nameof(OpenDocument.HasBug))
            && (FilterHasNoteToggle.IsChecked == true || FilterHasBookmarkToggle.IsChecked == true || FilterHasBugToggle.IsChecked == true))
        {
            CollectionViewSource.GetDefaultView(_documents).Refresh();
        }
    }

    private bool _documentBarVisible = true;

    /// <summary>
    /// Blendet die Dokumentenleiste (Kategorie-Häkchen und Eigenschaftsfilter) ein oder aus. Ausgeblendet
    /// wirken keine Filter mehr - sonst würden unsichtbare Filter Tabs verstecken.
    /// </summary>
    public void SetDocumentBarVisible(bool visible)
    {
        _documentBarVisible = visible;
        var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        CategoryFilterPanel.Visibility = visibility;
        PropertyFilterPanel.Visibility = visibility;
        CollectionViewSource.GetDefaultView(_documents).Refresh();
    }

    /// <summary>Blendet die Pfadleiste (Pfad, Kopieren, Ordner öffnen, Notizen anzeigen) ein oder aus (Einstellungen, Kategorie 3).</summary>
    public void SetPathBarVisible(bool visible) => PathBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Scope am Cursor und Warnungen der aktiven Datei, für den Abschnitt "Scope" im Notiz-Panel.</summary>
    public ScopeInsight ScopeInsight => new(_scopeStatus.Available, _scopeStatus.Context, _warnings.Issues);

    /// <summary>Scope am Cursor oder die Warnungen haben sich geändert.</summary>
    public event EventHandler? ScopeInsightChanged;

    /// <summary>Springt zur Klammer, die zu der am Cursor gehört (Strg+B). Liefert <see langword="false"/>, wenn keine Klammer mit Gegenstück am Cursor steht.</summary>
    public bool GoToMatchingBracket() => _brackets.GoToMatching();

    /// <summary>Wird ausgelöst, wenn sich das aktive Dokument dieses Arbeitsbereichs ändert (Tabwechsel, Schließen, Öffnen).</summary>
    public event EventHandler? ActiveDocumentChanged;

    /// <summary>
    /// Zeilenbereich (1-basiert) der aktuellen Markierung im aktiven Dokument, ohne Markierung die
    /// Cursorzeile - für Lesezeichen mit Kommentar. Endet die Markierung genau am Anfang einer Zeile,
    /// zählt diese Zeile nicht mehr mit.
    /// </summary>
    public (int StartLine, int EndLine)? CurrentLineRange
    {
        get
        {
            if (ActiveDocument is null)
            {
                return null;
            }

            var document = Editor.Document;
            if (Editor.SelectionLength == 0)
            {
                var caretLine = document.GetLineByOffset(Editor.CaretOffset).LineNumber;
                return (caretLine, caretLine);
            }

            var startLine = document.GetLineByOffset(Editor.SelectionStart).LineNumber;
            var endOffset = Editor.SelectionStart + Editor.SelectionLength;
            var endLine = document.GetLineByOffset(endOffset).LineNumber;
            if (endLine > startLine && endOffset == document.GetLineByNumber(endLine).Offset)
            {
                endLine--;
            }

            return (startLine, endLine);
        }
    }

    public IReadOnlyList<OpenDocument> Documents => _documents;

    public OpenDocument? ActiveDocument => Tabs.SelectedItem as OpenDocument;

    public string? CurrentFilePath => ActiveDocument?.FilePath;

    /// <summary>Der aktuelle Textinhalt des aktiven Dokuments — u. a. für die "Speichern"-Aktion.</summary>
    public string CurrentText => Editor.Text;

    /// <summary>Der markierte Text des sichtbaren Editors (leer ohne Markierung), z. B. als Vorgabe für Strg+F.</summary>
    public string SelectedText => Editor.SelectedText;

    /// <summary>
    /// Die aktuelle Cursorposition im sichtbaren Editor-Steuerelement dieses Arbeitsbereichs.
    /// Wird für die Session-Speicherung (Abschnitt 12) benötigt, da <see cref="OpenDocument.CaretOffset"/>
    /// des aktiven Dokuments erst beim Wegschalten auf einen anderen Tab aktualisiert wird
    /// (siehe <see cref="Tabs_SelectionChanged"/>) — beim Beenden des Programms passiert das nie.
    /// </summary>
    public int CurrentCaretOffset => Editor.CaretOffset;

    /// <summary>
    /// Die aktuelle Zeilennummer (1-basiert) des Cursors im aktiven Dokument, oder
    /// <see langword="null"/> ohne aktives Dokument - für das neue Bookmark-Feature (Nutzer-
    /// Feedback: "die Option hinzu, dass man im Dokument eine Codestelle bookmarken kann").
    /// </summary>
    public int? CurrentCaretLine => ActiveDocument is null
        ? null
        : Editor.Document.GetLineByOffset(Editor.CaretOffset).LineNumber;

    /// <summary>
    /// Liefert den reinen Textinhalt einer bestimmten Zeile (1-basiert, ohne Zeilenumbruch) des
    /// AKTIVEN Dokuments - für die Kurzvorschau eines Bookmarks (siehe
    /// ParadoxModTool.Notes.BookmarkEntry.Preview, ParadoxModTool.Editor referenziert dieses
    /// Projekt bewusst NICHT, siehe EditorPane.ShowNoteRequested-Kommentar weiter oben - daher
    /// hier nur als Textverweis statt als auflösbares cref). Werte außerhalb des Dokuments liefern
    /// einen leeren String statt einer Ausnahme, nach demselben defensiven Muster wie
    /// <see cref="GoToLine"/>.
    /// </summary>
    public string GetLineText(int lineNumber)
    {
        if (ActiveDocument is null || lineNumber < 1 || lineNumber > Editor.Document.LineCount)
        {
            return string.Empty;
        }

        var line = Editor.Document.GetLineByNumber(lineNumber);
        return Editor.Document.GetText(line.Offset, line.Length);
    }

    public bool HasActiveDocument => ActiveDocument is not null;

    /// <summary>
    /// Fügt erzeugten GUI-Code eines Bausteins am Cursor ein (Plan-Ruling 8): Zeilenenden wie im Dokument, folgende Zeilen mit der Einrückung der
    /// Cursorzeile.
    /// </summary>
    public void InsertGuiBlock(string text)
    {
        if (ActiveDocument is null)
        {
            return;
        }

        var document = Editor.Document;
        var line = document.GetLineByOffset(Editor.CaretOffset);
        var indent = new string(document.GetText(line.Offset, line.Length).TakeWhile(c => c is ' ' or '\t').ToArray());
        var newline = document.GetLineByNumber(1).DelimiterLength == 2 ? "\r\n" : "\n";
        InsertAtCaret(GuiDesign.Blocks.GuiBlockInsertion.ForCaret(text, indent, newline));
    }

    /// <summary>Fügt Text an der Cursorposition des aktiven Dokuments ein (ersetzt eine Auswahl) und gibt dem Editor den Fokus.</summary>
    public void InsertAtCaret(string text)
    {
        if (ActiveDocument is null)
        {
            return;
        }

        Editor.SelectedText = text;
        Editor.Focus();
    }

    /// <summary>
    /// Fügt eine Scope-Kette aus dem Scope Finder (siehe <c>ScopeChain</c>) ein: als verschachtelte Blöcke mit dem Cursor im ersten <c>limit</c> bzw. im innersten
    /// Block oder, bei <paramref name="chainOnly"/>, nur als Ausdruck (<c>owner.capital</c>, mit Wert am Ende). Auf einer leeren Zeile ersetzt der Block deren
    /// Inhalt; steht in der Zeile schon Text, kommt er in die Zeile darunter.
    /// </summary>
    public void InsertScopeChain(ScopeChain chain, string preferredForm, bool chainOnly)
    {
        if (ActiveDocument is null)
        {
            return;
        }

        if (chainOnly)
        {
            if (chain.ChainText() is { } expression)
            {
                InsertAtCaret(expression);
            }

            return;
        }

        var document = Editor.Document;
        var line = document.GetLineByOffset(Editor.CaretOffset);
        var lineText = document.GetText(line.Offset, line.Length);
        var indent = new string(lineText.TakeWhile(c => c is ' ' or '\t').ToArray());
        var newline = document.GetLineByNumber(1).DelimiterLength == 2 ? "\r\n" : "\n";
        var tab = indent.Length > 0 && indent[0] == ' ' ? "    " : "\t";
        var block = chain.ToBlock(preferredForm, indent, newline, out var caret, tab);

        int start, length;
        if (lineText.Trim().Length == 0)
        {
            (start, length) = (line.Offset, line.Length);
            block = indent + block;
            caret += indent.Length;
        }
        else
        {
            (start, length) = (line.EndOffset, 0);
            block = newline + indent + block;
            caret += newline.Length + indent.Length;
        }

        document.Replace(start, length, block);
        Editor.CaretOffset = start + caret;
        Editor.TextArea.Caret.BringCaretToView();
        Editor.Focus();
    }

    public event EventHandler? NewDocumentRequested;
    public event EventHandler? OpenDocumentRequested;
    public event EventHandler? SaveDocumentRequested;
    public event EventHandler<OpenDocument>? MoveActiveDocumentRequested;

    /// <summary>"Alle speichern": Das Hauptfenster speichert alle geänderten Dokumente beider Arbeitsbereiche (wie Strg+Umschalt+S).</summary>
    public event EventHandler? SaveAllRequested;

    /// <summary>Texte und Tooltips der Werkzeugleiste aus <see cref="MenuTexts"/>.</summary>
    private void ApplyToolbarTexts()
    {
        SetText(NewButton, MenuTexts.WorkspaceNew, MenuTexts.WorkspaceNewTip);
        SetText(OpenButton, MenuTexts.WorkspaceOpen, MenuTexts.WorkspaceOpenTip);
        SetText(SaveButton, MenuTexts.WorkspaceSave, MenuTexts.WorkspaceSaveTip);
        SetText(SaveAllButton, MenuTexts.WorkspaceSaveAll, MenuTexts.WorkspaceSaveAllTip);
        SetText(CloseDocumentButton, MenuTexts.WorkspaceClose, MenuTexts.WorkspaceCloseTip);
        SetText(MoveButton, MenuTexts.WorkspaceMove, MenuTexts.WorkspaceMoveTip);
        SetText(CategoryButton, MenuTexts.WorkspaceCategory, MenuTexts.WorkspaceCategoryTip);
        SetText(FilterBarButton, MenuTexts.WorkspaceFilter, MenuTexts.WorkspaceFilterTip);
        SetText(TabIconsButton, MenuTexts.WorkspaceIcons, MenuTexts.WorkspaceIconsTip);
        SetText(SortButton, MenuTexts.WorkspaceSort, MenuTexts.WorkspaceSortTip);
        ApplyMarkButtonTexts();
    }

    private static void SetText(Button button, string text, string tip)
    {
        button.Content = text;
        button.ToolTip = tip;
    }

    /// <summary>
    /// Nutzer-Feedback Runde 5: "Es sollte ... die Möglichkeit geben die Notizen direkt zu
    /// öffnen." Ausgelöst vom Notiz-Icon im Dateienreiter, dem "Notizen anzeigen"-Button neben
    /// der Pfadanzeige. Dieser Arbeitsbereich öffnet das Notizfenster nicht selbst - das würde
    /// eine Projektabhängigkeit von ParadoxModTool.Editor auf ParadoxModTool.Notes/das
    /// FileNoteWindow (in ParadoxModTool.App) einführen - sondern überlässt das MainWindow
    /// (siehe MainWindow.WireEditorPane/ShowNoteFor), genau wie bei den übrigen "Requested"-
    /// Events dieser Klasse.
    /// </summary>
    public event EventHandler<OpenDocument>? ShowNoteRequested;

    /// <summary>Klick auf das Bug-Symbol eines Tabs: MainWindow schaltet zum nächsten offenen Bug der Datei weiter.</summary>
    public event EventHandler<OpenDocument>? ShowBugRequested;

    /// <summary>Klick auf das Fehler-Symbol eines Tabs: Das Hauptfenster öffnet das Fehlerprotokoll, gefiltert auf die Datei.</summary>
    public event EventHandler<OpenDocument>? ShowErrorLogRequested;

    /// <summary>
    /// Lädt eine Datei standardmäßig als UTF-8 (Abschnitt 5h) in einen neuen Tab, oder
    /// wechselt zu einem in diesem Arbeitsbereich bereits offenen Tab derselben Datei.
    /// </summary>
    public OpenDocument LoadFile(string path, IHighlightingDefinition? highlighting = null)
    {
        var existing = _documents.FirstOrDefault(d => PathsEqual(d.FilePath, path));

        if (existing is not null)
        {
            Tabs.SelectedItem = existing;
            return existing;
        }

        var document = OpenDocument.LoadFromFile(path, highlighting);
        InsertDocument(document, null);
        Tabs.SelectedItem = document;
        UpdatePathDisplay();
        return document;
    }

    /// <summary>Öffnet ein neues, leeres Dokument in einem neuen Tab ("Neu"-Aktion).</summary>
    public OpenDocument NewDocument(IHighlightingDefinition? highlighting = null)
    {
        var document = OpenDocument.NewEmpty(highlighting);
        InsertDocument(document, null);
        Tabs.SelectedItem = document;
        UpdatePathDisplay();
        return document;
    }

    /// <summary>
    /// Lädt den Inhalt eines bereits offenen Dokuments neu, falls es in diesem Arbeitsbereich
    /// offen ist - z. B. nachdem "Suchen &amp; Ersetzen in Dateien" (Abschnitt 5b) die Datei auf
    /// der Platte verändert hat, damit ein offener Tab nicht einen veralteten Stand anzeigt
    /// (Nutzer-Feedback). Ist die Datei hier gerade das aktive Dokument, wird das sichtbar
    /// eingefärbte AvalonEdit-Steuerelement automatisch mit aktualisiert, da es auf dasselbe
    /// <see cref="OpenDocument.Document"/>-Objekt zeigt. Ohne Wirkung, falls die Datei hier nicht
    /// offen ist. Bekannte Einschränkung: macht "Rückgängig" der zugrundeliegenden Ersetzung im
    /// programmweiten Aktionsverlauf nicht rückgängig sichtbar - der offene Tab zeigt dann bis zum
    /// nächsten manuellen Neuladen weiterhin den (bereits zurückgenommenen) neuen Stand.
    /// </summary>
    /// <summary>Der aktuelle Text der Datei, wenn sie hier geöffnet ist (auch mit ungespeicherten Änderungen); sonst <see langword="null"/>.</summary>
    public string? TryGetOpenText(string filePath) => _documents.FirstOrDefault(d => PathsEqual(d.FilePath, filePath))?.Document.Text;

    public void ReloadIfOpen(string filePath, string newContent)
    {
        var document = _documents.FirstOrDefault(d => PathsEqual(d.FilePath, filePath));

        if (document is null)
        {
            return;
        }

        document.Document.Text = newContent;

        // Der Inhalt entspricht jetzt exakt der Datei auf der Platte.
        document.MarkSaved();
        document.RefreshDiskStamp();
    }

    /// <summary>
    /// Vergleicht zwei Dateipfade auf "meinen dieselbe Datei" — NICHT per reinem String-Vergleich,
    /// da zwei Pfade dieselbe Datei bezeichnen können, ohne als Zeichenkette identisch zu sein
    /// (Nutzer-Feedback: "nach Find und Replace in einem Ordner wird die geöffnete Datei nicht
    /// aktualisiert" — Ursache: der über "Öffnen…" geladene Pfad und der beim Ordner-Ersetzen aus
    /// dem eingegebenen/durchsuchten Ordnerpfad zusammengesetzte Pfad unterschieden sich rein
    /// textuell, z. B. durch einen abschließenden Backslash im eingegebenen Ordnerpfad — dadurch
    /// erzeugt <see cref="Directory.EnumerateFiles"/> Pfade mit doppeltem Backslash vor dem
    /// Dateinamen, die trotz identischer Zieldatei nicht mehr Zeichen-für-Zeichen zum über den
    /// Dateidialog geöffneten Pfad passen). <see cref="Path.GetFullPath"/> löst genau solche
    /// oberflächlichen Unterschiede auf (doppelte/abschließende Trennzeichen, relative Anteile),
    /// ohne die Groß-/Kleinschreibung realer Ordnernamen zu verändern — daher weiterhin zusätzlich
    /// <see cref="StringComparison.OrdinalIgnoreCase"/> für Windows' groß-/kleinschreibungs-
    /// unabhängiges Dateisystem. Bei einem (sehr unwahrscheinlichen) ungültigen Pfad wird auf den
    /// einfachen String-Vergleich zurückgefallen, statt eine Ausnahme durchschlagen zu lassen.
    /// </summary>
    /// <remarks>
    /// Öffentlich (statt weiterhin nur intern verwendet), damit MainWindow beim Aktualisieren des
    /// Notiz-Icons (Nutzer-Feedback Runde 5) prüfen kann, ob ein bereits offenes Dokument
    /// dieselbe Datei ist wie die gerade im Notizfenster bearbeitete — dieselbe Normalisierung
    /// statt einer zweiten, leicht abweichenden Implementierung an anderer Stelle.
    /// </remarks>
    public static bool PathsEqual(string? a, string? b)
    {
        if (a is null || b is null)
        {
            return false;
        }

        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Springt zu einer bestimmten Zeile (1-basiert) im aktiven Dokument - Nutzer-Feedback
    /// ("direkt zu einer Zeile springen"), Strg+G wie in Notepad++. Werte außerhalb des
    /// Dokuments werden auf die erste bzw. letzte Zeile begrenzt statt eine Ausnahme zu werfen.
    /// </summary>
    public void GoToLine(int lineNumber)
    {
        if (ActiveDocument is null)
        {
            return;
        }

        var document = Editor.Document;
        var clampedLine = Math.Max(1, Math.Min(lineNumber, document.LineCount));
        var line = document.GetLineByNumber(clampedLine);

        Editor.CaretOffset = line.Offset;
        Editor.ScrollToLine(clampedLine);
        Editor.Focus();
    }

    /// <summary>
    /// Ersetzt das Highlighting ALLER Dokumente dieses Arbeitsbereichs (z. B. nach einem Themenwechsel mit neuen Syntaxfarben)
    /// und zeigt es im aktiven Dokument sofort an.
    /// </summary>
    public void ReplaceHighlightingForAll(IHighlightingDefinition? highlighting)
    {
        foreach (var document in _documents)
        {
            document.Highlighting = highlighting;
        }

        if (ActiveDocument is not null)
        {
            Editor.SyntaxHighlighting = highlighting;
        }
    }

    /// <summary>Wendet ein Highlighting auf das aktive Dokument an.</summary>
    public void ApplyHighlighting(IHighlightingDefinition? highlighting)
    {
        if (ActiveDocument is { } document)
        {
            document.Highlighting = highlighting;
            Editor.SyntaxHighlighting = highlighting;
        }
    }

    /// <summary>
    /// Aktualisiert nur den gemerkten Dateipfad des aktiven Dokuments, ohne den Textinhalt
    /// neu zu laden — z. B. nachdem "Speichern" ein bislang ungespeichertes Dokument erstmals
    /// auf die Platte geschrieben hat.
    /// </summary>
    public void SetCurrentFilePath(string path)
    {
        // Einmal festhalten: der neue Name kann den Tab umsortieren.
        var document = ActiveDocument;
        document?.SetFilePath(path);
        document?.MarkSaved();
        document?.RefreshDiskStamp();
        Tabs.Items.Refresh();
        UpdatePathDisplay();
    }

    /// <summary>
    /// Nimmt ein <see cref="OpenDocument"/> aus dem jeweils ANDEREN Arbeitsbereich auf
    /// (Split-View: Dokument zwischen den Arbeitsbereichen verschieben). Fügt es nicht
    /// doppelt ein, falls es hier bereits offen wäre.
    /// </summary>
    public void AdoptDocument(OpenDocument document, OpenDocument? insertBefore = null)
    {
        if (!_documents.Contains(document))
        {
            InsertDocument(document, insertBefore);
        }

        Tabs.SelectedItem = document;
        UpdatePathDisplay();
    }

    /// <summary>Entfernt ein Dokument aus diesem Arbeitsbereich, ohne es zu verändern — Gegenstück zu <see cref="AdoptDocument"/> beim Verschieben.</summary>
    public void ReleaseDocument(OpenDocument document) => RemoveDocument(document);

    /// <summary>
    /// Schließt ein einzelnes Dokument (Nutzer-Feedback: "X"-Button direkt am Tab). Bewusst OHNE
    /// Rückfrage bei ungespeicherten Änderungen, da dieser Ausbauschritt noch keinen
    /// Dirty-Indikator kennt (siehe bekannte Einschränkungen von Abschnitt 5) - eine spätere
    /// Ausbaustufe kann hier eine Sicherheitsabfrage ergänzen, sobald der Änderungsstatus eines
    /// Dokuments bekannt ist.
    /// </summary>
    public void CloseDocument(OpenDocument document) => RemoveDocument(document);

    /// <summary>
    /// Zuletzt ausgewählte Dokumente dieses Arbeitsbereichs, das zuletzt gewählte am Ende (Nutzer-Feedback: nach dem Schließen
    /// soll das vorher ausgewählte, noch offene Dokument wieder erscheinen statt eines leeren Editors).
    /// </summary>
    private readonly List<OpenDocument> _selectionHistory = new();

    private void RemoveDocument(OpenDocument document)
    {
        _selectionHistory.Remove(document);
        if (ReferenceEquals(ActiveDocument, document) && DocumentAfterClosing(document) is { } next)
        {
            // Vor dem Entfernen umschalten: die Tableiste ist eine ListBox und wählt nach dem Entfernen des aktiven Eintrags
            // von sich aus nichts mehr aus.
            Tabs.SelectedItem = next;
        }

        _documents.Remove(document);
        UpdatePathDisplay();
    }

    /// <summary>
    /// Das zuletzt ausgewählte, noch offene und in der Tableiste sichtbare Dokument; ohne Verlauf der rechte, sonst der linke
    /// Nachbar in der Tableiste.
    /// </summary>
    private OpenDocument? DocumentAfterClosing(OpenDocument closing)
    {
        var visible = Tabs.Items.OfType<OpenDocument>().Where(d => !ReferenceEquals(d, closing)).ToList();
        for (var i = _selectionHistory.Count - 1; i >= 0; i--)
        {
            if (visible.Contains(_selectionHistory[i]))
            {
                return _selectionHistory[i];
            }
        }

        var index = Tabs.Items.IndexOf(closing);
        if (visible.Count == 0)
        {
            return null;
        }

        return visible[Math.Clamp(index, 0, visible.Count - 1)];
    }

    /// <summary>Schließt das aktive Dokument dieses Arbeitsbereichs (Knopf "Schließen" der Werkzeugleiste).</summary>
    public void CloseActiveDocument()
    {
        if (ActiveDocument is { } document)
        {
            RequestClose(document);
        }
    }

    /// <summary>
    /// Wird ausgelöst, wenn der Nutzer ein Dokument schließen will (✕ am Tab bzw. "Schließen"-Button).
    /// Das Hauptfenster fragt bei ungespeicherten Änderungen nach und ruft dann <see cref="CloseDocument"/>
    /// auf. Ohne Abonnent schließt der Arbeitsbereich direkt.
    /// </summary>
    public event EventHandler<OpenDocument>? CloseDocumentRequested;

    private void RequestClose(OpenDocument document)
    {
        if (CloseDocumentRequested is null)
        {
            CloseDocument(document);
        }
        else
        {
            CloseDocumentRequested.Invoke(this, document);
        }
    }

    /// <summary>
    /// Lädt ein Dokument nach einer Änderung durch ein anderes Programm neu von der Platte und behält
    /// die Cursorposition bei (soweit der neue Text lang genug ist).
    /// </summary>
    public void ReloadFromDisk(OpenDocument document)
    {
        var isActive = ReferenceEquals(document, ActiveDocument);
        var caret = isActive ? Editor.CaretOffset : document.CaretOffset;

        document.ReloadFromDisk();

        var offset = Math.Min(caret, document.Document.TextLength);
        if (isActive)
        {
            Editor.CaretOffset = offset;
        }
        else
        {
            document.CaretOffset = offset;
        }
    }

    /// <summary>Macht das Dokument zum aktiven Tab (z. B. vor dem Speichern aus der Schließen-Rückfrage).</summary>
    public void ActivateDocument(OpenDocument document)
    {
        if (_documents.Contains(document))
        {
            Tabs.SelectedItem = document;
        }
    }

    /// <summary>Fügt ein bereits erzeugtes Dokument hinzu (Wiederherstellung); wählt es nur auf Wunsch als aktiven Tab.</summary>
    public void AddDocument(OpenDocument document, bool select)
    {
        InsertDocument(document, null);
        if (select)
        {
            Tabs.SelectedItem = document;
        }

        UpdatePathDisplay();
    }

    /// <summary>
    /// Wählt den Tab am angegebenen Index aus, sofern vorhanden (Index in <see cref="Documents"/>).
    /// Die Session-Wiederherstellung wählt den zuletzt aktiven Tab über sein Dokument
    /// (<see cref="ActivateDocument"/>), weil die Sortierung die Indizes verschieben kann.
    /// </summary>
    public void SelectDocumentAt(int index)
    {
        if (index >= 0 && index < _documents.Count)
        {
            Tabs.SelectedIndex = index;
        }
    }

    /// <summary>
    /// Aktualisiert die Pfadanzeige unter der Tableiste (Nutzer-Feedback) anhand des aktuell
    /// aktiven Dokuments. Wird nach jeder Änderung aufgerufen, die das aktive Dokument oder
    /// dessen Dateipfad betreffen könnte - bewusst defensiv an mehreren Stellen statt nur einmal
    /// zentral, da PathText zum Zeitpunkt des allerersten Aufrufs (Konstruktor/InitializeComponent)
    /// unter Umständen noch nicht vollständig initialisiert ist.
    /// </summary>
    private void UpdatePathDisplay()
    {
        if (PathText is null)
        {
            return;
        }

        PathText.Text = ActiveDocument switch
        {
            null => EditorTexts.NoDocument,
            { FilePath: null } => EditorTexts.UntitledNotSaved,
            { FilePath: { } path } => path,
        };
    }

    /// <summary>
    /// Markiert den angegebenen Zeichenbereich im aktiven Dokument und scrollt dorthin —
    /// genutzt vom Suchen-&amp;-Ersetzen-Fenster (Abschnitt 5b), um zu einem Treffer zu springen.
    /// Werte außerhalb des Dokuments werden auf dessen Länge begrenzt, statt eine Ausnahme zu werfen.
    /// </summary>
    public void SelectRange(int offset, int length)
    {
        if (ActiveDocument is null)
        {
            return;
        }

        var textLength = Editor.Document.TextLength;
        var clampedOffset = Math.Max(0, Math.Min(offset, textLength));
        var clampedLength = Math.Max(0, Math.Min(length, textLength - clampedOffset));

        Editor.Select(clampedOffset, clampedLength);
        Editor.CaretOffset = clampedOffset;
        Editor.ScrollToLine(Editor.Document.GetLineByOffset(clampedOffset).LineNumber);
        Editor.Focus();
    }

    /// <summary>
    /// Ersetzt den kompletten Text des aktiven Dokuments (Abschnitt 5b, "Ersetzen" im aktuellen
    /// Dokument, z. B. das Ergebnis von <c>TextSearch.ReplaceAll</c>). Läuft bewusst über
    /// AvalonEdits eigenen Text-Undo-Mechanismus (Strg+Z im Textbereich), NICHT über den
    /// programmweiten Aktionsverlauf, der für tatsächliche Dateischreibvorgänge reserviert ist
    /// (siehe ParadoxModTool.UndoRedo) — die Änderung liegt bis zum expliziten "Speichern" nur
    /// im Editor-Puffer, genau wie bei normalen Texteingaben.
    /// </summary>
    public void ReplaceActiveDocumentText(string newText)
    {
        if (ActiveDocument is null)
        {
            return;
        }

        Editor.Document.Text = newText;
    }

    /// <summary>
    /// Filter-Prädikat für die Tab-Liste (Nutzer-Feedback: Dateikategorien ein-/ausblenden).
    /// Das aktive Dokument bleibt IMMER sichtbar, auch wenn seine Kategorie gerade ausgeblendet
    /// ist — sonst würde ein Kategorie-Filter den gerade bearbeiteten Tab unerwartet verschwinden
    /// lassen, ohne dass der Nutzer eine Möglichkeit hätte, zu ihm zurückzuwechseln.
    /// </summary>
    private bool FilterByCategory(object item)
    {
        if (item is not OpenDocument document)
        {
            return true;
        }

        if (ReferenceEquals(document, ActiveDocument) || !_documentBarVisible)
        {
            return true;
        }

        if ((FilterHasNoteToggle.IsChecked == true && !document.HasNote)
            || (FilterHasBookmarkToggle.IsChecked == true && !document.HasBookmark)
            || (FilterHasBugToggle.IsChecked == true && !document.HasBug))
        {
            return false;
        }

        // Ein aus der Leiste ausgeblendetes Häkchen filtert nicht mehr (siehe FilterBarSettings).
        var categoryHidden = _hiddenCategories.Contains(document.Category)
            && FilterBarSettings.IsShown(FilterEntryKeys.ForCategory(document.Category));
        var originHidden = _hiddenOrigins.Contains(document.Origin)
            && FilterBarSettings.IsShown(FilterEntryKeys.ForOrigin(document.Origin));
        return !categoryHidden && !originHidden && FolderFilterMatcher.Accepts(ActiveFolderFilters(), document.FilePath);
    }

    /// <summary>Ordner-Filter, deren Häkchen in diesem Arbeitsbereich an ist (aus der Leiste ausgeblendete filtern nicht).</summary>
    private IReadOnlyCollection<FolderFilterDefinition> ActiveFolderFilters() =>
        _activeFolderFilters.Count == 0
            ? Array.Empty<FolderFilterDefinition>()
            : FolderFilterSettings.Filters.Where(f => f.ShowInBar && _activeFolderFilters.Contains(f.Id)).ToList();

    /// <summary>Kennungen der Ordner-Filter, deren Häkchen in diesem Arbeitsbereich an ist.</summary>
    private readonly HashSet<string> _activeFolderFilters = new();

    /// <summary>Rechtsklick "Umbenennen" auf ein Ordner-Filter-Häkchen; der Wert ist die Kennung des Filters.</summary>
    public event EventHandler<string>? FolderFilterRenameRequested;

    private void CategoryFilter_Changed(object sender, RoutedEventArgs e)
    {
        CollectionViewSource.GetDefaultView(_documents).Refresh();
    }

    /// <summary>Kategorien, deren Häkchen in der Filterleiste abgewählt ist (Kennungen). Bleibt beim Neuaufbau der Leiste erhalten.</summary>
    private readonly HashSet<string> _hiddenCategories = new();

    /// <summary>Herkünfte (Vanilla, Mod, Extern), deren Häkchen in der Filterleiste abgewählt ist.</summary>
    private readonly HashSet<FileOrigin> _hiddenOrigins = new();

    /// <summary>Wird ausgelöst, wenn im Kategorie-Menü "Kategorien verwalten…" gewählt wird.</summary>
    public event EventHandler? ManageCategoriesRequested;

    /// <summary>
    /// Baut die Filterleiste aus der aktuellen Kategorie-Liste neu auf (Start und nach jeder Änderung der Liste).
    /// Ausgeblendete Kategorien bleiben ausgeblendet, sofern es sie noch gibt.
    /// </summary>
    public void RebuildCategoryFilter()
    {
        _hiddenCategories.RemoveWhere(id => !FileCategoryClassifier.Exists(id));
        CategoryFilterPanel.Children.Clear();

        var origins = new[]
        {
            (FileOrigin.Vanilla, "Vanilla"),
            (FileOrigin.Mod, "Mod"),
            (FileOrigin.External, EditorTexts.OriginExternal),
        }.Where(o => FilterBarSettings.IsShown(FilterEntryKeys.ForOrigin(o.Item1))).ToList();
        if (origins.Count > 0)
        {
            CategoryFilterPanel.Children.Add(CreateMutedLabel(EditorTexts.FilterOrigin, new Thickness(0, 0, 6, 0)));
            foreach (var (origin, text) in origins)
            {
                AddOriginCheckBox(origin, text, CreateOriginIcon(origin));
            }
        }

        var categories = FileCategoryClassifier.Categories
            .Where(c => FilterBarSettings.IsShown(FilterEntryKeys.ForCategory(c.Id)))
            .ToList();
        if (categories.Count > 0)
        {
            CategoryFilterPanel.Children.Add(CreateMutedLabel(EditorTexts.FilterCategory, new Thickness(origins.Count > 0 ? 10 : 0, 0, 6, 0)));
        }

        foreach (var category in categories)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new Border
            {
                Width = 9,
                Height = 9,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = FileCategoryClassifier.ColorFor(category.Id),
            });
            content.Children.Add(new TextBlock { Text = CategoryDefaults.DisplayName(category) });

            var checkBox = new CheckBox
            {
                Content = content,
                Tag = category.Id,
                IsChecked = !_hiddenCategories.Contains(category.Id),
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = EditorTexts.FilterCategoryTip(CategoryDefaults.DisplayName(category)),
            };
            checkBox.Click += CategoryFilterCheckBox_Click;
            checkBox.ContextMenu = CreateHideFromBarMenu(FilterEntryKeys.ForCategory(category.Id));
            CategoryFilterPanel.Children.Add(checkBox);
        }

        AddFolderFilterCheckBoxes(origins.Count > 0 || categories.Count > 0);
        CollectionViewSource.GetDefaultView(_documents).Refresh();
    }

    /// <summary>
    /// Gruppe "Nur Ordner:" der Filterleiste. Anders als bei Herkunft und Kategorie schränkt ein Häkchen hier ein: an = nur Tabs aus
    /// diesem Ordnerbaum; mehrere an = Tabs aus allen gewählten Ordnern; keins an = keine Einschränkung.
    /// </summary>
    private void AddFolderFilterCheckBoxes(bool hasGroupsBefore)
    {
        _activeFolderFilters.RemoveWhere(id => FolderFilterSettings.Find(id) is null);
        var filters = FolderFilterSettings.Filters.Where(f => f.ShowInBar).ToList();
        if (filters.Count == 0)
        {
            return;
        }

        CategoryFilterPanel.Children.Add(CreateMutedLabel(EditorTexts.FilterFolders, new Thickness(hasGroupsBefore ? 10 : 0, 0, 6, 0)));
        foreach (var filter in filters)
        {
            var id = filter.Id;
            var checkBox = new CheckBox
            {
                Content = new TextBlock { Text = filter.Name },
                IsChecked = _activeFolderFilters.Contains(id),
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = EditorTexts.FilterFolderTip(filter.FolderPath),
            };
            checkBox.Click += (_, _) =>
            {
                if (checkBox.IsChecked == true)
                {
                    _activeFolderFilters.Add(id);
                }
                else
                {
                    _activeFolderFilters.Remove(id);
                }

                CollectionViewSource.GetDefaultView(_documents).Refresh();
            };

            var menu = new ContextMenu();
            var rename = new MenuItem { Header = EditorTexts.FolderFilterRename };
            rename.Click += (_, _) => FolderFilterRenameRequested?.Invoke(this, id);
            var delete = new MenuItem { Header = EditorTexts.FolderFilterDelete };
            delete.Click += (_, _) => FolderFilterSettings.Remove(id);
            var hide = new MenuItem { Header = EditorTexts.HideFromBar };
            hide.Click += (_, _) => FolderFilterSettings.SetShown(id, false);
            menu.Items.Add(rename);
            menu.Items.Add(delete);
            menu.Items.Add(hide);
            checkBox.ContextMenu = menu;
            CategoryFilterPanel.Children.Add(checkBox);
        }
    }

    private static UIElement? CreateOriginIcon(FileOrigin origin) => origin switch
    {
        FileOrigin.Vanilla => new VanillaIcon { Width = 11, Height = 11 },
        FileOrigin.Mod => new TextBlock { Text = "🛠", FontSize = 10 },
        _ => null,
    };

    /// <summary>Rechtsklick-Menü eines Filter-Häkchens: entfernt es aus der Leiste (wieder einblendbar über "Filter ▾").</summary>
    private static ContextMenu CreateHideFromBarMenu(string key)
    {
        var menu = new ContextMenu();
        var item = new MenuItem { Header = EditorTexts.HideFromBar };
        item.Click += (_, _) => FilterBarSettings.SetShown(key, false);
        menu.Items.Add(item);
        return menu;
    }

    /// <summary>Menü "Filter": zeigt oder versteckt einzelne Häkchen der Filterleiste (Herkunft und Kategorien).</summary>
    private void FilterBarButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = FilterBarButton, Placement = PlacementMode.Bottom };
        AddFilterMenuItem(menu, FilterEntryKeys.ForOrigin(FileOrigin.Vanilla), SettingsTexts.OriginVanilla, CreateOriginIcon(FileOrigin.Vanilla));
        AddFilterMenuItem(menu, FilterEntryKeys.ForOrigin(FileOrigin.Mod), SettingsTexts.OriginMod, CreateOriginIcon(FileOrigin.Mod));
        AddFilterMenuItem(menu, FilterEntryKeys.ForOrigin(FileOrigin.External), SettingsTexts.OriginExternal, null);
        menu.Items.Add(new Separator());
        foreach (var category in FileCategoryClassifier.Categories)
        {
            AddFilterMenuItem(
                menu,
                FilterEntryKeys.ForCategory(category.Id),
                CategoryDefaults.DisplayName(category).Replace("_", "__"),
                new Border { Width = 10, Height = 10, Background = FileCategoryClassifier.ColorFor(category.Id) });
        }

        if (FolderFilterSettings.Filters.Count > 0)
        {
            menu.Items.Add(new Separator());
            foreach (var filter in FolderFilterSettings.Filters)
            {
                var id = filter.Id;
                var item = new MenuItem
                {
                    Header = filter.Name.Replace("_", "__"),
                    ToolTip = filter.FolderPath,
                    IsCheckable = true,
                    IsChecked = filter.ShowInBar,
                    StaysOpenOnClick = true,
                };
                item.Click += (_, _) => FolderFilterSettings.SetShown(id, item.IsChecked);
                menu.Items.Add(item);
            }
        }

        menu.IsOpen = true;
    }

    private static void AddFilterMenuItem(ContextMenu menu, string key, string header, UIElement? icon)
    {
        var item = new MenuItem
        {
            Header = header,
            Icon = icon,
            IsCheckable = true,
            IsChecked = FilterBarSettings.IsShown(key),
            StaysOpenOnClick = true,
        };
        item.Click += (_, _) => FilterBarSettings.SetShown(key, item.IsChecked);
        menu.Items.Add(item);
    }

    private static TextBlock CreateMutedLabel(string text, Thickness margin)
    {
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = margin };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        return label;
    }

    private void AddOriginCheckBox(FileOrigin origin, string text, UIElement? icon)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (icon is not null)
        {
            if (icon is FrameworkElement element)
            {
                element.Margin = new Thickness(0, 0, 4, 0);
                element.VerticalAlignment = VerticalAlignment.Center;
            }

            content.Children.Add(icon);
        }

        content.Children.Add(new TextBlock { Text = text });

        var checkBox = new CheckBox
        {
            Content = content,
            Tag = origin,
            IsChecked = !_hiddenOrigins.Contains(origin),
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = origin switch
            {
                FileOrigin.Vanilla => EditorTexts.FilterVanillaTip,
                FileOrigin.Mod => EditorTexts.FilterModTip,
                _ => EditorTexts.FilterExternalTip,
            },
        };
        checkBox.Click += OriginFilterCheckBox_Click;
        checkBox.ContextMenu = CreateHideFromBarMenu(FilterEntryKeys.ForOrigin(origin));
        CategoryFilterPanel.Children.Add(checkBox);
    }

    private void OriginFilterCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: FileOrigin origin } checkBox)
        {
            return;
        }

        if (checkBox.IsChecked == true)
        {
            _hiddenOrigins.Remove(origin);
        }
        else
        {
            _hiddenOrigins.Add(origin);
        }

        CollectionViewSource.GetDefaultView(_documents).Refresh();
    }

    /// <summary>Ermittelt die Herkunft aller Dokumente dieses Arbeitsbereichs neu (nach Änderung der Wurzelordner) und wendet den Filter an.</summary>
    public void RefreshOrigins()
    {
        SortAfterwards(() =>
        {
            foreach (var document in _documents)
            {
                document.RefreshOrigin();
            }
        });

        CollectionViewSource.GetDefaultView(_documents).Refresh();
    }

    /// <summary>
    /// Ermittelt die automatische Kategorie aller Dokumente dieses Arbeitsbereichs neu (nach Änderung der Kategorie-Regeln);
    /// eine Kategorie-Sortierung wird danach einmal angewendet.
    /// </summary>
    public void RefreshAutoCategories() => SortAfterwards(() =>
    {
        foreach (var document in _documents)
        {
            document.RefreshAutoCategory();
        }
    });

    /// <summary>Fragt für alle offenen Dokumente den Sperrzustand neu ab (nach Sperren, Entsperren und Änderungen an Mods).</summary>
    public void RefreshLocks()
    {
        foreach (var document in _documents)
        {
            document.RefreshLock();
        }
    }

    /// <summary>Fragt für alle offenen Dokumente die Fehlermarkierung neu ab (nach jeder Änderung im Fehlerprotokoll).</summary>
    public void RefreshErrorMarks()
    {
        foreach (var document in _documents)
        {
            document.RefreshErrorMark();
        }
    }

    /// <summary>Menü "Symbole": schaltet einzelne Tab-Symbole ein oder aus (siehe <see cref="TabIconSettings"/>).</summary>
    private void TabIconsButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = TabIconsButton, Placement = PlacementMode.Bottom };
        AddIconMenuItem(menu, TabIconKind.Note, new TextBlock { Text = "🗒" });
        AddIconMenuItem(menu, TabIconKind.Bookmark, new TextBlock { Text = "🔖" });
        AddIconMenuItem(menu, TabIconKind.Bug, new TextBlock { Text = "🐞" });
        AddIconMenuItem(menu, TabIconKind.Vanilla, new VanillaIcon { Width = 12, Height = 12 });
        AddIconMenuItem(menu, TabIconKind.Mod, new TextBlock { Text = "🛠" });
        AddIconMenuItem(menu, TabIconKind.Lock, new TextBlock { Text = "🔒" });
        AddIconMenuItem(menu, TabIconKind.ErrorLog, new TextBlock { Text = "⚠" });
        menu.IsOpen = true;
    }

    private static void AddIconMenuItem(ContextMenu menu, TabIconKind kind, UIElement icon)
    {
        var item = new MenuItem
        {
            Header = SettingsTexts.IconLabel(kind.ToString()),
            Icon = icon,
            IsCheckable = true,
            IsChecked = TabIconSettings.IsVisible(kind),
            StaysOpenOnClick = true,
        };
        item.Click += (_, _) => TabIconSettings.SetVisible(kind, item.IsChecked);
        menu.Items.Add(item);
    }

    private void CategoryFilterCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string id } checkBox)
        {
            return;
        }

        if (checkBox.IsChecked == true)
        {
            _hiddenCategories.Remove(id);
        }
        else
        {
            _hiddenCategories.Add(id);
        }

        CollectionViewSource.GetDefaultView(_documents).Refresh();
    }

    /// <summary>
    /// Öffnet das Kategorie-Menü für das aktive Dokument: "Automatisch" entfernt die manuelle Zuweisung,
    /// jede Kategorie weist sie manuell zu. Ohne aktives Dokument passiert nichts.
    /// </summary>
    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveDocument is not { } document)
        {
            return;
        }

        var menu = new ContextMenu { PlacementTarget = CategoryButton, Placement = PlacementMode.Bottom };

        var auto = new MenuItem
        {
            Header = EditorTexts.CategoryAutomatic(FileCategoryClassifier.DisplayName(FileCategoryClassifier.Classify(document.FilePath))),
            IsCheckable = true,
            IsChecked = document.ManualCategory is null,
        };
        auto.Click += (_, _) => AssignCategory(document, null);
        menu.Items.Add(auto);
        menu.Items.Add(new Separator());

        foreach (var category in FileCategoryClassifier.Categories)
        {
            var id = category.Id;
            var item = new MenuItem
            {
                Header = CategoryDefaults.DisplayName(category).Replace("_", "__"),
                IsCheckable = true,
                IsChecked = document.ManualCategory == id,
                Icon = new Border { Width = 10, Height = 10, Background = FileCategoryClassifier.ColorFor(id) },
            };
            item.Click += (_, _) => AssignCategory(document, id);
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        var manage = new MenuItem { Header = SettingsTexts.ManageCategories };
        manage.Click += (_, _) => ManageCategoriesRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(manage);

        menu.IsOpen = true;
    }

    private void AssignCategory(OpenDocument document, string? categoryId)
    {
        document.SetManualCategory(categoryId);
        CollectionViewSource.GetDefaultView(_documents).Refresh();
    }

    /// <summary>
    /// Leitet vertikales Mausrad-Scrollen auf der (horizontal angeordneten) Tab-Leiste in
    /// horizontales Scrollen um (Nutzer-Feedback: "Im Arbeitsbereich sollte man in der Leiste
    /// mit den offenen Dateien das Mausrad benutzen können, um in der Liste zu scrollen.").
    /// Ohne diese Umleitung würde das Mausrad auf einer horizontalen StackPanel-ItemsPanel gar
    /// nichts bewirken, da kein vertikaler Inhalt zum Scrollen vorhanden ist.
    /// </summary>
    private void Tabs_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_multiRowTabs)
        {
            return;
        }

        var scrollViewer = FindVisualChild<ScrollViewer>(Tabs);
        if (scrollViewer is null)
        {
            return;
        }

        scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _scriptHints.Close();

        // Cursorposition des zuvor aktiven Tabs merken (Abschnitt 12: Scroll-Position je
        // Dokument), bevor auf ein anderes Dokument umgeschaltet wird - sonst geht sie beim
        // Wegschalten verloren, da alle Tabs sich dasselbe AvalonEdit-Steuerelement teilen.
        if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is OpenDocument previousDocument)
        {
            previousDocument.CaretOffset = Editor.CaretOffset;
        }

        // Rahmenfarbe folgt der Kategorie des aktiven Dokuments, auch bei späterer Änderung (Wichtig/Hilfsdatei, Farbeinstellungen).
        if (_borderDocument is not null)
        {
            _borderDocument.PropertyChanged -= BorderDocument_PropertyChanged;
        }

        _borderDocument = Tabs.SelectedItem as OpenDocument;
        if (_borderDocument is not null)
        {
            _borderDocument.PropertyChanged += BorderDocument_PropertyChanged;
        }

        UpdateEditorBorder();

        if (Tabs.SelectedItem is OpenDocument document)
        {
            _selectionHistory.Remove(document);
            _selectionHistory.Add(document);
            Editor.Document = document.Document;
            Editor.SyntaxHighlighting = document.Highlighting;

            var offset = Math.Max(0, Math.Min(document.CaretOffset, document.Document.TextLength));
            Editor.CaretOffset = offset;
            Editor.ScrollToLine(document.Document.GetLineByOffset(offset).LineNumber);

        }
        else
        {
            Editor.Document = new ICSharpCode.AvalonEdit.Document.TextDocument();
            Editor.SyntaxHighlighting = null;
        }

        UpdatePathDisplay();

        // Nutzer-Feedback Runde 5 (Bugfix): "Das Ausblenden von den Wichtig und als Hilfsdatei
        // markierten Dateien funktioniert nicht." Ursache: FilterByCategory blendet das jeweils
        // AKTIVE Dokument absichtlich immer ein (siehe Kommentar dort) - das ist so gewollt, damit
        // ein Kategorie-Filter nicht den gerade bearbeiteten Tab verschwinden lässt. Der eigentliche
        // Fehler war aber, dass beim Wechsel des aktiven Tabs bisher KEIN Refresh() der gefilterten
        // Ansicht ausgelöst wurde - der zuvor aktive (jetzt nicht mehr aktive) Tab blieb dadurch
        // fälschlich sichtbar, selbst wenn seine Kategorie ausgeblendet war, bis irgendein anderes
        // Ereignis (z. B. Umschalten eines Filter-Kästchens) zufällig einen Refresh auslöste. Der
        // Refresh hier stellt sicher, dass nach jedem Tab-Wechsel sofort neu bewertet wird, welcher
        // Tab laut aktuellem Filterzustand sichtbar sein soll.
        CollectionViewSource.GetDefaultView(_documents).Refresh();

        _scopeStatus.Refresh();
        _brackets.Refresh();
        _warnings.Refresh();
        UpdateGuiMode();
        ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool _multiRowTabs;

    /// <summary>
    /// Schaltet die Tab-Leiste zwischen einer einzelnen, horizontal scrollbaren Zeile (Standard) und
    /// mehreren umbrechenden Zeilen um (Nutzer-Feedback: mehr offene Dokumente gleichzeitig sichtbar).
    /// </summary>
    public void SetMultiRowTabs(bool multiRow)
    {
        _multiRowTabs = multiRow;

        var factory = new FrameworkElementFactory(multiRow ? typeof(WrapPanel) : typeof(StackPanel));
        if (!multiRow)
        {
            factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        }

        Tabs.ItemsPanel = new ItemsPanelTemplate(factory);

        // Ein WrapPanel bricht nur um, wenn die Breite begrenzt ist - dann darf der ScrollViewer nicht horizontal scrollen.
        ScrollViewer.SetHorizontalScrollBarVisibility(Tabs, multiRow ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
    }

    // ---- Tabs per Drag&Drop umsortieren (Nutzer-Feedback) ----

    private Point _dragStart;
    private OpenDocument? _dragDocument;

    private void Tabs_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragDocument = null;

        // Klicks auf die Buttons im Tab (Notiz, Lesezeichen, Schließen) starten kein Ziehen.
        if (e.OriginalSource is DependencyObject source
            && FindAncestor<Button>(source) is null
            && FindAncestor<ListBoxItem>(source) is { DataContext: OpenDocument document })
        {
            _dragStart = e.GetPosition(null);
            _dragDocument = document;
        }
    }

    private void Tabs_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragDocument is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var document = _dragDocument;
        _dragDocument = null;
        DragDrop.DoDragDrop(Tabs, new DataObject(typeof(OpenDocument), document), DragDropEffects.Move);
    }

    /// <summary>
    /// Ein Tab aus dem ANDEREN Arbeitsbereich wurde hier abgelegt (Tab-Leiste oder Editorfläche). Das
    /// Hauptfenster verschiebt das Dokument (siehe MainWindow.MoveDocumentBetweenPanes).
    /// <see cref="DocumentDropEventArgs.InsertBefore"/> ist der Tab, vor dem eingefügt werden soll, oder null für "ans Ende".
    /// </summary>
    public event EventHandler<DocumentDropEventArgs>? DocumentDropped;

    private void Tabs_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetData(typeof(OpenDocument)) is OpenDocument
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Tabs_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(OpenDocument)) is not OpenDocument dragged)
        {
            return;
        }

        var target = e.OriginalSource is DependencyObject source
            ? FindAncestor<ListBoxItem>(source)?.DataContext as OpenDocument
            : null;

        if (!_documents.Contains(dragged))
        {
            DocumentDropped?.Invoke(this, new DocumentDropEventArgs(dragged, target));
            e.Handled = true;
            return;
        }

        MoveDocument(dragged, target);
        e.Handled = true;
    }

    // Ablegen eines Tabs aus dem anderen Arbeitsbereich direkt auf der Editorfläche.
    private void EditorArea_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(OpenDocument)) is OpenDocument dragged)
        {
            e.Effects = _documents.Contains(dragged) ? DragDropEffects.None : DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private void EditorArea_PreviewDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(OpenDocument)) is OpenDocument dragged)
        {
            if (!_documents.Contains(dragged))
            {
                DocumentDropped?.Invoke(this, new DocumentDropEventArgs(dragged, null));
            }

            e.Handled = true;
        }
    }

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        var current = start;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            // ContentElements (z. B. Run in einem TextBlock) haben keinen visuellen Elternknoten.
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }

    private OpenDocument? _borderDocument;

    private void BorderDocument_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OpenDocument.CategoryColor))
        {
            UpdateEditorBorder();
        }
    }

    private void UpdateEditorBorder() =>
        EditorBorder.BorderBrush = _borderDocument?.CategoryColor ?? Brushes.Transparent;

    private void NewDocument_Click(object sender, RoutedEventArgs e) => NewDocumentRequested?.Invoke(this, EventArgs.Empty);

    private void OpenDocument_Click(object sender, RoutedEventArgs e) => OpenDocumentRequested?.Invoke(this, EventArgs.Empty);

    private void SaveDocument_Click(object sender, RoutedEventArgs e) => SaveDocumentRequested?.Invoke(this, EventArgs.Empty);

    private void SaveAllDocuments_Click(object sender, RoutedEventArgs e) => SaveAllRequested?.Invoke(this, EventArgs.Empty);

    private void CloseActiveDocument_Click(object sender, RoutedEventArgs e) => CloseActiveDocument();

    private void MoveActiveDocument_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveDocument is { } document)
        {
            MoveActiveDocumentRequested?.Invoke(this, document);
        }
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: OpenDocument document })
        {
            RequestClose(document);
        }
    }

    /// <summary>Nutzer-Feedback Runde 5: Kontextmenü-Eintrag "Ordner öffnen, wo die Datei liegt" direkt am Tab.</summary>
    private void OpenContainingFolderFromTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: OpenDocument document })
        {
            OpenContainingFolder(document.FilePath);
        }
    }

    /// <summary>Nutzer-Feedback Runde 5: Klick auf das Notiz-Icon eines Tabs öffnet dessen Notiz direkt.</summary>
    private void TabNoteIcon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: OpenDocument document })
        {
            ShowNoteRequested?.Invoke(this, document);
        }
    }

    private void TabBugIcon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: OpenDocument document })
        {
            ShowBugRequested?.Invoke(this, document);
        }
    }

    private void TabErrorIcon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: OpenDocument document })
        {
            ShowErrorLogRequested?.Invoke(this, document);
        }
    }

    /// <summary>Nutzer-Feedback Runde 5: "📋"-Button neben der Pfadanzeige kopiert den Pfad der aktiven Datei.</summary>
    private void CopyPathButton_Click(object sender, RoutedEventArgs e) => CopyPathToClipboard(ActiveDocument?.FilePath);

    /// <summary>Nutzer-Feedback Runde 5: "📂"-Button neben der Pfadanzeige öffnet den Ordner der aktiven Datei.</summary>
    private void OpenFolderButton_Click(object sender, RoutedEventArgs e) => OpenContainingFolder(ActiveDocument?.FilePath);

    /// <summary>Nutzer-Feedback Runde 5: "Notizen anzeigen"-Button neben der Pfadanzeige für das aktive Dokument.</summary>
    private void ShowNoteButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveDocument is { } document)
        {
            ShowNoteRequested?.Invoke(this, document);
        }
    }

    /// <summary>
    /// Kopiert einen Dateipfad in die Zwischenablage (Nutzer-Feedback Runde 5: "Möglichkeit den
    /// angezeigten Dateipfad zu kopieren, mit einem kleinen Button daneben"). Ohne Wirkung, wenn
    /// kein Dokument aktiv ist oder es noch keinen Dateipfad hat (neues, ungespeichertes
    /// Dokument). Ein kurzzeitig durch ein anderes Programm gesperrter Zwischenspeicher (z. B.
    /// während ein Screenshot-Tool gerade selbst hineinschreibt) wird stillschweigend ignoriert,
    /// statt eine für diese Aktion unnötige Fehlermeldung zu zeigen - ein erneuter Klick genügt.
    /// </summary>
    private static void CopyPathToClipboard(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        try
        {
            Clipboard.SetText(filePath);
        }
        catch (COMException)
        {
        }
        catch (ExternalException)
        {
        }
    }

    /// <summary>
    /// Öffnet den Windows-Explorer im Ordner, in dem <paramref name="filePath"/> liegt (Nutzer-
    /// Feedback Runde 5: "Rechtsklick auf Datei: Optionen: Ordner öffnen wo Datei liegt" + Button
    /// neben der Pfadanzeige). Existiert die Datei noch (Normalfall), wird sie zusätzlich per
    /// "/select," im geöffneten Ordnerfenster markiert, statt den Ordner nur allgemein zu öffnen -
    /// das entspricht der ausdrücklichen Nutzer-Formulierung "wo Datei liegt". Ist die Datei seit
    /// dem Öffnen des Tabs gelöscht/verschoben worden, wird ersatzweise nur der zuletzt bekannte
    /// Elternordner geöffnet, falls er noch existiert. Ohne Wirkung, wenn kein Dokument aktiv ist,
    /// es noch keinen Dateipfad hat, oder weder Datei noch Ordner mehr existieren.
    /// </summary>
    private static void OpenContainingFolder(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        try
        {
            // Der Explorer versteht nur Backslashes; ein gemischter Pfad (…/mod\Name) öffnet sonst "Dokumente".
            filePath = Path.GetFullPath(filePath);
            if (File.Exists(filePath))
            {
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                return;
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Process.Start("explorer.exe", $"\"{directory}\"");
            }
        }
        catch (Exception ex) when (ex is Win32Exception or ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Explorer konnte nicht gestartet werden - stillschweigend ignorieren, kein
            // kritischer Pfad (der Nutzer sieht einfach keine Reaktion und kann es erneut
            // versuchen bzw. den Pfad stattdessen über "📋 Kopieren" selbst öffnen).
        }
    }
}

/// <summary>Daten eines aus dem anderen Arbeitsbereich hierher gezogenen Tabs (siehe <see cref="EditorPane.DocumentDropped"/>).</summary>
public sealed class DocumentDropEventArgs : EventArgs
{
    public DocumentDropEventArgs(OpenDocument document, OpenDocument? insertBefore)
    {
        Document = document;
        InsertBefore = insertBefore;
    }

    public OpenDocument Document { get; }

    public OpenDocument? InsertBefore { get; }
}
