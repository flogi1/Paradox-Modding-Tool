using ParadoxModTool.Compatch;
using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>Kategorie 6: Voreinstellungen des Komp-Patch Tools. Tooltips aus <see cref="CompatchTexts"/>.</summary>
public sealed class CompatchPage : SettingsPage
{
    public CompatchPage(ISettingsHost host)
        : base(host)
    {
        var settings = Host.Settings;
        AddCheckBox(SettingsTexts.CompatchCautious, CompatchTexts.MenuCautiousTip, () => settings.CompatchCautiousWrite, value => settings.CompatchCautiousWrite = value);
        AddCheckBox(SettingsTexts.CompatchSkipLocked, CompatchTexts.MenuSkipLockedTip, () => settings.CompatchSkipLocked, value => settings.CompatchSkipLocked = value);

        AddHeading(SettingsTexts.CompatchDefaultView);
        AddHint(CompatchTexts.MenuDefaultViewTip);
        foreach (var view in new[] { CompatchView.Actions, CompatchView.Document, CompatchView.Cards })
        {
            AddRadio("compatchView", CompatchTexts.View(view), () => CompatchViews.Parse(settings.CompatchDefaultView) == view, () => settings.CompatchDefaultView = CompatchViews.ToSetting(view));
        }

        RefreshOnSettings();
    }
}
