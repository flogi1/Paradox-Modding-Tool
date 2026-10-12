using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// "GUI-Einstellungen der Mod" (Spec "Einstellungen", Plan-Ruling 10): Dateinamen der Überschreib- und Typdatei und die Abhängigkeits-Mods für
/// den GUI-Index. Leere Namen stehen für die Standardnamen. Speichern übernimmt das Hauptfenster (Register der Mods).
/// </summary>
public sealed class GuiModSettingsWindow : Window
{
    private readonly TextBox _overrideFile = new() { MinWidth = 320, Margin = new Thickness(0, 2, 0, 8) };
    private readonly TextBox _typesFile = new() { MinWidth = 320, Margin = new Thickness(0, 2, 0, 8) };
    private readonly ListBox _dependencies = new() { Height = 120, Margin = new Thickness(0, 2, 0, 4) };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
    private readonly GuiLayout _layout;

    /// <param name="defaults">Standard-Zieldateien (aus dem Ordnernamen) als Platzhalter-Hinweis.</param>
    /// <param name="defaultDependencies">Vorbelegung der Abhängigkeiten (Knopf "Vorbelegung").</param>
    public GuiModSettingsWindow(
        string modName,
        GuiModTargets defaults,
        string? overrideFile,
        string? typesFile,
        IReadOnlyList<string> dependencies,
        IReadOnlyList<string> defaultDependencies,
        GuiLayout? layout = null)
    {
        _layout = layout ?? GuiLayout.Default;
        Title = GuiTexts.ModSettingsTitle(modName);
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        _overrideFile.Text = overrideFile ?? defaults.OverrideFileName;
        _typesFile.Text = typesFile ?? defaults.TypesFileName;
        foreach (var folder in dependencies)
        {
            _dependencies.Items.Add(folder);
        }

        _error.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");
        var add = new Button { Content = GuiTexts.ModSettingsAdd, Padding = new Thickness(8, 2, 8, 2) };
        add.Click += (_, _) =>
        {
            var dialog = new OpenFolderDialog { Title = GuiTexts.ModSettingsAddTitle };
            dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("gui.modSettings.folder");
            if (dialog.ShowDialog(this) == true && !_dependencies.Items.Contains(dialog.FolderName))
            {
                _dependencies.Items.Add(dialog.FolderName);
            }
        };
        var remove = new Button { Content = GuiTexts.ModSettingsRemove, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(6, 0, 0, 0) };
        remove.Click += (_, _) =>
        {
            if (_dependencies.SelectedItem is { } selected)
            {
                _dependencies.Items.Remove(selected);
            }
        };
        var restore = new Button { Content = GuiTexts.ModSettingsDefaults, ToolTip = GuiTexts.ModSettingsDefaultsTip, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(6, 0, 0, 0) };
        restore.Click += (_, _) =>
        {
            _dependencies.Items.Clear();
            foreach (var folder in defaultDependencies)
            {
                _dependencies.Items.Add(folder);
            }
        };
        var listButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        listButtons.Children.Add(add);
        listButtons.Children.Add(remove);
        listButtons.Children.Add(restore);

        var save = new Button { Content = GuiTexts.ModSettingsSave, IsDefault = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2) };
        save.Click += (_, _) =>
        {
            if (GuiModTargets.IsValidFileName(_overrideFile.Text.Trim()) && GuiModTargets.IsValidFileName(_typesFile.Text.Trim()))
            {
                DialogResult = true;
                return;
            }

            _error.Text = GuiTexts.ModSettingsInvalidFile;
            _error.Visibility = Visibility.Visible;
        };
        var cancel = new Button { Content = GuiTexts.ModSettingsCancel, IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(14), MaxWidth = 560 };
        panel.Children.Add(new TextBlock { Text = _layout.IsFlat ? GuiTexts.ModSettingsOverrideFileFlat : GuiTexts.ModSettingsOverrideFile, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(_overrideFile);
        panel.Children.Add(new TextBlock { Text = GuiTexts.ModSettingsTypesFile, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(_typesFile);
        if (_layout.FirstWins)
        {
            var firstWins = new TextBlock { Text = GuiTexts.ModSettingsFirstWinsHintFor(_layout), TextWrapping = TextWrapping.Wrap, MaxWidth = 460, Margin = new Thickness(0, 2, 0, 6) };
            firstWins.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
            panel.Children.Add(firstWins);
        }

        panel.Children.Add(new TextBlock { Text = GuiTexts.ModSettingsDependencies, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(_dependencies);
        panel.Children.Add(listButtons);
        panel.Children.Add(_error);
        panel.Children.Add(buttons);
        Content = panel;
    }

    public string OverrideFile => _overrideFile.Text.Trim();

    public string TypesFile => _typesFile.Text.Trim();

    public IReadOnlyList<string> Dependencies => _dependencies.Items.Cast<string>().ToList();
}
