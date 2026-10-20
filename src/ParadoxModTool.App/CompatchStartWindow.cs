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

    /// <summary>Welche Vanilla-Ordner A und B mit replace_path ausblenden; unsichtbar, wenn keine.</summary>
    private readonly Controls.TextBlock _hiddenVanilla = new() { Margin = new Thickness(60, 2, 0, 4), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private readonly Controls.CheckBox _bAfterA = new() { IsChecked = true, Content = new Controls.TextBlock { Text = CompatchTexts.BLoadsAfterA }, Margin = new Thickness(0, 4, 0, 8) };
    private readonly Controls.RadioButton _new = new() { Content = CompatchTexts.TargetNew, GroupName = "target", IsChecked = true };
    private readonly Controls.TextBox _name = new() { Margin = new Thickness(20, 2, 0, 6) };
    private readonly Controls.RadioButton _existing = new() { Content = CompatchTexts.TargetExisting, GroupName = "target" };
    private readonly Controls.ComboBox _existingCombo = new() { Margin = new Thickness(20, 2, 0, 6), DisplayMemberPath = "Name" };
    private readonly Controls.RadioButton _base = new() { Content = CompatchTexts.TargetBase, GroupName = "target" };
    private readonly Controls.ComboBox _baseCombo = new() { Margin = new Thickness(20, 2, 0, 6), DisplayMemberPath = "Name" };
    private readonly Controls.CheckBox _mainAsBase = new() { Content = new Controls.TextBlock { Text = CompatchTexts.MainAsBase }, ToolTip = CompatchTexts.MainAsBaseTip, Margin = new Thickness(20, 0, 0, 2), IsEnabled = false };
    private readonly Controls.CheckBox _fillMissing = new() { Content = new Controls.TextBlock { Text = CompatchTexts.FillMissing }, ToolTip = CompatchTexts.FillMissingTip, Margin = new Thickness(20, 0, 0, 6), IsEnabled = false };
    private readonly Controls.RadioButton _standard = new() { Content = CompatchTexts.WriteStandard, GroupName = "mode" };
    private readonly Controls.RadioButton _cautious = new() { Content = CompatchTexts.WriteCautious, GroupName = "mode" };
    private readonly Dictionary<string, Controls.CheckBox> _kinds = new();
    private readonly Controls.CheckBox _remember = new() { Content = new Controls.TextBlock { Text = CompatchTexts.RememberDefault }, ToolTip = CompatchTexts.RememberDefaultTip, Margin = new Thickness(0, 8, 0, 0) };
    private readonly Controls.CheckBox _skipLocked = new() { Content = new Controls.TextBlock { Text = CompatchTexts.SkipLocked }, Margin = new Thickness(0, 8, 0, 0) };
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
        AddResume(panel);
        panel.Children.Add(Header(CompatchTexts.NewStartHeader));
        panel.Children.Add(SideRow(CompatchTexts.ModA, _aText, picked => _a = picked));
        panel.Children.Add(SideRow(CompatchTexts.ModB, _bText, picked => _b = picked));
        _hiddenVanilla.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        panel.Children.Add(_hiddenVanilla);
        _bAfterA.Click += (_, _) => RefreshHiddenVanilla();
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
        // Standard: Liste untereinander; nebeneinander über Einstellungen > Komp-Patch (Nutzerwunsch 2026-09-30).
        var sideBySide = host.Defaults.KindsSideBySide;
        Controls.Panel kinds = sideBySide ? new Controls.WrapPanel() : new Controls.StackPanel();
        foreach (var kind in Kinds)
        {
            var box = new Controls.CheckBox
            {
                Content = new Controls.TextBlock { Text = CompatchTexts.FileKind(kind) },
                IsChecked = host.Defaults.HiddenKinds.Contains(kind),
                Margin = sideBySide ? new Thickness(0, 0, 12, 0) : new Thickness(0, 0, 0, 2),
            };
            _kinds[kind] = box;
            kinds.Children.Add(box);
        }

        panel.Children.Add(kinds);
        _skipLocked.IsChecked = host.Defaults.SkipLocked;
        panel.Children.Add(_skipLocked);
        panel.Children.Add(_remember);

        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var start = new Controls.Button { Content = CompatchTexts.Start, Width = 100, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        start.Click += (_, _) => StartClicked();
        buttons.Children.Add(start);
        buttons.Children.Add(new Controls.Button { Content = CompatchTexts.Cancel, Width = 100, IsCancel = true });
        panel.Children.Add(buttons);
        Content = panel;

        // Optionen kommen aus "Standard für neue Arbeitsstände" (Einstellungsfenster), damit dort eingestellte Werte auch gelten.
        _mainAsBase.IsChecked = host.Defaults.MainAsBase;
        _fillMissing.IsChecked = host.Defaults.FillMissing;

        // Ohne Vorgabe (Menü) Mods, Ziel und Dateiarten des letzten Starts übernehmen; eine Vorgabe (Mods-Fenster, Kompatibilitätsfund) hat Vorrang.
        if (host.LastStart is { } last)
        {
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

    /// <summary>Ein gespeicherter Arbeitsstand in der Liste "Arbeitsstand fortsetzen".</summary>
    private sealed record ResumeItem(SavedCompatch Saved, string Text)
    {
        public override string ToString() => Text;
    }

    /// <summary>
    /// Oben im Fenster: alle gespeicherten Arbeitsstände des Spiels (Nutzerwunsch). "Fortsetzen" oder Doppelklick öffnet das Merge-Fenster mit
    /// Entscheidungen, übernommenen Blöcken und Filtern, ohne die Angaben darunter neu zu wählen.
    /// </summary>
    private void AddResume(Controls.StackPanel panel)
    {
        panel.Children.Add(Header(CompatchTexts.ResumeHeader));
        var none = new Controls.TextBlock { Text = CompatchTexts.ResumeNone, Margin = new Thickness(0, 0, 0, 8) };
        none.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        var list = new Controls.ListBox { MaxHeight = 150, Margin = new Thickness(0, 0, 0, 4), ToolTip = CompatchTexts.ResumeTip };
        var resume = new Controls.Button { Content = new Controls.TextBlock { Text = CompatchTexts.Resume }, Padding = new Thickness(12, 2, 12, 2), HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8), IsEnabled = false };

        void Fill()
        {
            var saved = CompatchStateStore.Saved(_host.GameDirectory);
            var installed = InstalledVersion();
            list.ItemsSource = saved.Select(s => new ResumeItem(s, s.State.Target == PatchTarget.UpdateMod
                ? VanillaUpdateTexts.SavedKind(s.State.UpdateNewVersion ?? string.Empty) + ": " + s.State.ModBName
                    + " (" + s.SavedUtc.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture) + ")"
                    + (CompatchStateStore.IsOutdatedUpdate(s.State, installed) ? " – " + VanillaUpdateTexts.SavedOutdated : string.Empty)
                : CompatchTexts.ResumeEntry(
                    s.State.ModAName,
                    s.State.ModBName,
                    s.State.TargetModId is { } id && _host.Registry.Get(id) is { } target ? target.Name : Path.GetFileName(s.State.TargetFolder),
                    s.SavedUtc.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture)))).ToList();
            var any = saved.Count > 0;
            none.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
            list.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            resume.Visibility = list.Visibility;
            resume.IsEnabled = false;
        }

        // Rechtsklick wählt den Eintrag unter der Maus, damit das Menü für ihn gilt.
        list.PreviewMouseRightButtonDown += (_, e) =>
        {
            for (var node = e.OriginalSource as DependencyObject; node is not null; node = System.Windows.Media.VisualTreeHelper.GetParent(node))
            {
                if (node is Controls.ListBoxItem item)
                {
                    item.IsSelected = true;
                    break;
                }
            }
        };
        var delete = new Controls.MenuItem { Header = CompatchTexts.ResumeDelete };
        delete.Click += (_, _) =>
        {
            if (list.SelectedItem is ResumeItem item && DeleteSaved(item))
            {
                Fill();
            }
        };
        var menu = new Controls.ContextMenu();
        menu.Items.Add(delete);
        menu.Opened += (_, _) => delete.IsEnabled = list.SelectedItem is ResumeItem;
        list.ContextMenu = menu;

        list.SelectionChanged += (_, _) => resume.IsEnabled = list.SelectedItem is ResumeItem;
        resume.Click += (_, _) => Resume(list.SelectedItem as ResumeItem);
        list.MouseDoubleClick += (_, _) => Resume(list.SelectedItem as ResumeItem);
        panel.Children.Add(none);
        panel.Children.Add(list);
        panel.Children.Add(resume);
        Fill();
    }

    /// <summary>Arbeitsstand löschen (nach Rückfrage): nur <c>state.json</c>, in den Papierkorb. Die Patch-Dateien und die Vergleichsbasis bleiben.</summary>
    private bool DeleteSaved(ResumeItem item)
    {
        if (MessageBox.Show(this, CompatchTexts.ResumeDeleteAsk(item.Text), CompatchTexts.ResumeDelete, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            return false;
        }

        try
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                item.Saved.Store.StatePath,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            MessageBox.Show(this, CompatchTexts.ResumeDeleteFailed(ex.Message), CompatchTexts.ResumeDelete, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void Resume(ResumeItem? item)
    {
        if (item is null)
        {
            return;
        }

        var state = item.Saved.State;
        if (CompatchStateStore.IsOutdatedUpdate(state, InstalledVersion()))
        {
            // Das Spiel wurde seither erneut aktualisiert: nur noch löschen (Rechtsklick), ein neuer Lauf startet im Update-Reiter.
            MessageBox.Show(this, VanillaUpdateTexts.SavedOutdated, CompatchTexts.StartTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var missing = new[] { state.ModAFolder, state.ModBFolder, state.TargetFolder }.FirstOrDefault(f => string.IsNullOrWhiteSpace(f) || !Directory.Exists(f));
        if (missing is not null)
        {
            MessageBox.Show(this, CompatchTexts.ResumeMissing(missing), CompatchTexts.StartTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _result = (state, item.Saved.Store);
        DialogResult = true;
    }

    /// <summary>Installierte Spielversion (für veraltete Vanilla-Update-Läufe); <see langword="null"/>, wenn unbekannt.</summary>
    private string? InstalledVersion() => _host.VanillaRoot is { } root ? ParadoxModTool.VanillaDiff.GameVersionReader.TryReadVersion(root) : null;

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
        RefreshHiddenVanilla();
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

    private void RefreshHiddenVanilla()
    {
        var metadataFile = _host.Profile?.MetadataFile ?? string.Empty;
        var hidesEarlier = _host.Profile?.ReplacePathHidesEarlierMods == true;
        var bAfterA = _bAfterA.IsChecked == true;
        var lines = new List<string>();
        var folders = new List<string>();
        foreach (var (side, other, later) in new[] { (_a, _b, !bAfterA), (_b, _a, bAfterA) })
        {
            if (side is null)
            {
                continue;
            }

            var hidden = ReplacePaths.Of(new[] { side.Folder }, metadataFile, _host.Profile?.ReplacePathRoots);
            if (hidden.Count > 0)
            {
                // CK III, EU V und Victoria 3: Die später ladende Mod blendet auch die Dateien der anderen aus.
                lines.Add(hidesEarlier && later && other is not null
                    ? CompatchTexts.HiddenVanillaAndMod(side.Name, hidden.Count, other.Name)
                    : CompatchTexts.HiddenVanilla(side.Name, hidden.Count));
                folders.AddRange(hidden);
            }
        }

        _hiddenVanilla.Text = string.Join("\n", lines);
        _hiddenVanilla.ToolTip = lines.Count == 0
            ? null
            : CompatchTexts.HiddenVanillaTip + "\n" + string.Join("\n", folders.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
        _hiddenVanilla.Visibility = lines.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
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
                    ModMetadata.Read(_a.Folder, _host.MetadataFile)?.SupportedGameVersion,
                    // Stellaris nimmt im Deskriptor nur vX.Y.Z oder vX.Y.*; fehlt das bei Mod A, gilt die installierte Version
                    _host.Profile?.SupportedVersionMajorMinor == true ? InstalledVersion() : null,
                    _host.Profile?.SupportedVersionMajorMinor == true);
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

        if (state is null)
        {
            // Neuer Arbeitsstand: Filter des Merge-Fensters aus "Standard für neue Arbeitsstände"; ein fortgesetzter behält seine.
            state = new CompatchState
            {
                HideFilledMissing = _host.Defaults.HideFilled,
                HideReplacedFiles = _host.Defaults.HideReplaced,
                HideDecided = _host.Defaults.HideDecided,
                UncertainOnly = _host.Defaults.UncertainOnly,
            };
        }

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
            _host.RememberStartOptions?.Invoke(new CompatchStartOptions(
                _cautious.IsChecked == true,
                _skipLocked.IsChecked == true,
                _mainAsBase.IsChecked == true,
                _fillMissing.IsChecked == true));
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
