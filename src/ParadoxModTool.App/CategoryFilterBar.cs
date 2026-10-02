using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ParadoxModTool.Categories;
using ParadoxModTool.Editor;
using Border = System.Windows.Controls.Border;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Orientation = System.Windows.Controls.Orientation;
using Separator = System.Windows.Controls.Separator;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using WrapPanel = System.Windows.Controls.WrapPanel;

namespace ParadoxModTool.App;

/// <summary>
/// Kategorie-Filterleiste wie in der Hauptansicht (<c>EditorPane.RebuildCategoryFilter</c>): je Kategorie ein Häkchen mit Farbquadrat, Häkchen an = Dateien der Kategorie werden angezeigt.
/// Welche Häkchen die Leiste zeigt, steuert <see cref="FilterBarSettings"/> (Rechtsklick "Aus Leiste ausblenden", Knopf "Filter ▾"); ein ausgeblendetes Häkchen filtert nicht mehr.
/// </summary>
public sealed class CategoryFilterBar : WrapPanel
{
    private readonly HashSet<string> _unchecked = new();

    public CategoryFilterBar()
    {
        Orientation = Orientation.Horizontal;
        Loaded += (_, _) =>
        {
            FilterBarSettings.Changed -= OnSettingsChanged;
            FilterBarSettings.Changed += OnSettingsChanged;
            Rebuild();
        };
        Unloaded += (_, _) => FilterBarSettings.Changed -= OnSettingsChanged;
    }

    /// <summary>Wird ausgelöst, wenn sich ein Häkchen oder die Sichtbarkeit der Leiste ändert.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Ob eine Datei dieser Kategorie angezeigt wird.</summary>
    public bool Accepts(string categoryId) =>
        !FilterBarSettings.IsShown(FilterEntryKeys.ForCategory(categoryId)) || !_unchecked.Contains(categoryId);

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        Rebuild();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Rebuild()
    {
        _unchecked.RemoveWhere(id => !FileCategoryClassifier.Exists(id));
        Children.Clear();

        var categories = FileCategoryClassifier.Categories
            .Where(c => FilterBarSettings.IsShown(FilterEntryKeys.ForCategory(c.Id)))
            .ToList();
        if (categories.Count > 0)
        {
            var label = new TextBlock { Text = MiscWindowTexts.CategoryFilterLabel, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
            Children.Add(label);
        }

        foreach (var category in categories)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new Border
            {
                Width = 9,
                Height = 9,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = FileCategoryClassifier.ColorFor(category.Id),
            });
            content.Children.Add(new TextBlock { Text = category.Name });

            var checkBox = new CheckBox
            {
                Content = content,
                IsChecked = !_unchecked.Contains(category.Id),
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = MiscWindowTexts.CategoryFilterToggleTip(category.Name),
            };
            var id = category.Id;
            checkBox.Click += (_, _) =>
            {
                if (checkBox.IsChecked == true)
                {
                    _unchecked.Remove(id);
                }
                else
                {
                    _unchecked.Add(id);
                }

                SelectionChanged?.Invoke(this, EventArgs.Empty);
            };
            var menu = new ContextMenu();
            var hide = new MenuItem { Header = MiscWindowTexts.CategoryFilterHideFromBar };
            var key = FilterEntryKeys.ForCategory(category.Id);
            hide.Click += (_, _) => FilterBarSettings.SetShown(key, false);
            menu.Items.Add(hide);
            checkBox.ContextMenu = menu;
            Children.Add(checkBox);
        }

        var filterButton = new Button { Content = MiscWindowTexts.CategoryFilterButton, Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(6, 0, 0, 0) };
        filterButton.Click += (_, _) => ShowFilterMenu(filterButton);
        Children.Add(filterButton);
    }

    private static void ShowFilterMenu(FrameworkElement anchor)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom };
        foreach (var category in FileCategoryClassifier.Categories)
        {
            var key = FilterEntryKeys.ForCategory(category.Id);
            var item = new MenuItem
            {
                Header = category.Name.Replace("_", "__"),
                Icon = new Border { Width = 10, Height = 10, Background = FileCategoryClassifier.ColorFor(category.Id) },
                IsCheckable = true,
                IsChecked = FilterBarSettings.IsShown(key),
                StaysOpenOnClick = true,
            };
            item.Click += (_, _) => FilterBarSettings.SetShown(key, item.IsChecked);
            menu.Items.Add(item);
        }

        if (menu.Items.Count == 0)
        {
            menu.Items.Add(new Separator());
        }

        menu.IsOpen = true;
    }
}
