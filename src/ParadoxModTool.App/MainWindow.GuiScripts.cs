using System.IO;
using System.Windows;
using ParadoxModTool.Editor;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.UndoRedo;
using ParadoxModTool.VersionManager;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Scripted GUIs im klassischen GUI-Designer (HOI IV, Plan 3 Task 6): "Scripted GUI anlegen…" am Fenster und "Effekt anlegen" am Knopf. Beide schreiben
/// ihre Dateien (Skript, Kategorie, Lokalisierung) als einen Rückgängig-Schritt und öffnen danach die Skriptdatei am neuen Block.
/// </summary>
public partial class MainWindow
{
    /// <summary>Verbindet das Eigenschaftenfeld mit "Scripted GUI anlegen" und "Effekt anlegen"; nur für Spiele mit Scripted GUIs (HOI IV).</summary>
    private void WireScriptedGuiTools(bool classic)
    {
        var scripted = classic && GuiSettings.Layout.ScriptWiring == GuiScriptWiring.ScriptedGuis;
        GuiSettings.ScriptedGuiCreator = scripted ? CreateScriptedGui : null;
        GuiSettings.ScriptedGuiEffectCreator = scripted ? CreateScriptedGuiEffect : null;
        WireCustomGuiTools(classic);
    }

    /// <summary>"Scripted GUI anlegen…": zeigt den Dialog, schreibt und öffnet die Skriptdatei; <see langword="false"/> bei Abbruch oder Fehler.</summary>
    private bool CreateScriptedGui(string windowName, IReadOnlyList<string> buttonNames)
    {
        var title = GuiTexts.ScriptedGuiWindowTitle;
        if (ScriptTarget(title) is not { } target)
        {
            return false;
        }

        var (index, modRoot) = target;
        var language = GuiSettings.GameSettingsPath is { } settings ? ClassicScreenSettings.TryReadLanguage(settings) : null;
        var window = new ScriptedGuiWindow(windowName, buttonNames, modRoot, index, language) { Owner = this };
        if (window.ShowDialog() != true || window.Plan is not { CanApply: true } plan || window.Request is not { } request)
        {
            return false;
        }

        return ApplyScriptPlan(plan, GuiTexts.ScriptedGuiAction(request.EntryName), modRoot, title);
    }

    /// <summary>"Effekt anlegen": fügt <c>&lt;Knopf&gt;_click = { }</c> in den Eintrag ein und öffnet die Skriptdatei an der neuen Zeile.</summary>
    private bool CreateScriptedGuiEffect(ScriptedGuiEntry entry, string buttonName)
    {
        var title = GuiTexts.ScriptedGuiEffectTitle;
        if (ScriptTarget(title) is not { } target)
        {
            return false;
        }

        var modRoot = target.ModRoot;

        ScriptedGuiPlan plan;
        try
        {
            plan = ScriptedGuiDraft.AddEffect(entry, buttonName, path => File.Exists(path) ? GuiTextReader.Read(path).Text : null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, GuiTexts.TakeOverFailed(ex.Message), title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!plan.CanApply)
        {
            MessageBox.Show(this, string.Join("\n", plan.Problems), title, MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        return ApplyScriptPlan(plan, GuiTexts.ScriptedGuiEffectAction(buttonName + "_click"), modRoot, title);
    }

    /// <summary>Fertiger Scripted-GUI-Index und Mod-Ordner, passend zum Index; sonst Hinweis und <see langword="null"/>.</summary>
    private (ScriptedGuiIndex Index, string ModRoot)? ScriptTarget(string title)
    {
        if (GuiSettings.Workspace is not { Classic.ScriptedGuis: { IsCompletedSuccessfully: true } task } workspace)
        {
            MessageBox.Show(this, GuiTexts.DraftNoIndex, title, MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        if (CurrentGuiModRoots().ModRoot is not { } modRoot)
        {
            MessageBox.Show(this, GuiTexts.NewFileNoMod, title, MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        if (!GuiIndexFitsMod(workspace, modRoot))
        {
            MessageBox.Show(this, GuiTexts.DraftNoIndex, title, MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        return (task.Result, modRoot);
    }

    /// <summary>
    /// Schreibt alle Dateien des Plans wie <see cref="ApplyClassicSprite"/>: kein geänderter offener Tab, Sicherung, wenn eine bestehende Datei sich ändert,
    /// fehlende Ordner und Dateien als ein Rückgängig-Schritt, alles oder nichts. Danach offene Tabs und Index nachziehen und die Skriptdatei am neuen Block öffnen.
    /// </summary>
    private bool ApplyScriptPlan(ScriptedGuiPlan plan, string actionName, string modRoot, string title) =>
        ApplyScriptFiles(plan.Files, plan.ScriptPath, plan.FocusLine, actionName, modRoot, title, GuiTexts.ScriptedGuiSnapshotNote);

    /// <summary>Wie <see cref="ApplyScriptPlan"/> für eine Liste von Dateien (auch .gui-Dateien, Custom GUI in EU IV); öffnet danach <paramref name="focusPath"/> an <paramref name="focusLine"/>.</summary>
    private bool ApplyScriptFiles(
        IReadOnlyList<GuiPlanFile> files, string focusPath, int focusLine, string actionName, string modRoot, string title, string snapshotNote)
    {
        var plan = (Files: files, ScriptPath: focusPath, FocusLine: focusLine);
        var modified = plan.Files.FirstOrDefault(f => AllPanes.SelectMany(p => p.Documents).Any(d => d.IsModified && EditorPane.PathsEqual(d.FilePath, f.FullPath)));
        if (modified is not null)
        {
            MessageBox.Show(this, GuiTexts.TakeOverOpenModified(Path.GetFileName(modified.FullPath)), title, MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        if (plan.Files.Any(f => !f.IsNew))
        {
            try
            {
                new ModVersionManager(modRoot, StorageLocations.ResolveSnapshotsDirectory(modRoot, _storageSettings)).CreateSnapshot(snapshotNote);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                MessageBox.Show(this, GuiTexts.TakeOverSnapshotFailed(ex.Message), title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        // Die Kodierung steht fest, bevor die erste Datei geschrieben wird: bestehende behalten ihre, neue nach Spielprofil (.txt ohne BOM, .yml mit BOM).
        var folders = plan.Files.Select(f => Path.GetDirectoryName(f.FullPath)!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var writes = new List<IUndoableAction> { new CreateFoldersUndoableAction(actionName, folders) };
        try
        {
            foreach (var file in plan.Files)
            {
                writes.Add(new FileWriteUndoableAction(
                    GuiTexts.TakeOverFileAction(Path.GetFileName(file.FullPath)), file.FullPath, file.NewText, ClassicOverridePlanner.WriteEncoding(file, _activeProfile)));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, GuiTexts.TakeOverFailed(ex.Message), title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // Alle Schritte oder keiner: Scheitert einer, werden die schon ausgeführten zurückgenommen.
        var done = new List<IUndoableAction>();
        try
        {
            foreach (var write in writes)
            {
                write.Do();
                done.Add(write);
            }
        }
        catch (Exception ex)
        {
            for (var i = done.Count - 1; i >= 0; i--)
            {
                try
                {
                    done[i].Undo();
                }
                catch (Exception)
                {
                    // Weiter mit den übrigen; die Sicherung von eben enthält den alten Stand.
                }
            }

            ReloadOpenTabsAffectedBy(new CompositeUndoableAction(string.Empty, done));
            MessageBox.Show(this, GuiTexts.TakeOverFailed(ex.Message), title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var action = new CompositeUndoableAction(actionName, writes);
        _history.Record(action);
        foreach (var file in plan.Files.Where(f => f.IsNew))
        {
            _newModFiles.Created(file.FullPath, DateTime.UtcNow);
        }

        // Lädt offene Tabs neu und meldet Skript, Kategorie und Lokalisierung dem GUI-Index (Scripted GUIs werden im Hintergrund neu gelesen).
        ReloadOpenTabsAffectedBy(action);
        foreach (var folder in folders)
        {
            ExplorerPanelControl.NotifyFolderChanged(folder);
        }

        JumpToBookmarkInFile(plan.ScriptPath, plan.FocusLine);
        return true;
    }
}
