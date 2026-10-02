using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
// UseWPF + UseWindowsForms: mehrere nackte Typnamen wären mehrdeutig (CS0104), siehe MainWindow.xaml.cs.
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using TextChangedEventArgs = System.Windows.Controls.TextChangedEventArgs;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;
using ParadoxModTool.Categories;

namespace ParadoxModTool.App;

/// <summary>
/// Dialog "Kategorien verwalten": Liste der Dateikategorien mit Name, Farbe, Endungen und Mustern; anlegen, löschen,
/// Reihenfolge ändern, Standard wiederherstellen. Arbeitet auf Kopien; erst "OK" übergibt <see cref="Categories"/> und
/// <see cref="ThemeColors"/> an den Aufrufer.
///
/// Farben: Eingebaute Kategorien zeigen und ändern die Farbe des aktiven Themes (<see cref="ThemeColors"/>), eigene Kategorien
/// tragen ihre Farbe in der Definition und gelten in allen Themes.
/// </summary>
public partial class CategoriesWindow : Window
{
    private sealed class Row : INotifyPropertyChanged
    {
        public Row(CategoryDefinition definition, string colorHex)
        {
            Definition = definition;
            ColorHex = colorHex;
        }

        public CategoryDefinition Definition { get; }

        /// <summary>Wirksame Farbe im Dialog (Theme-Farbe bei eingebauten, Definitionsfarbe bei eigenen).</summary>
        public string ColorHex { get; set; }

        public string Name => Definition.Name;

        public Brush Brush => TryParse(ColorHex, out var c) ? new SolidColorBrush(c) : Brushes.Transparent;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private readonly IReadOnlyDictionary<string, string> _themeDefaults;
    private readonly ObservableCollection<Row> _rows = new();
    private bool _loading;

    /// <param name="categories">Aktuelle Kategorien.</param>
    /// <param name="effectiveThemeColors">Wirksame Farben der eingebauten Kategorien im aktiven Theme (Kennung auf Hex).</param>
    /// <param name="themeDefaults">Farben des Themes ohne persönliche Anpassungen, für "Standard wiederherstellen".</param>
    public CategoriesWindow(
        IReadOnlyList<CategoryDefinition> categories,
        IReadOnlyDictionary<string, string> effectiveThemeColors,
        IReadOnlyDictionary<string, string> themeDefaults)
    {
        InitializeComponent();
        _themeDefaults = themeDefaults;

        foreach (var category in categories)
        {
            var copy = category.Clone();
            _rows.Add(new Row(copy, CategoryDefaults.IsBuiltIn(copy.Id) && effectiveThemeColors.TryGetValue(copy.Id, out var themed) ? themed : copy.ColorHex));
        }

        CategoryList.ItemsSource = _rows;
        CategoryList.SelectedIndex = 0;
    }

    /// <summary>Ergebnis: die bearbeitete Liste (Fallback-Kategorie zuletzt).</summary>
    public List<CategoryDefinition> Categories { get; private set; } = new();

    /// <summary>Ergebnis: Theme-Farben der eingebauten Kategorien (Kennung auf Hex), für <c>ThemeManager.SetCategoryColors</c>.</summary>
    public Dictionary<string, string> ThemeColors { get; private set; } = new();

    private Row? Selected => CategoryList.SelectedItem as Row;

    private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _loading = true;
        try
        {
            var row = Selected;
            EditPanel.IsEnabled = row is not null;
            if (row is null)
            {
                NameBox.Text = ColorBox.Text = ExtensionsBox.Text = PatternsBox.Text = string.Empty;
                return;
            }

            var isOther = row.Definition.Id == CategoryDefaults.OtherId;
            NameBox.Text = row.Definition.Name;
            ColorBox.Text = row.ColorHex;
            ExtensionsBox.Text = string.Join(", ", row.Definition.Extensions);
            PatternsBox.Text = string.Join(Environment.NewLine, row.Definition.Patterns);
            ExtensionsBox.IsEnabled = PatternsBox.IsEnabled = !isOther;
            RulesHint.Visibility = Visibility.Visible;

            ColorHint.Text = CategoryDefaults.IsBuiltIn(row.Definition.Id)
                ? MiscWindowTexts.CategoriesColorHintBuiltIn
                : MiscWindowTexts.CategoriesColorHintCustom;
            if (isOther)
            {
                ColorHint.Text += " " + MiscWindowTexts.CategoriesColorHintOther(row.Definition.Name);
            }

            DeleteButton.IsEnabled = !isOther;
            UpButton.IsEnabled = !isOther && CategoryList.SelectedIndex > 0;
            DownButton.IsEnabled = !isOther && CategoryList.SelectedIndex < _rows.Count - 2;
            UpdateColorPreview();
        }
        finally
        {
            _loading = false;
        }
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || Selected is not { } row)
        {
            return;
        }

        row.Definition.Name = NameBox.Text;
        row.Refresh();
    }

    private void ColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateColorPreview();
        if (_loading || Selected is not { } row)
        {
            return;
        }

        if (CategoryMatcher.IsValidHex(ColorBox.Text.Trim()))
        {
            row.ColorHex = ColorBox.Text.Trim();
            row.Refresh();
        }
    }

    private void ExtensionsBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || Selected is not { } row)
        {
            return;
        }

        row.Definition.Extensions = CategoryMatcher.ParseExtensions(ExtensionsBox.Text);
    }

    private void PatternsBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || Selected is not { } row)
        {
            return;
        }

        row.Definition.Patterns = CategoryMatcher.ParsePatterns(PatternsBox.Text);
    }

    private void UpdateColorPreview()
    {
        ColorPreview.Background = TryParse(ColorBox.Text.Trim(), out var color) ? new SolidColorBrush(color) : Brushes.Transparent;
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var definition = new CategoryDefinition { Id = CategoryMatcher.NewCustomId(), Name = MiscWindowTexts.CategoriesNewName, ColorHex = "#E06C75" };
        var index = Math.Max(0, _rows.Count - 1);
        _rows.Insert(index, new Row(definition, definition.ColorHex));
        CategoryList.SelectedIndex = index;
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row || row.Definition.Id == CategoryDefaults.OtherId)
        {
            return;
        }

        var index = CategoryList.SelectedIndex;
        _rows.Remove(row);
        CategoryList.SelectedIndex = Math.Min(index, _rows.Count - 1);
    }

    private void Up_Click(object sender, RoutedEventArgs e) => Move(-1);

    private void Down_Click(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int delta)
    {
        var index = CategoryList.SelectedIndex;
        var target = index + delta;
        if (Selected is not { } row || row.Definition.Id == CategoryDefaults.OtherId || target < 0 || target > _rows.Count - 2)
        {
            return;
        }

        _rows.Move(index, target);
        CategoryList.SelectedIndex = target;
    }

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        _rows.Clear();
        foreach (var definition in CategoryDefaults.Create())
        {
            var color = _themeDefaults.TryGetValue(definition.Id, out var themed) ? themed : definition.ColorHex;
            _rows.Add(new Row(definition, color));
        }

        CategoryList.SelectedIndex = 0;
    }

    private void PickColor_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
        if (TryParse(ColorBox.Text.Trim(), out var current))
        {
            dialog.Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B);
        }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ColorBox.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows)
        {
            if (string.IsNullOrWhiteSpace(row.Definition.Name))
            {
                MessageBox.Show(MiscWindowTexts.CategoriesNameRequired, MiscWindowTexts.CategoriesMessageTitle);
                CategoryList.SelectedItem = row;
                NameBox.Focus();
                return;
            }

            if (!CategoryMatcher.IsValidHex(row.ColorHex))
            {
                MessageBox.Show(MiscWindowTexts.CategoriesInvalidColor(row.Definition.Name, row.ColorHex), MiscWindowTexts.CategoriesInvalidColorTitle);
                CategoryList.SelectedItem = row;
                ColorBox.Focus();
                return;
            }
        }

        var names = _rows.GroupBy(r => r.Definition.Name.Trim(), StringComparer.CurrentCultureIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (names is not null)
        {
            MessageBox.Show(MiscWindowTexts.CategoriesDuplicateName(names.Key), MiscWindowTexts.CategoriesMessageTitle);
            CategoryList.SelectedItem = names.First();
            NameBox.Focus();
            return;
        }

        Categories = new List<CategoryDefinition>();
        ThemeColors = new Dictionary<string, string>();
        foreach (var row in _rows)
        {
            var definition = row.Definition.Clone();
            if (CategoryDefaults.IsBuiltIn(definition.Id))
            {
                ThemeColors[definition.Id] = row.ColorHex;
            }
            else
            {
                definition.ColorHex = row.ColorHex;
            }

            Categories.Add(definition);
        }

        DialogResult = true;
    }

    private static bool TryParse(string hex, out Color color)
    {
        color = default;
        if (!CategoryMatcher.IsValidHex(hex))
        {
            return false;
        }

        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex)!;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
