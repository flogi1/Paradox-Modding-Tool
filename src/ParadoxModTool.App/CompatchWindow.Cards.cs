using System.Windows;
using ParadoxModTool.Compatch;
using Controls = System.Windows.Controls;
using MergeAction = ParadoxModTool.Compatch.MergeAction;

namespace ParadoxModTool.App;

/// <summary>Ansicht C: Review-Karten mit Fortschritt "gesehen" (Spec, "Ansicht C").</summary>
public sealed partial class CompatchWindow
{
    private readonly Controls.ListBox _cardList = new() { BorderThickness = new Thickness(0) };
    private readonly Controls.ProgressBar _seenBar = new() { Width = 240, Height = 12, Margin = new Thickness(0, 0, 8, 0) };
    private readonly Controls.TextBlock _seenText = new() { VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.TextBlock _cardsEmpty = new() { Text = CompatchTexts.NoActions, Margin = new Thickness(4, 8, 0, 0), Visibility = Visibility.Collapsed };
    private readonly HashSet<CompatchCardView> _liveCards = new();
    private readonly Controls.CheckBox _showFolders = new() { Content = CompatchTexts.ShowFolders, Margin = new Thickness(24, 0, 0, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.TreeView _cardTree = new() { Width = 260, Margin = new Thickness(0, 0, 6, 0) };
    private string? _cardFilter;
    private bool _cardTreeSyncing;

    internal CompatchSession? Session => _session;

    internal string ModAName => _state.ModAName;

    internal string ModBName => _state.ModBName;

    /// <summary>Entscheidung aus Karte oder Ansicht B. Aktualisiert Baum, Zähler und die sichtbaren Karten (Ruling 4).</summary>
    internal void ChangeAction(MergeAction action, Action<CompatchSession, MergeAction> change)
    {
        if (_session is null)
        {
            return;
        }

        change(_session, action);
        AfterChange();
    }

    internal void SetActionSeen(MergeAction action, bool seen)
    {
        if (_session is null)
        {
            return;
        }

        _session.SetSeen(action.Id, seen);
        AfterChange();
    }

    internal void OpenInList(string id, bool edit) => JumpToAction(id, edit);

    internal void RegisterCard(CompatchCardView card, bool live)
    {
        if (live)
        {
            _liveCards.Add(card);
        }
        else
        {
            _liveCards.Remove(card);
        }
    }

    partial void InitializeCards()
    {
        var panel = new Controls.DockPanel();
        var top = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        Controls.DockPanel.SetDock(top, Controls.Dock.Top);
        top.Children.Add(_seenBar);
        top.Children.Add(_seenText);
        var legend = CompatchLegend.Build(_state.ModAName, _state.ModBName);
        legend.Margin = new Thickness(24, 0, 0, 0);
        top.Children.Add(legend);
        top.Children.Add(_showFolders);
        _showFolders.IsChecked = _host.Defaults.CardsFolderPane;
        _cardTree.Visibility = _showFolders.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        _showFolders.Click += (_, _) =>
        {
            _cardTree.Visibility = _showFolders.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            _host.RememberCardsFolderPane(_showFolders.IsChecked == true);
            if (_showFolders.IsChecked != true && _cardFilter is not null)
            {
                _cardFilter = null;
                RefreshCards(rebuild: true);
            }
        };
        _cardTree.SelectedItemChanged += (_, _) =>
        {
            // Gleicher Filter: nichts tun (sonst Schleife über FillCardTree).
            if (!_cardTreeSyncing && _cardTree.SelectedItem is Controls.TreeViewItem item && !string.Equals(item.Tag as string, _cardFilter, StringComparison.OrdinalIgnoreCase))
            {
                _cardFilter = item.Tag as string;
                RefreshCards(rebuild: true);
            }
        };
        panel.Children.Add(top);
        Controls.DockPanel.SetDock(_cardsEmpty, Controls.Dock.Top);
        panel.Children.Add(_cardsEmpty);

        _cardList.ItemTemplate = new DataTemplate { VisualTree = new FrameworkElementFactory(typeof(CompatchCardView)) };
        var itemStyle = new Style(typeof(Controls.ListBoxItem));
        itemStyle.Setters.Add(new Setter(Controls.Control.HorizontalContentAlignmentProperty, System.Windows.HorizontalAlignment.Stretch));
        itemStyle.Setters.Add(new Setter(UIElement.FocusableProperty, false));
        _cardList.ItemContainerStyle = itemStyle;
        Controls.ScrollViewer.SetHorizontalScrollBarVisibility(_cardList, Controls.ScrollBarVisibility.Disabled);
        Controls.VirtualizingPanel.SetIsVirtualizing(_cardList, true);
        Controls.VirtualizingPanel.SetVirtualizationMode(_cardList, Controls.VirtualizationMode.Recycling);
        Controls.VirtualizingPanel.SetScrollUnit(_cardList, Controls.ScrollUnit.Pixel);
        Controls.DockPanel.SetDock(_cardTree, Controls.Dock.Left);
        panel.Children.Add(_cardTree);
        panel.Children.Add(_cardList);
        _cardsHost.Child = panel;
    }

    /// <summary>Ruling 4: neu aufbauen nur bei Lauf, Filter, "Alles neu prüfen" und Einblenden; sonst die sichtbaren Karten neu zeichnen.</summary>
    partial void RefreshCards(bool rebuild)
    {
        if (_session is null)
        {
            _cardList.ItemsSource = null;
            _liveCards.Clear();
            RefreshSeenBar();
            return;
        }

        if (rebuild || _cardList.ItemsSource is null)
        {
            _liveCards.Clear();
            var all = FilteredActions();
            FillCardTree(all);
            var items = all.Where(InCardFilter).Select(a => new CompatchCardItem(a, this)).ToList();
            _cardList.ItemsSource = items;
            _cardsEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            foreach (var card in _liveCards.ToList())
            {
                card.Rebuild();
            }
        }

        RefreshSeenBar();
    }

    /// <summary>Plan 5, Ruling 10: Datei = gleicher Pfad, Ordner = Präfix; kein Filter = alle.</summary>
    private bool InCardFilter(MergeAction action)
    {
        if (_cardFilter is null)
        {
            return true;
        }

        var file = TreePath.FileOf(action);
        return string.Equals(file, _cardFilter, StringComparison.OrdinalIgnoreCase) || file.StartsWith(_cardFilter + "/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ordnerbaum der Karten. Der aktive Filter wird fett markiert statt über IsSelected (das löst beim Neuaufbau SelectedItemChanged aus).</summary>
    private void FillCardTree(IReadOnlyList<MergeAction> actions)
    {
        _cardTreeSyncing = true;
        try
        {
            _cardTree.Items.Clear();
            var session = _session!;
            _cardTree.Items.Add(new Controls.TreeViewItem { Header = $"{CompatchTexts.AllFiles} ({actions.Count})", Tag = null, FontWeight = _cardFilter is null ? FontWeights.Bold : FontWeights.Normal });
            Controls.TreeViewItem Build(ActionTreeNode node)
            {
                var active = string.Equals(node.Path, _cardFilter, StringComparison.OrdinalIgnoreCase);
                var item = new Controls.TreeViewItem { Header = CompatchTreeBuilder.Header(node), Tag = node.Path, IsExpanded = true, FontWeight = active ? FontWeights.Bold : FontWeights.Normal };
                foreach (var child in node.Children.Where(c => c.Kind != TreeNodeKind.Action))
                {
                    item.Items.Add(Build(child));
                }

                return item;
            }

            foreach (var node in ActionTree.Build(actions, a => IsOpenConflict(session, a), _ => false))
            {
                _cardTree.Items.Add(Build(node));
            }
        }
        finally
        {
            _cardTreeSyncing = false;
        }
    }

    private void RefreshSeenBar()
    {
        var total = _session?.Actions.Count ?? 0;
        var seen = _session?.SeenCount ?? 0;
        _seenBar.Maximum = Math.Max(1, total);
        _seenBar.Value = seen;
        _seenText.Text = CompatchTexts.SeenProgress(seen, total);
    }
}
