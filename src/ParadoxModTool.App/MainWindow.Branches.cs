using System.IO;
using System.Windows;
using ParadoxModTool.Editor;
using ParadoxModTool.Explorer;
using ParadoxModTool.Mods;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Projekte im Hauptfenster: Explorer-Kontextmenü, Menü "Projekt", Vergleich mit einer anderen Mod des Projekts.</summary>
public partial class MainWindow
{
    private void InitializeBranches()
    {
        ExplorerPanelControl.BranchTargetsProvider = BuildBranchTargets;
        ExplorerPanelControl.BranchActionRequested += (_, e) => RunBranchAction(e.Path, e.IsDirectory, e.Action, e.TargetModId);
        ExplorerPanelControl.PathLockStateProvider = ProvideLockState;
        ExplorerPanelControl.PathLockRequested += (_, e) => RunPathLock(e.Path, e.Lock);
        ExplorerPanelControl.LockMarkProvider = ProvideLockMark;
        ExplorerPanelControl.LockCountBelowProvider = LockCountBelow;
        ExplorerPanelControl.UnlockBelowRequested += (_, folder) => RunUnlockBelow(folder);
        ExplorerPanelControl.SearchInPathRequested += (_, path) => OpenSearchReplaceWindow(path);
        TabLockProvider.Provider = TabLockTooltip;
        ProjectMenuItem.SubmenuOpened += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, ProjectMenuItem))
            {
                FillProjectMenu();
            }
        };
    }

    /// <summary>Was das Hauptfenster über offene Tabs weiß, für <see cref="BranchActions"/> und das Abgleichsfenster.</summary>
    private BranchHooks BranchHooksForEditors => new(
        path => AllPanes.SelectMany(p => p.Documents).Any(d => EditorPane.PathsEqual(d.FilePath, path) && d.IsModified),
        path =>
        {
            foreach (var pane in AllPanes)
            {
                foreach (var document in pane.Documents.Where(d => EditorPane.PathsEqual(d.FilePath, path)).ToList())
                {
                    pane.ReloadFromDisk(document);
                }
            }
        },
        path =>
        {
            foreach (var pane in AllPanes)
            {
                foreach (var document in pane.Documents.Where(d => EditorPane.PathsEqual(d.FilePath, path)).ToList())
                {
                    pane.CloseDocument(document);
                }
            }
        });

    private ExplorerBranchTargets? BuildBranchTargets(string path)
    {
        var registry = _mods?.Registry;
        if (registry is null || registry.Resolve(path) is not { } mod || !ModProjects.IsProject(registry, mod))
        {
            return null;
        }

        var others = ModProjects.Members(registry, mod).Where(m => m.Id != mod.Id).ToList();
        List<ExplorerBranchTarget> Allowed(TransferMode mode) => others
            .Where(m => BranchTransfer.Check(registry, mod, m, mode).Allowed)
            .Select(m => new ExplorerBranchTarget(m.Id, m.Name))
            .ToList();

        return new ExplorerBranchTargets(
            Allowed(TransferMode.Copy),
            Allowed(TransferMode.Move),
            others.Select(m => new ExplorerBranchTarget(m.Id, m.Name)).ToList());
    }

    private ExplorerLockState? ProvideLockState(string path)
    {
        var registry = _mods?.Registry;
        if (registry is null || registry.Resolve(path) is not { } mod || registry.ToRelative(mod.Id, path) is not { Length: > 0 } relative)
        {
            return null;
        }

        var by = ModLocks.LockedBy(mod, relative);
        if (by is null)
        {
            return new ExplorerLockState(ExplorerLockKind.Unlocked, null);
        }

        return string.Equals(by, relative, StringComparison.OrdinalIgnoreCase)
            ? new ExplorerLockState(ExplorerLockKind.Locked, null)
            : new ExplorerLockState(ExplorerLockKind.LockedByParent, by);
    }

    /// <summary>Tooltip des Schloss-Symbols im Explorer: nur der gesperrte Eintrag selbst und der Wurzelordner einer gesperrten Mod tragen es, nicht alles darunter.</summary>
    private string? ProvideLockMark(string path, bool isDirectory)
    {
        var registry = _mods?.Registry;
        if (registry is null || registry.Resolve(path) is not { } mod || registry.ToRelative(mod.Id, path) is not { } relative)
        {
            return null;
        }

        return ModLocks.Reason(mod, relative) is { } reason && (reason.Kind == LockReasonKind.ThisPath || (reason.Kind == LockReasonKind.WholeMod && relative.Length == 0))
            ? ModLocks.Describe(reason, isDirectory)
            : null;
    }

    /// <summary>Tooltip des Schloss-Symbols im Tab: jede wirksame Sperre der Datei (Datei, Ordner darüber oder ganze Mod).</summary>
    private string? TabLockTooltip(string path)
    {
        var registry = _mods?.Registry;
        if (registry is null || registry.Resolve(path) is not { } mod || registry.ToRelative(mod.Id, path) is not { Length: > 0 } relative)
        {
            return null;
        }

        return ModLocks.Reason(mod, relative) is { } reason ? ModLocks.Describe(reason, isDirectory: false) : null;
    }

    /// <summary>Zahl der Sperr-Einträge (Dateien und Ordner) unter einem Ordner; 0 außerhalb einer Mod.</summary>
    private int LockCountBelow(string folder)
    {
        var registry = _mods?.Registry;
        if (registry is null || registry.Resolve(folder) is not { } mod || registry.ToRelative(mod.Id, folder) is not { } relative)
        {
            return 0;
        }

        return ModLocks.CountUnder(mod, relative);
    }

    /// <summary>Hebt nach einer Warnung alle Sperren von Dateien und Ordnern unter dem Ordner auf.</summary>
    private void RunUnlockBelow(string folder)
    {
        if (_mods?.Registry is not { } registry
            || registry.Resolve(folder) is not { } mod
            || registry.ToRelative(mod.Id, folder) is not { } relative)
        {
            return;
        }

        var count = ModLocks.CountUnder(mod, relative);
        if (count == 0)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            relative.Length == 0 ? MainTexts.UnlockConfirmMod(count, mod.Name) : MainTexts.UnlockConfirmFolder(count, relative),
            MainTexts.UnlockTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        var removed = registry.ClearPathLocksUnder(mod.Id, relative);
        ModStatusText.Text = MainTexts.UnlockDone(removed);
        RefreshLockDisplays();
    }

    /// <summary>Aktualisiert die Schloss-Symbole im Explorer und in der Dokumentenleiste.</summary>
    private void RefreshLockDisplays()
    {
        ExplorerPanelControl.RefreshLockMarks();
        foreach (var pane in AllPanes)
        {
            pane.RefreshLocks();
        }
    }

    private void RunPathLock(string fullPath, bool lockIt)
    {
        if (_mods?.Registry is not { } registry
            || registry.Resolve(fullPath) is not { } mod
            || registry.ToRelative(mod.Id, fullPath) is not { Length: > 0 } relative)
        {
            return;
        }

        try
        {
            registry.SetPathLocked(mod.Id, relative, lockIt);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, MainTexts.ProjectCaption, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RefreshActiveMod();
        RefreshLockDisplays();
    }

    private void RunBranchAction(string fullPath, bool isDirectory, ExplorerBranchAction action, string targetModId)
    {
        if (_mods?.Registry is not { } registry
            || registry.Resolve(fullPath) is not { } from
            || registry.Get(targetModId) is not { } to
            || registry.ToRelative(from.Id, fullPath) is not { Length: > 0 } relative)
        {
            return;
        }

        if (action == ExplorerBranchAction.Compare)
        {
            CompareWithBranch(from, to, relative);
            return;
        }

        try
        {
            var files = isDirectory ? BranchTransfer.FilesUnder(from, relative) : new[] { relative };
            var text = BranchActions.Transfer(this, registry, from, to, files, action == ExplorerBranchAction.Move ? TransferMode.Move : TransferMode.Copy, BranchHooksForEditors);
            if (text.Length > 0)
            {
                ModStatusText.Text = text;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, MainTexts.ProjectCaption, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        ExplorerPanelControl.RefreshAll();
    }

    /// <summary>Öffnet dieselbe Datei aus zwei Mods des Projekts im Vergleichsfenster (links <paramref name="from"/>, rechts <paramref name="to"/>).</summary>
    private void CompareWithBranch(ModInfo from, ModInfo to, string relativePath)
    {
        var fromFolder = BranchTransfer.WorkingFolder(from);
        var toFolder = BranchTransfer.WorkingFolder(to);
        if (fromFolder is null || toFolder is null)
        {
            MessageBox.Show(this, MainTexts.ModFolderMissing(fromFolder is null ? from.Name : to.Name), MainTexts.ProjectCaption, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var left = Path.Combine(fromFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var right = Path.Combine(toFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var missing = !File.Exists(left) ? from : !File.Exists(right) ? to : null;
        if (missing is not null)
        {
            MessageBox.Show(this, MainTexts.CompareFileMissing(relativePath, missing.Name), MainTexts.ProjectCaption, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        OpenVanillaDiffWindow();
        _vanillaDiffWindow?.OpenFilePair(left, right, $"Mod: {from.Name}", $"Mod: {to.Name}");
    }

    /// <summary>Öffnet das Abgleichsfenster für das Projekt der Mod (nicht modal).</summary>
    private void OpenBranchSyncWindow(ModInfo mod)
    {
        if (_mods?.Registry is not { } registry)
        {
            return;
        }

        if (!ModProjects.IsProject(registry, mod))
        {
            MessageBox.Show(this, MainTexts.SyncNoProject(mod.Name), MainTexts.SyncTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        new BranchSyncWindow(registry, mod, CompareWithBranch, BranchHooksForEditors) { Owner = this }.Show();
    }

    private void OpenBranchSync_Click(object sender, RoutedEventArgs e)
    {
        if (_mods?.ActiveMod is { } mod)
        {
            OpenBranchSyncWindow(mod);
        }
        else
        {
            MessageBox.Show(this, MainTexts.SyncNoActiveMod, MainTexts.SyncTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>Füllt die Untermenüs des Menüs "Projekt" für die aktive Datei.</summary>
    private void FillProjectMenu()
    {
        var path = (_modSourcePane ?? PanelPane).CurrentFilePath;
        var targets = path is null ? null : BuildBranchTargets(path);
        FillProjectSubmenu(ProjectCopyMenuItem, path, targets?.Copy, ExplorerBranchAction.Copy);
        FillProjectSubmenu(ProjectMoveMenuItem, path, targets?.Move, ExplorerBranchAction.Move);
        FillProjectSubmenu(ProjectCompareMenuItem, path, targets?.Compare, ExplorerBranchAction.Compare);
    }

    private void FillProjectSubmenu(System.Windows.Controls.MenuItem parent, string? path, IReadOnlyList<ExplorerBranchTarget>? list, ExplorerBranchAction action)
    {
        parent.Items.Clear();
        parent.IsEnabled = path is not null && list is { Count: > 0 };
        if (!parent.IsEnabled)
        {
            return;
        }

        foreach (var target in list!)
        {
            var child = new System.Windows.Controls.MenuItem { Header = target.Name };
            var id = target.ModId;
            child.Click += (_, _) => RunBranchAction(path!, false, action, id);
            parent.Items.Add(child);
        }
    }
}
