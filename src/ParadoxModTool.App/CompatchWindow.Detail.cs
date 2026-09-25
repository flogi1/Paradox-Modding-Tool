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

/// <summary>Ansicht A: Kopf, Entscheidungsknöpfe, Spalten A | Ergebnis | B (| Vanilla) mit Herkunftsfarben, Bearbeiten, Formwahl.</summary>
public sealed partial class CompatchWindow
{
    private const int MaxRows = 3000;
    internal static readonly FontFamily Mono = new("Consolas");

    private readonly Controls.TextBlock _detailHeader = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.TextBlock _detailNote = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6), FontStyle = FontStyles.Italic };
    private readonly Controls.WrapPanel _detailButtons = new() { Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.CheckBox _seen = new() { Content = CompatchTexts.Seen, Margin = new Thickness(6, 0, 12, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.ComboBox _form = new() { Width = 220, Margin = new Thickness(4, 0, 12, 0) };
    private readonly Controls.CheckBox _wholeKey = new() { Content = CompatchTexts.WholeKey, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.CheckBox _vanillaColumn = new() { Content = CompatchTexts.VanillaColumn, VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.CheckBox _hideCommentsBox = new() { Content = CompatchTexts.HideComments, ToolTip = CompatchTexts.HideCommentsTip, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.Grid _columnHeader = new() { Margin = new Thickness(0, 0, 0, 2) };
    private readonly Controls.StackPanel _rows = new();
    private readonly Controls.ScrollViewer _rowsScroll = new() { HorizontalScrollBarVisibility = Controls.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto };
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
        options.Children.Add(new Controls.TextBlock { Text = CompatchTexts.FormLabel, VerticalAlignment = System.Windows.VerticalAlignment.Center });
        options.Children.Add(_form);
        options.Children.Add(_wholeKey);
        options.Children.Add(_vanillaColumn);
        _hideCommentsBox.IsChecked = _host.Defaults.HideComments;
        _hideCommentsBox.Click += (_, _) => SetHideComments(_hideCommentsBox.IsChecked == true);
        options.Children.Add(_hideCommentsBox);
        var legend = CompatchLegend.Build(_state.ModAName, _state.ModBName);
        legend.Margin = new Thickness(16, 0, 0, 0);
        options.Children.Add(legend);
        top.Children.Add(options);
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
        body.Children.Add(_rowsScroll);
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
        _wholeKey.Click += (_, _) => OnActionSelected(SelectedAction);
        _vanillaColumn.Click += (_, _) => OnActionSelected(SelectedAction);
        OnActionSelected(null);
    }

    /// <summary>Kommentarzeilen (#) in allen Code-Anzeigen ausblenden (gemeinsame Einstellung, nur Anzeige).</summary>
    internal bool HideComments => _hideCommentsBox.IsChecked == true;

    /// <summary>Schaltet das Ausblenden um (Ansicht A oder Vergleichsfenster), merkt es global und zeichnet alle Anzeigen neu.</summary>
    internal void SetHideComments(bool hide)
    {
        _hideCommentsBox.IsChecked = hide;
        _host.RememberHideComments(hide);
        OnActionSelected(SelectedAction);
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
        if (SelectedAction is { } action && _session is not null)
        {
            change(_session, action);
            AfterChange();
        }
    }

    /// <summary>Stufe, Herkunft, Hinweise und Entscheidung einer Aktion (Ansicht A, Karten, Ansicht B, Vergleich). Entscheidung mit Modnamen.</summary>
    internal static string Summary(CompatchSession session, MergeAction action, string modA, string modB) =>
        $"{CompatchTexts.Stage(action.Stage)} · {CompatchTexts.Origin(action.Origin)}"
        + (action.Uncertain ? " · " + CompatchTexts.Uncertain : string.Empty)
        + (IsOpenConflict(session, action) ? " · " + CompatchTexts.Open : string.Empty)
        + (action.Note is { } note ? " · " + note : string.Empty)
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

    /// <summary>Konflikt ohne Nutzerentscheidung (rote Ergebniszeilen, "offen").</summary>
    internal static bool IsOpenConflict(CompatchSession session, MergeAction action) =>
        action.Stage == MergeStage.Conflict && !session.HasDecision(action.Id);

    partial void OnActionSelected(MergeAction? action)
    {
        ShowEditor(false);
        _detailButtons.Children.Clear();
        _rows.Children.Clear();
        _columnHeader.Children.Clear();
        _columnHeader.ColumnDefinitions.Clear();
        if (action is null || _session is null)
        {
            _detailHeader.Text = CompatchTexts.NoSelection;
            _detailNote.Text = string.Empty;
            _form.ItemsSource = null;
            return;
        }

        var info = _session.Run.Resolved.Addresses[action.Address];
        var decision = _session.EffectiveDecision(action);
        var open = IsOpenConflict(_session, action);
        _detailHeader.Text = $"{action.DisplayPath}   [{TreePath.FileOf(action)}]\n" + Summary(_session, action, _state.ModAName, _state.ModBName);
        _detailNote.Text = action.Stage == MergeStage.FromExistingPatch ? CompatchTexts.ExistingPatchNote : string.Empty;

        var wholeFile = action.Address.Kind == AddressKind.WholeFile;
        _detailButtons.Children.Add(TintButton(DetailButton(CompatchTexts.TakeMod(_state.ModAName), () => Decide((s, a) => s.Decide(a.Id, new MergeDecision(DecisionKind.TakeA)))), "CompatchA"));
        _detailButtons.Children.Add(TintButton(DetailButton(CompatchTexts.TakeMod(_state.ModBName), () => Decide((s, a) => s.Decide(a.Id, new MergeDecision(DecisionKind.TakeB)))), "CompatchB"));
        if (!(_state.MainReplacesVanilla && MainSide() is not null))
        {
            _detailButtons.Children.Add(DetailButton(CompatchTexts.Decision(DecisionKind.TakeVanilla), () => Decide((s, a) => s.Decide(a.Id, new MergeDecision(DecisionKind.TakeVanilla)))));
        }
        var edit = DetailButton(CompatchTexts.Edit, () => ShowEditor(true));
        edit.IsEnabled = !wholeFile;
        _detailButtons.Children.Add(edit);
        _detailButtons.Children.Add(DetailButton(CompatchTexts.Confirm, () => Decide((s, a) => s.Confirm(a.Id))));
        _detailButtons.Children.Add(DetailButton(CompatchTexts.UndoLabel, () => Decide((s, a) => s.Undo(a.Id))));
        _detailButtons.Children.Add(DetailButton(CompatchTexts.Reset, () => Decide((s, a) => s.ResetDecision(a.Id))));
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
        _form.IsEnabled = forms.Count > 1;
        _updatingDetail = false;

        var showVanilla = _vanillaColumn.IsChecked == true;
        var (a, result, b, vanilla) = wholeFile
            ? (info.A.RelativePath ?? string.Empty, CompatchTexts.WholeFileChoice(SideName(decision.Kind)), info.B.RelativePath ?? string.Empty, info.Vanilla.RelativePath ?? string.Empty)
            : _wholeKey.IsChecked == true || action.Stage == MergeStage.FromExistingPatch
                ? (Render(info.A.Entries, HideComments), Render(_session.Run.Applied.Desired.TryGetValue(action.Address, out var desired) ? desired.Entries : info.NoPatch.Entries, HideComments), Render(info.B.Entries, HideComments), Render(info.Vanilla.Entries, HideComments))
                : (Render(action.A, HideComments), Render(ResultEntries(action, decision), HideComments), Render(action.B, HideComments), Render(action.Vanilla, HideComments));
        BuildColumns(showVanilla);
        foreach (var row in ColumnAligner.Align(a, result, b, showVanilla ? vanilla : null).Take(MaxRows))
        {
            _rows.Children.Add(RowGrid(row, showVanilla, open));
        }
    }

    private void BuildColumns(bool showVanilla)
    {
        var titles = new List<string> { CompatchTexts.ColumnA(_state.ModAName), CompatchTexts.ColumnResult, CompatchTexts.ColumnB(_state.ModBName) };
        if (showVanilla)
        {
            titles.Add(BaseColumnTitle);
        }

        for (var i = 0; i < titles.Count; i++)
        {
            _columnHeader.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var title = new Controls.TextBlock { Text = titles[i], FontWeight = FontWeights.Bold, Margin = new Thickness(4, 0, 4, 0) };
            Controls.Grid.SetColumn(title, i);
            _columnHeader.Children.Add(title);
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
        var cells = new List<(string? Text, Brush? Background)>
        {
            (row.A, row.A is null ? Tint("CompatchVanilla") : row.Origin == RowOrigin.FromA || row.Result is null ? Tint("CompatchA") : null),
            (row.Result, row.Origin switch
            {
                RowOrigin.FromA => Tint("CompatchA"),
                RowOrigin.FromB => Tint("CompatchB"),
                RowOrigin.Other => Tint(openConflict ? "CompatchConflict" : "CompatchReview"),
                RowOrigin.Gap => Tint("CompatchVanilla"),
                _ => null,
            }),
            (row.B, row.B is null ? Tint("CompatchVanilla") : row.Origin == RowOrigin.FromB || row.Result is null ? Tint("CompatchB") : null),
        };
        if (showVanilla)
        {
            cells.Add((row.Vanilla, row.Vanilla is null ? Tint("CompatchVanilla") : null));
        }

        for (var i = 0; i < cells.Count; i++)
        {
            grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var cell = new Controls.Border { Background = cells[i].Background ?? System.Windows.Media.Brushes.Transparent, Padding = new Thickness(4, 0, 4, 0) };
            cell.Child = new Controls.TextBlock { Text = cells[i].Text ?? string.Empty, FontFamily = Mono, TextWrapping = TextWrapping.NoWrap };
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

        _session.Decide(action.Id, new MergeDecision(DecisionKind.Manual, _editor.Text));
        var error = _session.Run.Applied.Errors.FirstOrDefault(e => e.StartsWith(action.DisplayPath, StringComparison.Ordinal));
        _status.Text = error is null ? string.Empty : CompatchTexts.ManualParseError(error);
        AfterChange();
    }
}
