using System.Windows;
using System.Windows.Input;
using ParadoxModTool.Compatch;
using Controls = System.Windows.Controls;
using MergeAction = ParadoxModTool.Compatch.MergeAction;

namespace ParadoxModTool.App;

/// <summary>Eine Karte der Liste (Ansicht C); mit <paramref name="Block"/> eine Blockkarte für alle Stellen einer Adresse.</summary>
public sealed record CompatchCardItem(MergeAction Action, CompatchWindow Window, IReadOnlyList<MergeAction>? Block = null);

/// <summary>
/// Eine Review-Karte: Kopf, Mini-Diff A | Ergebnis | B, Knöpfe ↶, Andere Seite, Passt, Bearbeiten und Häkchen "Gesehen".
/// Vorlage einer virtualisierten Liste; DataContext ist ein <see cref="CompatchCardItem"/>, bei jedem Wechsel baut sich die Karte neu.
/// Ein Klick auf die Karte (nicht auf einen Knopf) springt in Ansicht A (Ruling 6).
/// </summary>
public sealed class CompatchCardView : Controls.Border
{
    private const int MaxMiniRows = 8;
    private CompatchWindow? _window;
    private string? _pendingJump;
    private readonly System.Windows.Threading.DispatcherTimer _clickTimer = new() { Interval = TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime) };

    public CompatchCardView()
    {
        Margin = new Thickness(0, 0, 0, 8);
        Padding = new Thickness(8, 6, 8, 6);
        BorderThickness = new Thickness(4, 1, 1, 1);
        CornerRadius = new CornerRadius(3);
        Cursor = System.Windows.Input.Cursors.Hand;
        SetResourceReference(BackgroundProperty, "Theme.PanelBackground");
        DataContextChanged += (_, _) =>
        {
            if (DataContext is CompatchCardItem item)
            {
                _window = item.Window;
                _window.RegisterCard(this, true);
            }

            Rebuild();
        };
        Loaded += (_, _) => _window?.RegisterCard(this, true);
        Unloaded += (_, _) => _window?.RegisterCard(this, false);
        MouseLeftButtonDown += OnCardClick;
        MouseRightButtonUp += OnCardRightClick;
        // Die Karte merkt sich die geklickte Aktion: der Container kann bis zum Tick schon eine andere Karte zeigen (Recycling beim Scrollen).
        _clickTimer.Tick += (_, _) =>
        {
            _clickTimer.Stop();
            if (_pendingJump is { } id)
            {
                _pendingJump = null;
                _window?.OpenInList(id, edit: false);
            }
        };
        Unloaded += (_, _) => CancelJump();
        DataContextChanged += (_, _) => CancelJump();
    }

    /// <summary>Zeichnet die Karte aus dem aktuellen Stand der Sitzung neu. Eine Adresse aus einem früheren Lauf ergibt eine leere Karte.</summary>
    public void Rebuild()
    {
        Child = null;
        if (DataContext is not CompatchCardItem item || item.Window.Session is not { } session
            || !session.Run.Resolved.Addresses.TryGetValue(item.Action.Address, out var info))
        {
            return;
        }

        if (item.Block is { Count: >= 2 } block)
        {
            RebuildBlock(item, block, session, info);
            return;
        }

        var action = item.Action;
        var decision = session.EffectiveDecision(action);
        var open = CompatchWindow.IsOpenConflict(session, action);
        BorderBrush = ThemeManager.GetBrush(CompatchWindow.StageToken(session, action));

        var outer = new Controls.StackPanel();
        // Der Karten-Tooltip gilt nur für Kopf und Zeilen; die Knöpfe darunter haben eigene (sonst erbten sie ihn).
        var panel = new Controls.StackPanel { ToolTip = CompatchTexts.CardJumpTip, Background = System.Windows.Media.Brushes.Transparent };
        outer.Children.Add(panel);
        panel.Children.Add(new Controls.TextBlock { Text = action.DisplayPath, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
        var fileMod = CompatchMerger.FileSideOf(info) switch { MergeSide.A => item.Window.ModAName, MergeSide.B => item.Window.ModBName, _ => CompatchTexts.ColumnVanilla };
        var file = new Controls.TextBlock { Text = TreePath.FileOf(action), TextWrapping = TextWrapping.Wrap, ToolTip = CompatchTexts.CardFileTip(fileMod), FontFamily = CompatchWindow.Mono };
        file.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        panel.Children.Add(file);
        var summary = new Controls.TextBlock { Text = CompatchWindow.Summary(session, action, item.Window.ModAName, item.Window.ModBName), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) };
        summary.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        panel.Children.Add(summary);

        panel.Children.Add(Titles(item.Window.ModAName, item.Window.ModBName));
        var (rows, hidden) = action.Address.Kind == AddressKind.WholeFile
            ? (ActionText.WholeFileRows(info, decision), 0)
            : ActionText.MiniRows(action, decision, MaxMiniRows, item.Window.HideComments);
        foreach (var row in rows)
        {
            panel.Children.Add(CompatchWindow.RowGrid(row, showVanilla: false, open));
        }

        if (hidden > 0)
        {
            var more = new Controls.TextBlock { Text = CompatchTexts.MoreLines(hidden), Margin = new Thickness(4, 2, 0, 0) };
            more.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
            panel.Children.Add(more);
        }

        var buttons = new Controls.WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        buttons.Children.Add(Button(CompatchTexts.UndoLabel, CompatchTexts.CardUndoTip, () => item.Window.ChangeAction(action, (s, a) => s.Undo(a.Id))));
        buttons.Children.Add(Button(CompatchTexts.OtherSide, CompatchTexts.CardOtherSideTip, () => item.Window.ChangeAction(action, (s, a) => s.TakeOtherSide(a.Id))));
        buttons.Children.Add(Button(CompatchTexts.Confirm, CompatchTexts.CardConfirmTip, () => item.Window.ChangeAction(action, (s, a) => s.Confirm(a.Id))));
        var edit = Button(CompatchTexts.Edit, CompatchTexts.CardEditTip, () => item.Window.OpenInList(action.Id, edit: true));
        edit.IsEnabled = action.Address.Kind != AddressKind.WholeFile;
        buttons.Children.Add(edit);
        buttons.Children.Add(Button(CompatchTexts.Compare, CompatchTexts.CardCompareTip, () => item.Window.OpenCompare(action)));
        if (session.EqualPending(action.Id).Count is var equal and > 0)
        {
            buttons.Children.Add(Button(CompatchTexts.ApplyToEqual(equal), CompatchTexts.ApplyToEqualTip, () => item.Window.ChangeAction(action, (s, a) => s.ApplyToEqual(a.Id))));
        }

        var seen = SeenBox(session.IsSeen(action.Id));
        seen.Click += (_, _) => item.Window.SetActionSeen(action, seen.IsChecked == true);
        buttons.Children.Add(seen);
        outer.Children.Add(buttons);
        Child = outer;
    }

    /// <summary>
    /// Blockkarte (Nutzerwunsch "ein Eintrag je Block"): Schlüssel, Datei, Zählung, bis zu <see cref="MaxMiniRows"/> abweichende Zeilen des ganzen
    /// Blocks und Knöpfe für alle Stellen des Blocks.
    /// </summary>
    private void RebuildBlock(CompatchCardItem item, IReadOnlyList<MergeAction> block, CompatchSession session, AddressInfo info)
    {
        var open = block.Count(a => CompatchWindow.IsOpenConflict(session, a));
        var pending = block.Any(a => a.Stage is MergeStage.Conflict or MergeStage.Review or MergeStage.Outdated && !session.IsExplicit(a.Id));
        BorderBrush = ThemeManager.GetBrush(open > 0 ? "CompatchConflict" : pending ? "CompatchReview" : "CompatchVanilla");

        var outer = new Controls.StackPanel();
        var panel = new Controls.StackPanel { ToolTip = CompatchTexts.CardJumpTip, Background = System.Windows.Media.Brushes.Transparent };
        outer.Children.Add(panel);
        panel.Children.Add(new Controls.TextBlock { Text = CompatchTexts.BlockHeader(item.Action.Address.Key, block.Count), FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
        var file = new Controls.TextBlock { Text = TreePath.FileOf(item.Action), TextWrapping = TextWrapping.Wrap, FontFamily = CompatchWindow.Mono };
        file.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        panel.Children.Add(file);
        var summary = new Controls.TextBlock { Text = CompatchTexts.BlockSummary(block.Count, open), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) };
        summary.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        panel.Children.Add(summary);

        panel.Children.Add(Titles(item.Window.ModAName, item.Window.ModBName));
        session.Run.Applied.Desired.TryGetValue(item.Action.Address, out var desired);
        var differing = EntryAligner.Align(info.A.Entries, desired?.Entries ?? info.NoPatch.Entries, info.B.Entries, null, item.Window.HideComments)
            .Where(CompareNavigation.IsDifference)
            .ToList();
        foreach (var row in differing.Take(MaxMiniRows))
        {
            panel.Children.Add(CompatchWindow.RowGrid(row, showVanilla: false, open > 0));
        }

        if (differing.Count > MaxMiniRows)
        {
            var more = new Controls.TextBlock { Text = CompatchTexts.MoreLines(differing.Count - MaxMiniRows), Margin = new Thickness(4, 2, 0, 0) };
            more.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
            panel.Children.Add(more);
        }

        var buttons = new Controls.WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        buttons.Children.Add(Button(CompatchTexts.BlockTake(item.Window.ModAName), CompatchTexts.CardBlockTakeTip, () => item.Window.DecideAll(block, _ => new MergeDecision(DecisionKind.TakeA))));
        buttons.Children.Add(Button(CompatchTexts.BlockTake(item.Window.ModBName), CompatchTexts.CardBlockTakeTip, () => item.Window.DecideAll(block, _ => new MergeDecision(DecisionKind.TakeB))));
        buttons.Children.Add(Button(CompatchTexts.BlockConfirm, CompatchTexts.CardBlockConfirmTip, () => item.Window.DecideAll(block, null)));
        buttons.Children.Add(Button(CompatchTexts.Compare, CompatchTexts.CardCompareTip, () => item.Window.OpenCompare(block[0])));
        var seen = SeenBox(block.All(a => session.IsSeen(a.Id)));
        seen.Click += (_, _) => item.Window.SetSeenAll(block, seen.IsChecked == true);
        buttons.Children.Add(seen);
        outer.Children.Add(buttons);
        Child = outer;
    }

    private static Controls.CheckBox SeenBox(bool isChecked) => new()
    {
        Content = new Controls.TextBlock { Text = CompatchTexts.Seen },
        ToolTip = CompatchTexts.CardSeenTip,
        IsChecked = isChecked,
        Margin = new Thickness(6, 0, 0, 0),
        VerticalAlignment = System.Windows.VerticalAlignment.Center,
    };

    private static Controls.Grid Titles(string modA, string modB)
    {
        var grid = new Controls.Grid { Margin = new Thickness(0, 0, 0, 2) };
        CompatchWindow.AddColumnTitles(grid, modA, modB, null, FontWeights.SemiBold);
        return grid;
    }

    private static Controls.Button Button(string text, string tip, Action click)
    {
        var button = new Controls.Button { Content = new Controls.TextBlock { Text = text }, ToolTip = tip, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 6, 0), Cursor = System.Windows.Input.Cursors.Arrow };
        Controls.ToolTipService.SetShowOnDisabled(button, true);
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>Ruling 6 (Plan 3) und 9 (Plan 5): Klicks aus Knöpfen zählen nicht; einfacher Klick springt verzögert nach A, Doppelklick öffnet den Vergleich.</summary>
    private void OnCardClick(object sender, MouseButtonEventArgs e)
    {
        for (var node = e.OriginalSource as DependencyObject; node is not null && node != this; node = Parent(node))
        {
            if (node is System.Windows.Controls.Primitives.ButtonBase)
            {
                return;
            }
        }

        if (DataContext is not CompatchCardItem item)
        {
            return;
        }

        CancelJump();
        if (e.ClickCount >= 2)
        {
            item.Window.OpenCompare(item.Action);
        }
        else
        {
            // Blockkarte: zum Blockknoten im Baum (seine Id ist die Adress-Id).
            _pendingJump = item.Block is { Count: >= 2 } ? item.Action.Address.Id : item.Action.Id;
            _clickTimer.Start();
        }
    }

    /// <summary>Rechtsklick auf die Karte (nicht auf einen Knopf) öffnet den Vergleich an der Stelle (Nutzerwunsch).</summary>
    private void OnCardRightClick(object sender, MouseButtonEventArgs e)
    {
        for (var node = e.OriginalSource as DependencyObject; node is not null && node != this; node = Parent(node))
        {
            if (node is System.Windows.Controls.Primitives.ButtonBase)
            {
                return;
            }
        }

        if (DataContext is CompatchCardItem item)
        {
            CancelJump();
            item.Window.OpenCompare(item.Action);
            e.Handled = true;
        }
    }

    private void CancelJump()
    {
        _clickTimer.Stop();
        _pendingJump = null;
    }

    private static DependencyObject? Parent(DependencyObject node) =>
        node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
            ? System.Windows.Media.VisualTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);
}
