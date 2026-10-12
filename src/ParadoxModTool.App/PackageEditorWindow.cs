using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParadoxModTool.Generators;
using ParadoxModTool.Generators.Editing.Package;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using TreeView = System.Windows.Controls.TreeView;
using TreeViewItem = System.Windows.Controls.TreeViewItem;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// Was der Paket-Editor vom Hauptfenster braucht: aktives Spiel und dessen Datenordner, Spielliste, Öffnen im Generator, PMT-Version; für "Probelauf",
/// "Vorschlag" und "Vorschau" (Teil 5b) außerdem die bekannten Mods, die aktive Mod, die Spielordner, die Sprachen des Spielprofils und "Spielordner
/// einstellen".
/// </summary>
public sealed record PackageEditorHost(
    Func<string> GameId,
    Func<string> GameDirectory,
    Func<string, string> GameDirectoryFor,
    Func<IReadOnlyList<(string Id, string Name)>> Games,
    Action<string?> OpenInGenerator,
    string PmtVersion,
    Func<IReadOnlyList<(string Name, string Path)>> KnownMods,
    Func<string?> ActiveModRoot,
    Func<IReadOnlyList<string>> GameRoots,
    Func<IReadOnlyList<string>> ProfileLanguages,
    Func<bool> ChooseGameFolder);

/// <summary>
/// Paket-Editor (Spec 2026-10-05-generator-packages-editor): links die Pakete des aktiven Spiels, in der Mitte die Abschnitte des offenen Pakets,
/// rechts die Maske. Die Masken bearbeiten <see cref="PackageDocument"/>; Vorlagentexte bleiben bis "Speichern" im Speicher.
/// </summary>
public sealed partial class PackageEditorWindow : Window
{
    private const string FunctionsName = "functions.sbn";

    private readonly PackageEditorHost _host;
    private readonly ListBox _list = new();
    private readonly TextBox _search;
    private readonly TreeView _tree = new() { BorderThickness = new Thickness(0) };
    private readonly ContentControl _maskHost = new();
    private readonly Border _banner;
    private readonly ListBox _results = new() { Height = 140, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button _copyButton;
    private readonly Button _deleteButton;
    private readonly Button _exportButton;
    private readonly Button _saveButton;
    private readonly Button _discardButton;
    private readonly Button _checkButton;
    private readonly Button _testButton;

    /// <summary>Ungespeicherte Vorlagentexte (Name → Text; <c>functions.sbn</c> unter seinem Namen).</summary>
    private readonly Dictionary<string, string> _pending = new(StringComparer.OrdinalIgnoreCase);

    private Entry? _entry;
    private PackageDocument? _document;
    private PackageFiles? _files;
    private bool _switching;
    private bool _commentsConfirmed;

    public PackageEditorWindow(PackageEditorHost host)
    {
        _host = host;
        Title = PackageEditorTexts.Title;
        Width = 1350;
        Height = 860;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var (searchPanel, search) = SearchBox.Create();
        _search = search;
        _search.TextChanged += (_, _) => LoadList(_entry?.Id);

        var newButton = ToolButton(PackageEditorTexts.New, PackageEditorTexts.NewTip, (_, _) => NewPackage());
        _copyButton = ToolButton(PackageEditorTexts.Copy, PackageEditorTexts.CopyTip, (_, _) => CopyPackage());
        _deleteButton = ToolButton(PackageEditorTexts.Delete, PackageEditorTexts.DeleteTip, (_, _) => DeletePackage());
        _exportButton = ToolButton(PackageEditorTexts.Export, PackageEditorTexts.ExportTip, (_, _) => ExportPackage());
        var importButton = ToolButton(PackageEditorTexts.Import, PackageEditorTexts.ImportTip, (_, _) => ImportPackage());
        var aiButton = ToolButton(AiPackageTexts.Open, AiPackageTexts.OpenTip, (_, _) => OpenAiWindow());
        var listButtons = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var button in new[] { newButton, _copyButton, _deleteButton, _exportButton, importButton, aiButton })
        {
            button.Margin = new Thickness(0, 0, 6, 4);
            listButtons.Children.Add(button);
        }

        var left = new DockPanel();
        var packagesHeader = Header(PackageEditorTexts.Packages);
        DockPanel.SetDock(packagesHeader, Dock.Top);
        DockPanel.SetDock(searchPanel, Dock.Top);
        searchPanel.Margin = new Thickness(0, 0, 0, 4);
        DockPanel.SetDock(listButtons, Dock.Bottom);
        left.Children.Add(packagesHeader);
        left.Children.Add(searchPanel);
        left.Children.Add(listButtons);
        left.Children.Add(_list);
        _list.SelectionChanged += List_SelectionChanged;

        var middle = new DockPanel();
        var sectionsHeader = Header(PackageEditorTexts.Sections);
        DockPanel.SetDock(sectionsHeader, Dock.Top);
        middle.Children.Add(sectionsHeader);
        middle.Children.Add(_tree);
        _tree.SelectedItemChanged += (_, _) => ShowMask();

        var bannerText = new TextBlock { Text = PackageEditorTexts.ShippedBanner, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        var bannerCopy = ToolButton(PackageEditorTexts.Copy, PackageEditorTexts.CopyTip, (_, _) => CopyPackage());
        bannerCopy.Margin = new Thickness(10, 0, 0, 0);
        var bannerRow = new DockPanel();
        DockPanel.SetDock(bannerCopy, Dock.Right);
        bannerRow.Children.Add(bannerCopy);
        bannerRow.Children.Add(bannerText);
        _banner = new Border { Child = bannerRow, Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
        _banner.SetResourceReference(Border.BackgroundProperty, "Theme.NoticeBackground");
        var right = new DockPanel();
        DockPanel.SetDock(_banner, Dock.Top);
        right.Children.Add(_banner);
        right.Children.Add(new ScrollViewer { Content = _maskHost, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(4, 0, 4, 0) });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        AddAt(grid, left, 0);
        AddAt(grid, new GridSplitter { Width = 5, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch }, 1);
        AddAt(grid, middle, 2);
        AddAt(grid, new GridSplitter { Width = 5, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch }, 3);
        AddAt(grid, right, 4);

        _saveButton = ToolButton(PackageEditorTexts.Save, PackageEditorTexts.SaveTip, (_, _) => SaveCurrent());
        _discardButton = ToolButton(PackageEditorTexts.Discard, PackageEditorTexts.DiscardTip, (_, _) => Discard());
        _checkButton = ToolButton(PackageEditorTexts.Check, PackageEditorTexts.CheckTip, (_, _) => RunCheck());
        _testButton = ToolButton(PackageEditorTexts.TestInGenerator, PackageEditorTexts.TestInGeneratorTip, (_, _) => TestInGenerator());
        var guideButton = ToolButton(PackageEditorTexts.Guide, PackageEditorTexts.GuideTip, (_, _) => ShowGuide(null));
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        foreach (var button in new[] { _saveButton, _discardButton, _checkButton, _testButton, guideButton })
        {
            button.Margin = new Thickness(0, 0, 6, 0);
            bar.Children.Add(button);
        }

        var root = new DockPanel { Margin = new Thickness(6) };
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(_results, Dock.Bottom);
        root.Children.Add(bar);
        root.Children.Add(_results);
        root.Children.Add(grid);
        Content = root;

        PreviewKeyDown += Editor_PreviewKeyDown;
        Closing += Editor_Closing;
        LoadList(null);
        UpdateState();
    }

    private sealed record Entry(string Folder, string DataFolder, bool Shipped, string Id, string Label, string? Error);

    private string PackagesRoot => GeneratorPackageLoader.PackagesRoot(_host.GameDirectory());

    private bool IsDirty => _document?.IsDirty == true || _pending.Count > 0;

    private bool ReadOnly => _entry?.Shipped == true;

    /// <summary>Zeigt das Fenster und wählt das Paket mit dieser Kennung (falls in der Liste).</summary>
    public void Open(string? packageId)
    {
        if (packageId is not null && packageId != _entry?.Id)
        {
            SelectInList(packageId);
        }
    }

    /// <summary>Für den Knopf "Neues Paket" im Generator-Fenster.</summary>
    public void StartNewPackage() => NewPackage();

    private void LoadList(string? select)
    {
        _switching = true;
        _list.Items.Clear();
        var filter = _search.Text.Trim();
        var gameId = _host.GameId();
        // Dieselbe Auswahl wie im Generator-Fenster: mitgelieferte Pakete eines anderen Spiels nicht, Pakete ohne Spielangabe schon.
        foreach (var (folder, package, error, shipped) in GeneratorPackageLoader.ListAll(GeneratorPackageLoader.ShippedRoot, PackagesRoot, gameId))
        {
            var id = Path.GetFileName(folder);
            var label = (shipped ? "🔒 " : string.Empty) + (package?.Name ?? PackageEditorTexts.BrokenPackage(id));
            if (filter.Length > 0 && !label.Contains(filter, StringComparison.OrdinalIgnoreCase) && !id.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entry = new Entry(folder, shipped ? Path.Combine(PackagesRoot, id) : folder, shipped, id, label, error);
            _list.Items.Add(new ListBoxItem { Content = new TextBlock { Text = label }, Tag = entry, ToolTip = error });
        }

        _list.SelectedItem = _list.Items.Cast<ListBoxItem>().FirstOrDefault(i => ((Entry)i.Tag).Id == (select ?? _entry?.Id));
        _switching = false;
    }

    private void SelectInList(string id)
    {
        if (_list.Items.Cast<ListBoxItem>().FirstOrDefault(i => ((Entry)i.Tag).Id == id) is not { } item)
        {
            _search.Text = string.Empty;
            item = _list.Items.Cast<ListBoxItem>().FirstOrDefault(i => ((Entry)i.Tag).Id == id);
        }

        if (item is not null)
        {
            _list.SelectedItem = item;
        }
    }

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_switching || _list.SelectedItem is not ListBoxItem { Tag: Entry entry } || entry.Id == _entry?.Id && entry.Folder == _entry.Folder)
        {
            return;
        }

        if (!ConfirmLeave())
        {
            _switching = true;
            _list.SelectedItem = _list.Items.Cast<ListBoxItem>().FirstOrDefault(i => ((Entry)i.Tag).Id == _entry?.Id);
            _switching = false;
            return;
        }

        OpenEntry(entry);
    }

    private void OpenEntry(Entry entry)
    {
        PackageDocument document;
        try
        {
            document = PackageDocument.Load(entry.Folder);
        }
        catch (GeneratorPackageException ex)
        {
            MessageBox.Show(this, PackageEditorTexts.CannotOpen(ex.Message), PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _entry = entry;
        _document = document;
        _files = new PackageFiles(entry.Folder);
        _pending.Clear();
        _commentsConfirmed = false;
        document.Changed += UpdateState;
        _banner.Visibility = entry.Shipped ? Visibility.Visible : Visibility.Collapsed;
        _results.Visibility = Visibility.Collapsed;
        RebuildTree(null);
        UpdateState();
    }

    private void CloseEntry()
    {
        _entry = null;
        _document = null;
        _files = null;
        _pending.Clear();
        _banner.Visibility = Visibility.Collapsed;
        _results.Visibility = Visibility.Collapsed;
        _tree.Items.Clear();
        _maskHost.Content = new TextBlock { Text = PackageEditorTexts.NoPackage, Margin = new Thickness(4) };
        UpdateState();
    }

    /// <summary>Titel mit Stern bei ungespeicherten Änderungen, Knöpfe je nach Paket.</summary>
    private void UpdateState()
    {
        var name = _document?.Name.Resolve(ParadoxModTool.Localization.UiText.Current.Language);
        Title = (IsDirty ? "* " : string.Empty) + (name is null ? PackageEditorTexts.Title : PackageEditorTexts.TitleWith(name));
        var open = _document is not null;
        _copyButton.IsEnabled = open;
        _deleteButton.IsEnabled = open && !ReadOnly;
        _exportButton.IsEnabled = open;
        _saveButton.IsEnabled = open && !ReadOnly;
        _discardButton.IsEnabled = open && IsDirty;
        _checkButton.IsEnabled = open;
        _testButton.IsEnabled = open;
        if (!open && _maskHost.Content is null)
        {
            _maskHost.Content = new TextBlock { Text = PackageEditorTexts.NoPackage, Margin = new Thickness(4) };
        }
    }

    /// <summary>Speichert package.json und die geänderten Vorlagen; <see langword="false"/> bei Abbruch oder Fehler.</summary>
    private bool SaveCurrent()
    {
        if (_document is null || _files is null || ReadOnly)
        {
            return true;
        }

        if (_document.HadComments && !_commentsConfirmed)
        {
            if (MessageBox.Show(this, PackageEditorTexts.CommentsWarning, PackageEditorTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return false;
            }

            _commentsConfirmed = true;
        }

        try
        {
            _document.Save();
            foreach (var (name, text) in _pending)
            {
                if (string.Equals(name, FunctionsName, StringComparison.OrdinalIgnoreCase))
                {
                    _files.Functions = text;
                }
                else
                {
                    _files.Write(name, text);
                }
            }

            _pending.Clear();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, PackageEditorTexts.SaveFailed(ex.Message), PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        UpdateState();
        LoadList(_entry?.Id);
        return true;
    }

    private void Discard()
    {
        if (_entry is { } entry)
        {
            OpenEntry(entry);
        }
    }

    /// <summary>Fragt bei ungespeicherten Änderungen: Speichern, Verwerfen oder Abbrechen.</summary>
    private bool ConfirmLeave()
    {
        if (!IsDirty || _document is null)
        {
            return true;
        }

        switch (MessageBox.Show(this, PackageEditorTexts.UnsavedAsk(_document.Name.Resolve(ParadoxModTool.Localization.UiText.Current.Language)), PackageEditorTexts.Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
        {
            case MessageBoxResult.Yes:
                return SaveCurrent();
            case MessageBoxResult.No:
                // Verwerfen: Stand von der Platte, damit die Masken nichts Ungespeichertes mehr zeigen.
                _pending.Clear();
                if (_entry is { } current)
                {
                    OpenEntry(current);
                }

                return true;
            default:
                return false;
        }
    }

    private void Editor_Closing(object? sender, CancelEventArgs e)
    {
        if (!ConfirmLeave())
        {
            e.Cancel = true;
        }
    }

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SaveCurrent();
            e.Handled = true;
        }
        else if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
        {
            ShowGuide(CurrentGuideSection());
            e.Handled = true;
        }
    }

    private void NewPackage()
    {
        if (!ConfirmLeave())
        {
            return;
        }

        var choice = PackageIdDialog.AskNew(this, _host.Games(), _host.GameId(), id => PackageStore.IdTaken(id, GeneratorPackageLoader.ShippedRoot, PackagesRoot));
        if (choice is null)
        {
            return;
        }

        var root = GeneratorPackageLoader.PackagesRoot(_host.GameDirectoryFor(choice.GameId));
        if (PackageStore.IdTaken(choice.Id, GeneratorPackageLoader.ShippedRoot, root))
        {
            MessageBox.Show(this, PackageEditorTexts.IdTaken, PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            PackageStore.Create(root, choice.Id, choice.Name, choice.GameId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, PackageEditorTexts.SaveFailed(ex.Message), PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (choice.GameId != _host.GameId())
        {
            var game = _host.Games().FirstOrDefault(g => g.Id == choice.GameId).Name ?? choice.GameId;
            MessageBox.Show(this, PackageEditorTexts.OtherGameNote(game), PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _search.Text = string.Empty;
        LoadList(null);
        SelectInList(choice.Id);
    }

    private void CopyPackage()
    {
        if (_entry is not { } entry || _document is null || !ConfirmLeave())
        {
            return;
        }

        var name = _document?.Name.Resolve(ParadoxModTool.Localization.UiText.Current.Language) ?? entry.Id;
        var choice = PackageIdDialog.AskCopy(this, entry.Id, name, _host.GameId(), id => PackageStore.IdTaken(id, GeneratorPackageLoader.ShippedRoot, PackagesRoot));
        if (choice is null)
        {
            return;
        }

        try
        {
            PackageStore.Copy(entry.Folder, entry.DataFolder, PackagesRoot, choice.Id, choice.Name, choice.WithStates, choice.WithAssets);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or GeneratorPackageException)
        {
            MessageBox.Show(this, PackageEditorTexts.SaveFailed(ex.Message), PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _search.Text = string.Empty;
        LoadList(null);
        SelectInList(choice.Id);
    }

    private void DeletePackage()
    {
        if (_entry is not { Shipped: false } entry || _document is null)
        {
            return;
        }

        var (states, assets) = PackageStore.Contents(entry.DataFolder);
        var name = _document.Name.Resolve(ParadoxModTool.Localization.UiText.Current.Language);
        if (MessageBox.Show(this, PackageEditorTexts.DeleteAsk(name, states, assets), PackageEditorTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            PackageStore.Delete(entry.Folder, folder => Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                folder,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            MessageBox.Show(this, PackageEditorTexts.DeleteFailed(ex.Message), PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        CloseEntry();
        LoadList(null);
    }

    private static Button ToolButton(string text, string tip, RoutedEventHandler click)
    {
        var button = new Button { Content = new TextBlock { Text = text }, ToolTip = tip, Padding = new Thickness(8, 2, 8, 2) };
        button.Click += click;
        return button;
    }

    private static TextBlock Header(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };

    private static void AddAt(Grid grid, UIElement element, int column)
    {
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }
}
