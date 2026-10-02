using System.IO;
using System.Windows;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Mods;
using ParadoxModTool.SteamIntegration;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Eine Zeile der Mitgliederliste des Kompatibilitätssets.</summary>
internal sealed class MemberRow
{
    private readonly Action _changed;

    public MemberRow(CompatSetMember member, int position, Action changed)
    {
        Member = member;
        Position = position;
        _changed = changed;
    }

    public CompatSetMember Member { get; }

    public int Position { get; }

    public bool Missing => !Directory.Exists(Member.Folder);

    public string NameText => Missing ? ModCompatWindowTexts.Missing(Member.Name) : Member.Name;

    public string OriginText => Member.Origin switch
    {
        CompatOrigin.Register => ModCompatWindowTexts.OwnMod,
        CompatOrigin.Workshop => "Workshop",
        _ => ParadoxModTool.Compatch.CompatchTexts.FromFolder,
    };

    public bool Enabled
    {
        get => Member.Enabled;
        set
        {
            Member.Enabled = value;
            _changed();
        }
    }
}

/// <summary>Fenster "Mod-Kompatibilität": Set verwalten (links) und prüfen (rechts, siehe <c>ModCompatWindow.Analysis.cs</c>).</summary>
public partial class ModCompatWindow : Window
{
    private const string DefaultSetName = "Standard";

    private readonly ModCompatHost _host;
    private readonly CompatSetStore _store;
    private readonly ParseCache _cache = new();
    private CompatSet _set = new() { Name = DefaultSetName };
    private bool _loading;

    public ModCompatWindow(ModCompatHost host)
    {
        InitializeComponent();
        CreateCompatchButton.Content = ParadoxModTool.Compatch.CompatchTexts.CreateCompatch;
        CreateCompatchButton.ToolTip = ParadoxModTool.Compatch.CompatchTexts.CreateCompatchTip;
        _host = host;
        var setsDirectory = System.IO.Path.Combine(host.GameDirectory, "compat", "sets");
        _store = new CompatSetStore(setsDirectory);
        CompatStarterSet.SeedIfFirstUse(setsDirectory, _store);
        AddWorkshopButton.IsEnabled = host.SteamAppId is not null;
        LoadSetNames(_store.List().FirstOrDefault() ?? DefaultSetName);
        Closed += (_, _) => OnClosedCore();
    }

    partial void OnClosedCore();

    private List<MemberRow> MemberRows => MemberList.Items.Cast<MemberRow>().ToList();

    private List<MemberRow> SelectedMembers => MemberList.SelectedItems.Cast<MemberRow>().ToList();

    /// <summary>Speichert das aktuelle Set. Ein leeres, noch nie gespeichertes Standard-Set legt keine Datei an.</summary>
    private void SaveSet()
    {
        if (_set.Members.Count == 0 && !_store.List().Contains(_set.Name))
        {
            return;
        }

        _store.Save(_set);
    }

    private void LoadSetNames(string select)
    {
        _loading = true;
        var names = _store.List();
        if (!names.Contains(select, StringComparer.CurrentCultureIgnoreCase))
        {
            names.Add(select);
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
        }

        SetCombo.ItemsSource = names;
        SetCombo.SelectedItem = names.FirstOrDefault(n => string.Equals(n, select, StringComparison.CurrentCultureIgnoreCase));
        _loading = false;
        LoadSet(select);
    }

    private void LoadSet(string name)
    {
        _set = _store.Load(name) ?? new CompatSet { Name = name };
        _set.Name = name;
        SetDescriptionText.Text = _set.Description;
        SetDescriptionText.Visibility = string.IsNullOrWhiteSpace(_set.Description) ? Visibility.Collapsed : Visibility.Visible;
        RefreshMembers();
    }

    private void EditDescription_Click(object sender, RoutedEventArgs e)
    {
        var description = TextPromptWindow.Ask(this, ModCompatWindowTexts.EditDescription, ModCompatWindowTexts.DescriptionPrompt, _set.Description);
        if (description is null)
        {
            return;
        }

        _set.Description = description.Trim();
        _store.Save(_set);
        SetDescriptionText.Text = _set.Description;
        SetDescriptionText.Visibility = string.IsNullOrWhiteSpace(_set.Description) ? Visibility.Collapsed : Visibility.Visible;
        UpdateMemberButtons();
    }

    private void RefreshMembers()
    {
        var keep = SelectedMembers.Select(r => r.Member.Folder).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = _set.Members.Select((m, i) => new MemberRow(m, i + 1, SaveSet)).ToList();
        MemberList.ItemsSource = rows;
        foreach (var row in rows.Where(r => keep.Contains(r.Member.Folder)))
        {
            MemberList.SelectedItems.Add(row);
        }

        UpdateMemberButtons();
        OnMembersChanged();
    }

    /// <summary>Wird nach jeder Änderung der Mitglieder aufgerufen (die Mod-1-Auswahl wird neu gefüllt).</summary>
    partial void OnMembersChanged();

    private void UpdateMemberButtons()
    {
        var selected = SelectedMembers;
        var single = selected.Count == 1 ? selected[0] : null;
        UpButton.IsEnabled = single is not null && single.Position > 1;
        DownButton.IsEnabled = single is not null && single.Position < _set.Members.Count;
        RemoveMemberButton.IsEnabled = selected.Count > 0;
        RenameSetButton.IsEnabled = true;
        DeleteSetButton.IsEnabled = _store.List().Contains(_set.Name);
        ImportPlaysetButton.IsEnabled = !string.IsNullOrWhiteSpace(_host.PlaysetsFile());
    }

    private void SetCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_loading && SetCombo.SelectedItem is string name)
        {
            LoadSet(name);
        }
    }

    private void MemberList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateMemberButtons();

    private void NewSet_Click(object sender, RoutedEventArgs e)
    {
        var name = TextPromptWindow.Ask(this, ModCompatWindowTexts.NewSetTitle, ModCompatWindowTexts.SetNamePrompt, string.Empty)?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        if (_store.List().Contains(name, StringComparer.CurrentCultureIgnoreCase))
        {
            MessageBox.Show(this, ModCompatWindowTexts.SetExists(name), ModCompatWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _set = new CompatSet { Name = name };
        _store.Save(_set);
        LoadSetNames(name);
    }

    private void RenameSet_Click(object sender, RoutedEventArgs e)
    {
        var name = TextPromptWindow.Ask(this, ModCompatWindowTexts.RenameSetTitle, ModsTexts.NewNamePrompt, _set.Name)?.Trim();
        if (string.IsNullOrEmpty(name) || string.Equals(name, _set.Name, StringComparison.Ordinal))
        {
            return;
        }

        if (_store.List().Contains(name, StringComparer.CurrentCultureIgnoreCase))
        {
            MessageBox.Show(this, ModCompatWindowTexts.SetExists(name), ModCompatWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var old = _set.Name;
        _set.Name = name;
        _store.Save(_set);
        _store.Delete(old);
        LoadSetNames(name);
    }

    private void DeleteSet_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, ModCompatWindowTexts.DeleteSetConfirm(_set.Name), ModCompatWindowTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return;
        }

        _store.Delete(_set.Name);
        LoadSetNames(_store.List().FirstOrDefault() ?? DefaultSetName);
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);

    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int delta)
    {
        if (SelectedMembers is [{ } row] && _set.Move(row.Member.Folder, delta))
        {
            SaveSet();
            RefreshMembers();
        }
    }

    private void RemoveMember_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in SelectedMembers)
        {
            _set.Remove(row.Member.Folder);
        }

        SaveSet();
        RefreshMembers();
    }

    /// <summary>Die Register-Mod, deren Ordner <paramref name="folder"/> ist (oder <see langword="null"/>).</summary>
    private ModInfo? RegistryMatch(string folder) =>
        _host.Registry.Mods.FirstOrDefault(m => m.Paths.Any(p => string.Equals(CompatSet.NormalizeFolder(p), CompatSet.NormalizeFolder(folder), StringComparison.OrdinalIgnoreCase)));

    private CompatSetMember MemberFor(string folder, CompatOrigin fallback)
    {
        var mod = RegistryMatch(folder);
        return mod is not null
            ? CompatMembers.FromFolder(folder, CompatOrigin.Register, mod.Id, _host.MetadataFile)
            : CompatMembers.FromFolder(folder, fallback, null, _host.MetadataFile);
    }

    private int AddFolders(IEnumerable<string> folders, CompatOrigin fallback)
    {
        var added = 0;
        foreach (var folder in folders)
        {
            if (Directory.Exists(folder) && _set.Add(MemberFor(folder, fallback)))
            {
                added++;
            }
        }

        if (added > 0)
        {
            SaveSet();
            RefreshMembers();
        }

        return added;
    }

    /// <summary>Fügt eine Register-Mod hinzu (auch von außen, z. B. aus dem Mods-Fenster).</summary>
    public void AddMod(ModInfo mod)
    {
        if (BranchTransfer.WorkingFolder(mod) is not { } folder)
        {
            MessageBox.Show(this, ModCompatWindowTexts.ModFolderNotFound(mod.Name), ModCompatWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_set.Add(CompatMembers.FromFolder(folder, CompatOrigin.Register, mod.Id, _host.MetadataFile)))
        {
            MessageBox.Show(this, ModCompatWindowTexts.AlreadyInSet(mod.Name, _set.Name), ModCompatWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SaveSet();
        RefreshMembers();
    }

    private static string AddNewModLabel => ModCompatWindowTexts.AddNewModLabel;

    private void AddFromRegister_Click(object sender, RoutedEventArgs e)
    {
        var candidates = ModProjects.Ordered(_host.Registry)
            .Select(o => o.Mod)
            .Where(m => BranchTransfer.WorkingFolder(m) is { } f && !_set.Contains(f))
            .ToList();

        var labels = candidates.Select(m => ModProjects.RoleText(_host.Registry, m) is { } role ? $"{m.Name} ({role})" : m.Name).ToList();
        labels.Add(AddNewModLabel);
        var newModIndex = labels.Count - 1;

        if (PickListDialog.Ask(this, ParadoxModTool.Compatch.CompatchTexts.FromRegister, ModCompatWindowTexts.FromRegisterPrompt(AddNewModLabel), labels, multi: true) is not { } picked)
        {
            return;
        }

        if (picked.Contains(newModIndex))
        {
            AddNewRegisteredMod();
        }

        AddFolders(picked.Where(i => i != newModIndex).Select(i => BranchTransfer.WorkingFolder(candidates[i])!), CompatOrigin.Register);
    }

    /// <summary>Legt eine noch nicht verwaltete Mod im Register an (wie <see cref="ModsWindow.AddMod_Click"/>) und fügt sie sofort dem Set hinzu.</summary>
    private void AddNewRegisteredMod()
    {
        if (PickFolder(ModsTexts.PickModFolder) is not { } folder)
        {
            return;
        }

        var name = TextPromptWindow.Ask(this, ModsTexts.AddModTitle, ModsTexts.ModNamePrompt, ModRegistry.SuggestName(folder, _host.MetadataFile));
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            AddMod(_host.Registry.Add(name, folder));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, ModCompatWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AddFromWorkshop_Click(object sender, RoutedEventArgs e)
    {
        if (_host.SteamAppId is not { } appId || SteamFolders.Find() is not { } steam)
        {
            MessageBox.Show(this, ModCompatWindowTexts.SteamNotFound, ModCompatWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var folders = CompatMembers.FindWorkshopFolders(appId, SteamFolders.LibraryFolders(steam)).Where(f => !_set.Contains(f)).ToList();
        if (folders.Count == 0)
        {
            MessageBox.Show(this, ModCompatWindowTexts.NoWorkshopMods, ModCompatWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var labels = folders.Select(f => $"{CompatMembers.FromFolder(f, CompatOrigin.Workshop, null, _host.MetadataFile).Name}  [{System.IO.Path.GetFileName(f)}]").ToList();
        if (PickListDialog.Ask(this, ParadoxModTool.Compatch.CompatchTexts.FromWorkshop, ModCompatWindowTexts.FromWorkshopPrompt, labels, multi: true) is not { } picked)
        {
            return;
        }

        AddFolders(picked.Select(i => folders[i]), CompatOrigin.Workshop);
    }

    private static string? PickFolder(string description)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = description, UseDescriptionForTitle = true };
        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("modcompat.folder");
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        if (PickFolder(ModsTexts.PickModFolder) is not { } folder)
        {
            return;
        }

        if (AddFolders(new[] { folder }, CompatOrigin.Folder) == 0)
        {
            MessageBox.Show(this, ModCompatWindowTexts.FolderAlreadyInSet, ModCompatWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ImportPlayset_Click(object sender, RoutedEventArgs e)
    {
        var file = _host.PlaysetsFile();
        var playsets = PlaysetStore.ListPlaysets(file);
        if (playsets.Count == 0)
        {
            MessageBox.Show(this, ModCompatWindowTexts.NoPlaysets, ModCompatWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (PickListDialog.Ask(this, ModCompatWindowTexts.ImportPlayset, ModCompatWindowTexts.ImportPlaysetPrompt, playsets.Select(p => p.DisplayText).ToList(), multi: false) is not [var index])
        {
            return;
        }

        var mods = PlaysetStore.ListPlaysetMods(file, playsets[index].Name).Where(m => m.IsEnabled).ToList();
        var added = 0;
        foreach (var mod in mods)
        {
            var folder = mod.Path.Replace('/', System.IO.Path.DirectorySeparatorChar);
            var origin = folder.Contains("workshop", StringComparison.OrdinalIgnoreCase) ? CompatOrigin.Workshop : CompatOrigin.Folder;
            added += AddFolders(new[] { folder }, origin);
        }

        MessageBox.Show(this, ModCompatWindowTexts.PlaysetImported(added, mods.Count), ModCompatWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
