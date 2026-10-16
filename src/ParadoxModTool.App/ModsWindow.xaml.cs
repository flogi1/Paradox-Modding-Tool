using System.IO;
using System.Windows;
using ParadoxModTool.Compatch;
using ParadoxModTool.Mods;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Fenster "Mods…": Mods dieses Spiels anlegen, umbenennen, entfernen und ihre Ordner verwalten (siehe <see cref="ModRegistry"/>).</summary>
public partial class ModsWindow : Window
{
    private readonly ModRegistry _registry;
    private readonly string _metadataFile;
    private readonly Action _changed;
    private readonly Action<ModInfo> _beforeRemove;
    private readonly SteamPublishHost _steam;
    private readonly Action<ModInfo> _openSync;
    private readonly Action<ModInfo> _openCompat;
    private readonly Action<ModInfo> _openCompatch;
    private readonly CompatchPickContext _pick;
    private readonly PlaysetQuickActions? _playsetQuick;

    public ModsWindow(ModRegistry registry, string metadataFile, Action changed, Action<ModInfo> beforeRemove, SteamPublishHost steam, Action<ModInfo> openSync, Action<ModInfo> openCompat, Action<ModInfo> openCompatch, PlaysetQuickActions? playsetQuick = null)
    {
        InitializeComponent();
        _registry = registry;
        _metadataFile = metadataFile;
        _changed = changed;
        _beforeRemove = beforeRemove;
        _steam = steam;
        _openSync = openSync;
        _openCompat = openCompat;
        _openCompatch = openCompatch;
        _playsetQuick = playsetQuick;
        _pick = new CompatchPickContext(registry, metadataFile, steam.SteamAppId);
        SetCompatchMenuItem.Header = CompatchTexts.ModsSetCompatch;
        ClearCompatchMenuItem.Header = CompatchTexts.ModsClearCompatch;
        OpenCompatchMenuItem.Header = CompatchTexts.ModsOpenMerge;
        QuickStartMenuItem.Header = PlaysetWindowTexts.QuickStart;
        QuickNewMenuItem.Header = PlaysetWindowTexts.QuickNew;
        RefreshMods();
    }

    private ModInfo? SelectedMod => (ModList.SelectedItem as ModRow)?.Mod;

    private string? SelectedPath => (PathList.SelectedItem as PathRow)?.Folder;

    private void RefreshMods(string? selectId = null)
    {
        var keep = selectId ?? SelectedMod?.Id;
        var ordered = ModProjects.Ordered(_registry);
        var rows = new List<ModRow>();
        var block = new List<ModInfo>();

        void FlushReferences()
        {
            var compatches = block.SelectMany(m => CompatchTargets.CompatchesFor(_registry, m)).DistinctBy(m => m.Id).ToList();
            rows.AddRange(compatches.Select(c => new ModRow(c, _registry, 1, isReference: true, _pick)));
            block.Clear();
        }

        foreach (var (mod, depth) in ordered)
        {
            if (depth == 0)
            {
                FlushReferences();
            }

            rows.Add(new ModRow(mod, _registry, depth, isReference: false, _pick));
            block.Add(mod);
        }

        FlushReferences();
        ModList.ItemsSource = rows;
        ModList.SelectedItem = rows.FirstOrDefault(r => r.Mod.Id == keep && !r.IsReference);
        RefreshPaths();
    }

    private void ModContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var mod = SelectedMod;
        SetCompatchMenuItem.IsEnabled = mod is not null && !ModProjects.IsProject(_registry, mod);
        ClearCompatchMenuItem.IsEnabled = mod?.IsCompatch == true;
        OpenCompatchMenuItem.IsEnabled = mod?.IsCompatch == true;
        QuickStartMenuItem.IsEnabled = mod is not null && _playsetQuick is not null && _playsetQuick.CanStart();
        QuickNewMenuItem.IsEnabled = mod is not null && _playsetQuick is not null;
    }

    private void QuickStart_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is { } mod)
        {
            _playsetQuick?.StartWith(mod);
        }
    }

    private void QuickNew_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is { } mod)
        {
            _playsetQuick?.NewFrom(mod);
        }
    }

    private void SetCompatch_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is not { } mod || CompatchTargetsDialog.Ask(this, _pick, mod) is not { } targets)
        {
            return;
        }

        Apply(() => _registry.SetCompatch(mod.Id, targets), mod.Id);
    }

    private void ClearCompatch_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is not { } mod
            || MessageBox.Show(this, CompatchTexts.ClearConfirm(mod.Name), CompatchTexts.ModsClearCompatch, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        Apply(() => _registry.ClearCompatch(mod.Id), mod.Id);
    }

    private void OpenCompatch_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is { IsCompatch: true } mod)
        {
            _openCompatch(mod);
        }
    }

    private void RefreshPaths()
    {
        var keep = SelectedPath;
        var rows = (SelectedMod?.Paths ?? new List<string>()).Select(p => new PathRow(p)).ToList();
        PathList.ItemsSource = rows;
        PathList.SelectedItem = rows.FirstOrDefault(r => keep is not null && string.Equals(r.Folder, keep, StringComparison.OrdinalIgnoreCase));
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var hasMod = SelectedMod is not null;
        var hasPath = SelectedPath is not null;
        RenameButton.IsEnabled = hasMod;
        RemoveModButton.IsEnabled = hasMod;
        ReleasesButton.IsEnabled = hasMod;
        AssignProjectButton.IsEnabled = hasMod;
        CompatButton.IsEnabled = hasMod;
        LockButton.IsEnabled = hasMod;
        SyncButton.IsEnabled = SelectedMod is { } selected && ModProjects.IsProject(_registry, selected);
        LockButton.Content = SelectedMod?.IsLocked == true ? ModsTexts.Unlock : ModsTexts.Lock;
        AddPathButton.IsEnabled = hasMod;
        RelinkButton.IsEnabled = hasMod && hasPath;
        RemovePathButton.IsEnabled = hasMod && hasPath;
    }

    private void ModList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => RefreshPaths();

    private void PathList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateButtons();

    /// <summary>Führt eine Änderung am Register aus; zeigt Fehler als Meldung und meldet dem Hauptfenster die Änderung.</summary>
    private bool Apply(Action change, string? selectId = null)
    {
        try
        {
            change();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, "Mods", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        RefreshMods(selectId);
        _changed();
        return true;
    }

    private static string? PickFolder(string description, string? initial)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true,
            InitialDirectory = initial is not null && Directory.Exists(initial) ? ParadoxModTool.Explorer.PathNormalization.ToWindowsPath(initial) : string.Empty,
        };

        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("mods.folder");
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    private void AddMod_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder(ModsTexts.PickModFolder, null);
        if (folder is null)
        {
            return;
        }

        var name = TextPromptWindow.Ask(this, ModsTexts.AddModTitle, ModsTexts.ModNamePrompt, ModRegistry.SuggestName(folder, _metadataFile));
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        string? newId = null;
        if (Apply(() => newId = _registry.Add(name, folder).Id))
        {
            RefreshMods(newId);
        }
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is not { } mod)
        {
            return;
        }

        var name = TextPromptWindow.Ask(this, ModsTexts.RenameModTitle, ModsTexts.NewNamePrompt, mod.Name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            Apply(() => _registry.Rename(mod.Id, name), mod.Id);
        }
    }

    private void AssignProject_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is not { } mod)
        {
            return;
        }

        if (ProjectAssignDialog.Ask(this, _registry, mod) is not { } result)
        {
            return;
        }

        if (result.Members is { } members)
        {
            Apply(() => _registry.SetProjectMembers(mod.Id, members), mod.Id);
        }
        else
        {
            Apply(() => _registry.SetProject(mod.Id, result.ParentModId, result.Role), mod.Id);
        }
    }

    private void Sync_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is { } mod)
        {
            _openSync(mod);
        }
    }

    private void Compat_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is { } mod)
        {
            _openCompat(mod);
        }
    }

    private void Lock_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is { } mod)
        {
            Apply(() => _registry.SetLocked(mod.Id, !mod.IsLocked), mod.Id);
        }
    }

    private void Releases_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is { } mod)
        {
            new ReleasesWindow(_registry, mod, _metadataFile, _steam) { Owner = this }.ShowDialog();
            RefreshMods(mod.Id);
        }
    }

    private void RemoveMod_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is not { } mod)
        {
            return;
        }

        if (MessageBox.Show(this, ModsTexts.RemoveConfirm(mod.Name), "Mods", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var dataDirectory = _registry.DataDirectory(mod.Id);
        var files = Directory.Exists(dataDirectory) ? Directory.EnumerateFiles(dataDirectory, "*", SearchOption.AllDirectories).Count() : 0;
        var deleteData = files > 0
            && MessageBox.Show(this, ModsTexts.DeleteDataConfirm(files), "Mods", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

        if (Apply(() =>
            {
                _beforeRemove(mod);
                _registry.Remove(mod.Id);
            }) && deleteData)
        {
            try
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, ModsTexts.DeleteDataFailed(ex.Message), "Mods", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void AddPath_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is not { } mod)
        {
            return;
        }

        if (PickFolder(ModsTexts.PickAnotherFolder, mod.Paths.FirstOrDefault()) is { } folder)
        {
            Apply(() => _registry.AddPath(mod.Id, folder), mod.Id);
        }
    }

    private void Relink_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is not { } mod || SelectedPath is not { } oldPath)
        {
            return;
        }

        if (PickFolder(ModsTexts.PickNewFolder, Path.GetDirectoryName(oldPath)) is { } folder)
        {
            Apply(() => _registry.Relink(mod.Id, oldPath, folder), mod.Id);
        }
    }

    private void RemovePath_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMod is not { } mod || SelectedPath is not { } path)
        {
            return;
        }

        Apply(() => _registry.RemovePath(mod.Id, path), mod.Id);
    }

    private sealed class ModRow
    {
        private readonly ModRegistry _registry;
        private readonly CompatchPickContext _pick;

        public ModRow(ModInfo mod, ModRegistry registry, int depth, bool isReference, CompatchPickContext pick)
        {
            Mod = mod;
            _registry = registry;
            _pick = pick;
            IsReference = isReference;
            Indent = new Thickness(depth * 16, 0, 0, 0);
        }

        public ModInfo Mod { get; }

        public bool IsReference { get; }

        /// <summary>Linker Rand der Zeile: 16 px je Ebene, wie im Explorer-Baum.</summary>
        public Thickness Indent { get; }

        public string Title
        {
            get
            {
                if (IsReference)
                {
                    return CompatchTexts.ReferenceRow(Mod.Name);
                }

                var text = Mod.Name;
                if (Mod.IsCompatch)
                {
                    text += "  (" + CompatchTexts.RoleText(string.Join(" + ", Mod.CompatchTargets.Select(t => t.DisplayName))) + ")";
                    if (Mod.CompatchTargets.Any(t => CompatchTargets.Resolve(_registry, t, id => CompatchModPicker.WorkshopFolder(_pick, id)) is null))
                    {
                        text += "  ⚠ " + CompatchTexts.TargetMissing;
                    }
                }
                else if (ModProjects.RoleText(_registry, Mod) is { } role)
                {
                    text += $"  ({role})";
                }

                if (Mod.IsLocked)
                {
                    text += "  🔒";
                }

                return Mod.Paths.Any(p => !Directory.Exists(p)) ? ModsTexts.PathMissing(text) : text;
            }
        }
    }

    private sealed class PathRow
    {
        public PathRow(string folder)
        {
            Folder = folder;
        }

        public string Folder { get; }

        public string Display => Directory.Exists(Folder) ? Folder : ModsTexts.PathMissing(Folder);
    }
}
