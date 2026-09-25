using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>Kategorie 9: Oberflächensprache (wirkt nach Neustart).</summary>
public sealed class LanguagePage : SettingsPage
{
    public LanguagePage(ISettingsHost host)
        : base(host)
    {
        foreach (var language in Host.Languages)
        {
            AddRadio("language", language.Name, () => Host.Settings.UiLanguage == language.Code, () => Host.Settings.UiLanguage = language.Code);
        }

        AddHint(SettingsTexts.LanguageHint).Margin = new System.Windows.Thickness(0, 10, 0, 0);
        RefreshOnSettings();
    }
}
