using System.Windows;
using ParadoxModTool.ModOverview;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Menüleiste der Übersicht "Meine Mods". "Einstellungen" › "Hinweise": je Hinweisart ein Haken (mit Anzahl) zum Ein- und Ausblenden; ausgeblendete
/// Arten zählen auch bei "Nur mit Warnungen" nicht. Weitere Einstellungen der Übersicht kommen später als weitere Punkte unter "Einstellungen".
/// </summary>
internal sealed partial class MyModsWindow
{
    private IReadOnlyDictionary<string, int> _alertCounts = new Dictionary<string, int>();

    private Controls.Menu BuildMenu()
    {
        var alerts = new Controls.MenuItem { Header = OverviewTexts.MenuAlerts, ToolTip = OverviewTexts.AlertFilterTip };
        alerts.Items.Add(new Controls.MenuItem());
        alerts.SubmenuOpened += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, alerts))
            {
                FillAlertMenu(alerts);
            }
        };

        var settings = new Controls.MenuItem { Header = OverviewTexts.MenuSettings };
        settings.Items.Add(alerts);
        settings.Items.Add(BuildStartMenu());
        var menu = new Controls.Menu();
        menu.Items.Add(settings);
        return menu;
    }

    /// <summary>"Beim Öffnen" › "Vollbild" / "Fenster": wie das Fenster beim nächsten Öffnen startet (Standard: Vollbild).</summary>
    private Controls.MenuItem BuildStartMenu()
    {
        var start = new Controls.MenuItem { Header = OverviewTexts.MenuStart };
        var maximized = new Controls.MenuItem { Header = OverviewTexts.MenuStartMaximized, IsCheckable = true };
        var windowed = new Controls.MenuItem { Header = OverviewTexts.MenuStartWindowed, IsCheckable = true };

        void Sync()
        {
            maximized.IsChecked = _host.Settings.MyModsStartMaximized;
            windowed.IsChecked = !_host.Settings.MyModsStartMaximized;
        }

        maximized.Click += (_, _) =>
        {
            _host.Settings.MyModsStartMaximized = true;
            Sync();
        };
        windowed.Click += (_, _) =>
        {
            _host.Settings.MyModsStartMaximized = false;
            Sync();
        };
        start.SubmenuOpened += (_, _) => Sync();
        start.Items.Add(maximized);
        start.Items.Add(windowed);
        Sync();
        return start;
    }

    /// <summary>Baut die Haken beim Öffnen neu, damit die Anzahlen aktuell sind. Das Menü bleibt nach einem Klick offen.</summary>
    private void FillAlertMenu(Controls.MenuItem alerts)
    {
        alerts.Items.Clear();
        var hidden = _host.Settings.MyModsHiddenAlertKinds.ToHashSet(StringComparer.Ordinal);
        foreach (var category in OverviewAlertFilter.Categories)
        {
            var count = _alertCounts.TryGetValue(category, out var value) ? value : 0;
            var item = new Controls.MenuItem
            {
                Header = OverviewTexts.AlertFilterItem(OverviewTexts.AlertCategory(category), count),
                IsCheckable = true,
                IsChecked = !hidden.Contains(category),
                StaysOpenOnClick = true,
            };
            item.Click += (_, _) =>
            {
                var kinds = _host.Settings.MyModsHiddenAlertKinds.Where(k => k != category).ToList();
                if (!item.IsChecked)
                {
                    kinds.Add(category);
                }

                _host.Settings.MyModsHiddenAlertKinds = kinds;
                Rebuild();
            };
            alerts.Items.Add(item);
        }
    }
}
