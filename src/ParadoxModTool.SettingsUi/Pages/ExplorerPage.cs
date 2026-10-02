using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>Kategorie Verzeichnis-Übersicht: dieselben Schalter wie in der Werkzeugleiste der Übersicht bzw. am Suchfeld.</summary>
public sealed class ExplorerPage : SettingsPage
{
    public ExplorerPage(ISettingsHost host)
        : base(host)
    {
        var settings = Host.Settings;
        AddCheckBox(SettingsTexts.ExplorerButtonsLeft, SettingsTexts.ExplorerButtonsLeftTip, () => settings.ExplorerButtonsOnLeft, value => settings.ExplorerButtonsOnLeft = value);
        AddCheckBox(SettingsTexts.ExplorerBugMarks, SettingsTexts.ExplorerBugMarksTip, () => settings.ExplorerShowErrorMarks, value => settings.ExplorerShowErrorMarks = value);
        AddCheckBox(SettingsTexts.ExplorerFoldersOnly, SettingsTexts.ExplorerFoldersOnlyTip, () => settings.ExplorerSearchFoldersOnly, value => settings.ExplorerSearchFoldersOnly = value);
        AddCheckBox(SettingsTexts.ExplorerHideUgc, SettingsTexts.ExplorerHideUgcTip, () => settings.ExplorerHideUgcReferences, value => settings.ExplorerHideUgcReferences = value);

        RefreshOnSettings();
    }
}
