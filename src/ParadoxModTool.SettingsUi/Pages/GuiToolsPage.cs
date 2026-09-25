using System.Windows;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>Kategorie 7: GUI-Werkzeuge. Ist der Hauptschalter aus, sind die übrigen Einträge ausgegraut (ihr Wert bleibt erhalten).</summary>
public sealed class GuiToolsPage : SettingsPage
{
    private readonly List<UIElement> _dependent = new();

    public GuiToolsPage(ISettingsHost host)
        : base(host)
    {
        AddCheckBox(GuiTexts.MenuTools, GuiTexts.MenuToolsTip, () => GuiSettings.ToolsEnabled, Host.SetGuiToolsEnabled);
        _dependent.Add(AddCheckBox(GuiTexts.MenuDesigner, GuiTexts.MenuDesignerTip, () => GuiSettings.DesignerEnabled, GuiSettings.SetDesignerEnabled));
        _dependent.Add(AddCheckBox(GuiTexts.MenuBreadcrumb, GuiTexts.MenuBreadcrumbTip, () => GuiSettings.BreadcrumbEnabled, GuiSettings.SetBreadcrumbEnabled));
        _dependent.Add(AddCheckBox(GuiTexts.MenuShownIn, GuiTexts.MenuShownInTip, () => GuiSettings.ShownInEnabled, GuiSettings.SetShownInEnabled));
        _dependent.Add(AddTaskButton(GuiTexts.MenuRebuild, GuiTexts.MenuRebuildTip, Host.RebuildGuiIndexAsync));
        var status = AddHint(string.Empty);

        // Spiel-Anbindung (Plan 4 Ruling 6): unabhängig vom Hauptschalter, die Weitergabe öffnet auch ohne GUI-Werkzeuge Dateien.
        AddHeading(GuiTexts.GameEditorHeading);
        var register = AddButton(GuiTexts.GameEditorRegister(Host.GameDisplayName), GuiTexts.GameEditorRegisterTip, () =>
        {
            Host.RegisterGameEditor();
            Refresh();
        });
        var restore = AddButton(GuiTexts.GameEditorRestore, GuiTexts.GameEditorRestoreTip, () =>
        {
            Host.RestoreGameEditor();
            Refresh();
        });
        var editorStatus = AddHint(string.Empty);
        OnRefresh(() =>
        {
            editorStatus.Text = Host.GameEditorStatus;
            register.IsEnabled = restore.IsEnabled = Host.CanRegisterGameEditor;
        });

        OnRefresh(() =>
        {
            status.Text = GuiSettings.Status;
            foreach (var element in _dependent)
            {
                element.IsEnabled = GuiSettings.ToolsEnabled;
            }
        });
        RefreshOn(handler => GuiSettings.Changed += handler, handler => GuiSettings.Changed -= handler);
    }
}
