using System.Windows;
using System.Windows.Input;
using ParadoxModTool.Compatch;
using Controls = System.Windows.Controls;
using MergeAction = ParadoxModTool.Compatch.MergeAction;

namespace ParadoxModTool.App;

/// <summary>Ein Eintrag der Kartenliste (Ansicht C).</summary>
public sealed record CompatchCardItem(MergeAction Action, CompatchWindow Window);

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
        ToolTip = CompatchTexts.CardJumpTip;
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

        var action = item.Action;
        var decision = session.EffectiveDecision(action);
        var open = CompatchWindow.IsOpenConflict(session, action);
        BorderBrush = ThemeManager.GetBrush(CompatchWindow.StageToken(session, action));

        var panel = new Controls.StackPanel();
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
        buttons.Children.Add(Button(CompatchTexts.UndoLabel, () => item.Window.ChangeAction(action, (s, a) => s.Undo(a.Id))));
        buttons.Children.Add(Button(CompatchTexts.OtherSide, () => item.Window.ChangeAction(action, (s, a) => s.TakeOtherSide(a.Id))));
        buttons.Children.Add(Button(CompatchTexts.Confirm, () => item.Window.ChangeAction(action, (s, a) => s.Confirm(a.Id))));
        var edit = Button(CompatchTexts.Edit, () => item.Window.OpenInList(action.Id, edit: true));
        edit.IsEnabled = action.Address.Kind != AddressKind.WholeFile;
        buttons.Children.Add(edit);
        buttons.Children.Add(Button(CompatchTexts.Compare, () => item.Window.OpenCompare(action)));
        if (session.EqualPending(action.Id).Count is var equal and > 0)
        {
            var toEqual = Button(CompatchTexts.ApplyToEqual(equal), () => item.Window.ChangeAction(action, (s, a) => s.ApplyToEqual(a.Id)));
            toEqual.ToolTip = CompatchTexts.ApplyToEqualTip;
            buttons.Children.Add(toEqual);
        }

        var seen = new Controls.CheckBox { Content = CompatchTexts.Seen, IsChecked = session.IsSeen(action.Id), Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
        seen.Click += (_, _) => item.Window.SetActionSeen(action, seen.IsChecked == true);
        buttons.Children.Add(seen);
        panel.Children.Add(buttons);
        Child = panel;
    }

    private static Controls.Grid Titles(string modA, string modB)
    {
        var grid = new Controls.Grid { Margin = new Thickness(0, 0, 0, 2) };
        var titles = new[] { CompatchTexts.ColumnA(modA), CompatchTexts.ColumnResult, CompatchTexts.ColumnB(modB) };
        for (var i = 0; i < titles.Length; i++)
        {
            grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var title = new Controls.TextBlock { Text = titles[i], FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 0, 4, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            Controls.Grid.SetColumn(title, i);
            grid.Children.Add(title);
        }

        return grid;
    }

    private static Controls.Button Button(string text, Action click)
    {
        var button = new Controls.Button { Content = text, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 6, 0), Cursor = System.Windows.Input.Cursors.Arrow };
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
            _pendingJump = item.Action.Id;
            _clickTimer.Start();
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
