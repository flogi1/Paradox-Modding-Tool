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
/// Sprites im klassischen GUI-Designer (HOI IV, EU IV): "Auswählen…" und "Sprite anlegen…" im Eigenschaftenfeld. Das Anlegen schreibt Bildkopie und
/// <c>.gfx</c>-Eintrag als ein Rückgängig-Schritt.
/// </summary>
public partial class MainWindow
{
    /// <summary>Verbindet das Eigenschaftenfeld mit der Sprite-Auswahl und "Sprite anlegen"; nur klassisch (sonst fehlen die Knöpfe).</summary>
    private void WireClassicSpriteTools(bool classic)
    {
        GuiSettings.SpritePicker = classic ? PickClassicSprite : null;
        GuiSettings.SpriteCreator = classic ? CreateClassicSprite : null;
    }

    /// <summary>"Auswählen…": das gewählte Sprite oder <see langword="null"/> (abgebrochen, oder noch kein Index).</summary>
    private string? PickClassicSprite(string? current)
    {
        if (GuiSettings.Workspace?.Classic is not { } assets)
        {
            MessageBox.Show(this, GuiTexts.DraftNoIndex, GuiTexts.ClassicSpritePickerTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        var window = new ClassicSpritePickerWindow(assets, current) { Owner = this };
        return window.ShowDialog() == true && window.Chosen.Length > 0 ? window.Chosen : null;
    }

    /// <summary>"Sprite anlegen…": zeigt den Dialog, schreibt und liefert den Namen des neuen Sprites oder <see langword="null"/> (abgebrochen oder gescheitert).</summary>
    private string? CreateClassicSprite(string? suggestedName)
    {
        var title = GuiTexts.ClassicSpriteWindowTitle;
        if (GuiSettings.Workspace is not { Classic: { } assets } workspace)
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

        var options = workspace.Options;
        var roots = GuiSourceSet.ClassicRootsFor(options.InstallDirectory, workspace.Rules.Layout.VanillaRoots ?? new[] { string.Empty }, options.DependencyRoots, options.ModRoot);
        var window = new ClassicSpriteWindow(assets, modRoot, roots, workspace.ClassicGfxFiles, suggestedName) { Owner = this };
        if (window.ShowDialog() != true || window.Plan is not { CanApply: true } plan || window.Request is not { } request)
        {
            return null;
        }

        return ApplyClassicSprite(plan, request, modRoot) ? request.Name : null;
    }

    /// <summary>
    /// Schreibt den Plan: Sicherung, falls eine bestehende Datei sich ändert (die <c>.gfx</c> oder eine ersetzte Bildkopie); fehlende Ordner, Bildkopie und
    /// <c>.gfx</c> als ein Rückgängig-Schritt, alles oder nichts; danach offene Tabs, GUI-Index (neue Sprite-Tabelle) und Texturen nachziehen.
    /// </summary>
    private bool ApplyClassicSprite(ClassicSpritePlan plan, ClassicSpriteRequest request, string modRoot)
    {
        var title = GuiTexts.ClassicSpriteWindowTitle;
        var file = plan.Files[0];
        if (AllPanes.SelectMany(p => p.Documents).Any(d => d.IsModified && EditorPane.PathsEqual(d.FilePath, file.FullPath)))
        {
            MessageBox.Show(this, GuiTexts.TakeOverOpenModified(Path.GetFileName(file.FullPath)), title, MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        var copyReplaced = plan.CopyTo is { } target && File.Exists(target);
        if (!file.IsNew || copyReplaced)
        {
            try
            {
                new ModVersionManager(modRoot, StorageLocations.ResolveSnapshotsDirectory(modRoot, _storageSettings)).CreateSnapshot(GuiTexts.ClassicSpriteSnapshotNote);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                MessageBox.Show(this, GuiTexts.TakeOverSnapshotFailed(ex.Message), title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        // Die Kodierung steht fest, bevor die erste Datei geschrieben wird (ForWrite liest die Datei): bestehende behalten ihre, neue nach Spielprofil.
        // Die Bildkopie ist ein eigener Schritt (Bytes), damit Rückgängig sie wieder entfernt oder die ersetzte Datei zurückholt.
        var folders = new List<string> { Path.GetDirectoryName(file.FullPath)! };
        var writes = new List<IUndoableAction>();
        try
        {
            if (plan.CopyFrom is not null && plan.CopyTo is not null)
            {
                folders.Add(Path.GetDirectoryName(plan.CopyTo)!);
                writes.Add(new BinaryWriteUndoableAction(GuiTexts.ClassicSpriteImageAction(Path.GetFileName(plan.CopyTo)), plan.CopyTo, File.ReadAllBytes(plan.CopyFrom)));
            }

            writes.Insert(0, new CreateFoldersUndoableAction(GuiTexts.ClassicSpriteAction(request.Name), folders));
            writes.Add(new FileWriteUndoableAction(
                GuiTexts.TakeOverFileAction(Path.GetFileName(file.FullPath)),
                file.FullPath,
                file.NewText,
                ClassicOverridePlanner.WriteEncoding(file, _activeProfile)));
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

        var action = new CompositeUndoableAction(GuiTexts.ClassicSpriteAction(request.Name), writes);
        _history.Record(action);
        if (file.IsNew)
        {
            _newModFiles.Created(file.FullPath, DateTime.UtcNow);
        }

        if (plan.CopyTo is not null && !copyReplaced)
        {
            _newModFiles.Created(plan.CopyTo, DateTime.UtcNow);
        }

        // Die Textur liegt jetzt im Mod: Merkliste der Textursuche und der Bildpuffer prüfen beim nächsten Abruf neu (sonst bliebe ein früherer Fehlschlag stehen).
        GuiSettings.Workspace?.Classic?.Textures.NewRun();
        GuiImageCache.Shared.NewRun();

        // Lädt offene Tabs neu und meldet die .gfx dem GUI-Index (WithModFile baut die Sprite-Tabelle neu).
        ReloadOpenTabsAffectedBy(action);
        foreach (var folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ExplorerPanelControl.NotifyFolderChanged(folder);
        }

        return true;
    }
}
