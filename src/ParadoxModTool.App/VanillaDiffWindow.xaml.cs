using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
// Wie in MainWindow.xaml.cs: UseWindowsForms (nur für FolderBrowserDialog an anderer Stelle) +
// ImplicitUsings=enable macht OpenFileDialog zwischen Microsoft.Win32 und System.Windows.Forms
// mehrdeutig (CS0104) - expliziter using-Alias löst das eindeutig zugunsten des WPF-eigenen
// Dialogs auf.
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.App;

/// <summary>
/// Vergleichs- und Merge-Fenster (Abschnitt 7): automatisch erkannte Vanilla/Mod-Dateipaare
/// (<see cref="VanillaFileTracker"/>) ODER zwei frei gewählte Dateien nebeneinander, mit
/// strukturellem Diff (<see cref="ScriptDiffer"/>, folgt in Task 10) und optionalem 3-Wege-Merge
/// (<see cref="VanillaMergeWorkflow"/>, folgt in Task 14).
/// </summary>
public partial class VanillaDiffWindow : Window
{
    private readonly Func<Func<string>, Action<int, int>, Action<string>, Window, SearchReplaceWindow> _createSearchWindow;
    private readonly System.Windows.Threading.DispatcherTimer _diffTimer;
    private SearchReplaceWindow? _searchWindow;
    private ICSharpCode.AvalonEdit.TextEditor? _lastFocusedEditor;

    /// <summary>Rechter Editor hat ungespeicherte Änderungen (Nutzer-Feedback: Suchen &amp; Ersetzen im Vergleich).</summary>
    private bool _rightDirty;

    /// <summary>Unterdrückt die Änderungsverfolgung, solange <see cref="LoadFiles"/> die Editortexte setzt.</summary>
    private bool _loadingFiles;

    private bool _suppressComboEvent;

    /// <summary>Dateien, für die in dieser Sitzung schon vor dem Speichern gesichert wurde (ein Snapshot je Datei genügt).</summary>
    private readonly HashSet<string> _snapshotDoneFor = new(StringComparer.OrdinalIgnoreCase);

    private readonly BookmarkMargin _leftBookmarkMargin = new();
    private readonly BookmarkMargin _rightBookmarkMargin = new();

    private readonly GameProfile? _activeProfile;
    private readonly Func<string?> _resolveDefaultModRoot;
    private readonly Func<string?> _resolveGameRoot;
    private readonly Func<string?> _getModRootOverride;
    private readonly Action<string?> _setModRootOverride;
    private readonly Func<ParadoxModTool.VersionManager.StorageSettings> _getStorageSettings;

    private string? _leftFilePath;
    private string? _rightFilePath;

    /// <summary>Gesetzt, wenn die aktuelle Auswahl ein automatisch erkanntes Vanilla/Mod-Paar ist (Merge nur dann möglich).</summary>
    private OverriddenFile? _selectedOverriddenFile;

    /// <summary>Eigene Beschriftungen für das Dateipaar (z. B. "Mod: Beta"), gesetzt von <see cref="OpenFilePair"/>; <see langword="null"/> = Standard (Vanilla/Mod bzw. Datei A/B).</summary>
    private (string Left, string Right)? _pairLabels;

    private ICSharpCode.AvalonEdit.Folding.FoldingManager? _leftFoldingManager;
    private ICSharpCode.AvalonEdit.Folding.FoldingManager? _rightFoldingManager;

    private readonly AlignmentSpacerGenerator _leftSpacers = new();
    private readonly AlignmentSpacerGenerator _rightSpacers = new();
    private readonly Func<bool> _getFoldingEnabled;
    private readonly Action<bool> _setFoldingEnabled;
    private bool _foldingEnabled = true;
    /// <summary>Anfangs an: das Häkchen löst schon in <c>InitializeComponent</c> Ereignisse aus, bevor die Rückrufe des Konstruktors gesetzt sind.</summary>
    private bool _loadingFoldingOption = true;

    private bool _suppressFormatCheckboxEvent;

    private VanillaMergeBaselineResult? _mergeBaseline;
    private string? _detectedGameVersion;
    private string? _lastCommittedManualVersion;

    /// <summary>
    /// Verhindert eine Endlosschleife beim synchronen Scrollen zwischen den beiden Panes (siehe
    /// <see cref="SyncScroll"/>). Ein reiner Wertevergleich der Scroll-Offsets (frühere Fassung)
    /// reichte nicht: Rundungsdifferenzen zwischen den beiden AvalonEdit-Instanzen ließen die
    /// Offsets nie exakt gleich werden, wodurch jede Ziehbewegung an der horizontalen Scrollbar in
    /// ein sichtbares Hin-und-Her-Flackern ausartete (Nutzer-Feedback). Ein echter
    /// Wiedereintritts-Schutz statt eines Wertevergleichs behebt das unabhängig von Rundung.
    /// </summary>
    private bool _syncingScroll;

    public VanillaDiffWindow(
        GameProfile? activeProfile,
        Func<string?> resolveDefaultModRoot,
        Func<string?> resolveGameRoot,
        Func<string?> getModRootOverride,
        Action<string?> setModRootOverride,
        Func<ParadoxModTool.VersionManager.StorageSettings> getStorageSettings,
        Func<bool> getFoldingEnabled,
        Action<bool> setFoldingEnabled,
        Func<Func<string>, Action<int, int>, Action<string>, Window, SearchReplaceWindow> createSearchWindow)
    {
        InitializeComponent();

        _createSearchWindow = createSearchWindow;
        _diffTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _diffTimer.Tick += (_, _) =>
        {
            _diffTimer.Stop();
            RefreshDiffView();
        };
        RightEditor.TextChanged += RightEditor_TextChanged;
        LeftEditor.GotKeyboardFocus += (_, _) => _lastFocusedEditor = LeftEditor;
        RightEditor.GotKeyboardFocus += (_, _) => _lastFocusedEditor = RightEditor;

        _getStorageSettings = getStorageSettings;
        _getFoldingEnabled = getFoldingEnabled;
        _setFoldingEnabled = setFoldingEnabled;
        _foldingEnabled = getFoldingEnabled();
        _loadingFoldingOption = true;
        FoldingCheckBox.IsChecked = _foldingEnabled;
        _loadingFoldingOption = false;

        LeftEditor.TextArea.TextView.ElementGenerators.Add(_leftSpacers);
        RightEditor.TextArea.TextView.ElementGenerators.Add(_rightSpacers);
        LeftEditor.TextArea.TextView.BackgroundRenderers.Add(new AlignmentGapRenderer(_leftSpacers));
        RightEditor.TextArea.TextView.BackgroundRenderers.Add(new AlignmentGapRenderer(_rightSpacers));

        _activeProfile = activeProfile;
        _resolveDefaultModRoot = resolveDefaultModRoot;
        _resolveGameRoot = resolveGameRoot;
        _getModRootOverride = getModRootOverride;
        _setModRootOverride = setModRootOverride;

        LoadOverriddenFiles();

        // Lesezeichen-Rand ganz links im Editor, vor den Zeilennummern.
        // Textauswahl-Farbe aus dem Theme; Diff-Hintergründe der Zeilen zeichnen bei einem Themenwechsel neu.
        LeftEditor.TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty, "Theme.EditorSelection");
        RightEditor.TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty, "Theme.EditorSelection");
        EventHandler onThemeChanged = (_, _) =>
        {
            LeftEditor.TextArea.TextView.Redraw();
            RightEditor.TextArea.TextView.Redraw();
        };
        ThemeManager.ThemeChanged += onThemeChanged;
        Closed += (_, _) => ThemeManager.ThemeChanged -= onThemeChanged;

        LeftEditor.TextArea.LeftMargins.Insert(0, _leftBookmarkMargin);
        RightEditor.TextArea.LeftMargins.Insert(0, _rightBookmarkMargin);

        // Synchrones Scrollen zwischen den beiden Panes - siehe SyncScroll für den
        // Wiedereintritts-Schutz gegen Ping-Pong/Flackern.
        LeftEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) => SyncScroll(LeftEditor, RightEditor);
        RightEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) => SyncScroll(RightEditor, LeftEditor);
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

        // Der Guard darf NICHT sofort (im selben synchronen Aufruf) zurückgesetzt werden: das
        // ScrollOffsetChanged-Event der Zielseite, das durch die beiden Aufrufe oben ausgelöst
        // wird, feuert bei AvalonEdit erst nach dem nächsten Layout-Durchlauf, also NACH diesem
        // Aufruf - ein sofortiges Zurücksetzen (z. B. in "finally") schließt das Schutzfenster,
        // bevor genau dieses verzögerte Echo-Event eintrifft, und der alte Wertevergleich-Bug
        // (sichtbares Hin-und-Her-Flackern beim Ziehen der horizontalen Scrollbar, Nutzer-Feedback
        // mit Video bestätigt) tritt wieder auf. Das Zurücksetzen erst nach dem aktuellen
        // Dispatcher-Durchlauf (Background-Priorität, läuft nach Layout/Render) fängt auch dieses
        // verzögerte Echo noch ab.
        Dispatcher.BeginInvoke(new Action(() => _syncingScroll = false), System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Der Ordner EINER konkreten Mod, gegen den <see cref="VanillaFileTracker"/> abgleicht - der
    /// vom Nutzer über <see cref="ModRootButton_Click"/> gewählte Override, falls vorhanden, sonst
    /// der (bei mehreren installierten Mods meist falsche) Sammelordner-Vorschlag aus dem aktiven
    /// Spielprofil (Bugfix, Abschnitt 7: Paradox-Mods liegen als eigener Unterordner unter "mod/",
    /// nicht direkt darin).
    /// </summary>
    private string? ResolveModRoot() => _getModRootOverride() ?? _resolveDefaultModRoot();

    private void LoadOverriddenFiles()
    {
        var modRoot = ResolveModRoot();
        var gameRoot = _resolveGameRoot();

        ModRootStatusText.Text = modRoot is null
            ? "Kein Mod-Ordner gewählt."
            : $"Mod-Ordner: {modRoot}";

        if (_activeProfile is null || modRoot is null || gameRoot is null)
        {
            StatusText.Text = "Kein aktives Spielprofil, Mod-Ordner oder Spielpfad bekannt - automatische Erkennung nicht möglich. Manuelle Dateiwahl funktioniert trotzdem.";
            return;
        }

        var overridden = VanillaFileTracker.FindOverriddenFiles(gameRoot, modRoot, _activeProfile);
        OverriddenFilesCombo.ItemsSource = overridden;

        StatusText.Text = overridden.Count == 0
            ? "Keine von Vanilla überschriebenen Dateien im gewählten Mod-Ordner gefunden."
            : $"{overridden.Count} von Vanilla überschriebene Datei(en) gefunden.";
    }

    /// <summary>
    /// Lässt den Nutzer den Ordner EINER konkreten Mod wählen (Bugfix, Abschnitt 7: der
    /// Sammelordner-Vorschlag aus dem Spielprofil passt nur, wenn eine Mod ausnahmsweise direkt
    /// im "mod/"-Ordner liegt statt in einem eigenen Unterordner - im Normalfall nicht). Die Wahl
    /// wird über die Session gespeichert (<see cref="_setModRootOverride"/>) und löst sofort eine
    /// neue Suche nach überschriebenen Dateien aus.
    /// </summary>
    private void ModRootButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmProceedWithUnsavedChanges())
        {
            return;
        }

        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Ordner der zu bearbeitenden Mod auswählen (der Ordner direkt unterhalb von \"mod\", der \"common\"/\"gui\"/... enthält)",
        };

        var current = ResolveModRoot();
        if (!string.IsNullOrEmpty(current) && Directory.Exists(current))
        {
            dialog.SelectedPath = current;
        }

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        _setModRootOverride(dialog.SelectedPath);
        LoadOverriddenFiles();
    }

    private void OverriddenFilesCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_suppressComboEvent || OverriddenFilesCombo.SelectedItem is not OverriddenFile selected)
        {
            return;
        }

        if (!ReferenceEquals(selected, _selectedOverriddenFile) && !ConfirmProceedWithUnsavedChanges())
        {
            _suppressComboEvent = true;
            OverriddenFilesCombo.SelectedItem = _selectedOverriddenFile;
            _suppressComboEvent = false;
            return;
        }

        _selectedOverriddenFile = selected;
        _pairLabels = null;
        LoadFiles(selected.VanillaFullPath, selected.ModFullPath);
        RefreshFormatChoiceCheckbox();
        RefreshMergeBaseline();
    }

    private void ManualLeftButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmProceedWithUnsavedChanges())
        {
            return;
        }

        var dialog = new OpenFileDialog { Title = "Datei A wählen" };
        if (dialog.ShowDialog() == true)
        {
            _selectedOverriddenFile = null;
            _pairLabels = null;
            _mergeBaseline = null;
            RefreshFormatChoiceCheckbox();
            MergeButton.IsEnabled = false;
            _leftFilePath = dialog.FileName;

            // Sobald beide Seiten manuell gewählt sind, über LoadFiles laden, damit der
            // strukturelle Diff (und damit die Zeileneinfärbung) auch für frei gewählte
            // Dateipaare berechnet wird, nicht nur für automatisch erkannte Vanilla/Mod-Paare.
            if (_rightFilePath is not null)
            {
                LoadFiles(_leftFilePath, _rightFilePath);
            }
            else
            {
                LeftEditor.Text = File.ReadAllText(_leftFilePath);
            }
        }
    }

    private void ManualRightButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmProceedWithUnsavedChanges())
        {
            return;
        }

        var dialog = new OpenFileDialog { Title = "Datei B wählen" };
        if (dialog.ShowDialog() == true)
        {
            _selectedOverriddenFile = null;
            _pairLabels = null;
            _mergeBaseline = null;
            RefreshFormatChoiceCheckbox();
            MergeButton.IsEnabled = false;
            _rightFilePath = dialog.FileName;

            if (_leftFilePath is not null)
            {
                LoadFiles(_leftFilePath, _rightFilePath);
            }
            else
            {
                // Nur Anzeige (linke Seite fehlt noch) - zählt nicht als Änderung des Nutzers.
                _loadingFiles = true;
                try
                {
                    RightEditor.Text = File.ReadAllText(_rightFilePath);
                }
                finally
                {
                    _loadingFiles = false;
                }
            }
        }
    }

    /// <summary>Lädt ein Dateipaar mit eigenen Beschriftungen (z. B. dieselbe Datei in zwei Mods eines Projekts). Fragt bei ungespeicherten Änderungen zuerst nach.</summary>
    public void OpenFilePair(string leftPath, string rightPath, string leftLabel, string rightLabel)
    {
        if (!ConfirmProceedWithUnsavedChanges())
        {
            return;
        }

        _selectedOverriddenFile = null;
        _mergeBaseline = null;
        RefreshFormatChoiceCheckbox();
        MergeButton.IsEnabled = false;
        _pairLabels = (leftLabel, rightLabel);
        LoadFiles(leftPath, rightPath);
    }

    private void LoadFiles(string leftPath, string rightPath)
    {
        _leftFilePath = leftPath;
        _rightFilePath = rightPath;

        var leftText = File.ReadAllText(leftPath);
        var rightText = File.ReadAllText(rightPath);

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
        UpdateDirtyUi();

        // Rahmenfarbe unabhängig von der Dateikategorie: links (Vanilla bzw. Datei A) blau, rechts (Mod bzw. Datei B) orange,
        // damit man die Seiten auf einen Blick auseinanderhält (Nutzer-Feedback).
        // Vanilla- und Mod-Farbe kommen aus dem Theme (Tokens DiffVanilla/DiffMod) und folgen einem Themenwechsel.
        LeftBorder.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "Theme.DiffVanilla");
        RightBorder.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "Theme.DiffMod");
        LeftBorder.ToolTip = "Links: " + leftPath;
        RightBorder.ToolTip = "Rechts: " + rightPath;

        // Pfad über jedem Editor; bei automatisch erkannten Paaren mit Herkunft (Vanilla/Mod), sonst Datei A/B.
        var isAutoPair = _selectedOverriddenFile is not null;
        var (leftName, rightName) = isAutoPair ? ("Vanilla", "Mod") : _pairLabels ?? ("Datei A", "Datei B");
        LeftPathText.Text = $"{leftName}: {leftPath}";
        RightPathText.Text = $"{rightName}: {rightPath}";
        LeftPathText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "Theme.DiffVanilla");
        RightPathText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "Theme.DiffMod");
        LeftPathText.ToolTip = leftPath;
        RightPathText.ToolTip = rightPath;

        RefreshDiffView();
    }

    /// <summary>
    /// Berechnet Diff, Färbung, Übersichtsleisten und Faltung aus den AKTUELLEN Editortexten neu - nach dem
    /// Laden und (verzögert) nach jeder Änderung im rechten Editor.
    /// </summary>
    private void RefreshDiffView()
    {
        var leftPath = _leftFilePath;
        var rightPath = _rightFilePath;
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
            StatusText.Text = $"Vergleich pausiert - Syntaxfehler: {ex.Message}";
            return;
        }
        var entryDiffs = ScriptDiffer.Diff(leftRoot, rightRoot);

        // Lesezeichen der beiden Dateien (Strg+F2) als Symbol am Rand und als Markierung in der Übersichtsleiste.
        var bookmarks = ParadoxModTool.Notes.BookmarkStore.TryLoad(ParadoxModTool.Notes.BookmarkStore.GetDefaultBookmarksFilePath());
        var leftBookmarkLines = ParadoxModTool.Notes.BookmarkStore.GetBookmarks(bookmarks, leftPath).Select(b => b.Line).ToList();
        var rightBookmarkLines = ParadoxModTool.Notes.BookmarkStore.GetBookmarks(bookmarks, rightPath).Select(b => b.Line).ToList();
        _leftBookmarkMargin.SetLines(leftBookmarkLines);
        _rightBookmarkMargin.SetLines(rightBookmarkLines);

        LeftOverview.Update(LeftEditor, ApplyDiffColoring(LeftEditor, entryDiffs, useLeftSide: true, _leftSpacers), leftBookmarkLines);
        RightOverview.Update(RightEditor, ApplyDiffColoring(RightEditor, entryDiffs, useLeftSide: false, _rightSpacers), rightBookmarkLines);

        var leftDocument = LeftEditor.Document;
        var rightDocument = RightEditor.Document;
        var gaps = DiffAlignment.Compute(
            entryDiffs,
            offset => leftDocument.GetLineByOffset(Math.Clamp(offset, 0, leftDocument.TextLength)).LineNumber,
            offset => rightDocument.GetLineByOffset(Math.Clamp(offset, 0, rightDocument.TextLength)).LineNumber);
        _leftSpacers.SetGaps(gaps.Where(g => g.Side == DiffSide.Left), leftDocument);
        _rightSpacers.SetGaps(gaps.Where(g => g.Side == DiffSide.Right), rightDocument);
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

    private void FoldingCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingFoldingOption)
        {
            return;
        }

        _foldingEnabled = FoldingCheckBox.IsChecked == true;
        _setFoldingEnabled(_foldingEnabled);
        if (!_foldingEnabled)
        {
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
        else
        {
            RefreshDiffView();
        }
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

    /// <summary>
    /// Führt den 3-Wege-Merge (Abschnitt 7) für die aktuell gewählte, automatisch erkannte
    /// Vanilla/Mod-Datei aus, sobald <see cref="RefreshMergeBaseline"/> eine ältere Basis
    /// gefunden hat. Bei Konflikten entscheidet der Nutzer über <see cref="ConflictResolutionWindow"/>
    /// pro Stelle zwischen Spiel-Update- und Mod-Fassung; das Ergebnis wird gemäß der pro Datei
    /// gespeicherten Formatierungs-Wahl (<see cref="FormatChoiceStore"/>, Abschnitt 7) geschrieben.
    /// </summary>
    private void MergeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOverriddenFile is null || _mergeBaseline is not { HasOlderBaseline: true, OlderVanillaText: not null } baseline)
        {
            return;
        }

        // Der Merge liest die Mod-Datei von der Platte - ungespeicherte Änderungen müssen vorher gespeichert oder verworfen sein.
        if (!ConfirmProceedWithUnsavedChanges())
        {
            return;
        }

        var modRoot = ResolveModRoot();
        if (modRoot is null)
        {
            return;
        }

        var oldVanillaTempPath = System.IO.Path.GetTempFileName();
        File.WriteAllText(oldVanillaTempPath, baseline.OlderVanillaText);

        try
        {
            var result = VanillaMergeWorkflow.ComputeMerge(oldVanillaTempPath, _selectedOverriddenFile.VanillaFullPath, _selectedOverriddenFile.ModFullPath);
            var finalRoot = result.MergedRoot;

            if (result.HasConflicts)
            {
                var dialog = new ConflictResolutionWindow(result.Conflicts) { Owner = this };
                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                finalRoot = ConflictResolution.ApplyResolutions(result.MergedRoot, dialog.BuildResolutions());
            }

            var versionManager = new ParadoxModTool.VersionManager.ModVersionManager(
                modRoot,
                ParadoxModTool.VersionManager.StorageLocations.ResolveSnapshotsDirectory(modRoot, _getStorageSettings()));
            var formatSettings = FormatChoiceStore.TryLoad(FormatChoiceStore.GetDefaultFilePath(modRoot));
            var preferModFormatting = formatSettings.PreferModFormatting.TryGetValue(_selectedOverriddenFile.RelativePath, out var value) && value;

            VanillaMergeWorkflow.WriteMergedResult(finalRoot, _selectedOverriddenFile.ModFullPath, versionManager, preferModFormatting);

            LoadFiles(_selectedOverriddenFile.VanillaFullPath, _selectedOverriddenFile.ModFullPath);
            StatusText.Text = "Merge abgeschlossen - Sicherheits-Snapshot der vorherigen Moddatei wurde angelegt.";
        }
        finally
        {
            File.Delete(oldVanillaTempPath);
        }
    }

    /// <summary>
    /// Lädt den gespeicherten Formatierungs-Wunsch (Vanilla vs. Mod-Formatierung, Abschnitt 7)
    /// für die aktuell gewählte automatisch erkannte Vanilla/Mod-Datei und spiegelt ihn in der
    /// Checkbox wider. Nur für automatisch erkannte Paare sinnvoll, da der Schlüssel der
    /// Mod-relative Pfad ist - bei manueller Dateiwahl bleibt die Checkbox deaktiviert.
    /// </summary>
    private void RefreshFormatChoiceCheckbox()
    {
        if (_selectedOverriddenFile is null)
        {
            PreferModFormattingCheckBox.IsEnabled = false;
            return;
        }

        var modRoot = ResolveModRoot();
        if (modRoot is null)
        {
            PreferModFormattingCheckBox.IsEnabled = false;
            return;
        }

        var settings = FormatChoiceStore.TryLoad(FormatChoiceStore.GetDefaultFilePath(modRoot));
        var preferMod = settings.PreferModFormatting.TryGetValue(_selectedOverriddenFile.RelativePath, out var value) && value;

        _suppressFormatCheckboxEvent = true;
        PreferModFormattingCheckBox.IsEnabled = true;
        PreferModFormattingCheckBox.IsChecked = preferMod;
        _suppressFormatCheckboxEvent = false;
    }

    private void PreferModFormattingCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressFormatCheckboxEvent || _selectedOverriddenFile is null)
        {
            return;
        }

        var modRoot = ResolveModRoot();
        if (modRoot is null)
        {
            return;
        }

        var path = FormatChoiceStore.GetDefaultFilePath(modRoot);
        var settings = FormatChoiceStore.TryLoad(path);
        settings.PreferModFormatting[_selectedOverriddenFile.RelativePath] = PreferModFormattingCheckBox.IsChecked == true;
        FormatChoiceStore.Save(settings, path);
    }

    /// <summary>
    /// Prüft für die aktuell gewählte automatisch erkannte Vanilla/Mod-Datei, ob ein Merge nach
    /// Spiel-Update möglich ist (Abschnitt 7): zeichnet bei Bedarf die aktuelle Vanilla-Fassung als
    /// Snapshot der aktuellen Spielversion auf und aktiviert den Merge-Button nur, wenn zusätzlich
    /// ein ÄLTERER Snapshot existiert. Die Spielversion kommt bevorzugt aus
    /// <see cref="GameVersionReader"/>, sonst aus der manuellen Eingabe.
    /// </summary>
    private void RefreshMergeBaseline()
    {
        _mergeBaseline = null;
        MergeButton.IsEnabled = false;

        if (_selectedOverriddenFile is null)
        {
            StatusText.Text = "Merge nur für automatisch erkannte Vanilla/Mod-Paare möglich.";
            return;
        }

        var modRoot = ResolveModRoot();
        var gameRoot = _resolveGameRoot();
        if (modRoot is null || gameRoot is null)
        {
            return;
        }

        _detectedGameVersion = GameVersionReader.TryReadVersion(gameRoot);
        var effectiveVersion = _detectedGameVersion ?? ManualVersionTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(effectiveVersion))
        {
            StatusText.Text = "Spielversion konnte nicht automatisch erkannt werden - bitte manuell eintragen, um Merge zu ermöglichen.";
            return;
        }

        var store = VanillaHistoryStore.FromHistoryRoot(
            ParadoxModTool.VersionManager.StorageLocations.ResolveVanillaHistoryDirectory(modRoot, _getStorageSettings()));
        _mergeBaseline = VanillaMergeBaseline.Resolve(store, _selectedOverriddenFile.RelativePath, effectiveVersion, _selectedOverriddenFile.VanillaFullPath);

        MergeButton.IsEnabled = _mergeBaseline.HasOlderBaseline;
        StatusText.Text = _mergeBaseline.HasOlderBaseline
            ? $"Merge verfügbar: alte Version {_mergeBaseline.OlderVersion} -> aktuelle Version {effectiveVersion}."
            : $"Basis für Version {effectiveVersion} aufgezeichnet - Merge erst ab dem nächsten Spiel-Update verfügbar.";
    }

    /// <summary>
    /// Übernimmt die manuell eingetragene Spielversion erst bei Enter oder Fokusverlust - nicht bei
    /// jedem Tastendruck (früher entstand dadurch für "1", "1." und "1.4" je ein History-Eintrag).
    /// </summary>
    private void CommitManualVersion()
    {
        var version = ManualVersionTextBox.Text.Trim();
        if (version == _lastCommittedManualVersion)
        {
            return;
        }

        _lastCommittedManualVersion = version;

        if (_detectedGameVersion is null)
        {
            RefreshMergeBaseline();
        }
    }

    private void ManualVersionTextBox_Commit(object sender, RoutedEventArgs e) => CommitManualVersion();

    private void ManualVersionTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            CommitManualVersion();
            e.Handled = true;
        }
    }

    // ---- Bearbeiten, Speichern, Suchen & Ersetzen (Nutzer-Feedback) ----

    private void RightEditor_TextChanged(object? sender, EventArgs e)
    {
        if (_loadingFiles)
        {
            return;
        }

        _rightDirty = true;
        UpdateDirtyUi();

        // Vergleich erst nach einer kurzen Tipp-Pause neu berechnen.
        _diffTimer.Stop();
        _diffTimer.Start();
    }

    private void UpdateDirtyUi()
    {
        SaveButton.IsEnabled = _rightDirty && _rightFilePath is not null;
        DirtyIndicator.Visibility = _rightDirty ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Fragt bei ungespeicherten Änderungen im rechten Editor nach (Speichern / Verwerfen / Abbrechen). Liefert
    /// <see langword="true"/>, wenn der Aufrufer fortfahren darf.
    /// </summary>
    private bool ConfirmProceedWithUnsavedChanges()
    {
        if (!_rightDirty)
        {
            return true;
        }

        var answer = System.Windows.MessageBox.Show(
            this,
            $"Die rechte Datei \"{System.IO.Path.GetFileName(_rightFilePath)}\" hat ungespeicherte Änderungen.\n\nSpeichern?",
            "Ungespeicherte Änderungen",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Yes);

        return answer switch
        {
            MessageBoxResult.Yes => SaveRight(),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmProceedWithUnsavedChanges())
        {
            e.Cancel = true;
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
            var arguments = File.Exists(path)
                ? $"/select,\"{path}\""
                : $"\"{System.IO.Path.GetDirectoryName(path)}\"";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            System.Windows.MessageBox.Show(this, $"Explorer konnte nicht geöffnet werden: {ex.Message}", "Fehler");
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
            System.Windows.Clipboard.SetText(path);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Zwischenablage kurzzeitig durch ein anderes Programm gesperrt - erneuter Klick genügt.
        }
    }

    private string? PathOfSide(object sender) =>
        (sender as FrameworkElement)?.Tag as string == "Left" ? _leftFilePath : _rightFilePath;

    private void SaveButton_Click(object sender, RoutedEventArgs e) => SaveRight();

    /// <summary>
    /// Schreibt den rechten Editor in seine Datei. Vor dem ersten Speichern je Datei und Sitzung wird - bei
    /// automatisch erkannten Mod-Dateien - ein Snapshot des Mods angelegt (wie beim Merge); bei frei gewählten
    /// Dateien fragt ein Hinweis einmalig nach, da es dort keinen Mod-Ordner zum Sichern gibt. Behält ein
    /// vorhandenes UTF-8-BOM der Datei bei.
    /// </summary>
    private bool SaveRight()
    {
        if (_rightFilePath is not { } path)
        {
            return false;
        }

        try
        {
            if (_snapshotDoneFor.Add(path))
            {
                if (!TrySnapshotBeforeSave(path))
                {
                    _snapshotDoneFor.Remove(path);
                    return false;
                }
            }

            var hadBom = HasUtf8Bom(path);
            File.WriteAllText(path, RightEditor.Text, new System.Text.UTF8Encoding(hadBom));

            _rightDirty = false;
            UpdateDirtyUi();
            StatusText.Text = $"Gespeichert: {System.IO.Path.GetFileName(path)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _snapshotDoneFor.Remove(path);
            System.Windows.MessageBox.Show(this, $"Datei konnte nicht gespeichert werden: {ex.Message}", "Fehler");
            return false;
        }
    }

    private bool TrySnapshotBeforeSave(string path)
    {
        if (_selectedOverriddenFile is not null && ResolveModRoot() is { } modRoot)
        {
            var manager = new ParadoxModTool.VersionManager.ModVersionManager(
                modRoot,
                ParadoxModTool.VersionManager.StorageLocations.ResolveSnapshotsDirectory(modRoot, _getStorageSettings()));
            manager.CreateSnapshot($"vor-edit_{System.IO.Path.GetFileName(path)}");
            return true;
        }

        return System.Windows.MessageBox.Show(
            this,
            $"\"{System.IO.Path.GetFileName(path)}\" wurde frei gewählt und liegt nicht in einem erkannten Mod-Ordner - es wird KEIN Snapshot angelegt.\n\nTrotzdem speichern?",
            "Ohne Snapshot speichern",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
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

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.Control)
        {
            return;
        }

        if (e.Key is System.Windows.Input.Key.F or System.Windows.Input.Key.H)
        {
            OpenSearchWindow();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.S && _rightDirty)
        {
            SaveRight();
            e.Handled = true;
        }
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e) => OpenSearchWindow();

    private void OpenSearchWindow()
    {
        if (_searchWindow is null)
        {
            _searchWindow = _createSearchWindow(GetFocusedEditorText, SelectInFocusedEditor, ReplaceInRightEditor, this);
            _searchWindow.Closed += (_, _) => _searchWindow = null;
        }

        if (_searchWindow.IsVisible)
        {
            _searchWindow.Activate();
        }
        else
        {
            _searchWindow.Show();
        }
    }

    private ICSharpCode.AvalonEdit.TextEditor FocusedEditor => _lastFocusedEditor ?? RightEditor;

    private string GetFocusedEditorText() => FocusedEditor.Text;

    private void SelectInFocusedEditor(int offset, int length)
    {
        var editor = FocusedEditor;
        var start = Math.Clamp(offset, 0, editor.Document.TextLength);
        var end = Math.Clamp(length, 0, editor.Document.TextLength - start);

        editor.Select(start, end);
        editor.ScrollToLine(editor.Document.GetLineByOffset(start).LineNumber);
        editor.Focus();
    }

    /// <summary>Ersetzen wirkt nur im rechten Editor; die linke Vanilla-Seite bleibt schreibgeschützt.</summary>
    private void ReplaceInRightEditor(string newText)
    {
        if (ReferenceEquals(FocusedEditor, LeftEditor))
        {
            System.Windows.MessageBox.Show(
                this,
                "Die linke Seite (Vanilla bzw. Datei A) ist schreibgeschützt. Klicke in die rechte Seite (Mod bzw. Datei B), um dort zu ersetzen.",
                "Ersetzen nicht möglich");
            return;
        }

        RightEditor.Document.Text = newText;
    }
}
