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
    Search,
    CuTool,
    PatchRecipes,
    GeneratorPackages,
    Compatch,
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
        new("main.guiDataHelper", HotkeyGroup.Main, Ctrl | HotkeyModifiers.Shift, "D", "MainWindow.xaml.cs"),
        new("main.guiBlocks", HotkeyGroup.Main, Ctrl | HotkeyModifiers.Shift, "B", "MainWindow.xaml.cs"),
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
        new("designer.previewSizeApply", HotkeyGroup.GuiDesigner, None, "Enter", "GuiDesignerView.cs"),
        new("explorer.pin", HotkeyGroup.Explorer, HotkeyModifiers.Alt, "F", "ExplorerPanel.xaml.cs"),
        new("explorer.clearSearch", HotkeyGroup.Explorer, None, "Escape", "ExplorerPanel.Search.cs"),
        new("explorer.searchNow", HotkeyGroup.Explorer, None, "Enter", "ExplorerPanel.Search.cs"),
        new("search.find", HotkeyGroup.Search, None, "Enter", "SearchReplaceWindow.xaml.cs"),
        new("search.newLine", HotkeyGroup.Search, HotkeyModifiers.Shift, "Enter", "SearchReplaceWindow.xaml.cs"),
        new("search.toggleMode", HotkeyGroup.Search, None, "Tab", "SearchReplaceWindow.xaml.cs"),
        new("cu.search", HotkeyGroup.CuTool, Ctrl, "F", "VanillaDiffWindow.xaml.cs"),
        new("cu.search", HotkeyGroup.CuTool, Ctrl, "H", "VanillaDiffWindow.xaml.cs"),
        new("cu.save", HotkeyGroup.CuTool, Ctrl, "S", "VanillaDiffWindow.xaml.cs"),
        new("cu.version", HotkeyGroup.CuTool, None, "Enter", "VanillaDiffWindow.xaml.cs"),
        new("cu.nextDiff", HotkeyGroup.CuTool, None, "F7", "VanillaDiffWindow.xaml.cs"),
        new("cu.pasteRules", HotkeyGroup.CuTool, Ctrl, "V", "ReplaceListTab.xaml.cs"),
        new("cu.previousDiff", HotkeyGroup.CuTool, HotkeyModifiers.Shift, "F7", "VanillaDiffWindow.xaml.cs"),
        new("cu.take", HotkeyGroup.CuTool, None, "U", "VanillaDiffWindow.xaml.cs"),
        new("cu.clearMarks", HotkeyGroup.CuTool, None, "Escape", "VanillaDiffWindow.xaml.cs"),
        new("cu.undoTake", HotkeyGroup.CuTool, Ctrl, "Z", "VanillaDiffWindow.xaml.cs"),
        new("cu.compare", HotkeyGroup.CuTool, None, "Enter", "VanillaDiffWindow.xaml.cs"),
        new("patch.guide", HotkeyGroup.PatchRecipes, None, "F1", "PatchRecipesWindow.xaml.cs"),
        new("patch.take", HotkeyGroup.PatchRecipes, None, "U", "PatchRecipesWindow.Preview.cs"),
        new("patch.clearMarks", HotkeyGroup.PatchRecipes, None, "Escape", "PatchRecipesWindow.Preview.cs"),
        new("patch.nextDiff", HotkeyGroup.PatchRecipes, None, "F7", "PatchRecipesWindow.Preview.cs"),
        new("patch.previousDiff", HotkeyGroup.PatchRecipes, HotkeyModifiers.Shift, "F7", "PatchRecipesWindow.Preview.cs"),
        new("patch.undoTake", HotkeyGroup.PatchRecipes, Ctrl, "Z", "PatchRecipesWindow.Preview.cs"),
        new("aiRecipe.guide", HotkeyGroup.PatchRecipes, None, "F1", "AiTaskWindow.cs"),
        new("generator.applyCount", HotkeyGroup.GeneratorPackages, None, "Enter", "GeneratorWindow.Form.cs"),
        new("generator.take", HotkeyGroup.GeneratorPackages, None, "U", "GeneratorWindow.Preview.cs"),
        new("generator.clearMarks", HotkeyGroup.GeneratorPackages, None, "Escape", "GeneratorWindow.Preview.cs"),
        new("generator.nextDiff", HotkeyGroup.GeneratorPackages, None, "F7", "GeneratorWindow.Preview.cs"),
        new("generator.previousDiff", HotkeyGroup.GeneratorPackages, HotkeyModifiers.Shift, "F7", "GeneratorWindow.Preview.cs"),
        new("generator.undoTake", HotkeyGroup.GeneratorPackages, Ctrl, "Z", "GeneratorWindow.Preview.cs"),
        new("generator.modifierInsert", HotkeyGroup.GeneratorPackages, None, "Enter", "ModifierSearchWindow.cs"),
        new("generator.modifierList", HotkeyGroup.GeneratorPackages, None, "Down", "ModifierSearchWindow.cs"),
        new("generator.lookupTake", HotkeyGroup.GeneratorPackages, None, "Enter", "GeneratorLookupWindow.cs"),
        new("generator.guide", HotkeyGroup.GeneratorPackages, None, "F1", "GeneratorWindow.xaml.cs"),
        new("packageEditor.save", HotkeyGroup.GeneratorPackages, Ctrl, "S", "PackageEditorWindow.cs"),
        new("packageEditor.guide", HotkeyGroup.GeneratorPackages, None, "F1", "PackageEditorWindow.cs"),
        new("aiPackage.guide", HotkeyGroup.GeneratorPackages, None, "F1", "AiTaskWindow.cs"),
        new("candidates.guide", HotkeyGroup.GeneratorPackages, None, "F1", "CandidateWindow.cs"),
        new("aiPackage.sourceSearch", HotkeyGroup.GeneratorPackages, Ctrl, "F", "SnippetSourceWindow.cs"),
        new("aiPackage.sourceFindNext", HotkeyGroup.GeneratorPackages, None, "Enter", "SnippetSourceWindow.cs"),
        new("compatch.nextDiff", HotkeyGroup.Compatch, None, "F7", "CompatchCompareWindow.cs"),
        new("compatch.previousDiff", HotkeyGroup.Compatch, HotkeyModifiers.Shift, "F7", "CompatchCompareWindow.cs"),
        new("compatch.takeRows", HotkeyGroup.Compatch, None, "U", "CompatchCompareWindow.cs"),
        new("compatch.undoTake", HotkeyGroup.Compatch, Ctrl, "Z", "CompatchCompareWindow.cs"),
        new("compatch.takeRows", HotkeyGroup.Compatch, None, "U", "CompatchWindow.Detail.cs"),
        new("compatch.undoTake", HotkeyGroup.Compatch, Ctrl, "Z", "CompatchWindow.Detail.cs"),
        new("compatch.confirm", HotkeyGroup.Compatch, None, "O", "CompatchWindow.Detail.cs"),
        new("compatch.takeA", HotkeyGroup.Compatch, None, "A", "CompatchWindow.Detail.cs"),
        new("compatch.takeB", HotkeyGroup.Compatch, None, "B", "CompatchWindow.Detail.cs"),
        new("compatch.reset", HotkeyGroup.Compatch, None, "R", "CompatchWindow.Detail.cs"),
        new("compatch.compare", HotkeyGroup.Compatch, None, "C", "CompatchWindow.Detail.cs"),
        new("compatch.treePrevious", HotkeyGroup.Compatch, None, "Up", "CompatchWindow.Detail.cs"),
        new("compatch.treeNext", HotkeyGroup.Compatch, None, "Down", "CompatchWindow.Detail.cs"),
        new("gui.inspect", HotkeyGroup.Gui, None, "Enter", "GuiElementSearchWindow.cs"),
        new("gui.toList", HotkeyGroup.Gui, None, "Down", "GuiElementSearchWindow.cs"),
        new("gui.toList", HotkeyGroup.Gui, None, "Down", "GuiDataHelperWindow.cs"),
        new("gui.dataHelperAppend", HotkeyGroup.Gui, None, "Enter", "GuiDataHelperWindow.cs"),
        new("gui.condUndo", HotkeyGroup.Gui, Ctrl, "Z", "GuiConditionTab.cs"),
        new("gui.condRedo", HotkeyGroup.Gui, Ctrl, "Y", "GuiConditionTab.cs"),
        new("gui.condDelete", HotkeyGroup.Gui, None, "Delete", "GuiConditionTab.cs"),
        new("gui.condApply", HotkeyGroup.Gui, None, "Enter", "GuiConditionTab.cs"),
        new("publish.saveDescription", HotkeyGroup.Publish, Ctrl, "S", "PublishWindow.xaml.cs"),
        new("publish.version", HotkeyGroup.Publish, None, "Enter", "PublishWindow.xaml.cs"),
        new("publish.saveRelease", HotkeyGroup.Publish, Ctrl, "S", "ReleasesWindow.xaml.cs"),
        new("templates.find", HotkeyGroup.Templates, Ctrl, "F", "TemplateGeneratorWindow.xaml.cs"),
        new("templates.find", HotkeyGroup.Templates, Ctrl, "H", "TemplateGeneratorWindow.xaml.cs"),
        new("templates.next", HotkeyGroup.Templates, None, "F3", "TemplateGeneratorWindow.xaml.cs"),
        new("templates.enter", HotkeyGroup.Templates, None, "Enter", "TemplateGeneratorWindow.xaml.cs"),
        new("dialogs.cancel", HotkeyGroup.Dialogs, None, "Escape", "TextPromptWindow.cs"),
        new("bugs.reject", HotkeyGroup.Dialogs, None, "Delete", "BugTrackerWindow.cs"),
        new("dialogs.scopeFinderAppend", HotkeyGroup.Dialogs, None, "Enter", "ScopePathWindow.xaml.cs"),
        new("saveExplorer.copyId", HotkeyGroup.Dialogs, Ctrl, "C", "SaveExplorerWindow.cs"),
    };
}
