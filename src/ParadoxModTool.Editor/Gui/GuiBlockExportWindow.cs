using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Blocks;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// "Bausteine exportieren" (Plan-2b-Ruling 11): Häkchenliste der wirksamen eigenen Bausteine mit Suche, "Alle"/"Keine" für die gezeigten; "Exportieren"
/// fragt nach der Zieldatei und schreibt das Paket.
/// </summary>
public sealed class GuiBlockExportWindow : Window
{
    private readonly List<Entry> _entries;
    private readonly string _gameId;
    private readonly string _pmtVersion;
    private readonly TextBox _search = new() { Margin = new Thickness(0, 0, 0, 6) };
    private readonly ListBox _list = new();

    public GuiBlockExportWindow(IReadOnlyList<GuiBlock> ownBlocks, string gameId, string pmtVersion)
    {
        _entries = ownBlocks.Select(b => new Entry(b)).ToList();
        _gameId = gameId;
        _pmtVersion = pmtVersion;
        Title = GuiTexts.ShareExportTitle;
        Width = 560;
        Height = 480;
        MinWidth = 400;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _search.ToolTip = GuiTexts.BlocksSearchTip;
        _search.TextChanged += (_, _) => Filter();
        _list.ItemTemplate = CheckTemplate(nameof(Entry.Checked), nameof(Entry.Text));
        VirtualizingPanel.SetIsVirtualizing(_list, true);
        Filter();

        var export = new Button { Content = GuiTexts.ShareExport, IsDefault = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2) };
        export.Click += (_, _) => Export();
        Content = ShareLayout(_search, _list, () => SetAll(true), () => SetAll(false), export, null);
        Loaded += (_, _) => _search.Focus();
    }

    /// <summary>Angehakte Bausteine.</summary>
    public IReadOnlyList<GuiBlock> Chosen => _entries.Where(e => e.Checked).Select(e => e.Block).ToList();

    internal string SearchText
    {
        get => _search.Text;
        set => _search.Text = value;
    }

    /// <summary>Wirksame eigene Bausteine (Spiel und Mod, ohne mitgelieferte und eingebaute): Namen im Paket sind damit eindeutig.</summary>
    public static IReadOnlyList<GuiBlock> OwnBlocks(GuiBlockLibrary library) =>
        library.Effective.Where(b => !b.IsBuiltIn && b.Layer != GuiBlockLayer.Shipped).ToList();

    internal void SetAll(bool value)
    {
        foreach (var entry in _list.Items.Cast<Entry>())
        {
            entry.Checked = value;
        }
    }

    /// <summary>Gemeinsamer Aufbau der Teilen-Fenster: Suche oben, Liste, unten "Alle"/"Keine" links und die Knöpfe rechts.</summary>
    internal static UIElement ShareLayout(TextBox search, ListBox list, Action all, Action none, Button action, UIElement? header)
    {
        var allButton = new Button { Content = GuiTexts.SuggestAll, MinWidth = 70, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
        allButton.Click += (_, _) => all();
        var noneButton = new Button { Content = GuiTexts.SuggestNoneButton, MinWidth = 70, Padding = new Thickness(8, 2, 8, 2) };
        noneButton.Click += (_, _) => none();
        var cancel = new Button { Content = GuiTexts.BlocksSaveCancel, IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false };
        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(allButton);
        left.Children.Add(noneButton);
        DockPanel.SetDock(left, Dock.Left);
        buttons.Children.Add(left);
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        right.Children.Add(action);
        right.Children.Add(cancel);
        DockPanel.SetDock(right, Dock.Right);
        buttons.Children.Add(right);

        var root = new DockPanel { Margin = new Thickness(12) };
        if (header is not null)
        {
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
        }

        DockPanel.SetDock(search, Dock.Top);
        root.Children.Add(search);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(list);
        return root;
    }

    /// <summary>Häkchen mit TextBlock als Inhalt (die CheckBox-Vorlage liest Zugriffstasten, Unterstriche blieben sonst nicht sichtbar).</summary>
    internal static DataTemplate CheckTemplate(string checkedPath, string textPath)
    {
        var check = new FrameworkElementFactory(typeof(CheckBox));
        check.SetBinding(CheckBox.IsCheckedProperty, new Binding(checkedPath) { Mode = BindingMode.TwoWay });
        check.SetValue(MarginProperty, new Thickness(0, 1, 0, 1));
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(textPath));
        check.AppendChild(text);
        return new DataTemplate { VisualTree = check };
    }

    private void Filter()
    {
        var filter = _search.Text.Trim();
        _list.ItemsSource = _entries
            .Where(e => filter.Length == 0 || e.Block.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) || e.Block.Group.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void Export()
    {
        var chosen = Chosen;
        if (chosen.Count == 0)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = GuiTexts.ShareFilter, DefaultExt = GuiBlockPackage.Extension, AddExtension = true };
        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("gui.blocks.export");
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            GuiBlockPackage.Write(dialog.FileName, chosen, _gameId, _pmtVersion);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, GuiTexts.ShareFailed(ex.Message), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show(this, GuiTexts.ShareExported(chosen.Count, dialog.FileName), Title, MessageBoxButton.OK, MessageBoxImage.Information);
        DialogResult = true;
    }

    private sealed class Entry : INotifyPropertyChanged
    {
        private bool _checked = true;

        public Entry(GuiBlock block)
        {
            Block = block;
            var layer = block.Layer == GuiBlockLayer.Mod ? GuiTexts.BlocksLayerMod : GuiTexts.BlocksLayerGame;
            Text = $"{block.Name} {layer} · {block.Group}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public GuiBlock Block { get; }

        public string Text { get; }

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Checked)));
                }
            }
        }
    }
}
