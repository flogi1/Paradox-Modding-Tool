using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using ParadoxModTool.FlagTracker;
using Controls = System.Windows.Controls;
using Binding = System.Windows.Data.Binding;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ParadoxModTool.App;

/// <summary>
/// Reiter "Übersicht" (Spec Ziel 3): eine Zeile je gefundenem Namen mit Art, Anzahl gesetzt/gelesen/gelöscht und Hinweis. Die Warnungen
/// kommen fertig aus der Bibliothek; eine harte Warnung ("nie gesetzt"/"nie gelesen") steht in der Warnfarbe. Doppelklick oder Enter springt
/// zu den Stellen des Namens im Reiter "Alle Stellen".
/// </summary>
internal sealed partial class FlagTrackerWindow
{
    private readonly Controls.ListView _overviewList = new() { SelectionMode = Controls.SelectionMode.Extended };

    private Controls.ListView BuildOverviewTab()
    {
        var grid = new Controls.GridView();
        grid.Columns.Add(new Controls.GridViewColumn { Header = FlagTrackerTexts.ColName, Width = 320, DisplayMemberBinding = new Binding(nameof(OverviewRow.Name)) });
        grid.Columns.Add(new Controls.GridViewColumn { Header = FlagTrackerTexts.ColKind, Width = 180, DisplayMemberBinding = new Binding(nameof(OverviewRow.KindText)) });
        grid.Columns.Add(new Controls.GridViewColumn { Header = FlagTrackerTexts.ColSet, Width = 70, DisplayMemberBinding = new Binding(nameof(OverviewRow.SetCount)) });
        grid.Columns.Add(new Controls.GridViewColumn { Header = FlagTrackerTexts.ColRead, Width = 70, DisplayMemberBinding = new Binding(nameof(OverviewRow.ReadCount)) });
        grid.Columns.Add(new Controls.GridViewColumn { Header = FlagTrackerTexts.ColRemove, Width = 70, DisplayMemberBinding = new Binding(nameof(OverviewRow.RemoveCount)) });
        grid.Columns.Add(new Controls.GridViewColumn { Header = FlagTrackerTexts.ColHint, Width = 260, CellTemplate = HintTemplate() });
        _overviewList.View = grid;
        Controls.VirtualizingPanel.SetIsVirtualizing(_overviewList, true);
        Controls.VirtualizingPanel.SetVirtualizationMode(_overviewList, Controls.VirtualizationMode.Recycling);
        _overviewList.MouseDoubleClick += OnOverviewDoubleClick;
        _overviewList.KeyDown += OnOverviewKeyDown;
        _overviewList.ContextMenu = BuildOverviewMenu();
        return _overviewList;
    }

    /// <summary>
    /// Hinweis in der Warnfarbe, wenn er eine harte Warnung ist (<see cref="OverviewRow.HintIsWarning"/>), gedämpft bei ausgeblendeten Namen
    /// (<see cref="OverviewRow.Hidden"/>), sonst in der normalen Farbe.
    /// </summary>
    private static DataTemplate HintTemplate() => (DataTemplate)XamlReader.Parse(
        "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
        "<TextBlock Text=\"{Binding Hint, Mode=OneWay}\" TextTrimming=\"CharacterEllipsis\" ToolTip=\"{Binding Hint, Mode=OneWay}\">" +
        "<TextBlock.Style><Style TargetType=\"TextBlock\"><Style.Triggers>" +
        "<DataTrigger Binding=\"{Binding HintIsWarning}\" Value=\"True\"><Setter Property=\"Foreground\" Value=\"{DynamicResource Theme.Warning}\" /></DataTrigger>" +
        "<DataTrigger Binding=\"{Binding Hidden}\" Value=\"True\"><Setter Property=\"Foreground\" Value=\"{DynamicResource Theme.TextMuted}\" /></DataTrigger>" +
        "</Style.Triggers></Style></TextBlock.Style></TextBlock></DataTemplate>");

    private void ShowOverview() => _overviewList.ItemsSource = _view.Overview;

    /// <summary>Doppelklick nur auf einer Zeile (nicht auf Spaltenkopf oder Bildlaufleiste).</summary>
    private void OnOverviewDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && Controls.ItemsControl.ContainerFromElement(_overviewList, source) is Controls.ListViewItem { Content: OverviewRow row })
        {
            ShowSitesOf(row.Name);
            e.Handled = true;
        }
    }

    private void OnOverviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _overviewList.SelectedItem is OverviewRow row)
        {
            ShowSitesOf(row.Name);
            e.Handled = true;
        }
    }

    /// <summary>Wechselt zu "Alle Stellen" und rollt die Kopfzeile des Namens nach oben (die Liste entsteht erst nach dem Reiterwechsel).</summary>
    private void ShowSitesOf(string name)
    {
        var index = _view.IndexOfName(name);
        if (index < 0)
        {
            return;
        }

        _tabs.SelectedItem = _sitesTab;
        _sitesList.SelectedIndex = index;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => ScrollSitesTo(index));
    }
}
