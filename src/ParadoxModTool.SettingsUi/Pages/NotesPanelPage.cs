using ParadoxModTool.Editor;
using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>Kategorie 5: Notiz/Lesezeichen-Panel.</summary>
public sealed class NotesPanelPage : SettingsPage
{
    public NotesPanelPage(ISettingsHost host)
        : base(host)
    {
        var settings = Host.Settings;
        AddCheckBox(SettingsTexts.NotesPanelVisible, SettingsTexts.NotesPanelVisibleTip, () => settings.NotesPanelVisible, value => settings.NotesPanelVisible = value);
        AddCheckBox(SettingsTexts.NotesPanelKeepOpen, SettingsTexts.NotesPanelKeepOpenTip, () => settings.NotesPanelAlwaysVisible, value => settings.NotesPanelAlwaysVisible = value);
        AddCheckBox(SettingsTexts.NotesPanelScope, SettingsTexts.NotesPanelScopeTip, () => ScopeSettings.PanelEnabled, ScopeSettings.SetPanelEnabled);

        RefreshOnSettings();
        RefreshOn(handler => ScopeSettings.Changed += handler, handler => ScopeSettings.Changed -= handler);
    }
}
