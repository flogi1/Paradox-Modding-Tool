using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>Kategorie 11: Nachschlage-Liste der Tastenkürzel, gruppiert nach Fenster.</summary>
public sealed class HotkeysPage : SettingsPage
{
    public HotkeysPage(ISettingsHost host)
        : base(host)
    {
        AddHint(SettingsTexts.HotkeysHint);
        foreach (var group in Enum.GetValues<HotkeyGroup>())
        {
            var entries = HotkeyCatalog.Entries.Where(entry => entry.Group == group).ToList();
            if (entries.Count == 0)
            {
                continue;
            }

            AddHeading(SettingsTexts.HotkeyGroupLabel(group));
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            foreach (var entry in entries)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var row = grid.RowDefinitions.Count - 1;
                var gesture = new TextBlock { Text = SettingsTexts.Gesture(entry.Modifiers, entry.Key), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 8, 2) };
                var description = new TextBlock { Text = SettingsTexts.HotkeyDescription(entry.TextId), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
                Grid.SetRow(gesture, row);
                Grid.SetRow(description, row);
                Grid.SetColumn(description, 1);
                grid.Children.Add(gesture);
                grid.Children.Add(description);
            }

            Root.Children.Add(grid);
        }
    }
}
