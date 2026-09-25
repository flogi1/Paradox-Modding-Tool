using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>Kategorie 10: Theme-Auswahl (wie das bisherige Untermenü) und Theme-Editor. Die Liste wird bei jedem Theme-Wechsel neu aufgebaut, weil der Editor Themes anlegen, umbenennen und löschen kann.</summary>
public sealed class ThemesPage : SettingsPage
{
    private readonly StackPanel _list = new();

    public ThemesPage(ISettingsHost host)
        : base(host)
    {
        Root.Children.Add(_list);
        OnRefresh(BuildList);
        AddButton(SettingsTexts.ThemeEditor, SettingsTexts.ThemeEditorTip, () =>
        {
            Host.OpenThemeEditor(Window.GetWindow(View));
            Refresh();
        });

        RefreshOn(handler => Host.ThemeChanged += handler, handler => Host.ThemeChanged -= handler);
    }

    private void BuildList()
    {
        _list.Children.Clear();
        foreach (var choice in Host.ThemeChoices())
        {
            var radio = new RadioButton
            {
                GroupName = "theme",
                Content = Label(choice.Name),
                Margin = new Thickness(0, 3, 0, 3),
                IsChecked = Host.SelectedThemeId == choice.Id,
            };
            radio.Click += (_, _) =>
            {
                Host.SelectTheme(choice.Id);
                Refresh();
            };
            _list.Children.Add(radio);
        }
    }
}
