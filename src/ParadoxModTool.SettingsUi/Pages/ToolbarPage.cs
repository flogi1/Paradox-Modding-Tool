using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>Kategorie 2: ein Häkchen pro Eintrag der allgemeinen Werkzeugleiste, gruppiert wie die Leiste (ohne die Start-Einträge).</summary>
public sealed class ToolbarPage : SettingsPage
{
    public ToolbarPage(ISettingsHost host)
        : base(host)
    {
        AddHint(SettingsTexts.ToolbarHint);
        foreach (var group in Enum.GetValues<ToolbarGroup>().Where(group => group != ToolbarGroup.Launch))
        {
            AddHeading(SettingsTexts.ToolbarGroupLabel(group));
            foreach (var item in ToolbarCatalog.All.Where(item => item.Group == group))
            {
                AddToolbarCheckBox(item.Id);
            }
        }

        RefreshOnSettings();
    }
}
