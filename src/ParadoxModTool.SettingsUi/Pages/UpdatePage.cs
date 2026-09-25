using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>Kategorie 12: Update-Prüfung beim Start und "Jetzt suchen" (derselbe Weg wie Hilfe › Nach Updates suchen).</summary>
public sealed class UpdatePage : SettingsPage
{
    public UpdatePage(ISettingsHost host)
        : base(host)
    {
        var onStartup = AddCheckBox(SettingsTexts.UpdateOnStartup, SettingsTexts.UpdateOnStartupTip, () => Host.Settings.CheckForUpdatesOnStartup, value => Host.Settings.CheckForUpdatesOnStartup = value);
        var now = AddTaskButton(SettingsTexts.UpdateNow, null, Host.CheckForUpdatesAsync);
        if (!Host.UpdatesAvailable)
        {
            onStartup.IsEnabled = false;
            now.IsEnabled = false;
            AddHint(SettingsTexts.UpdatesUnavailable);
        }

        RefreshOnSettings();
    }
}
