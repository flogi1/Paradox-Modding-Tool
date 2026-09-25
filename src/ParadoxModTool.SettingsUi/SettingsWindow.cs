using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Session;
using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi;

/// <summary>
/// Einstellungsfenster (Code-only, nicht modal): links die 12 Kategorien, rechts die Seite, unten "Schließen". Änderungen wirken sofort.
/// Merkt sich Kategorie, Größe und Position im Einstellungs-Modell. Das Hauptfenster ruft <see cref="SaveBounds"/> zusätzlich vor dem
/// Speichern der Sitzung auf, weil ein Kindfenster erst nach dem Schließen-Ereignis des Hauptfensters schließt.
/// </summary>
public sealed class SettingsWindow : Window
{
    private const double DefaultWidth = 780;
    private const double DefaultHeight = 580;

    private readonly ISettingsHost _host;
    private readonly ListBox _categories = new() { Margin = new Thickness(8), BorderThickness = new Thickness(0) };
    private readonly ContentControl _pageHost = new();

    public SettingsWindow(ISettingsHost host)
    {
        _host = host;
        Title = SettingsTexts.WindowTitle;
        Width = DefaultWidth;
        Height = DefaultHeight;
        MinWidth = 520;
        MinHeight = 360;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        foreach (var category in SettingsCategories.All)
        {
            _categories.Items.Add(new ListBoxItem { Content = new TextBlock { Text = SettingsTexts.CategoryLabel(category) }, Tag = category, Padding = new Thickness(6, 4, 6, 4) });
        }

        _categories.SelectionChanged += (_, _) =>
        {
            if (_categories.SelectedItem is ListBoxItem { Tag: SettingsCategory category })
            {
                ShowCategory(category);
            }
        };
        grid.Children.Add(_categories);

        Grid.SetColumn(_pageHost, 1);
        grid.Children.Add(_pageHost);

        var close = new Button { Content = new TextBlock { Text = SettingsTexts.Close }, Padding = new Thickness(14, 3, 14, 3), Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true };
        close.Click += (_, _) => Close();
        Grid.SetRow(close, 1);
        Grid.SetColumnSpan(close, 2);
        grid.Children.Add(close);

        Content = grid;
        ApplySavedBounds();
        Select(SettingsCategories.Parse(host.Settings.SettingsWindowCategory));

        Closing += (_, _) => SaveBounds();
        Closed += (_, _) =>
        {
            CurrentPage?.Dispose();
            CurrentPage = null;
            _pageHost.Content = null;
        };
    }

    public SettingsCategory CurrentCategory { get; private set; }

    public SettingsPage? CurrentPage { get; private set; }

    public void Select(SettingsCategory category)
    {
        var index = SettingsCategories.All.ToList().IndexOf(category);
        if (_categories.SelectedIndex == index)
        {
            ShowCategory(category);
        }
        else
        {
            _categories.SelectedIndex = index;
        }
    }

    /// <summary>Schreibt Position und Größe (Normalzustand, auch wenn maximiert) ins Einstellungs-Modell.</summary>
    public void SaveBounds()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (bounds.IsEmpty || !double.IsFinite(bounds.Left) || !double.IsFinite(bounds.Top))
        {
            return;
        }

        _host.Settings.SettingsWindowBounds = new WindowBoundsState
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            IsMaximized = WindowState == WindowState.Maximized,
        };
    }

    private void ShowCategory(SettingsCategory category)
    {
        CurrentPage?.Dispose();
        CurrentCategory = category;
        CurrentPage = SettingsPages.Create(category, _host);
        _pageHost.Content = CurrentPage.View;
        _host.Settings.SettingsWindowCategory = category.ToString();
    }

    private void ApplySavedBounds()
    {
        var saved = _host.Settings.SettingsWindowBounds;
        if (!WindowBoundsCheck.IsUsable(saved, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight, MinWidth, MinHeight))
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = saved!.Left;
        Top = saved.Top;
        Width = saved.Width;
        Height = saved.Height;
        if (saved.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }
}
