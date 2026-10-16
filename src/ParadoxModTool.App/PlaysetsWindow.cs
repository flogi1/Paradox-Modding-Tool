using System.IO;
using System.Windows;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.Localization;
using Controls = System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Playset-Fenster: links die Playsets des Spiels, rechts Mods (mit Stichwortsuche) und DLCs des gewählten Playsets. Bearbeitet wird eine Kopie; gespeichert wird
/// mit "Speichern" über den <see cref="PlaysetManager"/> (Schutz, Prüfung, Sicherung).
/// </summary>
public sealed class PlaysetsWindow : Window
{
    private readonly PlaysetsHost _host;
    private readonly Controls.ListBox _playsetList = new() { MinHeight = 160 };
    private readonly Controls.TextBlock _modsHeader = new() { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.TextBox _search = new() { Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.ListBox _modList = new() { MinHeight = 160, SelectionMode = Controls.SelectionMode.Extended };
    private readonly Controls.ListBox _dlcList = new() { MinHeight = 70, MaxHeight = 150 };
    private readonly Controls.TextBlock _reorderHint = new() { Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.Button _newButton = Make(PlaysetWindowTexts.New);
    private readonly Controls.Button _copyButton = Make(PlaysetWindowTexts.Copy);
    private readonly Controls.Button _renameButton = Make(PlaysetWindowTexts.Rename);
    private readonly Controls.Button _deleteButton = Make(CommonTexts.Delete);
    private readonly Controls.Button _activeButton = Make(PlaysetWindowTexts.SetActive);
    private readonly Controls.Button _addButton = Make(PlaysetWindowTexts.AddMod);
    private readonly Controls.Button _upButton = Make(PlaysetWindowTexts.Up);
    private readonly Controls.Button _downButton = Make(PlaysetWindowTexts.Down);
    private readonly Controls.Button _removeButton = Make(CommonTexts.Remove);
    private readonly Controls.Button _saveButton = Make(CommonTexts.Save);
    private readonly Controls.Button _discardButton = Make(PlaysetWindowTexts.Discard);
    private readonly Controls.Button _backupsButton = Make(PlaysetWindowTexts.Backups);
    private readonly Controls.Button _startButton = Make(PlaysetWindowTexts.Start);

    private IReadOnlyList<PlaysetData> _playsets = Array.Empty<PlaysetData>();
    private PlaysetData? _working;
    private List<ModRow> _rows = new();
    private bool _dirty;
    private bool _loading;
    private bool _cloudHintShown;

    public PlaysetsWindow(PlaysetsHost host)
    {
        _host = host;
        Title = PlaysetWindowTexts.Title;
        Width = 980;
        Height = 640;
        MinWidth = 760;
        MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = BuildLayout();
        Wire();
        Reload();
        Loaded += (_, _) =>
        {
            if (_host.Manager() is null)
            {
                MessageBox.Show(this, PlaysetWindowTexts.NoFile, PlaysetWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        };
        Closing += (_, e) =>
        {
            if (!AskSaveIfDirty())
            {
                e.Cancel = true;
            }
        };
    }

    /// <summary>Liest die Playsets neu und wählt das mit dieser Id oder diesem Namen, sonst das der Werkzeugleiste, sonst das aktive, sonst das erste.</summary>
    public void Reload(string? selectId = null, string? selectName = null)
    {
        _loading = true;
        try
        {
            var manager = _host.Manager();
            _playsets = manager?.LoadAll() ?? Array.Empty<PlaysetData>();
            var items = _playsets.Select(p => new PlaysetItem(p)).ToList();
            _playsetList.ItemsSource = items;
            var wanted = (selectId is null ? null : items.FirstOrDefault(i => i.Data.Id == selectId))
                ?? (selectName is null ? null : items.FirstOrDefault(i => i.Data.Name == selectName))
                ?? items.FirstOrDefault(i => i.Data.Name == _host.SelectedPlaysetName())
                ?? items.FirstOrDefault(i => i.Data.IsActive)
                ?? items.FirstOrDefault();
            _playsetList.SelectedItem = wanted;
            _working = wanted?.Data.Clone();
            _dirty = false;
        }
        finally
        {
            _loading = false;
        }

        ShowWorking();
    }

    private static Controls.Button Make(string text) => new() { Content = new Controls.TextBlock { Text = text }, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 4) };

    private UIElement BuildLayout()
    {
        var root = new Controls.Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new Controls.RowDefinition { Height = GridLength.Auto });

        var body = new Controls.Grid();
        body.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(280) });
        body.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new Controls.DockPanel { Margin = new Thickness(0, 0, 12, 0) };
        var leftHeader = new Controls.TextBlock { Text = PlaysetWindowTexts.List, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
        Controls.DockPanel.SetDock(leftHeader, Controls.Dock.Top);
        left.Children.Add(leftHeader);
        var leftButtons = new Controls.WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        foreach (var button in new[] { _newButton, _copyButton, _renameButton, _deleteButton, _activeButton })
        {
            leftButtons.Children.Add(button);
        }

        Controls.DockPanel.SetDock(leftButtons, Controls.Dock.Bottom);
        left.Children.Add(leftButtons);
        left.Children.Add(_playsetList);
        Controls.Grid.SetColumn(left, 0);
        body.Children.Add(left);

        var right = new Controls.DockPanel();
        Controls.DockPanel.SetDock(_modsHeader, Controls.Dock.Top);
        right.Children.Add(_modsHeader);
        _search.ToolTip = PlaysetWindowTexts.Search;
        Controls.DockPanel.SetDock(_search, Controls.Dock.Top);
        right.Children.Add(_search);

        var dlcBox = new Controls.DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var dlcHeader = new Controls.TextBlock { Text = PlaysetWindowTexts.Dlcs, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        Controls.DockPanel.SetDock(dlcHeader, Controls.Dock.Top);
        dlcBox.Children.Add(dlcHeader);
        dlcBox.Children.Add(_dlcList);
        Controls.DockPanel.SetDock(dlcBox, Controls.Dock.Bottom);
        right.Children.Add(dlcBox);

        var modButtons = new Controls.WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        foreach (var button in new[] { _addButton, _upButton, _downButton, _removeButton })
        {
            modButtons.Children.Add(button);
        }

        modButtons.Children.Add(_reorderHint);
        Controls.DockPanel.SetDock(modButtons, Controls.Dock.Bottom);
        right.Children.Add(modButtons);
        right.Children.Add(_modList);
        Controls.Grid.SetColumn(right, 1);
        body.Children.Add(right);
        Controls.Grid.SetRow(body, 0);
        root.Children.Add(body);

        var bottom = new Controls.DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        var close = Make(CommonTexts.Close);
        close.Click += (_, _) => Close();
        Controls.DockPanel.SetDock(close, Controls.Dock.Right);
        bottom.Children.Add(close);
        var row = new Controls.WrapPanel();
        foreach (var button in new[] { _saveButton, _discardButton, _backupsButton, _startButton })
        {
            row.Children.Add(button);
        }

        bottom.Children.Add(row);
        Controls.Grid.SetRow(bottom, 1);
        root.Children.Add(bottom);
        _reorderHint.Text = PlaysetWindowTexts.ReorderHint;
        _reorderHint.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        return root;
    }

    private void Wire()
    {
        _playsetList.SelectionChanged += (_, _) => OnPlaysetSelected();
        _search.TextChanged += (_, _) => RefreshMods();
        _newButton.Click += (_, _) => NewPlayset();
        _copyButton.Click += (_, _) => CopyPlayset();
        _renameButton.Click += (_, _) => RenamePlayset();
        _deleteButton.Click += (_, _) => DeletePlayset();
        _activeButton.Click += (_, _) => SetActive();
        _addButton.Click += (_, _) => AddMods();
        _removeButton.Click += (_, _) => RemoveMods();
        _upButton.Click += (_, _) => Move(-1);
        _downButton.Click += (_, _) => Move(1);
        _saveButton.Click += (_, _) => Save();
        _discardButton.Click += (_, _) => Reload(selectId: _working?.Id);
        _backupsButton.Click += (_, _) => ShowBackups();
        _startButton.Click += (_, _) => Start();
    }

    /// <summary>Eine Zeile der Playset-Liste mit der Anzeige ("aktiv", "Konto").</summary>
    private sealed record PlaysetItem(PlaysetData Data)
    {
        public override string ToString()
        {
            var marks = new List<string>();
            if (Data.IsActive)
            {
                marks.Add(PlaysetWindowTexts.Active);
            }

            if (Data.IsCloud)
            {
                marks.Add(PlaysetWindowTexts.Cloud);
            }

            return marks.Count == 0 ? Data.Name : $"{Data.Name} ({string.Join(", ", marks)})";
        }
    }

    /// <summary>Eine Mod in der Bearbeitung.</summary>
    private sealed class ModRow
    {
        public ModRow(PlaysetModEntry entry)
        {
            Entry = entry;
            FolderMissing = entry.Path.Length == 0 || !Directory.Exists(entry.Path);
        }

        /// <summary>Ob der Ordner der Mod fehlt; einmal beim Anlegen der Zeile geprüft, nicht bei jedem Neuzeichnen der Liste.</summary>
        public bool FolderMissing { get; }

        public PlaysetModEntry Entry { get; set; }
    }

    private void OnPlaysetSelected()
    {
        if (_loading || _playsetList.SelectedItem is not PlaysetItem item)
        {
            return;
        }

        if (item.Data.Id == _working?.Id)
        {
            return;
        }

        var previous = _working;
        if (_dirty && !AskSaveIfDirty())
        {
            _loading = true;
            _playsetList.SelectedItem = _playsetList.ItemsSource.Cast<PlaysetItem>().FirstOrDefault(i => i.Data.Id == previous?.Id);
            _loading = false;
            return;
        }

        _playsets = _host.Manager()?.LoadAll() ?? _playsets;
        _working = (_playsets.FirstOrDefault(p => p.Id == item.Data.Id) ?? item.Data).Clone();
        _dirty = false;
        ShowWorking();
        _host.SelectPlayset(_working.Name);
    }

    /// <summary>Zeigt das bearbeitete Playset: Überschrift, Mods, DLCs und welche Knöpfe gelten.</summary>
    private void ShowWorking()
    {
        _rows = _working?.Mods.Select(m => new ModRow(m)).ToList() ?? new List<ModRow>();
        _modsHeader.Text = _working is null ? string.Empty : PlaysetWindowTexts.ModsHeader(_working.Name);
        RefreshMods();
        _dlcList.Items.Clear();
        var dlcs = _working?.Dlcs.ToList() ?? new List<PlaysetDlcEntry>();
        for (var position = 0; position < dlcs.Count; position++)
        {
            var dlc = dlcs[position];
            var index = position;
            var label = new Controls.TextBlock();
            var shown = _host.Manager()?.DlcName(dlc.Id) ?? dlc.Id;
            label.Inlines.Add(new System.Windows.Documents.Run(shown));
            if (!string.Equals(shown, dlc.Id, StringComparison.Ordinal))
            {
                var idRun = new System.Windows.Documents.Run("   " + dlc.Id);
                idRun.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "Theme.TextMuted");
                label.Inlines.Add(idRun);
            }

            var box = new Controls.CheckBox { Content = label, IsChecked = dlc.Enabled, Margin = new Thickness(0, 1, 0, 1) };
            box.Click += (_, _) =>
            {
                _working!.Dlcs[index] = _working.Dlcs[index] with { Enabled = box.IsChecked == true };
                MarkDirty();
            };
            _dlcList.Items.Add(box);
        }

        var has = _working is not null;
        foreach (var button in new[] { _copyButton, _renameButton, _deleteButton, _addButton, _removeButton, _upButton, _downButton })
        {
            button.IsEnabled = has;
        }

        _activeButton.IsEnabled = has && !_working!.IsActive;
        _saveButton.IsEnabled = false;
        _discardButton.IsEnabled = false;
        _startButton.Visibility = _host.CanStart() ? Visibility.Visible : Visibility.Collapsed;
        _startButton.IsEnabled = has;
        _newButton.IsEnabled = _host.Manager() is not null;
        _backupsButton.IsEnabled = _host.Manager() is not null;
        UpdateReorderState();
    }

    private void MarkDirty()
    {
        _dirty = true;
        _saveButton.IsEnabled = true;
        _discardButton.IsEnabled = true;
    }

    private bool Searching => _search.Text.Trim().Length > 0;

    private void UpdateReorderState()
    {
        _upButton.IsEnabled = _downButton.IsEnabled = _working is not null && !Searching;
        _reorderHint.Visibility = _working is not null && Searching ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshMods()
    {
        var selected = _modList.SelectedItems.Cast<Controls.ListBoxItem>().Select(i => (ModRow)i.Tag).ToHashSet();
        _modList.Items.Clear();
        foreach (var row in _rows.Where(r => PlaysetModFilter.Matches(r.Entry, _search.Text)))
        {
            var item = new Controls.ListBoxItem { Tag = row, Content = BuildRow(row) };
            _modList.Items.Add(item);
            if (selected.Contains(row))
            {
                item.IsSelected = true;
            }
        }

        UpdateReorderState();
    }

    private UIElement BuildRow(ModRow row)
    {
        var missing = row.FolderMissing;
        var name = row.Entry.DisplayName ?? Path.GetFileName(PlaysetGuard.NormalizePath(row.Entry.Path));
        var text = new Controls.TextBlock { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = row.Entry.Path };
        text.Inlines.Add(new System.Windows.Documents.Run(string.IsNullOrEmpty(name) ? "?" : name));
        var detail = new System.Windows.Documents.Run("   " + row.Entry.Path + (missing ? "  (" + PlaysetWindowTexts.FolderMissing + ")" : string.Empty));
        detail.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "Theme.TextMuted");
        text.Inlines.Add(detail);
        var box = new Controls.CheckBox { Content = text, IsChecked = row.Entry.Enabled };
        box.Click += (_, _) =>
        {
            row.Entry = row.Entry with { Enabled = box.IsChecked == true };
            MarkDirty();
        };
        return box;
    }

    private void AddMods()
    {
        if (_working is null || _host.Manager() is not { } manager)
        {
            return;
        }

        var known = manager.Backend is LauncherPlaysetBackend launcher ? launcher.InstalledMods() : Array.Empty<PlaysetModEntry>();
        var inside = _rows.Select(r => PlaysetGuard.NormalizePath(r.Entry.Path).Replace('/', '\\')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = PlaysetInstalledMods.Collect(known, manager.GameModFolder, _playsets, manager.UsesLauncherDatabase)
            .Where(m => !inside.Contains(PlaysetGuard.NormalizePath(m.Path).Replace('/', '\\')))
            .ToList();
        if (candidates.Count == 0)
        {
            MessageBox.Show(this, PlaysetWindowTexts.NoMods, PlaysetWindowTexts.AddTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var labels = candidates.Select(m => $"{m.DisplayName}   [{m.Path}]").ToList();
        if (PickListDialog.Ask(this, PlaysetWindowTexts.AddTitle, PlaysetWindowTexts.AddMessage, labels, multi: true) is not { } picked)
        {
            return;
        }

        foreach (var index in picked)
        {
            _rows.Add(new ModRow(candidates[index]));
        }

        MarkDirty();
        RefreshMods();
    }

    private void RemoveMods()
    {
        var selected = _modList.SelectedItems.Cast<Controls.ListBoxItem>().Select(i => (ModRow)i.Tag).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        _rows.RemoveAll(selected.Contains);
        MarkDirty();
        RefreshMods();
    }

    private void Move(int direction)
    {
        if (Searching || _modList.SelectedItems.Count != 1)
        {
            return;
        }

        var row = (ModRow)((Controls.ListBoxItem)_modList.SelectedItem).Tag;
        var index = _rows.IndexOf(row);
        var target = index + direction;
        if (target < 0 || target >= _rows.Count)
        {
            return;
        }

        _rows.RemoveAt(index);
        _rows.Insert(target, row);
        MarkDirty();
        RefreshMods();
    }

    /// <summary>Das bearbeitete Playset mit den Mods aus der Liste.</summary>
    private PlaysetData Current()
    {
        var data = _working!.Clone();
        data.Mods = _rows.Select(r => r.Entry).ToList();
        return data;
    }

    private bool Save()
    {
        if (_working is null || _host.Manager() is not { } manager)
        {
            return true;
        }

        if (!ConfirmCloud(_working) || !_host.EnsureGameExecutable())
        {
            return false;
        }

        manager = _host.Manager() ?? manager;
        var data = Current();
        var result = manager.Save(data);
        if (!result.Success)
        {
            Fail(result);
            return false;
        }

        _host.Changed();
        Reload(selectId: data.Id, selectName: data.Name);
        return true;
    }

    /// <summary>Fragt bei ungespeicherten Änderungen nach (Ja = speichern, Nein = verwerfen, Abbrechen = nichts tun); false = abbrechen.</summary>
    private bool AskSaveIfDirty()
    {
        if (!_dirty || _working is null)
        {
            return true;
        }

        switch (MessageBox.Show(this, PlaysetWindowTexts.Unsaved(_working.Name), PlaysetWindowTexts.Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
        {
            case MessageBoxResult.Yes:
                return Save();
            case MessageBoxResult.No:
                _dirty = false;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Einmal je Fenster der Hinweis, dass ein mit dem Paradox-Konto verknüpftes Playset nur lokal geändert wird; false = der Nutzer will nicht.</summary>
    private bool ConfirmCloud(PlaysetData playset)
    {
        if (!playset.IsCloud || _cloudHintShown)
        {
            return true;
        }

        if (MessageBox.Show(this, PlaysetWindowTexts.CloudHint(playset.Name), PlaysetWindowTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return false;
        }

        _cloudHintShown = true;
        return true;
    }

    private void Fail(PlaysetResult result) =>
        MessageBox.Show(this, PlaysetTexts.Describe(result), CommonTexts.Error, MessageBoxButton.OK, MessageBoxImage.Warning);

    private void NewPlayset()
    {
        if (!AskSaveIfDirty() || _host.Manager() is not { } manager)
        {
            return;
        }

        if (TextPromptWindow.Ask(this, PlaysetWindowTexts.Title, PlaysetWindowTexts.NewPrompt, string.Empty)?.Trim() is not { Length: > 0 } name || !_host.EnsureGameExecutable())
        {
            return;
        }

        manager = _host.Manager() ?? manager;
        var result = manager.CreateEmpty(name);
        if (!result.Success)
        {
            Fail(result);
            return;
        }

        _host.Changed();
        Reload(selectName: name);
    }

    private void CopyPlayset()
    {
        if (!AskSaveIfDirty() || _working is null || _host.Manager() is not { } manager)
        {
            return;
        }

        if (TextPromptWindow.Ask(this, PlaysetWindowTexts.Title, PlaysetWindowTexts.CopyPrompt, _working.Name + " 2")?.Trim() is not { Length: > 0 } name || !_host.EnsureGameExecutable())
        {
            return;
        }

        manager = _host.Manager() ?? manager;
        var result = manager.Copy(_working.Id, name);
        if (!result.Success)
        {
            Fail(result);
            return;
        }

        _host.Changed();
        Reload(selectName: name);
    }

    private void RenamePlayset()
    {
        if (!AskSaveIfDirty() || _working is null || _host.Manager() is not { } manager)
        {
            return;
        }

        if (TextPromptWindow.Ask(this, PlaysetWindowTexts.Title, PlaysetWindowTexts.RenamePrompt, _working.Name)?.Trim() is not { Length: > 0 } name || name == _working.Name
            || !ConfirmCloud(_working) || !_host.EnsureGameExecutable())
        {
            return;
        }

        manager = _host.Manager() ?? manager;
        var data = _working.Clone();
        data.Name = name;
        var result = manager.Save(data);
        if (!result.Success)
        {
            Fail(result);
            return;
        }

        _host.Changed();
        Reload(selectName: name);
    }

    private void DeletePlayset()
    {
        if (_working is null || _host.Manager() is not { } manager)
        {
            return;
        }

        if (MessageBox.Show(this, PlaysetWindowTexts.DeleteConfirm(_working.Name), PlaysetWindowTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes
            || !ConfirmCloud(_working) || !_host.EnsureGameExecutable())
        {
            return;
        }

        manager = _host.Manager() ?? manager;
        var result = manager.Delete(_working.Id);
        if (!result.Success)
        {
            Fail(result);
            return;
        }

        _host.Changed();
        Reload();
    }

    private void SetActive()
    {
        if (!AskSaveIfDirty() || _working is null || _host.Manager() is not { } manager)
        {
            return;
        }

        if (!ConfirmCloud(_working) || !_host.EnsureGameExecutable())
        {
            return;
        }

        manager = _host.Manager() ?? manager;
        var id = _working.Id;
        var result = manager.SetActive(id);
        if (!result.Success)
        {
            Fail(result);
        }

        _host.Changed();
        Reload(selectId: id);
    }

    private void ShowBackups()
    {
        if (_host.Manager() is not { } manager)
        {
            return;
        }

        var menu = new Controls.ContextMenu();
        var sets = manager.BackupSets();
        if (sets.Count == 0)
        {
            menu.Items.Add(new Controls.MenuItem { Header = PlaysetWindowTexts.NoBackups, IsEnabled = false });
        }

        foreach (var set in sets)
        {
            var name = set;
            var item = new Controls.MenuItem { Header = name.Replace("_", "__") };
            item.Click += (_, _) => Restore(manager, name);
            menu.Items.Add(item);
        }

        menu.PlacementTarget = _backupsButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        menu.IsOpen = true;
    }

    private void Restore(PlaysetManager manager, string set)
    {
        if (!AskSaveIfDirty() || MessageBox.Show(this, PlaysetWindowTexts.RestoreConfirm(set), PlaysetWindowTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        if (!_host.EnsureGameExecutable())
        {
            return;
        }

        manager = _host.Manager() ?? manager;
        var result = manager.RestoreBackup(set);
        if (!result.Success)
        {
            Fail(result);
            return;
        }

        _host.Changed();
        Reload(selectId: _working?.Id);
        MessageBox.Show(this, PlaysetWindowTexts.Restored, PlaysetWindowTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Start()
    {
        if (!AskSaveIfDirty() || _working is null)
        {
            return;
        }

        _host.StartGame(_working.Id);
        Reload(selectId: _working.Id);
    }
}
