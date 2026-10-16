using System.IO;
using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Generators;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Localization;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Orientation = System.Windows.Controls.Orientation;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ParadoxModTool.App;

/// <summary>Prüfen, Im Generator testen, Export und Import im Paket-Editor (Spec Teil 5a, Abschnitte 1 und 4).</summary>
public sealed partial class PackageEditorWindow
{
    private void RunCheck()
    {
        if (_document is null || _files is null)
        {
            return;
        }

        var issues = PackageChecker.Check(_document, _files, UiText.Current.Language, _pending);
        _results.Items.Clear();
        _results.ToolTip = PackageEditorTexts.CheckResultsTip;
        foreach (var issue in issues)
        {
            var text = new TextBlock { Text = PackageEditorTexts.CheckLine(issue.Where, issue.Line, issue.Message), TextWrapping = TextWrapping.Wrap };
            text.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");
            _results.Items.Add(new ListBoxItem { Content = text, Tag = issue });
        }

        if (issues.Count == 0)
        {
            _results.Items.Add(new ListBoxItem { Content = new TextBlock { Text = GeneratorsTexts.CheckOk } });
        }

        _results.SelectionChanged -= Results_SelectionChanged;
        _results.SelectionChanged += Results_SelectionChanged;
        _results.Visibility = Visibility.Visible;
    }

    private void Results_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((_results.SelectedItem as ListBoxItem)?.Tag is not PackageCheckIssue issue)
        {
            return;
        }

        if (issue.TemplateName is { } name)
        {
            ShowTemplate(name, issue.Line);
        }
        else if (issue.Where.StartsWith("read.", StringComparison.Ordinal))
        {
            SelectSection(SectionKind.Read);
        }
        else if (issue.Where.StartsWith("lookups[", StringComparison.Ordinal) || issue.Where.StartsWith("fields[", StringComparison.Ordinal))
        {
            JumpToLookup(issue.Where);
        }
    }

    private void TestInGenerator()
    {
        if (_entry is not { } entry)
        {
            return;
        }

        if (!ReadOnly && IsDirty && !SaveBeforeFileChange())
        {
            return;
        }

        _host.OpenInGenerator(entry.Id);
    }

    private void ExportPackage()
    {
        if (_entry is not { } entry || _document is null || (!ReadOnly && !SaveBeforeFileChange()))
        {
            return;
        }

        if (ExportDialog.Ask(this, _document.Assets, entry.DataFolder) is not { } choice)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = PackageEditorTexts.ExportFilter,
            FileName = entry.Id + GeneratorPackageArchive.Extension,
            ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("generator.export"),
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            GeneratorPackageArchive.Export(dialog.FileName, new PackageExportRequest(entry.Folder, entry.DataFolder, choice.WithStates, choice.AssetIds, _host.PmtVersion));
            MessageBox.Show(this, PackageEditorTexts.Exported(dialog.FileName), PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or GeneratorPackageException)
        {
            MessageBox.Show(this, PackageEditorTexts.ExportFailed(ex.Message), PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportPackage()
    {
        if (!ConfirmLeave())
        {
            return;
        }

        var dialog = new OpenFileDialog { Filter = PackageEditorTexts.ExportFilter, ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("generator.import") };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            Import(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or GeneratorPackageException)
        {
            MessageBox.Show(this, PackageEditorTexts.ImportFailed(ex.Message), PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Lesen, Zusammenfassung, Kennung klären, Probe-Entpacken mit Ladeprüfung, dann ins Spiel des Pakets entpacken.</summary>
    private string? Import(string file, Action<string>? loadFailed = null)
    {
        var content = GeneratorPackageArchive.Read(file);
        var games = _host.Games();
        if (games.All(g => g.Id != content.GameId))
        {
            MessageBox.Show(this, PackageEditorTexts.ImportUnknownGame(content.GameId), PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        var gameName = games.First(g => g.Id == content.GameId).Name;
        var assets = content.Assets.Where(a => a.Included).Select(a => a.Id).ToList();
        var summary = PackageEditorTexts.ImportSummary(content.Name, gameName, content.Templates, content.States,
            assets.Count > 0 ? string.Join(", ", assets) : PackageEditorTexts.ImportNothing,
            content.Skipped.Count > 0 ? string.Join(", ", content.Skipped) : PackageEditorTexts.ImportNothing);
        if (MessageBox.Show(this, summary, PackageEditorTexts.Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return null;
        }

        var root = GeneratorPackageLoader.PackagesRoot(_host.GameDirectoryFor(content.GameId));
        string? newId = null;
        var mode = ImportMode.NewPackage;
        if (Directory.Exists(Path.Combine(GeneratorPackageLoader.ShippedRoot, content.PackageId)))
        {
            newId = AskImportId(PackageEditorTexts.ImportShippedId(content.PackageId), content.PackageId + "-import", root);
            if (newId is null)
            {
                return null;
            }
        }
        else if (Directory.Exists(Path.Combine(root, content.PackageId)))
        {
            switch (MessageBox.Show(this, PackageEditorTexts.ImportOwnExists(content.PackageId), PackageEditorTexts.Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
            {
                case MessageBoxResult.Yes:
                    mode = ImportMode.Replace;
                    break;
                case MessageBoxResult.No:
                    newId = AskImportId(PackageEditorTexts.ImportNewId, content.PackageId + "-import", root);
                    if (newId is null)
                    {
                        return null;
                    }

                    break;
                default:
                    return null;
            }
        }

        // Probe: erst in einen Temp-Ordner entpacken und laden; ein kaputtes Paket kommt nur auf ausdrücklichen Wunsch hinein.
        var probe = Path.Combine(Path.GetTempPath(), "pmt-import-" + Path.GetRandomFileName());
        try
        {
            var probeFolder = GeneratorPackageArchive.Extract(content, probe, newId, ImportMode.NewPackage, _ => true);
            GeneratorPackageLoader.Load(probeFolder);
        }
        catch (GeneratorPackageException ex)
        {
            // Aus "Paket mit KI erstellen": nichts anlegen, die Meldung geht zurück an die KI.
            if (loadFailed is not null)
            {
                loadFailed(ex.Message);
                return null;
            }

            if (MessageBox.Show(this, PackageEditorTexts.ImportLoadFailed(ex.Message), PackageEditorTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return null;
            }
        }
        finally
        {
            if (Directory.Exists(probe))
            {
                Directory.Delete(probe, recursive: true);
            }
        }

        Directory.CreateDirectory(root);
        var target = GeneratorPackageArchive.Extract(content, root, newId, mode, names =>
            MessageBox.Show(this, PackageEditorTexts.ImportOverwriteStates(string.Join(", ", names)), PackageEditorTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
        if (content.GameId != _host.GameId())
        {
            MessageBox.Show(this, PackageEditorTexts.OtherGameNote(gameName), PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return target;
        }

        _entry = null;
        _search.Text = string.Empty;
        LoadList(null);
        SelectInList(Path.GetFileName(target));
        return target;
    }

    /// <summary>"Paket mit KI erstellen" öffnen; das offene Paket kann als Vorlage mitgehen.</summary>
    internal void OpenAiWindow() =>
        new AiTaskWindow(this, GeneratorAiFlavor.Host(_host), new GeneratorAiFlavor(_host, _entry?.Folder, (file, loadFailed) =>
        {
            // Wie beim normalen Import: ungespeicherte Änderungen am offenen Paket erst klären (die Antwort kann genau dieses Paket ersetzen).
            if (!ConfirmLeave())
            {
                return null;
            }

            var target = Import(file, loadFailed);
            if (target is not null && _entry is not null && string.Equals(Path.GetFullPath(_entry.Folder), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                RunCheck();
            }

            return target;
        })).Show();

    /// <summary>Fragt eine neue Kennung, bis sie gültig und frei ist; <see langword="null"/> bei Abbruch.</summary>
    private string? AskImportId(string message, string initial, string root)
    {
        var current = initial;
        while (TextPromptWindow.Ask(this, PackageEditorTexts.Title, message, current) is { } answer)
        {
            current = answer.Trim();
            if (!PackageStore.IsValidPackageId(current))
            {
                message = PackageEditorTexts.IdInvalid + "\n" + PackageEditorTexts.ImportNewId;
            }
            else if (PackageStore.IdTaken(current, GeneratorPackageLoader.ShippedRoot, root))
            {
                message = PackageEditorTexts.IdTaken + "\n" + PackageEditorTexts.ImportNewId;
            }
            else
            {
                return current;
            }
        }

        return null;
    }

    private void ShowGuide(string? section) => GeneratorPackageHelpWindow.Show(this, section);

    /// <summary>Export-Dialog: Arbeitsstände und je Asset ein Haken; Spiel-Assets und fehlende Dateien sind gesperrt.</summary>
    private sealed class ExportDialog : Window
    {
        private readonly CheckBox _states = new() { Content = new TextBlock { Text = PackageEditorTexts.ExportStates }, ToolTip = PackageEditorTexts.ExportStatesTip, Margin = new Thickness(0, 0, 0, 8) };
        private readonly List<(string Id, CheckBox Box)> _assets = new();

        private ExportDialog(IReadOnlyList<(string Id, AssetDef Asset)> assets, string dataFolder)
        {
            Title = PackageEditorTexts.ExportTitle;
            Width = 480;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(_states);
            if (assets.Count > 0)
            {
                panel.Children.Add(new TextBlock { Text = PackageEditorTexts.ExportAssets, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            }

            foreach (var (id, asset) in assets)
            {
                var full = SafeRelativePath.IsSafe(asset.Path) ? Path.Combine(dataFolder, asset.Path.Replace('/', Path.DirectorySeparatorChar)) : null;
                var present = full is not null && (File.Exists(full) || Directory.Exists(full));
                var note = asset.Origin == AssetOrigin.Game ? PackageEditorTexts.ExportAssetGame : !present ? PackageEditorTexts.ExportAssetMissing : null;
                var box = new CheckBox
                {
                    Content = new TextBlock { Text = note is null ? $"{id} ({asset.Path})" : $"{id} ({asset.Path}) – {note}" },
                    ToolTip = PackageEditorTexts.ExportAssetTip,
                    IsEnabled = note is null,
                    Margin = new Thickness(0, 2, 0, 2),
                };
                _assets.Add((id, box));
                panel.Children.Add(box);
            }

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var ok = new Button { Content = new TextBlock { Text = CommonTexts.Ok }, Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            ok.Click += (_, _) => DialogResult = true;
            var cancel = new Button { Content = new TextBlock { Text = CommonTexts.Cancel }, Width = 80, IsCancel = true };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            Content = panel;
        }

        public static (bool WithStates, IReadOnlyList<string> AssetIds)? Ask(Window owner, IReadOnlyList<(string Id, AssetDef Asset)> assets, string dataFolder)
        {
            var dialog = new ExportDialog(assets, dataFolder) { Owner = owner };
            return dialog.ShowDialog() == true
                ? (dialog._states.IsChecked == true, dialog._assets.Where(a => a.Box.IsChecked == true).Select(a => a.Id).ToList())
                : null;
        }
    }
}
