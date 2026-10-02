using System.IO;
using System.Windows;
using System.Windows.Controls;
using Border = System.Windows.Controls.Border;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;
using TextBlock = System.Windows.Controls.TextBlock;
using UserControl = System.Windows.Controls.UserControl;
using ParadoxModTool.Localization;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.App;

/// <summary>
/// Ein Vergleich im Compare und Update Tool: zwei Editoren mit strukturellem Diff, Ausrichtungslücken, Übersichtsleisten, Faltung,
/// synchronem Scrollen, Speichern und Sprung zur nächsten Abweichung. Jeder Reiter des Fensters hat eine eigene Instanz, damit beim
/// Umschalten beide Vergleiche erhalten bleiben.
/// </summary>
public partial class DiffComparePane : UserControl
{
    private readonly System.Windows.Threading.DispatcherTimer _diffTimer;
    private ICSharpCode.AvalonEdit.TextEditor? _lastFocusedEditor;
    private bool _rightDirty;

    /// <summary>false, solange eine Seite nicht lesbar ist (Vergleich pausiert): die Ausrichtung passt dann nicht zu den Texten.</summary>
    private bool _alignmentValid;
    private bool _leftDirty;

    /// <summary>Unterdrückt die Änderungsverfolgung, solange <see cref="LoadPair"/> die Editortexte setzt.</summary>
    private bool _loadingFiles;

    /// <summary>Dateien, für die in dieser Sitzung schon vor dem Speichern gesichert wurde (ein Snapshot je Datei genügt).</summary>
    private readonly HashSet<string> _snapshotDoneFor = new(StringComparer.OrdinalIgnoreCase);

    private readonly BookmarkMargin _leftBookmarkMargin = new();
    private readonly BookmarkMargin _rightBookmarkMargin = new();

    /// <summary>Haltepunkte für "Vorige/Nächste Abweichung" je Seite, aus dem letzten Vergleich.</summary>
    private IReadOnlyList<int> _leftStops = Array.Empty<int>();
    private IReadOnlyList<int> _rightStops = Array.Empty<int>();

    /// <summary>Lücken des letzten Vergleichs (für das Übernehmen markierter Zeilen).</summary>
    private IReadOnlyList<AlignmentGap> _gaps = Array.Empty<AlignmentGap>();

    private ICSharpCode.AvalonEdit.Folding.FoldingManager? _leftFoldingManager;
    private ICSharpCode.AvalonEdit.Folding.FoldingManager? _rightFoldingManager;
    private readonly AlignmentSpacerGenerator _leftSpacers = new();
    private readonly AlignmentSpacerGenerator _rightSpacers = new();
    private Action<bool> _setFoldingEnabled = _ => { };
    private Func<string, bool> _snapshotBeforeSave = _ => true;
    private bool _foldingEnabled = true;

    /// <summary>Anfangs an: das Häkchen löst schon in <c>InitializeComponent</c> Ereignisse aus, bevor <see cref="Initialize"/> lief.</summary>
    private bool _loadingFoldingOption = true;

    /// <summary>Wiedereintritts-Schutz beim synchronen Scrollen (siehe <see cref="SyncScroll"/>).</summary>
    private bool _syncingScroll;

    public DiffComparePane()
    {
        InitializeComponent();

        _diffTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _diffTimer.Tick += (_, _) =>
        {
            _diffTimer.Stop();
            RefreshDiffView();
        };
        RightEditor.TextChanged += (_, _) => EditorTextChanged(left: false);
        LeftEditor.TextChanged += (_, _) => EditorTextChanged(left: true);
        LeftEditor.GotKeyboardFocus += (_, _) => _lastFocusedEditor = LeftEditor;
        RightEditor.GotKeyboardFocus += (_, _) => _lastFocusedEditor = RightEditor;

        LeftEditor.TextArea.TextView.ElementGenerators.Add(_leftSpacers);
        RightEditor.TextArea.TextView.ElementGenerators.Add(_rightSpacers);
        LeftEditor.TextArea.TextView.BackgroundRenderers.Add(new AlignmentGapRenderer(_leftSpacers));
        RightEditor.TextArea.TextView.BackgroundRenderers.Add(new AlignmentGapRenderer(_rightSpacers));

        // Textauswahl-Farbe aus dem Theme; Diff-Hintergründe der Zeilen zeichnen bei einem Themenwechsel neu.
        LeftEditor.TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty, "Theme.EditorSelection");
        RightEditor.TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty, "Theme.EditorSelection");
        EventHandler onThemeChanged = (_, _) =>
        {
            LeftEditor.TextArea.TextView.Redraw();
            RightEditor.TextArea.TextView.Redraw();
        };
        Loaded += (_, _) =>
        {
            ThemeManager.ThemeChanged -= onThemeChanged;
            ThemeManager.ThemeChanged += onThemeChanged;
        };
        Unloaded += (_, _) => ThemeManager.ThemeChanged -= onThemeChanged;

        // Lesezeichen-Rand ganz links im Editor, vor den Zeilennummern.
        LeftEditor.TextArea.LeftMargins.Insert(0, _leftBookmarkMargin);
        RightEditor.TextArea.LeftMargins.Insert(0, _rightBookmarkMargin);

        LeftEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) => SyncScroll(LeftEditor, RightEditor);
        RightEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) => SyncScroll(RightEditor, LeftEditor);

        // Zeilen markieren im Zeilennummern-Rand; eine neue Markierung hebt die der anderen Seite auf.
        _leftMarks = new LineMarkController(LeftEditor, _leftSpacers);
        _rightMarks = new LineMarkController(RightEditor, _rightSpacers);
        _leftMarks.MarkingStarted += (_, _) => _rightMarks.Clear();
        _rightMarks.MarkingStarted += (_, _) => _leftMarks.Clear();
        _leftMarks.TakeRequested += (_, _) => TakeMarked();
        _rightMarks.TakeRequested += (_, _) => TakeMarked();
    }

    private readonly LineMarkController _leftMarks;
    private readonly LineMarkController _rightMarks;

    /// <summary>Dokument, in das zuletzt übernommen wurde, und seine Version danach (Strg+Z macht die Übernahme nur ungeändert rückgängig).</summary>
    private ICSharpCode.AvalonEdit.TextEditor? _takeTarget;
    private ICSharpCode.AvalonEdit.Document.ITextSourceVersion? _takeVersion;

    /// <summary>Auf einer Seite sind Zeilen markiert.</summary>
    public bool HasMarks => _leftMarks.Marks is not null || _rightMarks.Marks is not null;

    public void ClearMarks()
    {
        _leftMarks.Clear();
        _rightMarks.Clear();
    }

    /// <summary>
    /// Übernimmt die markierten Zeilen in die andere Seite (Taste U, Rechtsklick): an der ausgerichteten Stelle genau diese Zeilen
    /// (<see cref="LineTake"/>). Nur in eine bearbeitbare Seite; sonst sagt die Statuszeile, warum nichts geschah.
    /// </summary>
    public bool TakeMarked()
    {
        var fromLeft = _leftMarks.Marks is not null;
        if ((fromLeft ? _leftMarks.Marks : _rightMarks.Marks) is not { } marks || LeftPath is null || RightPath is null)
        {
            StatusChanged?.Invoke(this, MiscWindowTexts.DiffTakeNothing);
            return false;
        }

        var target = fromLeft ? RightEditor : LeftEditor;
        if (!(fromLeft ? RightEditable : LeftEditable))
        {
            StatusChanged?.Invoke(this, MiscWindowTexts.DiffTakeReadOnly(fromLeft ? _rightLabel : _leftLabel));
            return false;
        }

        // Ausrichtung zu den aktuellen Texten (eine Tipp-Pause kann den Vergleich noch zurückhalten).
        if (_diffTimer.IsEnabled)
        {
            _diffTimer.Stop();
            RefreshDiffView();
        }

        if (!_alignmentValid)
        {
            // Ohne gültige Ausrichtung träfe die Übernahme falsche Zeilen (z. B. bei .yml-Dateien, die kein Skript sind).
            StatusChanged?.Invoke(this, MiscWindowTexts.DiffTakePaused);
            return false;
        }

        var source = fromLeft ? LeftEditor : RightEditor;
        var rows = DiffRows.Build(LeftEditor.Document.LineCount, RightEditor.Document.LineCount, _gaps);
        var newText = LineTake.Apply(rows, source.Text, target.Text, fromLeft ? DiffSide.Left : DiffSide.Right, marks.First, marks.Last);
        ReplaceChangedPart(target.Document, newText);
        ClearMarks();
        _diffTimer.Stop();
        RefreshDiffView();
        _takeTarget = target;
        _takeVersion = target.Document.Version;
        StatusChanged?.Invoke(this, MiscWindowTexts.DiffTaken(marks.Count));
        return true;
    }

    /// <summary>Strg+Z direkt nach einer Übernahme: macht sie rückgängig, solange das Ziel seither nicht geändert wurde.</summary>
    public bool UndoLastTake()
    {
        if (_takeTarget is not { } target || !ReferenceEquals(target.Document.Version, _takeVersion) || !target.CanUndo)
        {
            _takeTarget = null;
            return false;
        }

        _takeTarget = null;
        target.Undo();
        return true;
    }

    /// <summary>Ersetzt nur den geänderten Mittelteil (gleicher Anfang und gleiches Ende bleiben), damit Cursor und Scrollposition stehen bleiben; ein Rückgängig-Schritt.</summary>
    private static void ReplaceChangedPart(ICSharpCode.AvalonEdit.Document.TextDocument document, string newText)
    {
        var oldText = document.Text;
        var prefix = 0;
        while (prefix < oldText.Length && prefix < newText.Length && oldText[prefix] == newText[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < oldText.Length - prefix && suffix < newText.Length - prefix && oldText[^(suffix + 1)] == newText[^(suffix + 1)])
        {
            suffix++;
        }

        document.Replace(prefix, oldText.Length - prefix - suffix, newText.Substring(prefix, newText.Length - prefix - suffix));
    }

    /// <summary>Meldung für die Statuszeile des Reiters.</summary>
    public event EventHandler<string>? StatusChanged;

    /// <summary>Knopf "Suchen" gedrückt; das Fenster öffnet Suchen &amp; Ersetzen für diesen Vergleich.</summary>
    public event EventHandler? SearchRequested;

    /// <summary>Häkchen "Blöcke einklappbar" geändert; das Fenster überträgt es auf den anderen Reiter.</summary>
    public event EventHandler<bool>? FoldingToggled;

    /// <summary>Eine Seite wurde gespeichert (der Wert ist der Pfad); der Ordnervergleich prüft die Datei neu.</summary>
    public event EventHandler<string>? FileSaved;

    public string? LeftPath { get; private set; }

    public string? RightPath { get; private set; }

    public bool LeftEditable { get; private set; }

    public bool RightEditable { get; private set; } = true;

    private string _leftLabel = string.Empty;
    private string _rightLabel = string.Empty;

    public bool HasUnsavedChanges => !_previewMode && (_leftDirty || _rightDirty);

    /// <summary>Vorschau-Modus (<see cref="LoadPreview"/>): die rechte Seite ist bearbeitbar, wird aber nie gespeichert.</summary>
    private bool _previewMode;

    /// <summary>Der aktuelle Text der rechten Seite (in der Vorschau: der geplante Text samt zurückgenommener Zeilen).</summary>
    public string RightText => RightEditor.Text;

    /// <summary>Die rechte Seite hat sich geändert (Tippen oder Übernehmen mit U), nicht beim Laden.</summary>
    public event EventHandler? RightTextChanged;

    /// <summary>Zuletzt benutzter Editor, sonst der rechte.</summary>
    public ICSharpCode.AvalonEdit.TextEditor FocusedEditor => _lastFocusedEditor ?? RightEditor;

    /// <param name="snapshotBeforeSave">Vor dem ersten Speichern je Datei: sichert bzw. fragt nach; <see langword="false"/> = nicht speichern.</param>
    public void Initialize(Func<bool> getFoldingEnabled, Action<bool> setFoldingEnabled, Func<string, bool> snapshotBeforeSave)
    {
        _setFoldingEnabled = setFoldingEnabled;
        _snapshotBeforeSave = snapshotBeforeSave;
        _foldingEnabled = getFoldingEnabled();
        _loadingFoldingOption = true;
        FoldingCheckBox.IsChecked = _foldingEnabled;
        _loadingFoldingOption = false;
    }

    /// <summary>Lädt ein Dateipaar; die Beschriftungen stehen vor den Pfaden über den Editoren.</summary>
    public void LoadPair(string leftPath, string rightPath, string leftLabel, string rightLabel, bool leftEditable, bool rightEditable)
    {
        // Ordnervergleich: eine Datei, die es nur auf einer Seite gibt, zeigt die andere Seite leer; Speichern legt sie dort an.
        var leftText = File.Exists(leftPath) ? File.ReadAllText(leftPath) : string.Empty;
        var rightText = File.Exists(rightPath) ? File.ReadAllText(rightPath) : string.Empty;
        LoadCore(leftText, rightText, leftPath, rightPath, leftLabel, rightLabel, leftEditable, rightEditable);
    }

    /// <summary>Liest beide Seiten neu von der Platte (nach Änderungen durch ein anderes Werkzeug), Beschriftungen und Schreibschutz bleiben.</summary>
    public void ReloadFromDisk()
    {
        if (LeftPath is { } left && RightPath is { } right)
        {
            LoadPair(left, right, _leftLabel, _rightLabel, LeftEditable, RightEditable);
        }
    }

    /// <summary>Zeigt zwei Texte, die nicht (so) auf der Platte stehen, z. B. die Vorschau einer Ersetzungsliste; beide Seiten schreibgeschützt.</summary>
    public void LoadTexts(string leftText, string rightText, string leftLabel, string rightLabel, string path) =>
        LoadCore(leftText, rightText, path, path, leftLabel, rightLabel, leftEditable: false, rightEditable: false);

    /// <summary>
    /// Vorschau eines geplanten Texts (Patch-Rezepte): links der aktuelle Stand schreibgeschützt, rechts der geplante Text bearbeitbar,
    /// damit Zeilen mit U zurückgenommen werden können. Gespeichert wird nie; den Text liest der Aufrufer über <see cref="RightText"/>.
    /// </summary>
    public void LoadPreview(string currentText, string plannedText, string leftLabel, string rightLabel, string path)
    {
        LoadCore(currentText, plannedText, path, path, leftLabel, rightLabel, leftEditable: false, rightEditable: true);
        _previewMode = true;
        UpdateDirtyUi();
    }

    private void LoadCore(string leftText, string rightText, string leftPath, string rightPath, string leftLabel, string rightLabel, bool leftEditable, bool rightEditable)
    {
        _previewMode = false;
        LeftPath = leftPath;
        RightPath = rightPath;
        LeftEditable = leftEditable;
        _leftLabel = leftLabel;
        _rightLabel = rightLabel;
        ClearMarks();
        _takeTarget = null;
        RightEditable = rightEditable;

        _loadingFiles = true;
        try
        {
            LeftEditor.Text = leftText;
            RightEditor.Text = rightText;
        }
        finally
        {
            _loadingFiles = false;
        }

        _diffTimer.Stop();
        _rightDirty = false;
        _leftDirty = false;
        LeftEditor.IsReadOnly = !leftEditable;
        RightEditor.IsReadOnly = !rightEditable;
        UpdateDirtyUi();

        // Rahmen- und Pfadfarbe unabhängig von der Dateikategorie: links blau, rechts orange (Theme-Tokens DiffVanilla/DiffMod).
        LeftBorder.SetResourceReference(Border.BorderBrushProperty, "Theme.DiffVanilla");
        RightBorder.SetResourceReference(Border.BorderBrushProperty, "Theme.DiffMod");
        LeftBorder.ToolTip = MiscWindowTexts.DiffLeftTip(leftPath);
        RightBorder.ToolTip = MiscWindowTexts.DiffRightTip(rightPath);
        LeftPathText.Text = $"{leftLabel}: {leftPath}";
        RightPathText.Text = $"{rightLabel}: {rightPath}";
        LeftPathText.SetResourceReference(TextBlock.ForegroundProperty, "Theme.DiffVanilla");
        RightPathText.SetResourceReference(TextBlock.ForegroundProperty, "Theme.DiffMod");
        LeftPathText.ToolTip = leftPath;
        RightPathText.ToolTip = rightPath;

        RefreshDiffView();
    }

    /// <summary>Setzt den Text der rechten Seite von außen (Suchen &amp; Ersetzen), zählt als Änderung.</summary>
    public void ReplaceFocusedText(string newText, Window owner)
    {
        if ((ReferenceEquals(FocusedEditor, LeftEditor) && !LeftEditable) || (ReferenceEquals(FocusedEditor, RightEditor) && !RightEditable))
        {
            MessageBox.Show(owner, MiscWindowTexts.DiffLeftReadOnly, MiscWindowTexts.DiffReplaceNotPossible);
            return;
        }

        FocusedEditor.Document.Text = newText;
    }

    /// <summary>Wählt einen Bereich im zuletzt benutzten Editor (Suchen &amp; Ersetzen).</summary>
    public void SelectInFocusedEditor(int offset, int length)
    {
        var editor = FocusedEditor;
        var start = Math.Clamp(offset, 0, editor.Document.TextLength);
        var end = Math.Clamp(length, 0, editor.Document.TextLength - start);
        editor.Select(start, end);
        editor.ScrollToLine(editor.Document.GetLineByOffset(start).LineNumber);
        editor.Focus();
    }

    /// <summary>Überträgt "Blöcke einklappbar" vom anderen Reiter, ohne die Einstellung erneut zu schreiben.</summary>
    public void ApplyFolding(bool enabled)
    {
        _loadingFoldingOption = true;
        FoldingCheckBox.IsChecked = enabled;
        _loadingFoldingOption = false;
        SetFolding(enabled);
    }

    /// <summary>Setzt den Cursor im zuletzt benutzten Editor (sonst rechts) auf den nächsten Haltepunkt (<see cref="DiffNavigation"/>).</summary>
    public void JumpToDifference(bool forward)
    {
        var editor = FocusedEditor;
        var stops = ReferenceEquals(editor, LeftEditor) ? _leftStops : _rightStops;
        if (DiffNavigation.Next(stops, editor.TextArea.Caret.Line, forward) is not { } line || line > editor.Document.LineCount)
        {
            StatusChanged?.Invoke(this, MiscWindowTexts.DiffNoFurther);
            return;
        }

        editor.TextArea.Caret.Line = line;
        editor.TextArea.Caret.Column = 1;
        editor.ScrollToLine(line);
        editor.Focus();
    }

    /// <summary>
    /// Fragt bei ungespeicherten Änderungen nach (Speichern / Verwerfen / Abbrechen). Liefert <see langword="true"/>, wenn der Aufrufer
    /// fortfahren darf.
    /// </summary>
    public bool ConfirmProceedWithUnsavedChanges(Window owner)
    {
        if (!HasUnsavedChanges)
        {
            return true;
        }

        var names = new[] { _leftDirty ? LeftPath : null, _rightDirty ? RightPath : null }
            .Where(p => p is not null)
            .Select(p => Path.GetFileName(p));
        var answer = MessageBox.Show(
            owner,
            MiscWindowTexts.DiffUnsavedPrompt(string.Join(", ", names)),
            MiscWindowTexts.DiffUnsavedTitle,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Yes);

        return answer switch
        {
            MessageBoxResult.Yes => SaveAll(),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    /// <summary>Speichert jede geänderte Seite; <see langword="false"/>, wenn eine davon nicht gespeichert werden konnte.</summary>
    public bool SaveAll()
    {
        if (_previewMode)
        {
            return true;
        }

        var ok = !_leftDirty || SaveSide(left: true);
        return (!_rightDirty || SaveSide(left: false)) && ok;
    }

    private void SyncScroll(ICSharpCode.AvalonEdit.TextEditor source, ICSharpCode.AvalonEdit.TextEditor target)
    {
        if (_syncingScroll)
        {
            return;
        }

        _syncingScroll = true;
        var offset = source.TextArea.TextView.ScrollOffset;
        target.ScrollToVerticalOffset(offset.Y);
        target.ScrollToHorizontalOffset(offset.X);

        // Das Echo-Ereignis der Zielseite kommt erst nach dem nächsten Layout-Durchlauf; der Schutz fällt deshalb erst danach
        // (Background-Priorität), sonst flackert das Ziehen der horizontalen Scrollbar hin und her.
        Dispatcher.BeginInvoke(new Action(() => _syncingScroll = false), System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>Berechnet Diff, Färbung, Übersichtsleisten und Faltung aus den aktuellen Editortexten neu.</summary>
    private void RefreshDiffView()
    {
        var leftPath = LeftPath;
        var rightPath = RightPath;
        if (leftPath is null || rightPath is null)
        {
            return;
        }

        ScriptBlock leftRoot;
        ScriptBlock rightRoot;
        try
        {
            leftRoot = ScriptDocument.Parse(LeftEditor.Text);
            rightRoot = ScriptDocument.Parse(RightEditor.Text);
        }
        catch (ScriptParseException ex)
        {
            // Beim Tippen ist die Datei zwischenzeitlich oft ungültig - Vergleich pausiert, alte Färbung bleibt stehen.
            StatusChanged?.Invoke(this, MiscWindowTexts.DiffPaused(ex.Message));
            _alignmentValid = false;
            return;
        }

        _alignmentValid = true;
        var entryDiffs = ScriptDiffer.Diff(leftRoot, rightRoot);

        // Lesezeichen der beiden Dateien (Strg+F2) als Symbol am Rand und als Markierung in der Übersichtsleiste.
        var bookmarks = ParadoxModTool.Notes.BookmarkStore.TryLoad(ParadoxModTool.Notes.BookmarkStore.GetDefaultBookmarksFilePath());
        var leftBookmarkLines = ParadoxModTool.Notes.BookmarkStore.GetBookmarks(bookmarks, leftPath).Select(b => b.Line).ToList();
        var rightBookmarkLines = ParadoxModTool.Notes.BookmarkStore.GetBookmarks(bookmarks, rightPath).Select(b => b.Line).ToList();
        _leftBookmarkMargin.SetLines(leftBookmarkLines);
        _rightBookmarkMargin.SetLines(rightBookmarkLines);

        var leftKinds = ApplyDiffColoring(LeftEditor, entryDiffs, useLeftSide: true, _leftSpacers);
        var rightKinds = ApplyDiffColoring(RightEditor, entryDiffs, useLeftSide: false, _rightSpacers);
        LeftOverview.Update(LeftEditor, leftKinds, leftBookmarkLines);
        RightOverview.Update(RightEditor, rightKinds, rightBookmarkLines);

        var leftDocument = LeftEditor.Document;
        var rightDocument = RightEditor.Document;
        var gaps = DiffAlignment.Compute(
            entryDiffs,
            offset => leftDocument.GetLineByOffset(Math.Clamp(offset, 0, leftDocument.TextLength)).LineNumber,
            offset => rightDocument.GetLineByOffset(Math.Clamp(offset, 0, rightDocument.TextLength)).LineNumber);
        _gaps = gaps;
        _leftSpacers.SetGaps(gaps.Where(g => g.Side == DiffSide.Left), leftDocument);
        _rightSpacers.SetGaps(gaps.Where(g => g.Side == DiffSide.Right), rightDocument);
        _leftStops = DiffNavigation.Stops(leftKinds.Where(k => k.Value != DiffKind.Unchanged).Select(k => k.Key), gaps.Where(g => g.Side == DiffSide.Left).Select(g => g.BeforeLine));
        _rightStops = DiffNavigation.Stops(rightKinds.Where(k => k.Value != DiffKind.Unchanged).Select(k => k.Key), gaps.Where(g => g.Side == DiffSide.Right).Select(g => g.BeforeLine));
        LeftEditor.TextArea.TextView.Redraw();
        RightEditor.TextArea.TextView.Redraw();
        _leftSpacers.ApplyHeights(LeftEditor.TextArea.TextView);
        _rightSpacers.ApplyHeights(RightEditor.TextArea.TextView);

        if (!_foldingEnabled)
        {
            return;
        }

        _leftFoldingManager ??= ICSharpCode.AvalonEdit.Folding.FoldingManager.Install(LeftEditor.TextArea);
        _rightFoldingManager ??= ICSharpCode.AvalonEdit.Folding.FoldingManager.Install(RightEditor.TextArea);
        _leftFoldingManager.UpdateFoldings(ScriptBlockFoldingStrategy.CreateFoldings(leftRoot), -1);
        _rightFoldingManager.UpdateFoldings(ScriptBlockFoldingStrategy.CreateFoldings(rightRoot), -1);
    }

    private static Dictionary<int, DiffKind> ApplyDiffColoring(ICSharpCode.AvalonEdit.TextEditor editor, IReadOnlyList<EntryDiff> diffs, bool useLeftSide, AlignmentSpacerGenerator spacers)
    {
        foreach (var renderer in editor.TextArea.TextView.BackgroundRenderers.OfType<DiffBackgroundRenderer>().ToList())
        {
            editor.TextArea.TextView.BackgroundRenderers.Remove(renderer);
        }

        var document = editor.Document;
        var lineKinds = DiffLineKinds.Compute(diffs, useLeftSide, offset => document.GetLineByOffset(Math.Min(offset, Math.Max(0, document.TextLength - 1))).LineNumber);
        editor.TextArea.TextView.BackgroundRenderers.Add(new DiffBackgroundRenderer(lineKinds, spacers));
        editor.TextArea.TextView.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Background);
        return lineKinds;
    }

    private void FoldingCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingFoldingOption)
        {
            return;
        }

        var enabled = FoldingCheckBox.IsChecked == true;
        _setFoldingEnabled(enabled);
        SetFolding(enabled);
        FoldingToggled?.Invoke(this, enabled);
    }

    private void SetFolding(bool enabled)
    {
        _foldingEnabled = enabled;
        if (enabled)
        {
            RefreshDiffView();
            return;
        }

        if (_leftFoldingManager is not null)
        {
            ICSharpCode.AvalonEdit.Folding.FoldingManager.Uninstall(_leftFoldingManager);
            _leftFoldingManager = null;
        }

        if (_rightFoldingManager is not null)
        {
            ICSharpCode.AvalonEdit.Folding.FoldingManager.Uninstall(_rightFoldingManager);
            _rightFoldingManager = null;
        }
    }

    private void EditorTextChanged(bool left)
    {
        if (_loadingFiles || (left ? !LeftEditable : !RightEditable))
        {
            return;
        }

        // Die Markierung gilt für den Text, über den sie gezogen wurde.
        (left ? _leftMarks : _rightMarks).Clear();

        if (left)
        {
            _leftDirty = true;
        }
        else
        {
            _rightDirty = true;
        }

        UpdateDirtyUi();
        if (!left)
        {
            RightTextChanged?.Invoke(this, EventArgs.Empty);
        }

        // Vergleich erst nach einer kurzen Tipp-Pause neu berechnen.
        _diffTimer.Stop();
        _diffTimer.Start();
    }

    private void UpdateDirtyUi()
    {
        SaveButton.IsEnabled = !_previewMode && ((_rightDirty && RightPath is not null) || (_leftDirty && LeftPath is not null));
        SaveButton.Visibility = _previewMode ? Visibility.Collapsed : Visibility.Visible;
        DirtyIndicator.Visibility = HasUnsavedChanges ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Schreibt einen Editor in seine Datei. Vor dem ersten Speichern je Datei und Sitzung sichert bzw. fragt <see cref="_snapshotBeforeSave"/>.
    /// Behält ein vorhandenes UTF-8-BOM der Datei bei.
    /// </summary>
    private bool SaveSide(bool left)
    {
        if (_previewMode || (left ? LeftPath : RightPath) is not { } path || !(left ? LeftEditable : RightEditable))
        {
            return false;
        }

        try
        {
            if (_snapshotDoneFor.Add(path) && !_snapshotBeforeSave(path))
            {
                _snapshotDoneFor.Remove(path);
                return false;
            }

            var hadBom = File.Exists(path) && HasUtf8Bom(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, (left ? LeftEditor : RightEditor).Text, new System.Text.UTF8Encoding(hadBom));
            if (left)
            {
                _leftDirty = false;
            }
            else
            {
                _rightDirty = false;
            }

            UpdateDirtyUi();
            StatusChanged?.Invoke(this, MiscWindowTexts.DiffSaved(Path.GetFileName(path)));
            FileSaved?.Invoke(this, path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _snapshotDoneFor.Remove(path);
            MessageBox.Show(Window.GetWindow(this)!, MiscWindowTexts.DiffSaveFailed(ex.Message), CommonTexts.Error);
            return false;
        }
    }

    private static bool HasUtf8Bom(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[3];
            return stream.Read(buffer, 0, 3) == 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Öffnet den Ordner der linken bzw. rechten Datei im Windows-Explorer, die Datei ist dort markiert.</summary>
    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (PathOfSide(sender) is not { } path)
        {
            return;
        }

        try
        {
            path = ParadoxModTool.Explorer.PathNormalization.ToWindowsPath(path);
            var arguments = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{Path.GetDirectoryName(path)}\"";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(Window.GetWindow(this)!, MiscWindowTexts.DiffExplorerFailed(ex.Message), CommonTexts.Error);
        }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (PathOfSide(sender) is not { } path)
        {
            return;
        }

        try
        {
            Clipboard.SetText(path);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Zwischenablage kurzzeitig durch ein anderes Programm gesperrt - erneuter Klick genügt.
        }
    }

    private string? PathOfSide(object sender) => (sender as FrameworkElement)?.Tag as string == "Left" ? LeftPath : RightPath;

    private void SaveButton_Click(object sender, RoutedEventArgs e) => SaveAll();

    private void PreviousDiff_Click(object sender, RoutedEventArgs e) => JumpToDifference(forward: false);

    private void NextDiff_Click(object sender, RoutedEventArgs e) => JumpToDifference(forward: true);

    private void SearchButton_Click(object sender, RoutedEventArgs e) => SearchRequested?.Invoke(this, EventArgs.Empty);
}
