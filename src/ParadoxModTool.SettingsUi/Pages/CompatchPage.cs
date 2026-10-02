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
        AddCheckBox(SettingsTexts.CompatchResultRight, CompatchTexts.MenuResultRightTip, () => settings.CompatchResultRight, value => settings.CompatchResultRight = value);

        // Pro Arbeitsstand gespeichert (Nutzerwunsch 2026-09-30): hier nur der Standard für neue Arbeitsstände.
        AddHeading(SettingsTexts.CompatchNewStateDefaults);
        AddHint(SettingsTexts.CompatchNewStateDefaultsHint);
        AddCheckBox(SettingsTexts.CompatchCautious, CompatchTexts.MenuCautiousTip, () => settings.CompatchCautiousWrite, value => settings.CompatchCautiousWrite = value);
        AddCheckBox(SettingsTexts.CompatchSkipLocked, CompatchTexts.MenuSkipLockedTip, () => settings.CompatchSkipLocked, value => settings.CompatchSkipLocked = value);
        AddCheckBox(CompatchTexts.MainAsBase, CompatchTexts.MainAsBaseTip, () => settings.CompatchMainAsBase, value => settings.CompatchMainAsBase = value);
        AddCheckBox(CompatchTexts.FillMissing, CompatchTexts.FillMissingTip, () => settings.CompatchFillMissing, value => settings.CompatchFillMissing = value);
        AddCheckBox(CompatchTexts.FilterHideFilled, CompatchTexts.FilterHideFilledTip, () => settings.CompatchHideFilled, value => settings.CompatchHideFilled = value);
        AddCheckBox(CompatchTexts.FilterHideReplaced, CompatchTexts.FilterHideReplacedTip, () => settings.CompatchHideReplaced, value => settings.CompatchHideReplaced = value);
        AddCheckBox(CompatchTexts.FilterHideDecided, CompatchTexts.FilterHideDecidedTip, () => settings.CompatchHideDecided, value => settings.CompatchHideDecided = value);
        AddCheckBox(CompatchTexts.FilterUncertain, CompatchTexts.FilterUncertainTip, () => settings.CompatchUncertainOnly, value => settings.CompatchUncertainOnly = value);

        AddHeading(SettingsTexts.CompatchDefaultView);
        AddHint(CompatchTexts.MenuDefaultViewTip);
        foreach (var view in new[] { CompatchView.Actions, CompatchView.Document, CompatchView.Cards })
        {
            AddRadio("compatchView", CompatchTexts.View(view), () => CompatchViews.Parse(settings.CompatchDefaultView) == view, () => settings.CompatchDefaultView = CompatchViews.ToSetting(view));
        }

        AddHeading(SettingsTexts.CompatchKindsLayout);
        AddRadio("compatchKinds", SettingsTexts.CompatchKindsList, () => !settings.CompatchKindsSideBySide, () => settings.CompatchKindsSideBySide = false);
        AddRadio("compatchKinds", SettingsTexts.CompatchKindsSideBySide, () => settings.CompatchKindsSideBySide, () => settings.CompatchKindsSideBySide = true);

        RefreshOnSettings();
    }
}
