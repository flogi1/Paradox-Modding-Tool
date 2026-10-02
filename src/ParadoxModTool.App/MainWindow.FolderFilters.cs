using ParadoxModTool.Categories;
using ParadoxModTool.Editor;
using ParadoxModTool.Session;

namespace ParadoxModTool.App;

/// <summary>
/// Ordner-Filter (Filterleiste der Arbeitsbereiche, Gruppe "Nur Ordner:"): angelegt per Rechtsklick auf einen Ordner in der
/// Verzeichnis-Übersicht, umbenannt per Rechtsklick auf das Häkchen, gespeichert pro Spiel.
/// </summary>
public partial class MainWindow
{
    private void InitializeFolderFilters()
    {
        ExplorerPanelControl.FolderFilterRequested += (_, folder) => CreateFolderFilter(folder);
        foreach (var pane in AllPanes)
        {
            pane.FolderFilterRenameRequested += (_, id) => RenameFolderFilter(id);
        }
    }

    private void CreateFolderFilter(string folder)
    {
        var name = TextPromptWindow.Ask(this, MainTexts.FolderFilterCreateTitle, MainTexts.FolderFilterCreatePrompt(folder), FolderFilterMatcher.SuggestName(folder));
        if (!string.IsNullOrWhiteSpace(name))
        {
            FolderFilterSettings.Add(name, folder);
        }
    }

    private void RenameFolderFilter(string id)
    {
        if (FolderFilterSettings.Find(id) is not { } filter)
        {
            return;
        }

        var name = TextPromptWindow.Ask(this, MainTexts.FolderFilterRenameTitle, MainTexts.FolderFilterRenamePrompt(filter.FolderPath), filter.Name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            FolderFilterSettings.Rename(id, name);
        }
    }

    private static void LoadFolderFilters(AppSessionState state) => FolderFilterSettings.Load(state.FolderFilters);

    private static void SaveFolderFilters(AppSessionState state) => state.FolderFilters = FolderFilterSettings.Snapshot();
}
