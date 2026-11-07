using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ParadoxModTool.FlagTracker;
using Controls = System.Windows.Controls;
using Binding = System.Windows.Data.Binding;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ParadoxModTool.App;

/// <summary>
/// Reiter "Alle Stellen" (Spec Ziel 3): flache, virtualisierte Liste aus <see cref="FlagTrackerView.Sites"/> ohne CollectionView-Gruppierung.
/// Kopfzeilen (je Name und "über Muster") sind fett und öffnen nichts. Die erste Spalte zeigt <see cref="SiteRow.Text"/>: den Kopf bzw. bei
/// Musterstellen den Musternamen. Doppelklick oder Enter öffnet die Fundstelle (bei aufgelösten Stellen die Aufrufzeile); der Link in der
/// Spalte "Aufruf" öffnet die Zeile im Effekt (Spec "Festlegungen" 5).
/// </summary>
internal sealed partial class FlagTrackerWindow
{
    private readonly Controls.ListView _sitesList = new() { SelectionMode = Controls.SelectionMode.Single };

    private Controls.ListView BuildSitesTab()
    {
        var grid = new Controls.GridView();
        grid.Columns.Add(new Controls.GridViewColumn { Header = FlagTrackerTexts.ColName, Width = 280, DisplayMemberBinding = new Binding(nameof(SiteRow.Text)) });
        grid.Columns.Add(new Controls.GridViewColumn { Header = FlagTrackerTexts.ColOrigin, Width = 100, DisplayMemberBinding = new Binding(nameof(SiteRow.OriginText)) });
        grid.Columns.Add(new Controls.GridViewColumn { Header = FlagTrackerTexts.ColRole, Width = 80, DisplayMemberBinding = new Binding(nameof(SiteRow.RoleText)) });
        grid.Columns.Add(new Controls.GridViewColumn { Header = FlagTrackerTexts.ColKind, Width = 150, DisplayMemberBinding = new Binding(nameof(SiteRow.KindText)) });
        grid.Columns.Add(new Controls.GridViewColumn { Header = FlagTrackerTexts.ColFile, Width = 380, DisplayMemberBinding = new Binding(nameof(SiteRow.FileText)) });
        grid.Columns.Add(new Controls.GridViewColumn { Header = FlagTrackerTexts.ColVia, Width = 320, CellTemplate = ViaTemplate() });
        _sitesList.View = grid;

        var rowStyle = new Style(typeof(Controls.ListViewItem), TryFindResource(typeof(Controls.ListViewItem)) as Style);
        foreach (var headerKind in new[] { SiteRowKind.NameHeader, SiteRowKind.PatternHeader })
        {
            var bold = new DataTrigger { Binding = new Binding(nameof(SiteRow.Kind)), Value = headerKind };
            bold.Setters.Add(new Setter(Controls.Control.FontWeightProperty, FontWeights.Bold));
            rowStyle.Triggers.Add(bold);
        }

        _sitesList.ItemContainerStyle = rowStyle;
        Controls.VirtualizingPanel.SetIsVirtualizing(_sitesList, true);
        Controls.VirtualizingPanel.SetVirtualizationMode(_sitesList, Controls.VirtualizationMode.Recycling);
        _sitesList.AddHandler(Hyperlink.ClickEvent, new RoutedEventHandler(OnViaClick));
        _sitesList.MouseDoubleClick += OnSitesDoubleClick;
        _sitesList.KeyDown += OnSitesKeyDown;
        return _sitesList;
    }

    /// <summary>"Aufruf": <see cref="SiteRow.ViaText"/> als Link; ohne Via (und in Kopfzeilen) bleibt die Zelle leer.</summary>
    private static DataTemplate ViaTemplate() => (DataTemplate)XamlReader.Parse(
        "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" +
        "<TextBlock TextTrimming=\"CharacterEllipsis\"><TextBlock.Style><Style TargetType=\"TextBlock\"><Style.Triggers>" +
        "<DataTrigger Binding=\"{Binding ViaText}\" Value=\"{x:Null}\"><Setter Property=\"Visibility\" Value=\"Collapsed\" /></DataTrigger>" +
        "</Style.Triggers></Style></TextBlock.Style>" +
        "<Hyperlink><Run Text=\"{Binding ViaText, Mode=OneWay}\" /></Hyperlink></TextBlock></DataTemplate>");

    private void ShowSites() => _sitesList.ItemsSource = _view.Sites;

    /// <summary>Rollt die Zeile <paramref name="index"/> möglichst an den oberen Rand (erst ans Ende, dann zurück) und gibt ihr den Fokus.</summary>
    private void ScrollSitesTo(int index)
    {
        if (index < 0 || index >= _sitesList.Items.Count)
        {
            return;
        }

        _sitesList.ScrollIntoView(_sitesList.Items[_sitesList.Items.Count - 1]);
        _sitesList.UpdateLayout();
        _sitesList.ScrollIntoView(_sitesList.Items[index]);
        _sitesList.UpdateLayout();
        (_sitesList.ItemContainerGenerator.ContainerFromIndex(index) as Controls.ListViewItem)?.Focus();
    }

    /// <summary>Link "Aufruf": öffnet die Zeile im Scripted Effect bzw. Trigger.</summary>
    private void OnViaClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Hyperlink { DataContext: SiteRow { Site.Via: { } via } })
        {
            OpenLocation(via);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Doppelklick nur auf einer Stellenzeile (nicht auf Kopfzeile, Spaltenkopf oder Bildlaufleiste). Ein Doppelklick auf den Link öffnet nur
    /// den Link (über <see cref="OnViaClick"/>), nicht zusätzlich die Fundstelle.
    /// </summary>
    private void OnSitesDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RowAt(e.OriginalSource as DependencyObject, out var onLink) is { Site: { } site } && !onLink)
        {
            OpenLocation(site.Location);
            e.Handled = true;
        }
    }

    private void OnSitesKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _sitesList.SelectedItem is SiteRow { Site: { } site })
        {
            OpenLocation(site.Location);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Zeile unter <paramref name="source"/>; <paramref name="onLink"/> ist true, wenn dazwischen ein Link liegt. Läuft über logische Eltern,
    /// wo die Quelle ein Textelement ist (z. B. der <see cref="Run"/> im Link), sonst über den visuellen Baum.
    /// </summary>
    private static SiteRow? RowAt(DependencyObject? source, out bool onLink)
    {
        onLink = false;
        for (var current = source; current is not null;)
        {
            if (current is Hyperlink)
            {
                onLink = true;
            }

            if (current is Controls.ListViewItem item)
            {
                return item.Content as SiteRow;
            }

            current = current is Visual or Visual3D ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
