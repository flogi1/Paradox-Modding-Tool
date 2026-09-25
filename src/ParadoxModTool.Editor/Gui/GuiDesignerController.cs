using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ParadoxModTool.Categories;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Verbindet Editor und Designer für die aktive .gui-Datei: parst 300 ms nach der letzten Änderung neu (je Dokument ein
/// <see cref="GuiLiveDocument"/>), baut Baum und Vorschau des obersten Elements am Cursor im Hintergrund (veraltete Läufe werden
/// abgebrochen) und hält die Auswahl in Code, Baum und Vorschau gleich. Den Text ändern nur Designer-Aktionen (<see cref="Execute"/>),
/// immer als minimale Ersetzungen aus <see cref="GuiDesignerEdits"/> und je Aktion als ein Rückgängig-Schritt.
/// </summary>
public sealed class GuiDesignerController
{
    private readonly TextEditor _editor;
    private readonly GuiDesignerView _view;
    private readonly DispatcherTimer _reparseTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly DispatcherTimer _caretTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly ConditionalWeakTable<OpenDocument, GuiLiveDocument> _live = new();
    private OpenDocument? _document;
    private GuiResolver? _resolver;
    private GuiPreview? _preview;
    private GuiNodePath? _previewTargetPath;
    private CancellationTokenSource? _treeRun;
    private CancellationTokenSource? _previewRun;
    private ITextSourceVersion? _parsedVersion;
    private GuiReadOnlyReason? _readOnly;
    private bool _selectingFromView;

    public GuiDesignerController(TextEditor editor, GuiDesignerView view)
    {
        _editor = editor;
        _view = view;
        _reparseTimer.Tick += (_, _) =>
        {
            _reparseTimer.Stop();
            Reparse();
        };
        _caretTimer.Tick += (_, _) =>
        {
            _caretTimer.Stop();
            SyncToCaret(forcePreview: false);
        };
        _editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            if (_document is not null && !_selectingFromView)
            {
                _caretTimer.Stop();
                _caretTimer.Start();
            }
        };
        _view.NodeActivated += (_, node) => SelectInEditor(node);
        _view.PreviewOptionsChanged += (_, _) => SyncToCaret(forcePreview: true);
        _view.ActionRequested += (_, action) => Execute(action);
        _view.UndoRequested += (_, redo) => Undo(redo);
        _view.OpenDefinitionRequested += (_, node) => OpenDefinition(node);
        _view.NewTypeRequested += (_, node) => AskNewType(node, extract: false);
        _view.ExtractTypeRequested += (_, node) => AskNewType(node, extract: true);
        _view.RetypeRequested += (_, node) => AskRetype(node);
        _view.TypeEverywhereRequested += (_, node) => EditDefinition(node);
        _view.DraftRequested += (_, _) =>
        {
            if (_document is not null)
            {
                DraftRequested?.Invoke(this, _document);
            }
        };
        GuiSettings.Changed += (_, _) =>
        {
            if (_document is not null)
            {
                _resolver = null;
                UpdateEditability();
                Refresh();
            }
        };
    }

    /// <summary>"Definition öffnen" in einer anderen Datei: Datei und Zeile zum Öffnen.</summary>
    public event EventHandler<GuiLocationEventArgs>? LocationRequested;

    /// <summary>"Für Mod bearbeiten" im Banner des Designers.</summary>
    public event EventHandler<OpenDocument>? DraftRequested;

    /// <summary>"Typ überall" mit der Definition in einer anderen Datei: öffnen zum Bearbeiten (eine Vanilla-Datei als Entwurf).</summary>
    public event EventHandler<GuiLocationEventArgs>? EditLocationRequested;

    /// <summary>Angezeigter Stand der aktiven Datei (für "Inspizieren" aus dem Elementbaum); null, wenn der Designer aus ist.</summary>
    public GuiDocument? CurrentDocument => _document is null ? null : LiveOf(_document).Current;

    /// <summary>Zeigt den Designer für dieses Dokument; null schaltet ihn aus (Code-Ansicht oder keine .gui-Datei).</summary>
    public void Show(OpenDocument? document)
    {
        if (ReferenceEquals(document, _document))
        {
            UpdateEditability();
            return;
        }

        if (_document is not null)
        {
            _document.Document.TextChanged -= Document_TextChanged;
        }

        _treeRun?.Cancel();
        _previewRun?.Cancel();
        _reparseTimer.Stop();
        _caretTimer.Stop();
        _document = document;
        _resolver = null;
        _preview = null;
        _previewTargetPath = null;
        UpdateEditability();
        if (document is null)
        {
            return;
        }

        document.Document.TextChanged += Document_TextChanged;
        var version = document.Document.Version;
        _parsedVersion = LiveOf(document).Update(document.Document.Text) ? version : null;
        Refresh();
    }

    /// <summary>
    /// Führt eine Designer-Aktion aus: Text synchron neu parsen (neue Fehler: nichts schreiben), Knoten per Pfad wiederfinden, Ersetzungen als
    /// ein Rückgängig-Schritt anwenden, Cursor auf das Ergebnis setzen und sofort neu parsen (Plan-Ruling 11).
    /// </summary>
    public void Execute(GuiDesignerAction action)
    {
        if (_document is null)
        {
            return;
        }

        if (_readOnly is { } reason)
        {
            _view.SetStatus(GuiTexts.ReadOnly(reason));
            return;
        }

        _reparseTimer.Stop();
        var live = LiveOf(_document);
        var shown = live.Current;
        var version = _document.Document.Version;
        if (!live.Update(_document.Document.Text))
        {
            ShowStale(live);
            _view.SetStatus(GuiTexts.EditBlockedByErrors);
            return;
        }

        _parsedVersion = version;
        var document = live.Current;
        _resolver = CreateResolver(document);
        GuiDesignEdit? edit;
        try
        {
            edit = Compute(action, shown, document, _resolver, _editor.CaretOffset);
        }
        catch (ArgumentException)
        {
            edit = GuiDesignEdit.None;
        }

        if (edit is null)
        {
            _view.SetStatus(GuiTexts.EditElementGone);
            Refresh();
            return;
        }

        if (edit.IsEmpty)
        {
            _view.SetStatus(GuiTexts.EditNothing);

            // Der Neuparse-Timer ist gestoppt: Hat sich der Text seit dem angezeigten Stand geändert, den Baum jetzt neu aufbauen.
            if (!ReferenceEquals(shown.Root, document.Root))
            {
                Refresh();
            }

            return;
        }

        using (_document.Document.RunUpdate())
        {
            foreach (var change in TextEdit.InApplyOrder(edit.Edits))
            {
                _document.Document.Replace(change.Offset, change.Length, change.Text);
            }
        }

        if (edit.Focus is { } focus)
        {
            _selectingFromView = true;
            try
            {
                var offset = Math.Clamp(focus, 0, _editor.Document.TextLength);
                _editor.Select(offset, 0);
                _editor.CaretOffset = offset;
                _editor.ScrollToLine(_editor.Document.GetLineByOffset(offset).LineNumber);
            }
            finally
            {
                _selectingFromView = false;
            }
        }

        _view.SetStatus(string.Empty);
        _reparseTimer.Stop();
        Reparse();
    }

    /// <summary>Strg+Z bzw. Strg+Y im Designer: der Rückgängig-Verlauf des Dokuments, wie im Code-Editor (Plan-Ruling 9).</summary>
    public void Undo(bool redo)
    {
        if (_document is null)
        {
            return;
        }

        var stack = _document.Document.UndoStack;
        if (redo ? !stack.CanRedo : !stack.CanUndo)
        {
            return;
        }

        if (redo)
        {
            stack.Redo();
        }
        else
        {
            stack.Undo();
        }

        _reparseTimer.Stop();
        Reparse();
    }

    /// <summary>"Im Code zeigen": Stelle markieren und den Code-Editor fokussieren.</summary>
    public void Reveal(GuiNode node)
    {
        SelectInEditor(node);
        _editor.Focus();
    }

    /// <summary>"Definition öffnen": Typ der Instanz; in dieser Datei markieren, sonst Datei und Zeile melden.</summary>
    public void OpenDefinition(GuiNode node)
    {
        if (_document is null || _resolver?.FindType(node.Name) is not { } definition)
        {
            return;
        }

        if (ReferenceEquals(definition.Root, LiveOf(_document).Current.Root))
        {
            Reveal(definition);
        }
        else if (_resolver.FilePathOf(definition) is { } path)
        {
            LocationRequested?.Invoke(this, new GuiLocationEventArgs(path, definition.Line));
        }
    }

    /// <summary>"Typ überall" (Plan 3c Ruling 1): die Definition des Typs; in dieser Datei markieren, sonst zum Bearbeiten öffnen lassen.</summary>
    public void EditDefinition(GuiNode node)
    {
        if (_document is null)
        {
            return;
        }

        _resolver ??= CreateResolver(LiveOf(_document).Current);
        if (_resolver.FindType(node.Name) is not { } definition)
        {
            _view.SetStatus(GuiTexts.TypeEverywhereNone(node.Name));
            return;
        }

        if (ReferenceEquals(definition.Root, LiveOf(_document).Current.Root))
        {
            Reveal(definition);
        }
        else if (_resolver.FilePathOf(definition) is { } path)
        {
            EditLocationRequested?.Invoke(this, new GuiLocationEventArgs(path, definition.Line));
        }
    }

    /// <summary>"Neuer Typ" bzw. "Als neuen Typ herausziehen": Name (und bei "Neuer Typ" weitere Stellen) abfragen, dann als Aktion ausführen.</summary>
    private void AskNewType(GuiNode node, bool extract)
    {
        if (_document is null)
        {
            return;
        }

        if (_readOnly is { } reason)
        {
            _view.SetStatus(GuiTexts.ReadOnly(reason));
            return;
        }

        var document = LiveOf(_document).Current;
        if (!ReferenceEquals(node.Root, document.Root))
        {
            _view.SetStatus(GuiTexts.EditElementGone);
            return;
        }

        _resolver ??= CreateResolver(document);
        var workspace = GuiSettings.Workspace;
        var index = workspace?.Index ?? GuiIndex.Empty;
        Func<string, bool> engine = workspace is null ? _ => false : workspace.Catalog.IsKnownEngineWidget;
        var modRoot = workspace?.Options.ModRoot;
        bool Taken(string name) => GuiTypeEdits.IsTaken(name, GuiDefinitionKind.Type, index, document, engine);
        var spots = extract ? Array.Empty<GuiNode>() : GuiTypeEdits.SameKeySpots(document, node, _resolver);
        var window = new GuiNewTypeWindow(
            extract ? GuiNewTypeMode.Extract : GuiNewTypeMode.Derive,
            GuiElementTree.LabelOf(node),
            GuiTypeEdits.SuggestName(GuiTypeEdits.ModBase(modRoot), node.Name, Taken),
            spots.Select(s => GuiTexts.NewTypeSpot(s.Line, GuiElementTree.LabelOf(s))).ToList(),
            extract ? 0 : GuiTypeEdits.UsagesElsewhere(index, node.Name, _document.FilePath),
            _document.IsGuiDraft ? GuiTexts.NewTypeWhereDraft : GuiTexts.NewTypeWhereFile,
            Taken)
        {
            Owner = Window.GetWindow(_view),
        };
        if (window.ShowDialog() != true)
        {
            return;
        }

        Execute(new GuiDesignerAction(extract ? GuiDesignerActionKind.ExtractType : GuiDesignerActionKind.DeriveType, node)
        {
            Name = window.TypeName,
            Value = GuiTypeEdits.TypesContainerFor(modRoot),
            Others = window.SelectedSpots.Select(i => spots[i]).ToList(),
        });
    }

    /// <summary>"Widget-Schlüssel umstellen": Schlüssel abfragen (Typen und Engine-Widgets zur Auswahl, Warnungen nach Ruling 5), dann ausführen.</summary>
    private void AskRetype(GuiNode node)
    {
        if (_document is null)
        {
            return;
        }

        if (_readOnly is { } reason)
        {
            _view.SetStatus(GuiTexts.ReadOnly(reason));
            return;
        }

        var document = LiveOf(_document).Current;
        if (!ReferenceEquals(node.Root, document.Root))
        {
            _view.SetStatus(GuiTexts.EditElementGone);
            return;
        }

        var resolver = _resolver ??= CreateResolver(document);
        var workspace = GuiSettings.Workspace;
        Func<string, bool> engine = workspace is null ? _ => false : workspace.Catalog.IsKnownEngineWidget;
        var keys = (workspace is null ? Enumerable.Empty<string>() : GuiPalette.Entries(workspace.Index, resolver, workspace.Catalog).Where(e => e.Snippet is not null).Select(e => e.Name))
            .Concat(GuiDraft.Definitions(document).Where(d => d.Kind == GuiNodeKind.TypeDefinition).Select(d => d.Name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        string? WarningFor(string key)
        {
            var lines = new List<string>();
            if (!resolver.IsType(key) && !engine(key))
            {
                lines.Add(GuiTexts.RetypeUnknown(key));
            }

            var lost = GuiTypeEdits.LostBlockOverrides(node, key, resolver);
            if (lost.Count > 0)
            {
                lines.Add(GuiTexts.RetypeLostBlocks(key, string.Join(", ", lost)));
            }

            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        var window = new GuiRetypeWindow(node.Name, keys, WarningFor) { Owner = Window.GetWindow(_view) };
        if (window.ShowDialog() == true)
        {
            Execute(new GuiDesignerAction(GuiDesignerActionKind.Retype, node) { Name = window.NewKey });
        }
    }

    private GuiLiveDocument LiveOf(OpenDocument document) => _live.GetValue(document, d => new GuiLiveDocument(d.Document.Text));

    private GuiResolver CreateResolver(GuiDocument document) =>
        GuiSettings.Workspace is { } workspace
            ? workspace.CreateResolver(document, _document?.FilePath)
            : new GuiResolver(GuiIndex.Empty, GuiRules.Empty, document, _document?.FilePath);

    /// <summary>Plan 3a Ruling 2: nicht Vanilla (außer als Entwurf, Plan 3b), nicht aus einer Abhängigkeits-Mod, nicht gesperrt.</summary>
    private void UpdateEditability()
    {
        var draft = _document?.IsGuiDraft == true;
        _readOnly = _document is null
            ? null
            : GuiEditability.ReasonFor(
                _document.FilePath,
                GuiSettings.IsVanillaGui(_document) && !draft,
                _document.LockTooltip is not null,
                GuiSettings.Workspace?.Options.DependencyRoots ?? Array.Empty<string>());
        _view.SetReadOnly(_readOnly is { } reason ? GuiTexts.ReadOnly(reason) : null, _readOnly == GuiReadOnlyReason.Vanilla);
        _view.SetDraft(draft, _document?.FilePath);
    }

    /// <summary>
    /// Ersetzungen für die Aktion im frisch geparsten Dokument; null, wenn ein Knoten dort nicht sicher wiederzufinden ist.
    /// <paramref name="shown"/> ist der Stand vor dem synchronen Parse (den der Baum zeigt, sofern er schon neu aufgebaut ist).
    /// </summary>
    private static GuiDesignEdit? Compute(GuiDesignerAction action, GuiDocument shown, GuiDocument document, GuiResolver resolver, int caret)
    {
        GuiNode? Fresh(GuiNode stale)
        {
            if (ReferenceEquals(stale.Root, shown.Root))
            {
                return GuiDesignerEdits.Rebind(stale, shown, document);
            }

            // Der Baum zeigt noch den Stand vor der letzten Aktion; der Cursor steht auf deren Ergebnis (Plan-Ruling 11).
            return action.FromSelection && document.Text.Length > 0 ? GuiElementPath.ElementAt(document, caret, resolver) : null;
        }

        var node = action.Node is null ? null : Fresh(action.Node);
        var target = action.Target is null ? null : Fresh(action.Target);
        if ((action.Node is not null && node is null) || (action.Target is not null && target is null))
        {
            return null;
        }

        switch (action.Kind)
        {
            case GuiDesignerActionKind.Insert when action.Entry is { Snippet: null } template:
                return target is not null && GuiDesignerEdits.AcceptsUsing(target, resolver)
                    ? GuiDesignerEdits.AddUsing(document, target, template.Name)
                    : GuiDesignEdit.None;

            case GuiDesignerActionKind.Insert when action.Entry?.Snippet is { } snippet:
                return GuiDesignerEdits.InsertionFor(target, action.Placement, action.DropY, document, resolver) is { } at
                    ? GuiDesignerEdits.Insert(document, at, snippet)
                    : GuiDesignEdit.None;

            case GuiDesignerActionKind.Move when node is not null:
                return GuiDesignerEdits.InsertionFor(target, action.Placement, action.DropY, document, resolver) is { } to
                    ? GuiDesignerEdits.Move(document, node, to)
                    : GuiDesignEdit.None;

            case GuiDesignerActionKind.Delete when node is not null:
                return GuiDesignerEdits.Delete(document, node);

            case GuiDesignerActionKind.Duplicate when node is not null:
                return GuiDesignerEdits.Duplicate(document, node);

            case GuiDesignerActionKind.MoveUp when node is not null:
                return GuiDesignerEdits.MoveUp(document, node);

            case GuiDesignerActionKind.MoveDown when node is not null:
                return GuiDesignerEdits.MoveDown(document, node);

            case GuiDesignerActionKind.WrapIn when node is not null && action.Name is { } box:
                return GuiDesignerEdits.WrapIn(document, node, box);

            case GuiDesignerActionKind.OverrideBlock when node is not null && action.Name is { } block:
                var owner = action.ContentFrom ?? (action.KeepContent ? GuiBlockOverrides.EffectiveContent(node, block, resolver) : null);
                var content = owner is null ? string.Empty : GuiBlockOverrides.ContentOf(owner, resolver);
                return GuiDesignerEdits.OverrideBlock(document, node, block, content, action.FocusKey);

            case GuiDesignerActionKind.SetProperty when node is not null && action.Name is { } name && action.Value is { } value:
                return GuiDesignerEdits.SetProperty(document, node, name, value);

            case GuiDesignerActionKind.SetValue when node is not null && action.Value is { } newValue:
                return GuiDesignerEdits.SetValue(document, node, newValue);

            case GuiDesignerActionKind.RemoveProperty when node is not null:
                return GuiDesignerEdits.RemoveProperty(document, node);

            case GuiDesignerActionKind.DeriveType when node is not null && action.Name is { } typeName && action.Value is { } container:
                var others = new List<GuiNode>();
                foreach (var other in action.Others)
                {
                    if (Fresh(other) is not { } fresh)
                    {
                        return null;
                    }

                    others.Add(fresh);
                }

                return GuiTypeEdits.Derive(document, node, typeName, others, container);

            case GuiDesignerActionKind.ExtractType when node is not null && action.Name is { } extracted && action.Value is { } extractContainer:
                return GuiTypeEdits.Extract(document, node, extracted, extractContainer);

            case GuiDesignerActionKind.Retype when node is not null && action.Name is { } newKey:
                return GuiTypeEdits.Retype(document, node, newKey);

            default:
                return GuiDesignEdit.None;
        }
    }

    private void Document_TextChanged(object? sender, EventArgs e)
    {
        _reparseTimer.Stop();
        _reparseTimer.Start();
    }

    private void Reparse()
    {
        if (_document is null)
        {
            return;
        }

        var live = LiveOf(_document);
        var version = _document.Document.Version;
        if (live.Update(_document.Document.Text))
        {
            _parsedVersion = version;
            _resolver = null;
            Refresh();
        }
        else
        {
            ShowStale(live);
        }
    }

    private void ShowStale(GuiLiveDocument live) =>
        _view.SetStale(
            live.IsStale,
            live.Problem is { } problem ? GuiTexts.StaleAt(problem.Line, GuiTexts.Diagnostic(problem.Kind)) : null);

    /// <summary>Baum im Hintergrund neu bauen, danach Vorschau und Auswahl zum Cursor.</summary>
    private void Refresh()
    {
        if (_document is null)
        {
            return;
        }

        var live = LiveOf(_document);
        ShowStale(live);
        var workspace = GuiSettings.Workspace;
        var document = live.Current;
        _resolver ??= CreateResolver(document);
        var resolver = _resolver;
        Func<string, bool>? known = workspace is null ? null : workspace.Catalog.IsKnownEngineWidget;
        _view.SetStatus(workspace is null ? GuiSettings.Status : string.Empty);

        _treeRun?.Cancel();
        var run = new CancellationTokenSource();
        _treeRun = run;
        Task.Run(() => GuiElementTree.Build(document, resolver, known), run.Token).ContinueWith(
            task =>
            {
                if (run.IsCancellationRequested || !task.IsCompletedSuccessfully)
                {
                    return;
                }

                _view.SetTree(task.Result);
                SyncToCaret(forcePreview: true);
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void SyncToCaret(bool forcePreview)
    {
        if (_document is null || _resolver is null)
        {
            return;
        }

        var document = LiveOf(_document).Current;
        GuiNode? node = null;
        if (TextMatchesParse() && document.Text.Length > 0)
        {
            node = document.FindDeepestAt(Math.Min(_editor.CaretOffset, document.Text.Length - 1));
        }

        node ??= _preview is not null && ReferenceEquals(_preview.Target.Root, document.Root) ? _preview.Target : FirstTarget(document);
        SyncToNode(node, forcePreview);
    }

    private void SyncToNode(GuiNode? node, bool forcePreview)
    {
        var widget = NearestWidget(node);
        var target = node is null ? null : GuiPreviewBuilder.PreviewTarget(node);
        if (target is null)
        {
            _preview = null;
            _previewTargetPath = null;
            _view.SetPreview(null, keepView: false);
            _view.Select(widget, null);
            return;
        }

        var path = GuiNodePath.Of(target);
        var sameTarget = path.SameAs(_previewTargetPath);
        if (!forcePreview && sameTarget && _preview is not null && ReferenceEquals(_preview.Target.Root, target.Root))
        {
            _view.Select(widget, _preview);
            return;
        }

        _previewTargetPath = path;
        StartPreview(target, widget, keepView: sameTarget);
    }

    private void StartPreview(GuiNode target, GuiNode? widget, bool keepView)
    {
        _previewRun?.Cancel();
        var run = new CancellationTokenSource();
        _previewRun = run;
        var resolver = _resolver!;
        var document = LiveOf(_document!).Current;
        var options = _view.PreviewOptions;
        var workspace = GuiSettings.Workspace;
        Func<GuiLayoutEngine> createEngine = workspace is not null ? workspace.CreateLayoutEngine : () => new GuiLayoutEngine(GuiRules.Empty, _ => null);
        _view.SetStatus(GuiTexts.PreviewRunning);
        Task.Run(() => GuiPreviewBuilder.Build(resolver, createEngine, document, target, options, run.Token), run.Token).ContinueWith(
            task =>
            {
                if (run.IsCancellationRequested)
                {
                    return;
                }

                if (task.IsFaulted)
                {
                    _view.SetStatus(GuiTexts.PreviewFailed(task.Exception!.GetBaseException().Message));
                    return;
                }

                _preview = task.Result;
                _view.SetStatus(GuiSettings.Workspace is null ? GuiSettings.Status : string.Empty);
                _view.SetPreview(_preview, keepView);
                _view.Select(widget, _preview);
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>Baum- oder Vorschau-Klick: Stelle im Code markieren (nur, wenn der Code dem angezeigten Stand entspricht).</summary>
    private void SelectInEditor(GuiNode node)
    {
        if (_document is null)
        {
            return;
        }

        if (LiveOf(_document).IsStale || !TextMatchesParse())
        {
            return;
        }

        _selectingFromView = true;
        try
        {
            var start = node.Span.Start;
            var end = node.NameSpan.End > start ? node.NameSpan.End : Math.Min(node.Span.End, start + 1);
            _editor.Select(start, end - start);
            _editor.ScrollToLine(_editor.Document.GetLineByOffset(start).LineNumber);
        }
        finally
        {
            _selectingFromView = false;
        }

        SyncToNode(node, forcePreview: false);
    }

    private GuiNode? NearestWidget(GuiNode? node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current.Kind is GuiNodeKind.TypeDefinition or GuiNodeKind.TemplateDefinition)
            {
                return current;
            }

            if (current.Kind == GuiNodeKind.KeyedBlock && _resolver!.Classify(current) is GuiRole.TypeInstance or GuiRole.EngineWidget or GuiRole.WidgetHolder)
            {
                return current;
            }
        }

        return null;
    }

    private static GuiNode? FirstTarget(GuiDocument document) =>
        document.Root.Descendants().Select(GuiPreviewBuilder.PreviewTarget).FirstOrDefault(t => t is not null);

    /// <summary>Der Text wurde seit dem Parsen des angezeigten Stands nicht geändert (Vergleich der AvalonEdit-Version, ohne den Text zu kopieren).</summary>
    private bool TextMatchesParse() =>
        _document is not null
        && _parsedVersion is not null
        && _document.Document.Version is { } current
        && current.BelongsToSameDocumentAs(_parsedVersion)
        && current.CompareAge(_parsedVersion) == 0;
}
