using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>Kategorie "Fehlerprotokoll": eigenes Fenster oder über PMT (Fehlerprotokoll Plan 2b, Nutzerwunsch).</summary>
public sealed class ErrorLogPage : SettingsPage
{
    public ErrorLogPage(ISettingsHost host)
        : base(host)
    {
        var settings = Host.Settings;
        AddCheckBox(SettingsTexts.ErrorLogStandalone, SettingsTexts.ErrorLogStandaloneTip, () => settings.ErrorLogStandalone, value => settings.ErrorLogStandalone = value);
        AddHint(SettingsTexts.ErrorLogPlacementHint);
        RefreshOnSettings();
    }
}
