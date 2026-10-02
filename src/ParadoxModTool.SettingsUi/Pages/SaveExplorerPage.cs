using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>Kategorie "Spielstand-Explorer": eigenes Fenster oder über PMT (Spielstand-Explorer Plan 2a, Nutzerwunsch: eigene Einstellung).</summary>
public sealed class SaveExplorerPage : SettingsPage
{
    public SaveExplorerPage(ISettingsHost host)
        : base(host)
    {
        var settings = Host.Settings;
        AddCheckBox(SettingsTexts.SaveExplorerStandalone, SettingsTexts.SaveExplorerStandaloneTip, () => settings.SaveExplorerStandalone, value => settings.SaveExplorerStandalone = value);
        AddHint(SettingsTexts.SaveExplorerPlacementHint);
        RefreshOnSettings();
    }
}
