using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.Editor;

/// <summary>Sprung zu einer Datei und Zeile (z.B. Herkunft einer Eigenschaft im Designer).</summary>
public sealed class GuiLocationEventArgs : EventArgs
{
    public GuiLocationEventArgs(string path, int line)
    {
        Path = path;
        Line = line;
    }

    public string Path { get; }

    public int Line { get; }
}

/// <summary>GUI-Designer im Arbeitsbereich: Umschalter Code | Designer | Geteilt für .gui-Dateien (siehe <see cref="GuiDesignerController"/>).</summary>
public partial class EditorPane
{
    private readonly Dictionary<OpenDocument, GuiViewMode> _guiModes = new();
    private GuiDesignerView? _designer;
    private GuiDesignerController? _guiController;
    private double _designerShare = 0.6;
    private bool _guiLayoutApplied;
    private readonly ContextMenu _editorMenu = new();
    private GuiBreadcrumbBar? _breadcrumb;
    private GuiBreadcrumbController? _breadcrumbController;
    private readonly Button _draftButton = new() { Margin = new Thickness(8, 2, 2, 2), Padding = new Thickness(6, 2, 6, 2) };
    private readonly TextBlock _draftLabel = new() { Margin = new Thickness(8, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
    private readonly Button _takeOverButton = new() { Margin = new Thickness(2), Padding = new Thickness(6, 2, 6, 2) };
    private readonly Button _discardButton = new() { Margin = new Thickness(2), Padding = new Thickness(6, 2, 6, 2) };
    private readonly Button _blocksButton = new() { Margin = new Thickness(8, 2, 2, 2), Padding = new Thickness(6, 2, 6, 2) };
    private OpenDocument? _watchedGuiDocument;

    /// <summary>"Baustein einfügen" (Kontextmenü, Knopf in der GUI-Leiste): Das Hauptfenster öffnet das Fenster "Bausteine".</summary>
    public event EventHandler? GuiBlocksRequested;

    /// <summary>"Als Baustein speichern" (markierter Code oder Element im Designer-Baum): Das Hauptfenster öffnet den Speichern-Dialog mit diesem Code.</summary>
    public event EventHandler<string>? GuiSaveBlockRequested;

    /// <summary>"Für Mod bearbeiten" für die aktive Vanilla-.gui-Datei (Plan-Ruling 4).</summary>
    public event EventHandler<OpenDocument>? GuiDraftRequested;

    /// <summary>"In Mod übernehmen" für den aktiven Entwurf.</summary>
    public event EventHandler<OpenDocument>? GuiTakeOverRequested;

    /// <summary>"Entwurf verwerfen" für den aktiven Entwurf.</summary>
    public event EventHandler<OpenDocument>? GuiDraftDiscardRequested;

    /// <summary>"Typ überall" mit der Definition in einer anderen Datei: Das Hauptfenster öffnet sie im Designer (eine Vanilla-Datei als Entwurf).</summary>
    public event EventHandler<GuiLocationEventArgs>? GuiEditDefinitionRequested;

    /// <summary>"Im Spiel ansehen" im Designer.</summary>
    public event EventHandler? GuiViewInGameRequested;

    public event EventHandler<GuiLocationEventArgs>? OpenLocationRequested;

    /// <summary>Kontextmenü "Neuer Bug hier": das Hauptfenster legt einen Bug zur aktiven Datei an (Cursorzeile bzw. Markierung).</summary>
    public event EventHandler? NewBugHereRequested;

    /// <summary>Kontextmenü "An Bugreport anhängen": das Hauptfenster hängt die Stelle an eine bestehende Meldung.</summary>
    public event EventHandler? AttachBugHereRequested;

    /// <summary>"Inspizieren" aus Code, Elementbaum oder Brotkrumen-Leiste: Das Hauptfenster zeigt das Element im GUI-Inspektor.</summary>
    public event EventHandler<GuiInspectTarget>? GuiInspectRequested;

    /// <summary>"Wird angezeigt in (GUI)" in einer Skriptdatei: Das Hauptfenster sucht das Wort in allen .gui-Dateien.</summary>
    public event EventHandler<string>? GuiShownInRequested;

    private void InitializeGuiDesigner()
    {
        GuiModeCodeButton.Content = GuiTexts.ModeCode;
        GuiModeCodeButton.ToolTip = GuiTexts.ModeCodeTip;
        GuiModeDesignerButton.Content = GuiTexts.ModeDesigner;
        GuiModeDesignerButton.ToolTip = GuiTexts.ModeDesignerTip;
        GuiModeSplitButton.Content = GuiTexts.ModeSplit;
        GuiModeSplitButton.ToolTip = GuiTexts.ModeSplitTip;
        _designer = new GuiDesignerView();
        DesignerHost.Content = _designer;
        _guiController = new GuiDesignerController(Editor, _designer);
        _designer.OriginActivated += (_, origin) =>
        {
            if (origin.FilePath is { } path)
            {
                OpenLocationRequested?.Invoke(this, new GuiLocationEventArgs(path, origin.Line));
            }
        };
        _designer.LayoutChanged += (_, _) => SaveGuiLayout();
        _designer.ShowInCodeRequested += (_, node) => ShowGuiNodeInCode(node);
        _designer.ViewInGameRequested += (_, _) => GuiViewInGameRequested?.Invoke(this, EventArgs.Empty);
        _designer.InspectDefinitionRequested += (_, definition) =>
        {
            if (GuiSettings.Workspace is { } workspace)
            {
                GuiInspectRequested?.Invoke(this, GuiInspection.TargetFor(workspace.Index, definition));
            }
        };
        _guiController.LocationRequested += (_, e) => OpenLocationRequested?.Invoke(this, e);
        _guiController.EditLocationRequested += (_, e) => GuiEditDefinitionRequested?.Invoke(this, e);
        _guiController.DraftRequested += (_, document) => GuiDraftRequested?.Invoke(this, document);
        _guiController.SaveAsBlockRequested += (_, code) => GuiSaveBlockRequested?.Invoke(this, code);
        _draftButton.Content = GuiTexts.DraftEdit;
        _draftButton.ToolTip = GuiTexts.DraftEditTip;
        _draftButton.Click += (_, _) => RaiseForActive(GuiDraftRequested);
        _draftLabel.Text = GuiTexts.DraftLabel;
        _draftLabel.ToolTip = GuiTexts.DraftLabelTip;
        _draftLabel.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Accent");
        _takeOverButton.Content = GuiTexts.DraftTakeOver;
        _takeOverButton.ToolTip = GuiTexts.DraftTakeOverTip;
        _takeOverButton.Click += (_, _) => RaiseForActive(GuiTakeOverRequested);
        _discardButton.Content = GuiTexts.DraftDiscard;
        _discardButton.ToolTip = GuiTexts.DraftDiscardTip;
        _discardButton.Click += (_, _) => RaiseForActive(GuiDraftDiscardRequested);
        GuiModePanel.Children.Add(_draftButton);
        GuiModePanel.Children.Add(_draftLabel);
        GuiModePanel.Children.Add(_takeOverButton);
        GuiModePanel.Children.Add(_discardButton);
        _blocksButton.Content = GuiTexts.BlocksButton;
        _blocksButton.ToolTip = GuiTexts.BlocksButtonTip;
        _blocksButton.Click += (_, _) => GuiBlocksRequested?.Invoke(this, EventArgs.Empty);
        GuiModePanel.Children.Add(_blocksButton);
        _designer.InspectRequested += (_, node) =>
        {
            if (_guiController?.CurrentDocument is { } document)
            {
                GuiInspectRequested?.Invoke(this, new GuiInspectTarget(node, document, ActiveDocument?.FilePath));
            }
        };
        _breadcrumb = new GuiBreadcrumbBar();
        BreadcrumbHost.Content = _breadcrumb;
        _breadcrumbController = new GuiBreadcrumbController(Editor, _breadcrumb);
        _breadcrumbController.InspectRequested += (_, target) => GuiInspectRequested?.Invoke(this, target);
        Editor.ContextMenu = _editorMenu;
        Editor.ContextMenuOpening += Editor_ContextMenuOpening;
        GuiSettings.Changed += (_, _) => UpdateGuiMode();
        UpdateGuiMode();
    }

    private static bool IsGuiFile(OpenDocument? document) =>
        document?.FilePath is { } path && path.EndsWith(".gui", StringComparison.OrdinalIgnoreCase);

    private void RaiseForActive(EventHandler<OpenDocument>? handler)
    {
        if (ActiveDocument is { } document)
        {
            handler?.Invoke(this, document);
        }
    }

    /// <summary>Entwurf an oder aus (auch von außen, z.B. nach "In Mod übernehmen"): Schreibschutz und Leiste neu setzen.</summary>
    private void WatchedGuiDocument_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OpenDocument.IsGuiDraft) or nameof(OpenDocument.Origin))
        {
            UpdateGuiMode();
        }
    }

    /// <summary>Zeigt Umschalter und Designer passend zum aktiven Dokument (bei Tabwechsel und Einstellungsänderung).</summary>
    private void UpdateGuiMode()
    {
        if (_guiController is null)
        {
            return;
        }

        var document = ActiveDocument;
        if (!ReferenceEquals(document, _watchedGuiDocument))
        {
            if (_watchedGuiDocument is not null)
            {
                _watchedGuiDocument.PropertyChanged -= WatchedGuiDocument_PropertyChanged;
            }

            _watchedGuiDocument = document;
            if (document is not null)
            {
                document.PropertyChanged += WatchedGuiDocument_PropertyChanged;
            }
        }

        // Vanilla-.gui: im Code schreibgeschützt, außer als Entwurf (Plan-Ruling 3); die Leiste bietet "Für Mod bearbeiten" bzw. die Entwurfsknöpfe.
        var offered = GuiSettings.DesignerOffered && IsGuiFile(document);
        var vanilla = GuiSettings.IsVanillaGui(document);

        // Eine geänderte Vanilla-.gui kann nur ein Entwurf sein (z.B. aus der Wiederherstellung); sonst säße sie schreibgeschützt fest.
        if (vanilla && document!.IsModified && !document.IsGuiDraft)
        {
            document.IsGuiDraft = true;
        }

        var draft = vanilla && document!.IsGuiDraft;
        Editor.IsReadOnly = vanilla && !draft;

        // "Bausteine" nur in bearbeitbaren .gui-Dateien mit eingeschalteten GUI-Werkzeugen (Plan-Ruling 2 und 10), auch ohne Designer.
        var blocks = GuiSettings.ToolsEnabled && IsGuiFile(document) && !Editor.IsReadOnly;
        _blocksButton.Visibility = blocks ? Visibility.Visible : Visibility.Collapsed;
        GuiModePanel.Visibility = offered || vanilla || blocks ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in new[] { GuiModeCodeButton, GuiModeDesignerButton, GuiModeSplitButton })
        {
            button.Visibility = offered ? Visibility.Visible : Visibility.Collapsed;
        }

        _draftButton.Visibility = vanilla && !draft ? Visibility.Visible : Visibility.Collapsed;
        _draftLabel.Visibility = _takeOverButton.Visibility = _discardButton.Visibility = draft ? Visibility.Visible : Visibility.Collapsed;
        var mode = offered && document is not null
            ? _guiModes.TryGetValue(document, out var chosen) ? chosen : GuiSettings.DefaultMode
            : GuiViewMode.Code;

        GuiModeCodeButton.IsChecked = mode == GuiViewMode.Code;
        GuiModeDesignerButton.IsChecked = mode == GuiViewMode.Designer;
        GuiModeSplitButton.IsChecked = mode == GuiViewMode.Split;

        // Die gespeicherte Aufteilung erst beim ersten Einblenden übernehmen: Die Arbeitsbereiche entstehen vor dem Laden der Sitzung.
        if (mode != GuiViewMode.Code && !_guiLayoutApplied && GuiSettings.Layout is not null)
        {
            ApplyGuiLayout(GuiSettings.Layout);
            _guiLayoutApplied = true;
        }

        switch (mode)
        {
            case GuiViewMode.Designer:
                DesignerRow.Height = new GridLength(1, GridUnitType.Star);
                DesignerSplitterRow.Height = new GridLength(0);
                CodeRow.Height = new GridLength(0);
                break;
            case GuiViewMode.Split:
                DesignerRow.Height = new GridLength(_designerShare, GridUnitType.Star);
                DesignerSplitterRow.Height = GridLength.Auto;
                CodeRow.Height = new GridLength(1 - _designerShare, GridUnitType.Star);
                break;
            default:
                DesignerRow.Height = new GridLength(0);
                DesignerSplitterRow.Height = new GridLength(0);
                CodeRow.Height = new GridLength(1, GridUnitType.Star);
                break;
        }

        DesignerHost.Visibility = mode == GuiViewMode.Code ? Visibility.Collapsed : Visibility.Visible;
        DesignerSplitter.Visibility = mode == GuiViewMode.Split ? Visibility.Visible : Visibility.Collapsed;
        EditorBorder.Visibility = mode == GuiViewMode.Designer ? Visibility.Collapsed : Visibility.Visible;
        _guiController.Show(mode == GuiViewMode.Code ? null : document);

        // Brotkrumen: unabhängig vom Schalter "GUI-Designer anbieten", nur über sichtbarem Code.
        var breadcrumb = GuiSettings.BreadcrumbOffered && IsGuiFile(document) && mode != GuiViewMode.Designer;
        BreadcrumbHost.Visibility = breadcrumb ? Visibility.Visible : Visibility.Collapsed;
        _breadcrumbController?.Show(breadcrumb ? document : null);
    }

    /// <summary>
    /// Kontextmenü im Code (Plan-Ruling 10): "Inspizieren" in .gui-Dateien (Element an der Klickstelle, aus dem fehlertoleranten Parse des
    /// aktuellen Texts; Plan-Ruling 12), "Wird angezeigt in (GUI)" in .txt- und .yml-Dateien (Wort unter der Maus). Ohne passenden Eintrag
    /// öffnet sich kein Menü.
    /// </summary>
    private void Editor_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        _editorMenu.Items.Clear();
        var document = ActiveDocument;
        if (ContextOffset(e) is { } offset)
        {
            if (GuiSettings.ToolsEnabled && IsGuiFile(document))
            {
                var parsed = GuiParser.Parse(Editor.Text);
                if (GuiElementPath.ElementAt(parsed, offset, CreateGuiResolver(parsed, document?.FilePath)) is { } node)
                {
                    var item = new MenuItem { Header = GuiTexts.ContextInspect };
                    item.Click += (_, _) => GuiInspectRequested?.Invoke(this, new GuiInspectTarget(node, parsed, document?.FilePath));
                    _editorMenu.Items.Add(item);
                }

                if (!Editor.IsReadOnly)
                {
                    var blocks = new MenuItem { Header = GuiTexts.ContextBlocks, InputGestureText = GuiTexts.KeyBlocks };
                    blocks.Click += (_, _) => GuiBlocksRequested?.Invoke(this, EventArgs.Empty);
                    _editorMenu.Items.Add(blocks);
                }

                // Auch in schreibgeschützten Vanilla-Dateien: sie sind die Hauptquelle für Bausteine (Teil-2-Ruling 3). Die Auswahl gilt nach
                // ContextOffset, das bei einem Klick außerhalb der Auswahl den Cursor versetzt.
                if (Editor.SelectionLength > 0)
                {
                    var code = Editor.SelectedText;
                    var save = new MenuItem { Header = GuiTexts.ContextSaveBlock };
                    save.Click += (_, _) => GuiSaveBlockRequested?.Invoke(this, code);
                    _editorMenu.Items.Add(save);
                }
            }
            else if (GuiSettings.ShownInOffered && IsShownInFile(document?.FilePath) && WordAtOffset(offset) is { } word)
            {
                // TextBlock statt Zeichenkette: WPF würde "_" im Wort als Zugriffstaste lesen.
                var item = new MenuItem { Header = new TextBlock { Text = GuiTexts.ContextShownIn(word) } };
                item.Click += (_, _) => GuiShownInRequested?.Invoke(this, word);
                _editorMenu.Items.Add(item);
            }
        }

        if (document?.FilePath is not null)
        {
            if (_editorMenu.Items.Count > 0)
            {
                _editorMenu.Items.Add(new Separator());
            }

            var newBug = new MenuItem { Header = ParadoxModTool.Settings.MenuTexts.EditorNewBug };
            newBug.Click += (_, _) => NewBugHereRequested?.Invoke(this, EventArgs.Empty);
            _editorMenu.Items.Add(newBug);
            var attachBug = new MenuItem { Header = ParadoxModTool.Settings.MenuTexts.EditorAttachBug };
            attachBug.Click += (_, _) => AttachBugHereRequested?.Invoke(this, EventArgs.Empty);
            _editorMenu.Items.Add(attachBug);
        }

        if (_editorMenu.Items.Count == 0)
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Stelle für das Kontextmenü: bei der Menütaste der Cursor, sonst die Mausposition. Liegt die Klickstelle außerhalb der Auswahl, kommt
    /// der Cursor dorthin, damit Brotkrumen und Menü dasselbe Element zeigen.
    /// </summary>
    private int? ContextOffset(ContextMenuEventArgs e)
    {
        if (e.CursorLeft < 0 || e.CursorTop < 0)
        {
            return Editor.CaretOffset;
        }

        // Nur im Textbereich: Rechtsklick auf Zeilennummern oder Lesezeichen-Rand öffnet kein Menü (dort nutzt bisher nichts die rechte Maustaste).
        var textView = Editor.TextArea.TextView;
        var inView = Mouse.GetPosition(textView);
        if (inView.X < 0 || inView.Y < 0 || inView.X > textView.ActualWidth || inView.Y > textView.ActualHeight)
        {
            return null;
        }

        if (Editor.GetPositionFromPoint(Mouse.GetPosition(Editor)) is not { } position)
        {
            return null;
        }

        var offset = Editor.Document.GetOffset(position.Location);
        if (Editor.SelectionLength == 0 || offset < Editor.SelectionStart || offset > Editor.SelectionStart + Editor.SelectionLength)
        {
            Editor.Select(offset, 0);
        }

        return offset;
    }

    private string? WordAtOffset(int offset)
    {
        var line = Editor.Document.GetLineByOffset(offset);
        return GuiReferenceSearch.WordAt(Editor.Document.GetText(line.Offset, line.Length), offset - line.Offset);
    }

    private static bool IsShownInFile(string? path) =>
        path is not null
        && (path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase));

    private static GuiResolver CreateGuiResolver(GuiDocument document, string? path) =>
        GuiSettings.Workspace?.CreateResolver(document, path) ?? new GuiResolver(GuiIndex.Empty, GuiRules.Empty, document, path);

    /// <summary>Wählt die Ansicht für ein Dokument (z.B. "Geteilt" für eine neu angelegte GUI-Datei).</summary>
    public void ShowGuiMode(OpenDocument document, GuiViewMode mode)
    {
        _guiModes[document] = mode;
        UpdateGuiMode();
    }

    /// <summary>"Im Code zeigen" aus dem Designer: bei ausgeblendetem Code auf "Geteilt" umschalten, dann die Stelle markieren.</summary>
    private void ShowGuiNodeInCode(GuiNode node)
    {
        if (ActiveDocument is { } document
            && (_guiModes.TryGetValue(document, out var mode) ? mode : GuiSettings.DefaultMode) == GuiViewMode.Designer)
        {
            ShowGuiMode(document, GuiViewMode.Split);
        }

        _guiController?.Reveal(node);
    }

    private void GuiModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveDocument is not { } document)
        {
            return;
        }

        var mode = ReferenceEquals(sender, GuiModeDesignerButton) ? GuiViewMode.Designer
            : ReferenceEquals(sender, GuiModeSplitButton) ? GuiViewMode.Split
            : GuiViewMode.Code;
        _guiModes[document] = mode;
        GuiSettings.SetDefaultMode(mode);
        UpdateGuiMode();
    }

    private void DesignerSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        var total = DesignerRow.ActualHeight + CodeRow.ActualHeight;
        if (total > 0)
        {
            _designerShare = Math.Clamp(DesignerRow.ActualHeight / total, 0.15, 0.85);
        }

        SaveGuiLayout();
    }

    private void SaveGuiLayout() =>
        GuiSettings.Layout = string.Create(CultureInfo.InvariantCulture, $"{_designerShare:0.###};{_designer?.LayoutState}");

    private void ApplyGuiLayout(string? layout)
    {
        if (string.IsNullOrEmpty(layout))
        {
            return;
        }

        var split = layout.IndexOf(';');
        var share = split < 0 ? layout : layout[..split];
        if (double.TryParse(share, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            _designerShare = Math.Clamp(value, 0.15, 0.85);
        }

        if (split >= 0 && _designer is not null)
        {
            _designer.LayoutState = layout[(split + 1)..];
        }
    }
}
