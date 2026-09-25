using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>Kategorie 1: Spielpfad, Playset-Datei, Speicherorte, Neu-Lernen und die drei Start-Einträge der Werkzeugleiste.</summary>
public sealed class GeneralPage : SettingsPage
{
    public GeneralPage(ISettingsHost host)
        : base(host)
    {
        AddPathRow(SettingsTexts.GamePath, SettingsTexts.GamePathTip, () => Host.GameExecutablePath, Host.ChooseGameExecutable);
        AddPathRow(SettingsTexts.PlaysetFile, SettingsTexts.PlaysetFileTip, () => Host.PlaysetsFilePath, Host.ChoosePlaysetsFile);

        AddHeading(SettingsTexts.DataHeading);
        AddButton(SettingsTexts.StorageLocations, SettingsTexts.StorageLocationsTip, Host.OpenStorageSettings);
        AddTaskButton(SettingsTexts.RelearnLocProfiles, SettingsTexts.RelearnLocProfilesTip, Host.RelearnLocalizationProfilesAsync);
        AddTaskButton(SettingsTexts.RelearnScopeRules, SettingsTexts.RelearnScopeRulesTip, Host.RelearnScopeRulesAsync);

        AddHeading(SettingsTexts.ShowInToolbar);
        foreach (var item in ToolbarCatalog.All.Where(item => item.Group == ToolbarGroup.Launch))
        {
            AddToolbarCheckBox(item.Id);
        }

        RefreshOnSettings();
    }

    private void AddPathRow(string heading, string tip, Func<string?> path, Action choose)
    {
        AddHeading(heading);
        var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 4) };
        var button = new Button { Content = Label(SettingsTexts.Choose), ToolTip = tip, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(button, Dock.Right);
        button.Click += (_, _) =>
        {
            choose();
            Refresh();
        };
        var text = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, ToolTip = tip };
        OnRefresh(() => text.Text = path() ?? SettingsTexts.NotSet);
        row.Children.Add(button);
        row.Children.Add(text);
        Root.Children.Add(row);
    }
}
