using System.Windows;
using System.Windows.Media;
using ParadoxModTool.Compatch;
using ParadoxModTool.VanillaDiff;
using Controls = System.Windows.Controls;
using MergeAction = ParadoxModTool.Compatch.MergeAction;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using static ParadoxModTool.Compatch.ActionText;

namespace ParadoxModTool.App;

/// <summary>Ein Block im Baum und auf Karten: alle Stellen einer Adresse (ein Gebäude, ein Event ...).</summary>
internal sealed record CompatchBlock(EntryAddress Address, IReadOnlyList<MergeAction> Actions);

/// <summary>Ansicht A: Kopf, Entscheidungsknöpfe, Spalten A | Ergebnis | B (| Vanilla) mit Herkunftsfarben, Bearbeiten, Formwahl.</summary>
public sealed partial class CompatchWindow
{
    private const int MaxRows = 3000;
    internal static readonly FontFamily Mono = new("Consolas");

    private readonly Controls.TextBlock _detailHeader = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.TextBlock _detailNote = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6), FontStyle = FontStyles.Italic };
    private readonly Controls.WrapPanel _detailButtons = new() { Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.CheckBox _seen = new() { Content = new Controls.TextBlock { Text = CompatchTexts.Seen }, Margin = new Thickness(6, 0, 12, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.ComboBox _form = new() { Width = 220, Margin = new Thickness(4, 0, 12, 0) };

    /// <summary>"Form" mit Auswahl; nur sichtbar, wenn die Stelle mehr als eine Form erlaubt und nicht im Update-Modus.</summary>
    private readonly Controls.StackPanel _formPanel = new() { Orientation = Controls.Orientation.Horizontal, Visibility = Visibility.Collapsed };
    private readonly Controls.CheckBox _wholeKey = new() { Content = new Controls.TextBlock { Text = CompatchTexts.WholeKey }, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    /// <summary>Nur bei einem Schlüssel mit Form "Ganze Datei": die ganzen Dateien statt des Schlüssels zeigen (Nutzerwunsch 2026-10-12).</summary>
    private readonly Controls.CheckBox _showWholeFile = new() { Content = new Controls.TextBlock { Text = CompatchTexts.ShowWholeFile }, ToolTip = CompatchTexts.ShowWholeFileTip, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly Controls.CheckBox _vanillaColumn = new() { Content = new Controls.TextBlock { Text = CompatchTexts.VanillaColumn }, VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.CheckBox _hideCommentsBox = new() { Content = new Controls.TextBlock { Text = CompatchTexts.HideComments }, ToolTip = CompatchTexts.HideCommentsTip, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    /// <summary>Überschrift über der Spaltenvorschau (Nutzerwunsch 2026-10-01): Tooltip sagt, dass hier nichts bearbeitet wird und wo es geht.</summary>
    private readonly Controls.TextBlock _previewTitle = new()
    {
        Text = CompatchTexts.PreviewTitle,
        ToolTip = CompatchTexts.PreviewTitleTip,
        FontWeight = FontWeights.Bold,
        Cursor = System.Windows.Input.Cursors.Help,
        HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
        Margin = new Thickness(0, 0, 0, 2),
    };

    private readonly Controls.Grid _columnHeader = new() { Margin = new Thickness(0, 0, SystemParameters.VerticalScrollBarWidth + 18, 2) };

    /// <summary>Übersichtsleiste neben der Spaltenvorschau (wie im Vergleichsfenster): wo die Mods abweichen, Klick springt hin.</summary>
    private readonly RowOverviewBar _detailOverview = new() { Margin = new Thickness(4, 0, 0, 0) };
    private readonly Controls.StackPanel _rows = new();
    private readonly Controls.ScrollViewer _rowsScroll = new() { HorizontalScrollBarVisibility = Controls.ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Visible };
    private readonly Controls.TextBox _editor = new() { AcceptsReturn = true, AcceptsTab = true, FontFamily = Mono, VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = Controls.ScrollBarVisibility.Auto };
    private readonly Controls.DockPanel _editPanel = new() { Visibility = Visibility.Collapsed };
    private bool _updatingDetail;

    partial void InitializeDetail()
    {
        var panel = new Controls.DockPanel { Margin = new Thickness(8, 0, 0, 0) };
        var top = new Controls.StackPanel();
        Controls.DockPanel.SetDock(top, Controls.Dock.Top);
        top.Children.Add(_detailHeader);
        top.Children.Add(_detailNote);
        top.Children.Add(_detailButtons);
        var options = new Controls.WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        _formPanel.Children.Add(new Controls.TextBlock { Text = CompatchTexts.FormLabel, VerticalAlignment = System.Windows.VerticalAlignment.Center });
        _formPanel.Children.Add(_form);
        options.Children.Add(_formPanel);
        options.Children.Add(_wholeKey);
        options.Children.Add(_showWholeFile);
        options.Children.Add(_vanillaColumn);
        _hideCommentsBox.IsChecked = _host.Defaults.HideComments;
        _hideCommentsBox.Click += (_, _) => SetHideComments(_hideCommentsBox.IsChecked == true);
        options.Children.Add(_hideCommentsBox);
        var legend = CompatchLegend.Build(_state.ModAName, _state.ModBName);
        legend.Margin = new Thickness(16, 0, 0, 0);
        options.Children.Add(legend);
        top.Children.Add(options);
        top.Children.Add(_previewTitle);
        top.Children.Add(_columnHeader);
        panel.Children.Add(top);

        var editButtons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        Controls.DockPanel.SetDock(editButtons, Controls.Dock.Bottom);
        editButtons.Children.Add(DetailButton(CompatchTexts.Apply, ApplyEdit));
        editButtons.Children.Add(DetailButton(CompatchTexts.Cancel, () => ShowEditor(false)));
        _editPanel.Children.Add(editButtons);
        _editPanel.Children.Add(_editor);

        var body = new Controls.Grid();
        _rowsScroll.Content = _rows;
        body.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = GridLength.Auto });
        body.Children.Add(_rowsScroll);
        Controls.Grid.SetColumn(_detailOverview, 1);
        body.Children.Add(_detailOverview);
        _detailOverview.RowClicked += (_, index) => ScrollDetailTo(index);
        body.Children.Add(_editPanel);
        panel.Children.Add(body);
        _detailHost.Child = panel;

        _seen.Click += (_, _) =>
        {
            if (SelectedAction is { } action && _session is not null)
            {
                _session.SetSeen(action.Id, _seen.IsChecked == true);
                AfterChange();
            }
        };
        _form.SelectionChanged += (_, _) =>
        {
            if (!_updatingDetail && SelectedAction is { } action && _session is not null && _form.SelectedItem is FormItem item)
            {
                _session.SetForm(action.Address, item.Form);
                AfterChange();
            }
        };
        PreviewKeyDown += OnDetailKeyDown;
        _wholeKey.Click += (_, _) => RefreshDetail();
        _showWholeFile.Click += (_, _) => RefreshDetail();
        _vanillaColumn.Click += (_, _) => RefreshDetail();
        OnActionSelected(null);
    }

    /// <summary>Kommentarzeilen (#) in allen Code-Anzeigen ausblenden (gemeinsame Einstellung, nur Anzeige).</summary>
    internal bool HideComments => _hideCommentsBox.IsChecked == true;

    /// <summary>Schaltet das Ausblenden um (Ansicht A oder Vergleichsfenster), merkt es global und zeichnet alle Anzeigen neu.</summary>
    internal void SetHideComments(bool hide)
    {
        _hideCommentsBox.IsChecked = hide;
        _host.RememberHideComments(hide);
        RefreshDetail();
        RefreshCurrentView(rebuild: false);
        foreach (var window in _compareWindows.ToList())
        {
            window.Refresh();
        }
    }

    private sealed record FormItem(PatchForm Form, string Text)
    {
        public override string ToString() => Text;
    }

    private Controls.Button DetailButton(string text, Action click)
    {
        var button = new Controls.Button { Content = text, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 4) };
        button.Click += (_, _) => click();
        return button;
    }

    private void Decide(Action<CompatchSession, MergeAction> change)
    {
        if (SelectedAction is { } action)
        {
            ChangeAction(action, change);
        }
    }

    private readonly Stack<DecisionSnapshot> _undo = new();

    /// <summary>
    /// Jede Entscheidung läuft hier durch (Nutzerwunsch Rückgängig für alles): Stand der betroffenen Blöcke sichern, ändern, neu zeichnen.
    /// Strg+Z (<see cref="UndoTake"/>) stellt den gesicherten Stand wieder her.
    /// </summary>
    internal void Change(IEnumerable<EntryAddress> addresses, Action<CompatchSession> change)
    {
        if (_session is null)
        {
            return;
        }

        var snapshot = _session.Capture(addresses);
        change(_session);
        _undo.Push(snapshot);
        AfterChange();
    }

    /// <summary>Nach einem neuen Lauf gehören gesicherte Stände zu alten Stellen: Rückgängig beginnt von vorn.</summary>
    private void ClearUndo() => _undo.Clear();

    /// <summary>Stufe, Herkunft, Hinweise und Entscheidung einer Aktion (Ansicht A, Karten, Ansicht B, Vergleich). Entscheidung mit Modnamen.</summary>
    internal static string Summary(CompatchSession session, MergeAction action, string modA, string modB) =>
        $"{CompatchTexts.Stage(action.Stage)} · {CompatchTexts.Origin(action.Origin)}"
        + (action.Uncertain ? " · " + CompatchTexts.Uncertain : string.Empty)
        + (IsOpenConflict(session, action) ? " · " + CompatchTexts.Open : string.Empty)
        + (action.Note is { } note ? " · " + note : string.Empty)
        + (session.HasBlockText(action.Address) ? " · " + CompatchTexts.BlockTextNote : string.Empty)
        + "\n" + CompatchTexts.DecisionLabel(DecisionText(session.EffectiveDecision(action).Kind, modA, modB)) + (session.IsExplicit(action.Id) ? " " + CompatchTexts.ExplicitMark : string.Empty);

    /// <summary>Plan 5, Ruling 5: "A/B übernehmen" mit Modnamen.</summary>
    internal static string DecisionText(DecisionKind kind, string modA, string modB) => kind switch
    {
        DecisionKind.TakeA => CompatchTexts.TakeMod(modA),
        DecisionKind.TakeB => CompatchTexts.TakeMod(modB),
        _ => CompatchTexts.Decision(kind),
    };

    /// <summary>Plan 5, Ruling 6: Farbe von Kartenrand und Baumpunkt.</summary>
    internal static string StageToken(CompatchSession session, MergeAction action) => action.Stage switch
    {
        MergeStage.Conflict => IsOpenConflict(session, action) ? "CompatchConflict" : "CompatchReview",
        MergeStage.Review or MergeStage.Outdated or MergeStage.FromExistingPatch => "CompatchReview",
        _ => "CompatchVanilla",
    };

    /// <summary>Knopf in der Farbe einer Seite (Legende).</summary>
    internal static Controls.Button TintButton(Controls.Button button, string token)
    {
        button.Background = Tint(token);
        return button;
    }

    /// <summary>Erledigt: entschieden (auch aus einem gespeicherten Stand) oder mit "Passt" bestätigt. Grüner Haken im Baum, Filter "Erledigte ausblenden".</summary>
    internal static bool IsDone(CompatchSession session, MergeAction action) =>
        action.Stage == MergeStage.Decided || session.IsExplicit(action.Id);

    /// <summary>Konflikt ohne Nutzerentscheidung (rote Ergebniszeilen, "offen").</summary>
    internal static bool IsOpenConflict(CompatchSession session, MergeAction action) =>
        action.Stage == MergeStage.Conflict && !session.HasDecision(action.Id);

    partial void OnActionSelected(MergeAction? action)
    {
        ShowEditor(false);
        _detailButtons.Children.Clear();
        _detailHotkeys.Clear();
        _rows.Children.Clear();
        _detailShown = Array.Empty<AlignedRow>();
        _detailTake = null;
        _detailSelection?.Reset();
        _detailOverview.Update(Array.Empty<AlignedRow>(), false);
        _columnHeader.Children.Clear();
        _columnHeader.ColumnDefinitions.Clear();
        if (action is null || _session is null)
        {
            _detailHeader.Text = CompatchTexts.NoSelection;
            _detailNote.Text = string.Empty;
            _form.ItemsSource = null;
            _formPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var info = _session.Run.Resolved.Addresses[action.Address];
        var decision = _session.EffectiveDecision(action);
        var open = IsOpenConflict(_session, action);
        _detailHeader.Text = $"{action.DisplayPath}   [{TreePath.FileOf(action)}]\n" + Summary(_session, action, _state.ModAName, _state.ModBName);
        _detailNote.Text = action.Stage == MergeStage.FromExistingPatch ? CompatchTexts.ExistingPatchNote : string.Empty;

        var wholeFile = action.Address.Kind == AddressKind.WholeFile;
        _detailButtons.Children.Add(TintButton(DetailHotkeyButton(CompatchTexts.TakeMod(_state.ModAName), System.Windows.Input.Key.A, () => Decide((s, a) => s.Decide(a.Id, new MergeDecision(DecisionKind.TakeA)))), "CompatchA"));
        _detailButtons.Children.Add(TintButton(DetailHotkeyButton(CompatchTexts.TakeMod(_state.ModBName), System.Windows.Input.Key.B, () => Decide((s, a) => s.Decide(a.Id, new MergeDecision(DecisionKind.TakeB)))), "CompatchB"));
        if (!(_state.MainReplacesVanilla && MainSide() is not null))
        {
            _detailButtons.Children.Add(DetailButton(CompatchTexts.Decision(DecisionKind.TakeVanilla), () => Decide((s, a) => s.Decide(a.Id, new MergeDecision(DecisionKind.TakeVanilla)))));
        }
        var edit = DetailButton(CompatchTexts.Edit, () => ShowEditor(true));
        edit.IsEnabled = !wholeFile;
        _detailButtons.Children.Add(edit);
        _detailButtons.Children.Add(DetailHotkeyButton(CompatchTexts.Confirm, System.Windows.Input.Key.O, () => Decide((s, a) => s.Confirm(a.Id))));
        _detailButtons.Children.Add(DetailButton(CompatchTexts.UndoLabel, () => Decide((s, a) => s.Undo(a.Id))));
        _detailButtons.Children.Add(DetailHotkeyButton(CompatchTexts.Reset, System.Windows.Input.Key.R, () => Decide((s, a) => s.ResetDecision(a.Id))));
        // Einzelne Stelle hat keinen Vergleich-Knopf; C öffnet das Fenster wie ein Doppelklick im Baum.
        _detailHotkeys[System.Windows.Input.Key.C] = () => OpenCompare(action);
        if (_session.EqualPending(action.Id).Count is var equal and > 0)
        {
            var toEqual = DetailButton(CompatchTexts.ApplyToEqual(equal), () => Decide((s, a) => s.ApplyToEqual(a.Id)));
            toEqual.ToolTip = CompatchTexts.ApplyToEqualTip;
            _detailButtons.Children.Add(toEqual);
        }
        _seen.IsChecked = _session.IsSeen(action.Id);
        _detailButtons.Children.Add(_seen);

        _updatingDetail = true;
        var forms = _session.Run.Plan.AvailableForms.TryGetValue(action.Address, out var available) ? available : Array.Empty<PatchForm>();
        _form.ItemsSource = forms.Select(f => new FormItem(f, CompatchTexts.Form(f))).ToList();
        _form.SelectedItem = ((IEnumerable<FormItem>)_form.ItemsSource).FirstOrDefault(i => i.Form == (_session.FormOf(action.Address) ?? forms.FirstOrDefault()));
        // Update-Modus: immer in die eigene Datei der Mod (ganze Datei), keine Wahl. Ohne Wahl ist das Feld ausgeblendet (Nutzerwunsch
        // 2026-10-01): im Update-Modus und an Stellen mit nur einer Form, z. B. die meisten in CK III ohne INJECT:/REPLACE:.
        _formPanel.Visibility = forms.Count > 1 && !IsUpdate ? Visibility.Visible : Visibility.Collapsed;
        if (IsUpdate)
        {
            _form.SelectedItem = ((IEnumerable<FormItem>)_form.ItemsSource).FirstOrDefault(i => i.Form == PatchForm.WholeFile);
        }
        _updatingDetail = false;

        // In A und B entfernt (z. B. nach einem fertigen Update): der Text steht nur noch in Vanilla, also diese Spalte zeigen (Nutzertest 2026-10-01).
        var removed = RemovedInBoth(action);
        var showVanilla = _vanillaColumn.IsChecked == true || removed;
        if (removed)
        {
            _detailNote.Text = (_detailNote.Text.Length > 0 ? _detailNote.Text + "\n" : string.Empty) + CompatchTexts.RemovedInBoth(BaseColumnTitle);
        }

        _showWholeFile.Visibility = IsWholeFileForm(action) ? Visibility.Visible : Visibility.Collapsed;
        var keyFile = _showWholeFile.Visibility == Visibility.Visible && _showWholeFile.IsChecked == true;
        var rows = wholeFile
            ? ActionText.WholeFileRows(info, decision, showVanilla)
            : keyFile
                ? ActionText.KeyFileRows(info, PreviewWrite(), showVanilla)
            : _wholeKey.IsChecked == true || action.Stage == MergeStage.FromExistingPatch
                ? EntryAligner.Align(info.A.Entries, _session.Run.Applied.Desired.TryGetValue(action.Address, out var desired) ? desired.Entries : info.NoPatch.Entries, info.B.Entries, showVanilla ? info.Vanilla.Entries : null, HideComments)
                : EntryAligner.Align(action.A, ResultEntries(action, decision), action.B, showVanilla ? action.Vanilla : null, HideComments);
        BuildColumns(showVanilla);
        var wholeBlock = _wholeKey.IsChecked == true || action.Stage == MergeStage.FromExistingPatch;
        ShowDetailRows(rows, showVanilla, open, wholeFile || keyFile
            ? null
            : wholeBlock
                ? (column, from, to) => TakeBlockText(action.Address, CompareNavigation.TakeRows(_detailShown, from, to, column), column)
                : (column, from, to) => TakeActionText(action, CompareNavigation.TakeRows(_detailShown, from, to, column)));
    }

    private CompatchBlock? SelectedBlock => (_tree.SelectedItem as Controls.TreeViewItem)?.Tag as CompatchBlock;

    /// <summary>Zeigt die Auswahl im Baum neu: Block oder einzelne Stelle.</summary>
    private void RefreshDetail()
    {
        if (SelectedBlock is { } block)
        {
            OnBlockSelected(block);
        }
        else if ((_tree.SelectedItem as Controls.TreeViewItem)?.Tag is string key && key.Length > 2 && key[1] == ':' && key[0] is 'f' or 'd')
        {
            OnNodeSelected(key[2..], key[0] == 'f' ? TreeNodeKind.File : TreeNodeKind.Folder);
        }
        else
        {
            OnActionSelected(SelectedAction);
        }
    }

    /// <summary>Sammelentscheidung (Block, Datei, Ordner); <paramref name="decisionFor"/> <see langword="null"/> = Vorschlag jeder Stelle bestätigen.</summary>
    internal void DecideAll(IReadOnlyList<MergeAction> actions, Func<MergeAction, MergeDecision>? decisionFor)
    {
        if (_session is null || actions.Count == 0)
        {
            return;
        }

        var ids = actions.Select(a => a.Id).ToList();
        if (decisionFor is not null && _session.Overwritten(ids, decisionFor) is { Count: > 0 } overwritten)
        {
            // Nachfrage (Nutzerwunsch): schon getroffene Entscheidungen nicht stillschweigend überschreiben.
            switch (CompatchOverwriteDialog.Ask(this, overwritten.Count, ids.Count))
            {
                case OverwriteChoice.Cancel:
                    return;
                case OverwriteChoice.OnlyOpen:
                    var keep = overwritten.ToHashSet(StringComparer.Ordinal);
                    ids = ids.Where(id => !keep.Contains(id)).ToList();
                    if (ids.Count == 0)
                    {
                        _status.Text = CompatchTexts.NothingOpenToDecide;
                        return;
                    }

                    break;
            }
        }
        else if (decisionFor is null && _session.OpenConflicts(ids) is { Count: > 0 } open)
        {
            // "Alle als gelöst markieren" übernimmt bei offenen Konflikten den Vorschlag: vorher nachfragen (Nutzerwunsch).
            switch (CompatchOverwriteDialog.AskConfirm(this, open.Count, ids.Count))
            {
                case OverwriteChoice.Cancel:
                    return;
                case OverwriteChoice.OnlyOpen:
                    var skip = open.ToHashSet(StringComparer.Ordinal);
                    ids = ids.Where(id => !skip.Contains(id)).ToList();
                    if (ids.Count == 0)
                    {
                        _status.Text = CompatchTexts.ConfirmAllNothingLeft;
                        return;
                    }

                    break;
            }
        }

        Change(actions.Select(a => a.Address), session =>
        {
            if (decisionFor is null)
            {
                session.ConfirmMany(ids);
            }
            else
            {
                session.DecideMany(ids, decisionFor);
            }
        });
    }

    /// <summary>
    /// Markierte Zeilen eines Blocks übernehmen (Vergleichsfenster, Block in Ansicht A): je geänderte Stelle eine Entscheidung, sonst Blocktext;
    /// Strg+Z nimmt es zurück. Liefert den Text für die Statuszeile.
    /// </summary>
    internal string TakeBlockText(EntryAddress address, string text, CompatchColumn column)
    {
        try
        {
            ScriptDocument.Parse(text);
        }
        catch (ScriptParseException ex)
        {
            return CompatchTexts.TakeFailed(ex.Message);
        }

        string? error = null;
        var before = DesiredText(address);
        Change(new[] { address }, session => error = session.TakeBlockLines(address, text, column));
        return error is not null ? CompatchTexts.TakeFailed(error) : TakeOutcome(address, before);
    }

    /// <summary>
    /// Markierte Zeilen einer einzelnen Stelle übernehmen (Ansicht A): als eigener Text der Stelle, wie "Bearbeiten". Strg+Z nimmt es zurück.
    /// Liefert den Text für die Statuszeile.
    /// </summary>
    internal string TakeActionText(MergeAction action, string text)
    {
        try
        {
            ScriptDocument.Parse(text);
        }
        catch (ScriptParseException ex)
        {
            return CompatchTexts.TakeFailed(ex.Message);
        }

        var before = DesiredText(action.Address);
        Change(new[] { action.Address }, session => session.Decide(action.Id, new MergeDecision(DecisionKind.Manual, text)));
        return TakeOutcome(action.Address, before);
    }

    /// <summary>Inhalt des Ergebnisses einer Adresse ohne Kommentare und Formatierung (Vergleich vor und nach dem Übernehmen).</summary>
    private string DesiredText(EntryAddress address) =>
        _session is not null && _session.Run.Applied.Desired.TryGetValue(address, out var desired)
            ? SiblingMatcher.Canonical(new ScriptBlock { Entries = desired.Entries.ToList() })
            : string.Empty;

    /// <summary>
    /// "Übernommen" oder, wenn sich der Inhalt des Ergebnisses nicht geändert hat, ein Hinweis darauf (Nutzertest 2026-10-01: "markiert, nichts
    /// passiert"). Kommentare und Leerzeilen zählen beim Vergleich nicht, auskommentierter Code ändert also nichts.
    /// </summary>
    private string TakeOutcome(EntryAddress address, string before) =>
        DesiredText(address) == before ? CompatchTexts.TakeUnchanged : CompatchTexts.TakeDone;

    /// <summary>Letzte Entscheidung oder Übernahme rückgängig machen (alle Ansichten und Vergleichsfenster); <see langword="false"/>, wenn es keine gibt.</summary>
    internal bool UndoTake()
    {
        if (_session is null || _undo.Count == 0)
        {
            return false;
        }

        _session.Restore(_undo.Pop());
        AfterChange();
        return true;
    }

    /// <summary>
    /// U übernimmt die markierten Zeilen der Spaltenvorschau (nur Ansicht A), Strg+Z nimmt die letzte Entscheidung oder Übernahme zurück (jede
    /// Ansicht). In Ansicht A entscheiden O ("Passt"), A, B und R ("Zurücksetzen") die gewählte Stelle bzw. den Block, C öffnet den Vergleich;
    /// Pfeil hoch/runter in der Vorschau wechselt den Eintrag im Baum. Nicht beim Tippen in einem Eingabefeld (Suche, Bearbeiten), dort gelten die
    /// Tasten wie gewohnt.
    /// </summary>
    private void OnDetailKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (System.Windows.Input.Keyboard.FocusedElement is Controls.Primitives.TextBoxBase)
        {
            return;
        }

        var modifiers = System.Windows.Input.Keyboard.Modifiers;
        var noModifier = modifiers == System.Windows.Input.ModifierKeys.None;
        // Entscheidungstasten und Pfeiltasten nur in sichtbarer Ansicht A und nicht während "Bearbeiten" (Text ginge sonst verloren).
        var listActive = _listView.IsVisible && _editPanel.Visibility != Visibility.Visible;
        if (e.Key == System.Windows.Input.Key.U && noModifier && _view == CompatchView.Actions)
        {
            _detailSelection?.Take();
            e.Handled = true;
        }
        else if (noModifier && listActive
            && e.Key is System.Windows.Input.Key.O or System.Windows.Input.Key.A or System.Windows.Input.Key.B or System.Windows.Input.Key.R or System.Windows.Input.Key.C
            && _detailHotkeys.TryGetValue(e.Key, out var hotkey))
        {
            hotkey();
            e.Handled = true;
        }
        else if (noModifier && listActive && e.Key is System.Windows.Input.Key.Up or System.Windows.Input.Key.Down && FocusInPreview())
        {
            SelectNeighbourInTree(forward: e.Key == System.Windows.Input.Key.Down);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Z && modifiers == System.Windows.Input.ModifierKeys.Control)
        {
            _status.Text = UndoTake() ? CompatchTexts.TakeUndone : CompatchTexts.NothingToUndo;
            e.Handled = true;
        }
    }

    /// <summary>Tasten O, A, B, R, C der gewählten Stelle bzw. des gewählten Blocks (Nutzerwunsch 2026-10-07); leer bei Datei, Ordner und ohne Auswahl.</summary>
    private readonly Dictionary<System.Windows.Input.Key, Action> _detailHotkeys = new();

    /// <summary>Knopf mit Taste: Tooltip nennt sie, die Taste löst dasselbe aus wie der Klick.</summary>
    private Controls.Button DetailHotkeyButton(string text, System.Windows.Input.Key key, Action click)
    {
        var button = DetailButton(text, click);
        button.ToolTip = CompatchTexts.HotkeyTip(key.ToString());
        _detailHotkeys[key] = click;
        return button;
    }

    /// <summary>
    /// Fokus in der rechten Vorschau (Zeilen, Knöpfe, Häkchen) oder nirgends, z. B. nachdem ein Knopf beim Neuzeichnen verschwunden ist. Im Baum
    /// gelten die Pfeiltasten ohnehin, im Feld "Form" wählen sie die Form.
    /// </summary>
    private bool FocusInPreview()
    {
        if (_form.IsKeyboardFocusWithin || _form.IsDropDownOpen)
        {
            return false;
        }

        var focused = System.Windows.Input.Keyboard.FocusedElement;
        return _detailHost.IsKeyboardFocusWithin || focused is null || ReferenceEquals(focused, this);
    }

    /// <summary>
    /// Pfeil hoch/runter in der Vorschau (Nutzerwunsch 2026-10-07): wählt den vorigen bzw. nächsten sichtbaren Eintrag im Baum, wie die Pfeiltasten
    /// im Baum selbst. Der Fokus bleibt in der Vorschau, damit man mit den Pfeiltasten und den Entscheidungstasten weiter durchgehen kann.
    /// </summary>
    private void SelectNeighbourInTree(bool forward)
    {
        var visible = new List<Controls.TreeViewItem>();
        void Collect(Controls.ItemCollection items)
        {
            foreach (var item in items.OfType<Controls.TreeViewItem>())
            {
                if (item.Visibility != Visibility.Visible || ReferenceEquals(item.Tag, TreePlaceholder))
                {
                    continue;
                }

                visible.Add(item);
                if (item.IsExpanded)
                {
                    Collect(item.Items);
                }
            }
        }

        Collect(_tree.Items);
        var index = _tree.SelectedItem is Controls.TreeViewItem selected ? visible.IndexOf(selected) : -1;
        var next = index < 0 ? 0 : index + (forward ? 1 : -1);
        if (next < 0 || next >= visible.Count)
        {
            return;
        }

        visible[next].IsSelected = true;
        visible[next].BringIntoView();
        if (!_detailHost.IsKeyboardFocusWithin)
        {
            _rowsScroll.FocusVisualStyle = null;
            _rowsScroll.Focusable = true;
            _rowsScroll.Focus();
        }
    }

    private IReadOnlyList<AlignedRow> _detailShown = Array.Empty<AlignedRow>();
    private Func<CompatchColumn, int, int, string?>? _detailTake;
    private CompatchRowSelection? _detailSelection;
    private bool _detailShowsVanilla;

    /// <summary>Stelle, die A und B beide entfernt haben und die nur in Vanilla stand (sonst bliebe die Vorschau leer).</summary>
    internal static bool RemovedInBoth(MergeAction action) =>
        action.Address.Kind != AddressKind.WholeFile && action.A.Count == 0 && action.B.Count == 0 && action.Vanilla.Count > 0;

    /// <summary>
    /// Zeilen der Spaltenvorschau in Ansicht A. Jede Zeile steht in einem Rahmen, damit man sie markieren und mit Rechtsklick oder U übernehmen kann
    /// (<paramref name="take"/>; <see langword="null"/> = nicht markierbar, z. B. ganze Dateien).
    /// </summary>
    private void ShowDetailRows(IReadOnlyList<AlignedRow> rows, bool showVanilla, bool open, Func<CompatchColumn, int, int, string?>? take)
    {
        _detailSelection ??= new CompatchRowSelection(
            _rows,
            () => _detailShown,
            () => _detailTake is not null,
            () => _detailShowsVanilla,
            SourceName,
            (column, from, to) => _detailTake?.Invoke(column, from, to),
            text => _status.Text = text);
        _detailSelection.Reset();
        _detailShown = rows.Take(MaxRows).ToList();
        _detailTake = take;
        _detailShowsVanilla = showVanilla;
        foreach (var row in _detailShown)
        {
            _rows.Children.Add(new Controls.Border { Child = RowGrid(row, showVanilla, open), BorderThickness = new Thickness(1), BorderBrush = System.Windows.Media.Brushes.Transparent });
        }

        _detailOverview.Update(_detailShown, open);
    }

    /// <summary>Klick in die Übersichtsleiste: die Zeile nach oben in den sichtbaren Bereich holen.</summary>
    private void ScrollDetailTo(int index)
    {
        if (index < 0 || index >= _rows.Children.Count || _rows.Children[index] is not FrameworkElement row)
        {
            return;
        }

        _rows.UpdateLayout();
        _rowsScroll.ScrollToVerticalOffset(Math.Max(0, row.TranslatePoint(new System.Windows.Point(0, 0), _rows).Y - 24));
    }

    /// <summary>Anzeigename der Quelle einer Spalte (Menü "Markierte Zeilen aus … übernehmen").</summary>
    internal string SourceName(CompatchColumn column) => column switch
    {
        CompatchColumn.A => ModAName,
        CompatchColumn.B => ModBName,
        _ => BaseColumnTitle,
    };

    /// <summary>Ein Block (alle Stellen einer Adresse, Nutzerwunsch): Kopf, Knöpfe für den ganzen Block und der ganze Block in Spalten.</summary>
    private void OnBlockSelected(CompatchBlock block)
    {
        ShowEditor(false);
        _detailButtons.Children.Clear();
        _detailHotkeys.Clear();
        _rows.Children.Clear();
        _detailShown = Array.Empty<AlignedRow>();
        _detailTake = null;
        _detailSelection?.Reset();
        _detailOverview.Update(Array.Empty<AlignedRow>(), false);
        _columnHeader.Children.Clear();
        _columnHeader.ColumnDefinitions.Clear();
        _form.ItemsSource = null;
        _formPanel.Visibility = Visibility.Collapsed;
        _detailNote.Text = string.Empty;
        if (_session is not { } session || !session.Run.Resolved.Addresses.TryGetValue(block.Address, out var info))
        {
            _detailHeader.Text = CompatchTexts.NoSelection;
            return;
        }

        var open = block.Actions.Count(a => IsOpenConflict(session, a));
        _detailHeader.Text = $"{block.Actions[0].Address.Key}   [{TreePath.FileOf(block.Actions[0])}]\n" + CompatchTexts.BlockSummary(block.Actions.Count, open);
        _detailButtons.Children.Add(TintButton(DetailHotkeyButton(CompatchTexts.BlockTake(_state.ModAName), System.Windows.Input.Key.A, () => DecideAll(block.Actions, _ => new MergeDecision(DecisionKind.TakeA))), "CompatchA"));
        _detailButtons.Children.Add(TintButton(DetailHotkeyButton(CompatchTexts.BlockTake(_state.ModBName), System.Windows.Input.Key.B, () => DecideAll(block.Actions, _ => new MergeDecision(DecisionKind.TakeB))), "CompatchB"));
        _detailButtons.Children.Add(DetailHotkeyButton(CompatchTexts.BlockConfirm, System.Windows.Input.Key.O, () => DecideAll(block.Actions, null)));
        _detailButtons.Children.Add(DetailHotkeyButton(CompatchTexts.BlockReset, System.Windows.Input.Key.R, () => ResetAll(block.Address.Id, TreeNodeKind.Block)));
        _detailButtons.Children.Add(DetailHotkeyButton(CompatchTexts.Compare, System.Windows.Input.Key.C, () => OpenCompare(block.Actions[0])));

        var showVanilla = _vanillaColumn.IsChecked == true;
        session.Run.Applied.Desired.TryGetValue(block.Address, out var desired);
        var rows = EntryAligner.Align(info.A.Entries, desired?.Entries ?? info.NoPatch.Entries, info.B.Entries, showVanilla ? info.Vanilla.Entries : null, HideComments);
        BuildColumns(showVanilla);
        ShowDetailRows(rows, showVanilla, open > 0, (column, from, to) => TakeBlockText(block.Address, CompareNavigation.TakeRows(_detailShown, from, to, column), column));
    }

    /// <summary>
    /// Datei oder Ordner im Baum gewählt (Nutzerwunsch 2026-10-01): Kopf mit Anzahl und dieselben Sammelknöpfe wie im Rechtsklick-Menü, dazu
    /// "Alle Stellen zurücksetzen" als Ausweg für eine ganze Datei. Keine Spalten (eine Datei kann tausende Stellen haben).
    /// </summary>
    private void OnNodeSelected(string path, TreeNodeKind kind)
    {
        OnActionSelected(null);
        if (_session is not { } session)
        {
            return;
        }

        var all = ActionFilters.Under(session.Actions, path, kind);
        _detailHeader.Text = path + "\n" + CompatchTexts.NodeSummary(all.Count, all.Count(a => IsOpenConflict(session, a)));
        _detailButtons.Children.Add(TintButton(DetailButton(CompatchTexts.BulkTake(_state.ModAName), () => DecideAll(ActionFilters.Under(FilteredActions(), path, kind), _ => new MergeDecision(DecisionKind.TakeA))), "CompatchA"));
        _detailButtons.Children.Add(TintButton(DetailButton(CompatchTexts.BulkTake(_state.ModBName), () => DecideAll(ActionFilters.Under(FilteredActions(), path, kind), _ => new MergeDecision(DecisionKind.TakeB))), "CompatchB"));
        _detailButtons.Children.Add(DetailButton(CompatchTexts.BulkConfirm, () => DecideAll(ActionFilters.Under(FilteredActions(), path, kind), null)));
        _detailButtons.Children.Add(DetailButton(CompatchTexts.BulkReset, () => ResetAll(path, kind)));
    }

    private void BuildColumns(bool showVanilla) =>
        AddColumnTitles(_columnHeader, _state.ModAName, _state.ModBName, showVanilla ? BaseColumnTitle : null, FontWeights.Bold);

    /// <summary>Einstellung "Ergebnis ganz rechts" (<see cref="ParadoxModTool.Settings.AppSettings.CompatchResultRight"/>); das Hauptfenster setzt sie.</summary>
    internal static bool ResultRight { get; set; }

    /// <summary>Spaltenköpfe in der Reihenfolge von <see cref="CompatchColumns"/>; <paramref name="vanillaTitle"/> = <see langword="null"/> ohne Vanilla-Spalte.</summary>
    internal static void AddColumnTitles(Controls.Grid grid, string modA, string modB, string? vanillaTitle, FontWeight weight)
    {
        var order = CompatchColumns.Order(vanillaTitle is not null, ResultRight);
        for (var i = 0; i < order.Count; i++)
        {
            grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var text = order[i] switch
            {
                CompatchColumn.A => CompatchTexts.ColumnA(modA),
                CompatchColumn.B => CompatchTexts.ColumnB(modB),
                CompatchColumn.Vanilla => vanillaTitle ?? string.Empty,
                _ => CompatchTexts.ColumnResult,
            };
            var title = new Controls.Border
            {
                Padding = new Thickness(4, 0, 4, 0),
                Child = new Controls.TextBlock { Text = text, FontWeight = weight, TextTrimming = TextTrimming.CharacterEllipsis },
            };
            MarkDivider(title, order[i]);
            Controls.Grid.SetColumn(title, i);
            grid.Children.Add(title);
        }
    }

    /// <summary>Trennlinie zwischen Vergleich und neuem Stand, wenn das Ergebnis ganz rechts steht.</summary>
    private static void MarkDivider(Controls.Border border, CompatchColumn column)
    {
        if (CompatchColumns.DividerBefore(column, ResultRight))
        {
            border.BorderThickness = new Thickness(3, 0, 0, 0);
            border.SetResourceReference(Controls.Border.BorderBrushProperty, "Theme.Separator");
        }
    }

    internal static Brush Tint(string token)
    {
        var color = ThemeManager.GetColor(token);
        var brush = new SolidColorBrush(Color.FromArgb(70, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    internal static Controls.Grid RowGrid(AlignedRow row, bool showVanilla, bool openConflict)
    {
        var grid = new Controls.Grid();
        (string? Text, Brush? Background) Cell(CompatchColumn column) => column switch
        {
            CompatchColumn.A => (row.A, row.A is null ? Tint("CompatchVanilla") : row.Origin == RowOrigin.FromA || row.Result is null ? Tint("CompatchA") : null),
            CompatchColumn.B => (row.B, row.B is null ? Tint("CompatchVanilla") : row.Origin == RowOrigin.FromB || row.Result is null ? Tint("CompatchB") : null),
            CompatchColumn.Vanilla => (row.Vanilla, row.Vanilla is null ? Tint("CompatchVanilla") : null),
            _ => (row.Result, row.Origin switch
            {
                RowOrigin.FromA => Tint("CompatchA"),
                RowOrigin.FromB => Tint("CompatchB"),
                RowOrigin.Other => Tint(openConflict ? "CompatchConflict" : "CompatchReview"),
                RowOrigin.Gap => Tint("CompatchVanilla"),
                _ => null,
            }),
        };
        var order = CompatchColumns.Order(showVanilla, ResultRight);
        var cells = order.Select(Cell).ToList();

        for (var i = 0; i < cells.Count; i++)
        {
            grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var cell = new Controls.Border { Background = cells[i].Background ?? System.Windows.Media.Brushes.Transparent, Padding = new Thickness(4, 0, 4, 0) };
            MarkDivider(cell, order[i]);
            // Umbruch statt fester Zeile: jede Zeile ist ein eigenes Grid, ein überlanger Text würde sonst seine Spalte verbreitern
            // und die Spalten dieser Zeile gegenüber den Nachbarzeilen verschieben (Nutzer-Feedback: Farben fließen ineinander).
            cell.Child = new Controls.TextBlock { Text = cells[i].Text ?? string.Empty, FontFamily = Mono, TextWrapping = TextWrapping.Wrap };
            Controls.Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        return grid;
    }

    private void ShowEditor(bool show)
    {
        if (show && SelectedAction is { } action && _session is not null)
        {
            _editor.Text = Render(ResultEntries(action, _session.EffectiveDecision(action)));
        }

        _editPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        _rowsScroll.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        _detailOverview.Visibility = _rowsScroll.Visibility;
        _previewTitle.Visibility = _rowsScroll.Visibility;
        if (show)
        {
            _editor.Focus();
        }
    }

    private void ApplyEdit()
    {
        if (SelectedAction is not { } action || _session is null)
        {
            return;
        }

        Change(new[] { action.Address }, s => s.Decide(action.Id, new MergeDecision(DecisionKind.Manual, _editor.Text)));
        var error = _session.Run.Applied.Errors.FirstOrDefault(e => e.StartsWith(action.DisplayPath, StringComparison.Ordinal));
        _status.Text = error is null ? string.Empty : CompatchTexts.ManualParseError(error);
    }
}
