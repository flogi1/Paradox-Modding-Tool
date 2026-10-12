using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Localization;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>Ergebnis von "Neu" und "Als Kopie bearbeiten": Kennung, Name, Spiel und die beiden Kopier-Haken.</summary>
internal sealed record PackageIdChoice(string Id, string Name, string GameId, bool WithStates, bool WithAssets);

/// <summary>
/// Dialog für Kennung und Namen eines neuen Pakets (Spec Teil 5a, Abschnitt 2). Die Kennung wird beim Tippen geprüft (Muster und schon vergeben);
/// "OK" geht erst bei gültiger Kennung. Mit <c>copy</c> statt der Spielwahl die Haken für Arbeitsstände und Assets.
/// </summary>
internal sealed class PackageIdDialog : Window
{
    private readonly TextBox _id = new();
    private readonly TextBox _name = new();
    private readonly ComboBox _game = new();
    private readonly CheckBox _states = new() { Content = new TextBlock { Text = PackageEditorTexts.WithStates }, ToolTip = PackageEditorTexts.WithStatesTip, Margin = new Thickness(0, 8, 0, 0) };
    private readonly CheckBox _assets = new() { Content = new TextBlock { Text = PackageEditorTexts.WithAssets }, ToolTip = PackageEditorTexts.WithAssetsTip, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _problem = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button _ok;
    private readonly Func<string, bool> _taken;
    private readonly bool _copy;

    private PackageIdDialog(string title, string id, string name, IReadOnlyList<(string Id, string Name)> games, string gameId, bool copy, Func<string, bool> taken)
    {
        _taken = taken;
        _copy = copy;
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        _problem.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");

        var grid = new Grid { Margin = new Thickness(12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var row = 0;
        void Add(string label, string tip, FrameworkElement input)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var text = new TextBlock { Text = label, ToolTip = tip, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 10, 3) };
            input.ToolTip = tip;
            input.Margin = new Thickness(0, 3, 0, 3);
            Grid.SetRow(text, row);
            Grid.SetRow(input, row);
            Grid.SetColumn(input, 1);
            grid.Children.Add(text);
            grid.Children.Add(input);
            row++;
        }

        _id.Text = id;
        _name.Text = name;
        Add(PackageEditorTexts.IdLabel, PackageEditorTexts.IdTip, _id);
        Add(PackageEditorTexts.NameLabel, PackageEditorTexts.NameTip, _name);
        if (!copy)
        {
            foreach (var (gid, gname) in games)
            {
                _game.Items.Add(new ComboBoxItem { Content = new TextBlock { Text = gname }, Tag = gid, IsSelected = gid == gameId });
            }

            Add(PackageEditorTexts.GameLabel, PackageEditorTexts.GameTip, _game);
        }

        var panel = new StackPanel { Margin = new Thickness(0) };
        panel.Children.Add(grid);
        if (copy)
        {
            var checks = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };
            checks.Children.Add(_states);
            checks.Children.Add(_assets);
            panel.Children.Add(checks);
        }

        _problem.Margin = new Thickness(12, 4, 12, 0);
        panel.Children.Add(_problem);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12) };
        _ok = new Button { Content = new TextBlock { Text = CommonTexts.Ok }, Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        _ok.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = new TextBlock { Text = CommonTexts.Cancel }, Width = 80, IsCancel = true };
        buttons.Children.Add(_ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;

        _id.TextChanged += (_, _) => Validate();
        _name.TextChanged += (_, _) => Validate();
        _game.SelectionChanged += (_, _) => Validate();
        Loaded += (_, _) =>
        {
            _id.Focus();
            _id.SelectAll();
        };
        Validate();
    }

    /// <summary>Neues Paket: Kennung, Name, Spiel.</summary>
    public static PackageIdChoice? AskNew(Window owner, IReadOnlyList<(string Id, string Name)> games, string gameId, Func<string, bool> taken) =>
        Ask(new PackageIdDialog(PackageEditorTexts.NewTitle, string.Empty, string.Empty, games, gameId, copy: false, taken) { Owner = owner });

    /// <summary>Kopie: neue Kennung und Name, Haken für Arbeitsstände und Assets; das Spiel bleibt.</summary>
    public static PackageIdChoice? AskCopy(Window owner, string id, string name, string gameId, Func<string, bool> taken) =>
        Ask(new PackageIdDialog(PackageEditorTexts.CopyTitle, id + "-copy", name, Array.Empty<(string, string)>(), gameId, copy: true, taken) { Owner = owner });

    private static PackageIdChoice? Ask(PackageIdDialog dialog)
    {
        var gameId = dialog._copy ? null : (dialog._game.SelectedItem as ComboBoxItem)?.Tag as string;
        return dialog.ShowDialog() == true
            ? new PackageIdChoice(dialog._id.Text.Trim(), dialog._name.Text.Trim(), (dialog._game.SelectedItem as ComboBoxItem)?.Tag as string ?? gameId ?? string.Empty, dialog._states.IsChecked == true, dialog._assets.IsChecked == true)
            : null;
    }

    private void Validate()
    {
        var id = _id.Text.Trim();
        _problem.Text = id.Length == 0 ? string.Empty
            : !PackageStore.IsValidPackageId(id) ? PackageEditorTexts.IdInvalid
            : _taken(id) ? PackageEditorTexts.IdTaken
            : string.Empty;
        _ok.IsEnabled = id.Length > 0 && _problem.Text.Length == 0 && _name.Text.Trim().Length > 0 && (_copy || _game.SelectedItem is not null);
    }
}
