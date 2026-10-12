using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParadoxModTool.Generators.Lookups;
using ParadoxModTool.Localization;
using ParadoxModTool.Session;
using Binding = System.Windows.Data.Binding;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ListView = System.Windows.Controls.ListView;
using Orientation = System.Windows.Controls.Orientation;
using SelectionMode = System.Windows.Controls.SelectionMode;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// "Aus Datenbank" (Spec 2026-10-05-generator-packages-lookups, Abschnitt 1): alle Einträge der Quellen eines Felds mit Suche, Filter "Art" und "Tag", Beschreibung
/// des gewählten Eintrags und Hinweisen zu ausgelassenen Quellen. Suche und Filter merkt sich die Session des Spiels.
/// </summary>
public sealed class GeneratorLookupWindow : Window
{
    private readonly Func<bool, Task<LookupCatalog>> _load;
    private readonly AppSessionState _session;
    private readonly Func<bool> _chooseGameFolder;
    private readonly Action<string> _chooseAsset;
    private readonly ListView _list = new() { SelectionMode = SelectionMode.Single };
    private readonly TextBox _search;
    private readonly ComboBox _source = new() { Width = 150, Margin = new Thickness(6, 0, 12, 0) };
    private readonly TextBox _tag = new() { Width = 70, Margin = new Thickness(6, 0, 0, 0), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBox _desc = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Height = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 6, 0, 0) };
    private readonly StackPanel _notes = new();
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 4, 0, 0) };
    private LookupCatalog? _catalog;
    private bool _filling;

    /// <param name="load">Lädt den Katalog (im Hintergrund); <see langword="true"/> = neu lesen statt gemerkten Katalog nehmen.</param>
    /// <param name="chooseAsset">Öffnet "Asset wählen" für die Asset-Kennung.</param>
    public GeneratorLookupWindow(Window owner, Func<bool, Task<LookupCatalog>> load, AppSessionState session, Func<bool> chooseGameFolder, Action<string> chooseAsset)
    {
        _load = load;
        _session = session;
        _chooseGameFolder = chooseGameFolder;
        _chooseAsset = chooseAsset;
        Owner = owner;
        Title = GeneratorTexts.LookupTitle;
        Width = 900;
        Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var view = new GridView();
        view.Columns.Add(new GridViewColumn { Header = GeneratorTexts.LookupColumnName, DisplayMemberBinding = new Binding(nameof(Row.Name)), Width = 340 });
        view.Columns.Add(new GridViewColumn { Header = GeneratorTexts.LookupColumnNation, DisplayMemberBinding = new Binding(nameof(Row.Nation)), Width = 200 });
        view.Columns.Add(new GridViewColumn { Header = GeneratorTexts.LookupColumnTag, DisplayMemberBinding = new Binding(nameof(Row.Tag)), Width = 70 });
        view.Columns.Add(new GridViewColumn { Header = GeneratorTexts.LookupColumnArt, DisplayMemberBinding = new Binding(nameof(Row.Art)), Width = 120 });
        _list.View = view;
        _list.SelectionChanged += (_, _) => _desc.Text = (_list.SelectedItem as Row)?.Entry.Desc ?? string.Empty;
        _list.MouseDoubleClick += (_, _) => Take();
        _list.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Take();
                e.Handled = true;
            }
        };

        var (searchPanel, search) = SearchBox.Create(session.GeneratorLookupSearch);
        _search = search;
        _search.TextChanged += (_, _) => Fill();
        _tag.Text = session.GeneratorLookupTag ?? string.Empty;
        _tag.TextChanged += (_, _) => Fill();
        _source.SelectionChanged += (_, _) => Fill();

        var filters = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 0, 0) };
        right.Children.Add(new TextBlock { Text = GeneratorTexts.LookupArt, VerticalAlignment = VerticalAlignment.Center });
        right.Children.Add(_source);
        right.Children.Add(new TextBlock { Text = GeneratorTexts.LookupTag, VerticalAlignment = VerticalAlignment.Center });
        right.Children.Add(_tag);
        DockPanel.SetDock(right, Dock.Right);
        filters.Children.Add(right);
        filters.Children.Add(searchPanel);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var take = new Button { Content = new TextBlock { Text = GeneratorTexts.LookupTake }, Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        take.Click += (_, _) => Take();
        var close = new Button { Content = new TextBlock { Text = CommonTexts.Close }, Padding = new Thickness(12, 2, 12, 2), IsCancel = true };
        buttons.Children.Add(take);
        buttons.Children.Add(close);

        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(filters, Dock.Top);
        root.Children.Add(filters);
        DockPanel.SetDock(_notes, Dock.Top);
        root.Children.Add(_notes);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        DockPanel.SetDock(_desc, Dock.Bottom);
        root.Children.Add(_desc);
        DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(_status);
        root.Children.Add(_list);
        Content = root;

        Loaded += async (_, _) =>
        {
            _search.Focus();
            await Reload(force: false);
        };
    }

    /// <summary>Gewählter Eintrag nach "Übernehmen".</summary>
    public LookupEntry? Picked { get; private set; }

    /// <summary>Katalog, aus dem <see cref="Picked"/> stammt (für die Übersetzungen).</summary>
    public LookupCatalog? Catalog => _catalog;

    /// <summary>Eine Zeile der Tabelle.</summary>
    public sealed record Row(string Name, string Nation, string Tag, string Art, LookupEntry Entry);

    private async Task Reload(bool force)
    {
        _status.Text = GeneratorTexts.LookupLoading;
        _list.ItemsSource = null;
        try
        {
            _catalog = await _load(force);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = ex.Message;
            return;
        }

        _filling = true;
        _source.Items.Clear();
        _source.Items.Add(new ComboBoxItem { Content = GeneratorTexts.LookupAll, Tag = string.Empty });
        foreach (var (id, label) in _catalog.Sources)
        {
            _source.Items.Add(new ComboBoxItem { Content = label, Tag = id });
        }

        _source.SelectedItem = _source.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == (_session.GeneratorLookupSource ?? string.Empty)) ?? _source.Items[0];
        _filling = false;
        ShowNotes();
        Fill();
    }

    private void ShowNotes()
    {
        _notes.Children.Clear();
        foreach (var note in _catalog?.Notes ?? Array.Empty<LookupNote>())
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            var text = new TextBlock { Text = note.Text, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 640 };
            text.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
            row.Children.Add(text);
            Button? action = note.Kind switch
            {
                "gameFolder" => new Button { Content = new TextBlock { Text = GeneratorTexts.LookupChooseGameFolder } },
                "asset" => new Button { Content = new TextBlock { Text = GeneratorTexts.AssetPick } },
                _ => null,
            };
            if (action is not null)
            {
                action.Padding = new Thickness(8, 1, 8, 1);
                action.Margin = new Thickness(8, 0, 0, 0);
                var kind = note.Kind;
                var sourceId = note.SourceId;
                action.Click += async (_, _) =>
                {
                    if (kind == "gameFolder")
                    {
                        if (!_chooseGameFolder())
                        {
                            return;
                        }
                    }
                    else
                    {
                        _chooseAsset(sourceId);
                    }

                    await Reload(force: true);
                };
                row.Children.Add(action);
            }

            _notes.Children.Add(row);
        }
    }

    private void Fill()
    {
        if (_filling || _catalog is null)
        {
            return;
        }

        var sourceId = (_source.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;
        _session.GeneratorLookupSearch = _search.Text;
        _session.GeneratorLookupSource = sourceId.Length == 0 ? null : sourceId;
        _session.GeneratorLookupTag = _tag.Text;
        var labels = _catalog.Sources.ToDictionary(s => s.Id, s => s.Label, StringComparer.Ordinal);
        var rows = LookupFilter.Apply(_catalog.Entries, _search.Text, sourceId, _tag.Text)
            .Select(e => new Row(
                e.Name.Length > 0 ? e.Name : e.Key,
                e.Nation.Length > 0 ? e.Nation : GeneratorTexts.LookupGeneric,
                e.Tag,
                labels.GetValueOrDefault(e.SourceId, e.SourceId),
                e))
            .ToList();
        _list.ItemsSource = rows;
        _status.Text = rows.Count == 0 ? GeneratorTexts.LookupEmpty : GeneratorTexts.LookupCount(rows.Count, _catalog.Entries.Count);
    }

    private void Take()
    {
        if (_list.SelectedItem is not Row row)
        {
            return;
        }

        Picked = row.Entry;
        DialogResult = true;
    }
}
