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
    private readonly Controls.CheckBox _showFolders = new() { Content = new Controls.TextBlock { Text = CompatchTexts.ShowFolders }, Margin = new Thickness(24, 0, 0, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
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

        // "Für alle gleichen übernehmen" ändert auch andere Blöcke; die werden mitgesichert.
        Change(_session.EqualTo(action.Id).Select(a => a.Address).Append(action.Address), session => change(session, action));
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
            PlaceSearch();
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
        // Suchfeld über dem Ordnerbaum; ist der Ordnerbereich aus, über den Karten (PlaceSearch).
        var side = new Controls.DockPanel();
        Controls.DockPanel.SetDock(_searchSlotCardTree, Controls.Dock.Top);
        side.Children.Add(_searchSlotCardTree);
        side.Children.Add(_cardTree);
        Controls.DockPanel.SetDock(side, Controls.Dock.Left);
        panel.Children.Add(side);
        Controls.DockPanel.SetDock(_searchSlotCards, Controls.Dock.Top);
        panel.Children.Add(_searchSlotCards);
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
            var items = CardItems(all.Where(InCardFilter).ToList());
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

    /// <summary>
    /// Eine Karte je Stelle, aber eine Blockkarte für alle Stellen einer Adresse, sobald es mindestens zwei sind (wie der Baum, <see cref="ActionTree"/>).
    /// Die Blockkarte steht an der Stelle der ersten.
    /// </summary>
    private List<CompatchCardItem> CardItems(IReadOnlyList<MergeAction> actions)
    {
        var blocks = actions.Where(a => a.Address.Kind != AddressKind.WholeFile)
            .GroupBy(a => a.Address)
            .Where(g => g.Count() >= 2)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<MergeAction>)g.ToList());
        var items = new List<CompatchCardItem>();
        var done = new HashSet<EntryAddress>();
        foreach (var action in actions)
        {
            if (!blocks.TryGetValue(action.Address, out var block))
            {
                items.Add(new CompatchCardItem(action, this));
            }
            else if (done.Add(action.Address))
            {
                items.Add(new CompatchCardItem(action, this, block));
            }
        }

        return items;
    }

    /// <summary>"Gesehen" für alle Stellen einer Blockkarte.</summary>
    internal void SetSeenAll(IReadOnlyList<MergeAction> actions, bool seen)
    {
        if (_session is null)
        {
            return;
        }

        foreach (var action in actions)
        {
            _session.SetSeen(action.Id, seen);
        }

        AfterChange();
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
                // Nur Ordner und Dateien: Karten filtern nach Dateipfad, Blöcke und Stellen gehören nicht in diese Leiste.
                foreach (var child in node.Children.Where(c => c.Kind is TreeNodeKind.Folder or TreeNodeKind.File))
                {
                    item.Items.Add(Build(child));
                }

                return item;
            }

            foreach (var node in ActionTree.Build(actions, a => IsOpenConflict(session, a), _ => false, presorted: true))
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
