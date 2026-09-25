namespace ParadoxModTool.Settings;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
}

/// <summary>Fenster bzw. Bereich eines Tastenkürzels (Überschriften der Hotkey-Seite, in dieser Reihenfolge).</summary>
public enum HotkeyGroup
{
    Main,
    GuiDesigner,
    Explorer,
    CuTool,
    Gui,
    Publish,
    Templates,
    Dialogs,
}

/// <summary>
/// Ein Tastenkürzel. <see cref="Key"/> ist der Name aus <c>System.Windows.Input.Key</c>, <see cref="SourceFile"/> die Datei mit dem
/// Tastenvergleich (leer = eingebaut, z. B. AvalonEdit). Mehrere Einträge dürfen dieselbe Beschreibung (<see cref="TextId"/>) teilen.
/// </summary>
public sealed record HotkeyEntry(string TextId, HotkeyGroup Group, HotkeyModifiers Modifiers, string Key, string SourceFile);

/// <summary>
/// Feste Liste aller Tastenkürzel für die Nachschlage-Seite im Einstellungsfenster. Ein Test durchsucht den Quellcode nach Tastenvergleichen
/// (Vergleiche von e.Key mit einer Taste) und verlangt zu jedem einen Eintrag: Wer ein Kürzel hinzufügt, muss es hier nachtragen.
/// </summary>
public static class HotkeyCatalog
{
    private const HotkeyModifiers Ctrl = HotkeyModifiers.Ctrl;
    private const HotkeyModifiers None = HotkeyModifiers.None;

    public static IReadOnlyList<HotkeyEntry> Entries { get; } = new HotkeyEntry[]
    {
        new("editor.undo", HotkeyGroup.Main, Ctrl, "Z", ""),
        new("editor.redo", HotkeyGroup.Main, Ctrl, "Y", ""),
        new("main.search", HotkeyGroup.Main, Ctrl, "F", "MainWindow.xaml.cs"),
        new("main.guiInspector", HotkeyGroup.Main, Ctrl, "H", "MainWindow.xaml.cs"),
        new("main.goToLine", HotkeyGroup.Main, Ctrl, "G", "MainWindow.xaml.cs"),
        new("main.save", HotkeyGroup.Main, Ctrl, "S", "MainWindow.xaml.cs"),
        new("main.saveAll", HotkeyGroup.Main, Ctrl | HotkeyModifiers.Shift, "S", "MainWindow.xaml.cs"),
        new("main.bracket", HotkeyGroup.Main, Ctrl, "B", "MainWindow.xaml.cs"),
        new("main.bookmark", HotkeyGroup.Main, Ctrl, "F2", "MainWindow.xaml.cs"),
        new("main.scopeFinder", HotkeyGroup.Main, Ctrl | HotkeyModifiers.Shift, "P", "MainWindow.xaml.cs"),
        new("editor.autoComplete", HotkeyGroup.Main, Ctrl, "Space", "ScriptHintController.cs"),
        new("designer.undo", HotkeyGroup.GuiDesigner, Ctrl, "Z", "GuiDesignerView.cs"),
        new("designer.redo", HotkeyGroup.GuiDesigner, Ctrl, "Y", "GuiDesignerView.cs"),
        new("designer.delete", HotkeyGroup.GuiDesigner, None, "Delete", "GuiDesignerView.cs"),
        new("designer.duplicate", HotkeyGroup.GuiDesigner, Ctrl, "D", "GuiDesignerView.cs"),
        new("designer.moveUp", HotkeyGroup.GuiDesigner, HotkeyModifiers.Alt, "Up", "GuiDesignerView.cs"),
        new("designer.moveDown", HotkeyGroup.GuiDesigner, HotkeyModifiers.Alt, "Down", "GuiDesignerView.cs"),
        new("designer.paletteInsert", HotkeyGroup.GuiDesigner, None, "Enter", "GuiPaletteView.cs"),
        new("designer.propertyApply", HotkeyGroup.GuiDesigner, None, "Enter", "GuiPropertyEditor.cs"),
        new("designer.propertyReset", HotkeyGroup.GuiDesigner, None, "Escape", "GuiPropertyEditor.cs"),
        new("explorer.pin", HotkeyGroup.Explorer, HotkeyModifiers.Alt, "F", "ExplorerPanel.xaml.cs"),
        new("explorer.clearSearch", HotkeyGroup.Explorer, None, "Escape", "ExplorerPanel.Search.cs"),
        new("explorer.searchNow", HotkeyGroup.Explorer, None, "Enter", "ExplorerPanel.Search.cs"),
        new("cu.search", HotkeyGroup.CuTool, Ctrl, "F", "VanillaDiffWindow.xaml.cs"),
        new("cu.search", HotkeyGroup.CuTool, Ctrl, "H", "VanillaDiffWindow.xaml.cs"),
        new("cu.save", HotkeyGroup.CuTool, Ctrl, "S", "VanillaDiffWindow.xaml.cs"),
        new("cu.version", HotkeyGroup.CuTool, None, "Enter", "VanillaDiffWindow.xaml.cs"),
        new("gui.inspect", HotkeyGroup.Gui, None, "Enter", "GuiElementSearchWindow.cs"),
        new("gui.toList", HotkeyGroup.Gui, None, "Down", "GuiElementSearchWindow.cs"),
        new("publish.saveDescription", HotkeyGroup.Publish, Ctrl, "S", "PublishWindow.xaml.cs"),
        new("publish.version", HotkeyGroup.Publish, None, "Enter", "PublishWindow.xaml.cs"),
        new("publish.saveRelease", HotkeyGroup.Publish, Ctrl, "S", "ReleasesWindow.xaml.cs"),
        new("templates.find", HotkeyGroup.Templates, Ctrl, "F", "TemplateGeneratorWindow.xaml.cs"),
        new("templates.find", HotkeyGroup.Templates, Ctrl, "H", "TemplateGeneratorWindow.xaml.cs"),
        new("templates.next", HotkeyGroup.Templates, None, "F3", "TemplateGeneratorWindow.xaml.cs"),
        new("templates.enter", HotkeyGroup.Templates, None, "Enter", "TemplateGeneratorWindow.xaml.cs"),
        new("dialogs.cancel", HotkeyGroup.Dialogs, None, "Escape", "TextPromptWindow.cs"),
        new("bugs.reject", HotkeyGroup.Dialogs, None, "Delete", "BugTrackerWindow.cs"),
    };
}
