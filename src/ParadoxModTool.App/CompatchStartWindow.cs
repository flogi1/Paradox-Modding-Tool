using System.IO;
using System.Windows;
using ParadoxModTool.Compatch;
using ParadoxModTool.Mods;
using Controls = System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Startdialog des Kompatch-Merges (Spec, "Oberfläche", Startdialog).</summary>
public sealed class CompatchStartWindow : Window
{
    private static readonly string[] Kinds = CompatchFileFilter.DefaultHiddenExtensions.Values.Distinct().ToArray();

    private readonly CompatchHost _host;
    private PickedMod? _a;
    private PickedMod? _b;
    private readonly Controls.TextBlock _aText = new() { VerticalAlignment = System.Windows.VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Controls.TextBlock _bText = new() { VerticalAlignment = System.Windows.VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Controls.CheckBox _bAfterA = new() { IsChecked = true, Content = CompatchTexts.BLoadsAfterA, Margin = new Thickness(0, 4, 0, 8) };
    private readonly Controls.RadioButton _new = new() { Content = CompatchTexts.TargetNew, GroupName = "target", IsChecked = true };
    private readonly Controls.TextBox _name = new() { Margin = new Thickness(20, 2, 0, 6) };
    private readonly Controls.RadioButton _existing = new() { Content = CompatchTexts.TargetExisting, GroupName = "target" };
    private readonly Controls.ComboBox _existingCombo = new() { Margin = new Thickness(20, 2, 0, 6), DisplayMemberPath = "Name" };
    private readonly Controls.RadioButton _base = new() { Content = CompatchTexts.TargetBase, GroupName = "target" };
    private readonly Controls.ComboBox _baseCombo = new() { Margin = new Thickness(20, 2, 0, 6), DisplayMemberPath = "Name" };
    private readonly Controls.CheckBox _mainAsBase = new() { Content = CompatchTexts.MainAsBase, ToolTip = CompatchTexts.MainAsBaseTip, Margin = new Thickness(20, 0, 0, 2), IsEnabled = false };
    private readonly Controls.CheckBox _fillMissing = new() { Content = CompatchTexts.FillMissing, ToolTip = CompatchTexts.FillMissingTip, Margin = new Thickness(20, 0, 0, 6), IsEnabled = false };
    private readonly Controls.RadioButton _standard = new() { Content = CompatchTexts.WriteStandard, GroupName = "mode" };
    private readonly Controls.RadioButton _cautious = new() { Content = CompatchTexts.WriteCautious, GroupName = "mode" };
    private readonly Dictionary<string, Controls.CheckBox> _kinds = new();
    private readonly Controls.CheckBox _remember = new() { Content = CompatchTexts.RememberDefault, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Controls.CheckBox _skipLocked = new() { Content = CompatchTexts.SkipLocked, Margin = new Thickness(0, 8, 0, 0) };
    private (CompatchState, CompatchStateStore)? _result;

    private CompatchStartWindow(CompatchHost host, CompatchPreset preset)
    {
        _host = host;
        Title = CompatchTexts.StartTitle;
        Width = 620;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new Controls.StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(SideRow(CompatchTexts.ModA, _aText, picked => _a = picked));
        panel.Children.Add(SideRow(CompatchTexts.ModB, _bText, picked => _b = picked));
        panel.Children.Add(_bAfterA);

        panel.Children.Add(Header(CompatchTexts.TargetHeader));
        panel.Children.Add(_new);
        panel.Children.Add(_name);
        panel.Children.Add(_existing);
        panel.Children.Add(_existingCombo);
        panel.Children.Add(_base);
        panel.Children.Add(_baseCombo);
        panel.Children.Add(_mainAsBase);
        panel.Children.Add(_fillMissing);
        _base.ToolTip = CompatchTexts.TargetBaseTip;
        Controls.ToolTipService.SetShowOnDisabled(_mainAsBase, true);
        Controls.ToolTipService.SetShowOnDisabled(_fillMissing, true);
        _base.Checked += (_, _) => UpdateMainOptions();
        _base.Unchecked += (_, _) => UpdateMainOptions();

        panel.Children.Add(Header(CompatchTexts.WriteModeHeader));
        panel.Children.Add(_standard);
        panel.Children.Add(_cautious);
        (host.Defaults.Cautious ? _cautious : _standard).IsChecked = true;

        panel.Children.Add(Header(CompatchTexts.FilterHeader));
        var kinds = new Controls.WrapPanel();
        foreach (var kind in Kinds)
        {
            var box = new Controls.CheckBox { Content = kind, IsChecked = host.Defaults.HiddenKinds.Contains(kind), Margin = new Thickness(0, 0, 12, 0) };
            _kinds[kind] = box;
            kinds.Children.Add(box);
        }

        panel.Children.Add(kinds);
        panel.Children.Add(_remember);
        _skipLocked.IsChecked = host.Defaults.SkipLocked;
        panel.Children.Add(_skipLocked);

        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var start = new Controls.Button { Content = CompatchTexts.Start, Width = 100, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        start.Click += (_, _) => StartClicked();
        buttons.Children.Add(start);
        buttons.Children.Add(new Controls.Button { Content = CompatchTexts.Cancel, Width = 100, IsCancel = true });
        panel.Children.Add(buttons);
        Content = panel;

        // Ohne Vorgabe (Menü) die Angaben des letzten Starts übernehmen; eine Vorgabe (Mods-Fenster, Kompatibilitätsfund) hat Vorrang.
        if (host.LastStart is { } last)
        {
            (last.Cautious ? _cautious : _standard).IsChecked = true;
            _skipLocked.IsChecked = last.SkipLocked;
            _mainAsBase.IsChecked = last.MainReplacesVanilla;
            _fillMissing.IsChecked = last.FillMissingFromMain;
            if (last.HiddenKinds is { } hidden)
            {
                foreach (var (kind, box) in _kinds)
                {
                    box.IsChecked = hidden.Contains(kind);
                }
            }
        }

        var memory = preset.FolderA is null && preset.FolderB is null && preset.TargetModId is null ? host.LastStart : null;
        if (memory is not null)
        {
            preset = new CompatchPreset(memory.FolderA, memory.FolderB, memory.TargetMode == "existing" ? memory.TargetModId : null);
            _bAfterA.IsChecked = memory.BLoadsAfterA;
            if (!string.IsNullOrWhiteSpace(memory.NewName))
            {
                _name.Text = memory.NewName;
            }
        }

        if (preset.FolderA is { } a && Directory.Exists(a))
        {
            _a = CompatchModPicker.Describe(host.Pick, a);
        }

        if (preset.FolderB is { } b && Directory.Exists(b))
        {
            _b = CompatchModPicker.Describe(host.Pick, b);
        }

        RefreshSides();
        if (preset.TargetModId is { } targetId && host.Registry.Get(targetId) is { } target)
        {
            _existing.IsChecked = true;
            _existingCombo.SelectedItem = _existingCombo.Items.Cast<ModInfo>().FirstOrDefault(m => m.Id == target.Id);
        }
        else if (memory is { TargetMode: "base", TargetModId: { } baseId } && _baseCombo.Items.Cast<ModInfo>().FirstOrDefault(m => m.Id == baseId) is { } baseMod)
        {
            _base.IsChecked = true;
            _baseCombo.SelectedItem = baseMod;
        }

        UpdateMainOptions();
    }

    public static (CompatchState State, CompatchStateStore Store)? Ask(Window owner, CompatchHost host, CompatchPreset preset)
    {
        var window = new CompatchStartWindow(host, preset) { Owner = owner };
        return window.ShowDialog() == true ? window._result : null;
    }

    private static Controls.TextBlock Header(string text) => new() { Text = text, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 4) };

    /// <summary>Plan 4, Ruling 10: die Hauptmod-Optionen gelten nur für die Zielart "In eine der beiden Mods schreiben".</summary>
    private void UpdateMainOptions()
    {
        _mainAsBase.IsEnabled = _base.IsChecked == true;
        _fillMissing.IsEnabled = _base.IsChecked == true;
    }

    private Controls.DockPanel SideRow(string label, Controls.TextBlock display, Action<PickedMod> set)
    {
        var row = new Controls.DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var caption = new Controls.TextBlock { Text = label, Width = 60, FontWeight = FontWeights.Bold, VerticalAlignment = System.Windows.VerticalAlignment.Center };
        Controls.DockPanel.SetDock(caption, Controls.Dock.Left);
        row.Children.Add(caption);
        var pickers = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        Controls.DockPanel.SetDock(pickers, Controls.Dock.Right);
        void Add(string text, Func<PickedMod?> pick)
        {
            var button = new Controls.Button { Content = text, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(4, 0, 0, 0) };
            button.Click += (_, _) =>
            {
                if (pick() is { } picked)
                {
                    set(picked);
                    RefreshSides();
                }
            };
            pickers.Children.Add(button);
        }

        Add(CompatchTexts.FromRegister, () => CompatchModPicker.FromRegister(this, _host.Pick));
        Add(CompatchTexts.FromWorkshop, () => CompatchModPicker.FromWorkshop(this, _host.Pick));
        Add(CompatchTexts.FromFolder, () => CompatchModPicker.FromFolder(this, _host.Pick));
        row.Children.Add(pickers);
        row.Children.Add(display);
        return row;
    }

    private void RefreshSides()
    {
        _aText.Text = _a is null ? CompatchTexts.NotChosen : $"{_a.Name}  ({_a.Folder})";
        _bText.Text = _b is null ? CompatchTexts.NotChosen : $"{_b.Name}  ({_b.Folder})";
        if (_a is not null && _b is not null && (_name.Text.Length == 0 || _name.Tag as string == _name.Text))
        {
            _name.Text = CompatchTexts.DefaultName(_a.Name, _b.Name);
            _name.Tag = _name.Text;
        }

        // Bestehende Ziele: Kompatchs, die A und B abdecken, zuerst; dann alle übrigen eigenen Mods (keine Workshop-Ordner, nicht A oder B).
        var own = _host.Registry.Mods
            .Where(m => BranchTransfer.WorkingFolder(m) is { } f && !CompatchTargets.IsWorkshopFolder(f))
            .Where(m => m.Id != _a?.ModId && m.Id != _b?.ModId)
            .OrderByDescending(m => _a is not null && _b is not null && CompatchTargets.Covers(_host.Registry, m, _a.ModId, _a.Folder) && CompatchTargets.Covers(_host.Registry, m, _b.ModId, _b.Folder))
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var keepExisting = (_existingCombo.SelectedItem as ModInfo)?.Id;
        _existingCombo.ItemsSource = own;
        _existingCombo.SelectedItem = own.FirstOrDefault(m => m.Id == keepExisting) ?? own.FirstOrDefault();
        var bases = new[] { _a, _b }.Where(p => p?.ModId is not null && !CompatchTargets.IsWorkshopFolder(p.Folder)).Select(p => _host.Registry.Get(p!.ModId!)!).ToList();
        _baseCombo.ItemsSource = bases;
        _baseCombo.SelectedItem = bases.FirstOrDefault();
    }

    private void Fail(string message) => MessageBox.Show(this, message, CompatchTexts.StartTitle, MessageBoxButton.OK, MessageBoxImage.Warning);

    private void StartClicked()
    {
        if (_a is null || _b is null)
        {
            Fail(CompatchTexts.ErrorPickBoth);
            return;
        }

        if (ModPaths.AreEqual(_a.Folder, _b.Folder))
        {
            Fail(CompatchTexts.ErrorSame);
            return;
        }

        foreach (var side in new[] { _a, _b })
        {
            if (!Directory.Exists(side.Folder))
            {
                Fail(CompatchTexts.ErrorMissingFolder(side.Folder));
                return;
            }
        }

        if (_host.VanillaRoot is null || !Directory.Exists(_host.VanillaRoot))
        {
            Fail(CompatchTexts.ErrorNoVanilla);
            return;
        }

        var targets = new[] { _a.ToTarget(), _b.ToTarget() };
        ModInfo target;
        PatchTarget kind;
        try
        {
            if (_new.IsChecked == true)
            {
                if (_host.ModRoot is null || !Directory.Exists(_host.ModRoot))
                {
                    Fail(CompatchTexts.ErrorNoModRoot);
                    return;
                }

                var created = NewCompatch.Create(_host.ModRoot, _name.Text, _host.MetadataFile,
                    new[] { _a, _b }.Select(p => new NewCompatchTarget(p.Name, ModMetadata.Read(p.Folder, _host.MetadataFile)?.Id)).ToList(),
                    ModMetadata.Read(_a.Folder, _host.MetadataFile)?.SupportedGameVersion);
                target = _host.Registry.Add(_name.Text.Trim(), created.Folder);
                _host.Registry.SetCompatch(target.Id, targets);
                if (created.WithoutDependency.Count > 0)
                {
                    MessageBox.Show(this, CompatchTexts.NoDependency(string.Join(", ", created.WithoutDependency)), CompatchTexts.StartTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                }

                kind = PatchTarget.Compatch;
            }
            else if (_existing.IsChecked == true)
            {
                if (_existingCombo.SelectedItem is not ModInfo chosen)
                {
                    Fail(CompatchTexts.ErrorNoTarget);
                    return;
                }

                target = chosen;
                if (!target.IsCompatch)
                {
                    if (MessageBox.Show(this, CompatchTexts.AskMakeCompatch(target.Name), CompatchTexts.StartTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    _host.Registry.SetCompatch(target.Id, targets);
                }

                kind = PatchTarget.Compatch;
            }
            else
            {
                if (_baseCombo.SelectedItem is not ModInfo chosen)
                {
                    Fail(CompatchTexts.ErrorBaseNotOwn);
                    return;
                }

                target = chosen;
                kind = chosen.Id == _a.ModId ? PatchTarget.BaseModA : PatchTarget.BaseModB;
            }
        }
        catch (Exception ex) when (ex is CompatchInputException or InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            Fail(ex.Message);
            return;
        }

        _host.RegistryChanged();
        var folder = BranchTransfer.WorkingFolder(target);
        if (folder is null || CompatchTargets.IsWorkshopFolder(folder))
        {
            Fail(folder is null ? CompatchTexts.ErrorMissingFolder(target.Name) : CompatchTexts.ErrorWorkshopTarget);
            return;
        }

        var store = new CompatchStateStore(CompatchStateStore.DirectoryFor(_host.GameDirectory, target.Id));
        var state = store.Load();
        if (state is not null && !(ModPaths.AreEqual(state.ModAFolder, _a.Folder) && ModPaths.AreEqual(state.ModBFolder, _b.Folder)))
        {
            if (MessageBox.Show(this, CompatchTexts.AskReplaceState, CompatchTexts.StartTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            state = null;
        }

        state ??= new CompatchState();
        state.ModAFolder = _a.Folder;
        state.ModBFolder = _b.Folder;
        state.ModAId = _a.ModId;
        state.ModBId = _b.ModId;
        state.ModAName = _a.Name;
        state.ModBName = _b.Name;
        state.BLoadsAfterA = _bAfterA.IsChecked == true;
        state.Target = kind;
        state.TargetFolder = folder;
        state.TargetModId = target.Id;
        state.Cautious = _cautious.IsChecked == true;
        state.SkipLocked = _skipLocked.IsChecked == true;
        state.HiddenKinds = _kinds.Where(k => k.Value.IsChecked == true).Select(k => k.Key).ToList();
        var baseTarget = kind is PatchTarget.BaseModA or PatchTarget.BaseModB;
        state.MainReplacesVanilla = baseTarget && _mainAsBase.IsChecked == true;
        state.FillMissingFromMain = baseTarget && _fillMissing.IsChecked == true;
        if (_remember.IsChecked == true)
        {
            _host.RememberHiddenKinds(state.HiddenKinds);
        }

        store.Save(state);
        _host.RememberStart(new Session.CompatchStartMemory
        {
            FolderA = _a.Folder,
            FolderB = _b.Folder,
            TargetMode = _base.IsChecked == true ? "base" : "existing",
            TargetModId = target.Id,
            NewName = _name.Text.Trim(),
            BLoadsAfterA = _bAfterA.IsChecked == true,
            Cautious = _cautious.IsChecked == true,
            SkipLocked = _skipLocked.IsChecked == true,
            HiddenKinds = state.HiddenKinds.ToList(),
            MainReplacesVanilla = _mainAsBase.IsChecked == true,
            FillMissingFromMain = _fillMissing.IsChecked == true,
        });
        _result = (state, store);
        DialogResult = true;
    }
}
