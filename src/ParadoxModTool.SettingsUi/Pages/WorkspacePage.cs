using ParadoxModTool.Editor;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>
/// Kategorie 3: Arbeitsbereich. Split View und Verzeichnis-Übersicht spiegeln die Werkzeugleisten-Schalter; die Editor-Schalter schreiben in
/// ihre Einstellungsklassen, der GUI-Hauptschalter geht über den Host (Index aufbauen/freigeben, Fenster schließen).
/// </summary>
public sealed class WorkspacePage : SettingsPage
{
    public WorkspacePage(ISettingsHost host)
        : base(host)
    {
        var settings = Host.Settings;
        AddCheckBox(SettingsTexts.SplitView, SettingsTexts.SplitViewTip, () => settings.SplitViewEnabled, value => settings.SplitViewEnabled = value);
        AddCheckBox(SettingsTexts.ExplorerVisible, SettingsTexts.ExplorerVisibleTip, () => settings.ExplorerVisible, value => settings.ExplorerVisible = value);
        AddCheckBox(SettingsTexts.BracketHighlight, SettingsTexts.BracketHighlightTip, () => BracketSettings.HighlightEnabled, BracketSettings.SetHighlightEnabled);
        AddCheckBox(SettingsTexts.AutoComplete, SettingsTexts.AutoCompleteTip, () => ScriptHintSettings.AutoCompleteEnabled, ScriptHintSettings.SetAutoCompleteEnabled);
        AddCheckBox(SettingsTexts.CompleteCustomLocalizations, SettingsTexts.CompleteCustomLocalizationsTip, () => ScriptHintSettings.CompleteCustomLocalizations, ScriptHintSettings.SetCompleteCustomLocalizations);
        AddCheckBox(SettingsTexts.DocumentBar, SettingsTexts.DocumentBarTip, () => settings.DocumentBarVisible, value => settings.DocumentBarVisible = value);
        AddCheckBox(SettingsTexts.MultiRowTabs, SettingsTexts.MultiRowTabsTip, () => settings.MultiRowTabs, value => settings.MultiRowTabs = value);
        AddCheckBox(SettingsTexts.PathBar, SettingsTexts.PathBarTip, () => settings.PathBarVisible, value => settings.PathBarVisible = value);
        AddCheckBox(SettingsTexts.ReplaceWarningsWindow, SettingsTexts.ReplaceWarningsWindowTip, () => settings.ReplaceWarningsInWindow, value => settings.ReplaceWarningsInWindow = value);
        AddCheckBox(SettingsTexts.ScopeStatusBar, SettingsTexts.ScopeStatusBarTip, () => ScopeSettings.StatusBarEnabled, ScopeSettings.SetStatusBarEnabled);
        AddCheckBox(SettingsTexts.ScopeStatusBarTop, SettingsTexts.ScopeStatusBarTopTip, () => ScopeSettings.StatusBarTopEnabled, ScopeSettings.SetStatusBarTopEnabled);
        AddCheckBox(SettingsTexts.ScopeWarnings, SettingsTexts.ScopeWarningsTip, () => ScopeSettings.WarningsEnabled, ScopeSettings.SetWarningsEnabled);
        AddCheckBox(SettingsTexts.ScriptHover, SettingsTexts.ScriptHoverTip, () => ScriptHintSettings.HoverEnabled, ScriptHintSettings.SetHoverEnabled);
        AddCheckBox(SettingsTexts.ScopeHover, SettingsTexts.ScopeHoverTip, () => ScopeSettings.HoverEnabled, ScopeSettings.SetHoverEnabled);
        AddCheckBox(SettingsTexts.ModifierHover, SettingsTexts.ModifierHoverTip, () => ModifierValueSettings.HoverEnabled, ModifierValueSettings.SetHoverEnabled);
        AddCheckBox(SettingsTexts.ModifierNames, SettingsTexts.ModifierNamesTip, () => ModifierValueSettings.NamesEnabled, ModifierValueSettings.SetNamesEnabled);
        AddButton(SettingsTexts.ToolbarItemLabel(ToolbarItemIds.ModifierReload), MenuTexts.ToolbarTip(ToolbarItemIds.ModifierReload), ModifierValueSettings.RequestReload);
        AddCheckBox(SettingsTexts.ResultsOnTop, SettingsTexts.ResultsOnTopTip, () => settings.ResultsPanelOnTop, value => settings.ResultsPanelOnTop = value);
        AddCheckBox(GuiTexts.MenuTools, GuiTexts.MenuToolsTip, () => GuiSettings.ToolsEnabled, Host.SetGuiToolsEnabled);

        RefreshOnSettings();
        RefreshOn(handler => ScopeSettings.Changed += handler, handler => ScopeSettings.Changed -= handler);
        RefreshOn(handler => ModifierValueSettings.Changed += handler, handler => ModifierValueSettings.Changed -= handler);
        RefreshOn(handler => BracketSettings.Changed += handler, handler => BracketSettings.Changed -= handler);
        RefreshOn(handler => ScriptHintSettings.Changed += handler, handler => ScriptHintSettings.Changed -= handler);
        RefreshOn(handler => GuiSettings.Changed += handler, handler => GuiSettings.Changed -= handler);
    }
}
