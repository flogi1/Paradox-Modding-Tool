using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ParadoxModTool.Settings;
using ParadoxModTool.Theming;
// WPF + Windows Forms: mehrdeutige Typnamen (CS0104) explizit auflösen.
using Border = System.Windows.Controls.Border;
using Button = System.Windows.Controls.Button;
using Grid = System.Windows.Controls.Grid;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using TextChangedEventArgs = System.Windows.Controls.TextChangedEventArgs;
using ColumnDefinition = System.Windows.Controls.ColumnDefinition;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Brushes = System.Windows.Media.Brushes;

namespace ParadoxModTool.App;

/// <summary>
/// Theme-Editor (Paket 12b). Arbeitet auf Arbeitskopien: eigene Themes werden erst mit "OK" gespeichert, die Live-Vorschau
/// läuft über <see cref="ThemeManager.Preview"/>. "Abbrechen"/Schließen stellt die tatsächliche Auswahl wieder her.
/// </summary>
public partial class ThemeEditorWindow : Window
{
    private sealed record ThemeEntry(ThemeDefinition Theme)
    {
        public string Display => Theme.IsBuiltIn ? $"{Theme.Name} (eingebaut)" : Theme.Name;
    }

    private sealed record TokenRow(Border Swatch, TextBox Box);

    private readonly List<ThemeEntry> _entries = new();
    private readonly HashSet<string> _dirty = new();
    private readonly Dictionary<string, TokenRow> _rows = new();
    private bool _loading;
    private bool _committed;

    public ThemeEditorWindow()
    {
        InitializeComponent();
        BuildRows();

        _entries.Add(new ThemeEntry(BuiltInThemes.CreateLight()));
        _entries.Add(new ThemeEntry(BuiltInThemes.CreateDark()));
        foreach (var custom in ThemeManager.CustomThemes)
        {
            _entries.Add(new ThemeEntry(custom.Clone()));
        }

        RefreshList(ThemeManager.Current.Id);
    }

    private ThemeDefinition? Selected => (ThemeList.SelectedItem as ThemeEntry)?.Theme;

    private void RefreshList(string selectId)
    {
        _loading = true;
        ThemeList.ItemsSource = null;
        ThemeList.ItemsSource = _entries;
        _loading = false;
        ThemeList.SelectedItem = _entries.FirstOrDefault(e => e.Theme.Id == selectId) ?? _entries[0];
    }

    private void BuildRows()
    {
        foreach (var group in ThemeTokens.Groups)
        {
            RowsPanel.Children.Add(new TextBlock
            {
                Text = group,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Margin = new Thickness(0, 10, 0, 4),
            });

            foreach (var token in ThemeTokens.All.Where(t => t.Group == group))
            {
                var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var label = new TextBlock { Text = token.Label, VerticalAlignment = VerticalAlignment.Center };
                var swatch = new Border
                {
                    Width = 34,
                    Height = 20,
                    Margin = new Thickness(8, 0, 6, 0),
                    BorderThickness = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = "Farbe wählen",
                };
                swatch.SetResourceReference(Border.BorderBrushProperty, "Theme.ControlBorder");
                var box = new TextBox { Width = 92, VerticalContentAlignment = VerticalAlignment.Center, FontFamily = new System.Windows.Media.FontFamily("Consolas") };

                Grid.SetColumn(swatch, 1);
                Grid.SetColumn(box, 2);
                row.Children.Add(label);
                row.Children.Add(swatch);
                row.Children.Add(box);
                RowsPanel.Children.Add(row);

                var key = token.Key;
                box.TextChanged += (_, _) => Row_Changed(key);
                swatch.MouseLeftButtonUp += (_, _) => PickColor(key);
                _rows[key] = new TokenRow(swatch, box);
            }
        }
    }

    private void ThemeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Selected is not { } theme)
        {
            return;
        }

        _loading = true;
        try
        {
            NameBox.Text = theme.Name;
            DarkCheck.IsChecked = theme.IsDark;
            foreach (var token in ThemeTokens.All)
            {
                _rows[token.Key].Box.Text = theme.Colors[token.Key];
                UpdateSwatch(token.Key, theme.Colors[token.Key]);
            }
        }
        finally
        {
            _loading = false;
        }

        var editable = !theme.IsBuiltIn;
        NameBox.IsReadOnly = !editable;
        DarkCheck.IsEnabled = editable;
        DeleteButton.IsEnabled = editable;
        InfoText.Visibility = editable ? Visibility.Collapsed : Visibility.Visible;
        foreach (var row in _rows.Values)
        {
            row.Box.IsReadOnly = !editable;
            row.Swatch.Opacity = editable ? 1 : 0.6;
        }

        ThemeManager.Preview(theme);
    }

    private void Row_Changed(string key)
    {
        if (_loading || Selected is not { IsBuiltIn: false } theme)
        {
            return;
        }

        var box = _rows[key].Box;
        if (!HexColor.IsValid(box.Text.Trim()))
        {
            box.SetResourceReference(System.Windows.Controls.Control.BorderBrushProperty, "Theme.Danger");
            return;
        }

        box.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
        theme.Colors[key] = box.Text.Trim();
        _dirty.Add(theme.Id);
        UpdateSwatch(key, theme.Colors[key]);
        ThemeManager.Preview(theme);
    }

    private void UpdateSwatch(string key, string hex)
    {
        try
        {
            _rows[key].Swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch (FormatException)
        {
            _rows[key].Swatch.Background = Brushes.Transparent;
        }
    }

    /// <summary>
    /// Farbwahl für ein Farbfeld. Bei einem eingebauten (schreibgeschützten) Theme fragt PMT, ob eine eigene Kopie angelegt werden soll,
    /// wählt sie aus und öffnet die Farbwahl dort - statt den Klick still zu ignorieren.
    /// </summary>
    private void PickColor(string key)
    {
        if (Selected is not { } selected)
        {
            return;
        }

        if (selected.IsBuiltIn)
        {
            var answer = MessageBox.Show(this, SettingsTexts.ThemeReadOnlyCopyPrompt(selected.Name), SettingsTexts.ThemeReadOnlyTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            CreateCopyOf(selected);
        }

        if (Selected is not { IsBuiltIn: false } theme)
        {
            return;
        }

        var current = theme.Colors[key];
        var alpha = current.Length == 9 ? current[1..3] : null;
        var rgb = current.Length == 9 ? current[3..] : current[1..];

        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            Color = System.Drawing.ColorTranslator.FromHtml("#" + rgb),
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var picked = $"{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        _rows[key].Box.Text = alpha is null ? "#" + picked : "#" + alpha + picked;
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || Selected is not { IsBuiltIn: false } theme)
        {
            return;
        }

        theme.Name = NameBox.Text;
        _dirty.Add(theme.Id);
    }

    private void DarkCheck_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { IsBuiltIn: false } theme)
        {
            return;
        }

        theme.IsDark = DarkCheck.IsChecked == true;
        _dirty.Add(theme.Id);
        ThemeManager.Preview(theme);
    }

    private void NewCopy_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } source)
        {
            CreateCopyOf(source);
        }
    }

    /// <summary>Legt eine eigene Kopie des Themes an, speichert sie und wählt sie in der Liste aus.</summary>
    private void CreateCopyOf(ThemeDefinition source)
    {
        var copy = ThemeManager.CreateCustomCopy(source, source.Name + " (Kopie)");
        ThemeManager.SaveCustom(copy);
        _entries.Add(new ThemeEntry(copy));
        RefreshList(copy.Id);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { IsBuiltIn: false } theme)
        {
            return;
        }

        if (MessageBox.Show(this, $"Das Theme „{theme.Name}“ wirklich löschen?", "Theme löschen", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        ThemeManager.DeleteCustom(theme.Id);
        if (ThemeManager.SelectionId == theme.Id)
        {
            ThemeManager.SelectTheme(ThemeSelection.SystemId);
        }

        _dirty.Remove(theme.Id);
        _entries.RemoveAll(entry => entry.Theme.Id == theme.Id);
        RefreshList(ThemeDefinition.LightId);
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Theme-Datei (*.json)|*.json|Alle Dateien (*.*)|*.*", Title = "Theme importieren" };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var imported = ThemeStore.Import(dialog.FileName);
        if (imported is null)
        {
            MessageBox.Show(this, "Die Datei ist kein gültiges Theme.", "Import nicht möglich", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ThemeManager.SaveCustom(imported);
        _entries.Add(new ThemeEntry(imported));
        RefreshList(imported.Id);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } theme)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Theme-Datei (*.json)|*.json",
            Title = "Theme exportieren",
            FileName = new string(theme.Name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()) + ".json",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var export = theme.Clone();
        if (theme.Id == ThemeManager.Current.Id)
        {
            // Das Theme, dessen Kategorie-Farben gerade wirksam sind: inklusive persönlicher Anpassungen exportieren.
            export.CategoryColors = new Dictionary<string, string>(ThemeManager.EffectiveCategoryColors);
        }

        try
        {
            ThemeStore.Export(export, dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Die Datei konnte nicht geschrieben werden: {ex.Message}", "Fehler");
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } chosen)
        {
            return;
        }

        foreach (var entry in _entries.Where(x => _dirty.Contains(x.Theme.Id)))
        {
            ThemeManager.SaveCustom(entry.Theme);
        }

        ThemeManager.SelectTheme(chosen.Id);
        _committed = true;
        DialogResult = true;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_committed)
        {
            // Vorschau verwerfen: tatsächliche Auswahl wieder anwenden.
            ThemeManager.Reapply();
        }
    }
}
