using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Input;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.SearchReplace;
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventHandler = System.Windows.DragEventHandler;
using MenuItem = System.Windows.Controls.MenuItem;
using TextBox = System.Windows.Controls.TextBox;
using Point = System.Windows.Point;

namespace ParadoxModTool.App;

/// <summary>
/// Schnellzugriff-Knöpfe auf Ordner (Nutzer-Feedback Runde 10; seit 2026-09-30 auch im Compare-Reiter des Compare und Update Tools):
/// feste Knöpfe für den Mod-Ordner und die Ordnerkategorien in_game/main_menu/loading_screen des Spielordners, dazu beliebig viele
/// eigene Knöpfe ("+", Rechtsklick). Belegung und Sichtbarkeit liegen gemeinsam in <see cref="QuickPathStore"/>; eine Änderung in einem Fenster baut
/// die Leisten aller offenen Fenster neu auf. Ein Klick meldet den Ordner über <see cref="PathChosen"/>; ziehen auf ein Pfadfeld (<see cref="AcceptDrops"/>) setzt ihn dort ein.
/// </summary>
public sealed class QuickPathBar : StackPanel
{
    private static readonly string SettingsPath = QuickPathStore.GetDefaultFilePath();

    /// <summary>Belegung oder Sichtbarkeit wurde in irgendeinem Fenster geändert.</summary>
    private static event EventHandler? SettingsChanged;

    /// <summary>Diese Leiste hat gerade gespeichert und behält ihren Stand (auch wenn das Speichern scheiterte).</summary>
    private bool _saving;

    private bool _initialized;

    private readonly ToggleButton _toggle = new() { Padding = new Thickness(4, 1, 4, 1), IsChecked = true };
    private readonly StackPanel _buttons = new();
    private readonly WrapPanel _fixed = new() { Margin = new Thickness(0, 0, 0, 2) };
    private readonly WrapPanel _custom = new();
    private GameProfile? _profile;
    private Func<string?> _modRoot = () => null;
    private Func<string?> _gameContentRoot = () => null;
    private QuickPathSettings _settings = new();

    public QuickPathBar()
    {
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 2) };
        DockPanel.SetDock(_toggle, Dock.Right);
        _toggle.ToolTip = SearchTexts.QuickPathToggleTip;
        _toggle.Click += (_, _) =>
        {
            _settings.Visible = _toggle.IsChecked == true;
            Save();
        };
        header.Children.Add(_toggle);
        header.Children.Add(new TextBlock { Text = SearchTexts.QuickPathHeading, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold });
        Children.Add(header);
        _buttons.Children.Add(_fixed);
        _buttons.Children.Add(_custom);
        Children.Add(_buttons);

        EventHandler rebuild = (_, _) => Rebuild(reload: !_saving);
        Loaded += (_, _) =>
        {
            SettingsChanged -= rebuild;
            SettingsChanged += rebuild;

            // Solange die Leiste nicht angezeigt war (z. B. anderer Reiter), hat sie Änderungen anderer Fenster verpasst.
            if (_initialized)
            {
                Rebuild(reload: true);
            }
        };
        Unloaded += (_, _) => SettingsChanged -= rebuild;
    }

    /// <summary>Ein Knopf wurde geklickt; der Wert ist der Ordner.</summary>
    public event EventHandler<string>? PathChosen;

    /// <param name="modRoot">Ordner für den Knopf "Mod-Ordner".</param>
    /// <param name="gameContentRoot">Spielordner ("game"), unter dem die Ordnerkategorien des Profils liegen.</param>
    public void Initialize(GameProfile? profile, Func<string?> modRoot, Func<string?> gameContentRoot)
    {
        _profile = profile;
        _modRoot = modRoot;
        _gameContentRoot = gameContentRoot;
        _initialized = true;
        Rebuild(reload: true);
    }

    private void Rebuild(bool reload)
    {
        if (reload)
        {
            _settings = QuickPathStore.TryLoad(SettingsPath);
        }

        BuildFixed();
        BuildCustom();
        _toggle.IsChecked = _settings.Visible;
        _toggle.Content = _settings.Visible ? SearchTexts.QuickPathHide : SearchTexts.QuickPathShow;
        _buttons.Visibility = _settings.Visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Feste Knöpfe: Mod-Ordner und die Kategorien in_game/main_menu/loading_screen unter dem Spielordner. Nicht auflösbare Knöpfe
    /// bleiben sichtbar, aber deaktiviert, damit man sieht, dass es sie gibt.
    /// </summary>
    private void BuildFixed()
    {
        _fixed.Children.Clear();
        AddFixed(SearchTexts.QuickPathModFolder, _modRoot(), SearchTexts.QuickPathModFolderTip);
        if (_profile is null)
        {
            return;
        }

        var gameContentRoot = _gameContentRoot();
        foreach (var categoryPath in new[] { "in_game", "main_menu", "loading_screen" })
        {
            var category = _profile.FolderCategories.FirstOrDefault(c => string.Equals(c.Path, categoryPath, StringComparison.OrdinalIgnoreCase));
            if (category is not null)
            {
                var resolved = gameContentRoot is null ? null : Path.Combine(gameContentRoot, category.Path);
                AddFixed(category.Label, resolved, category.Description ?? $"{category.Label} ({category.Path})");
            }
        }
    }

    private void AddFixed(string label, string? path, string description)
    {
        var button = PathButton(label, () => path);
        button.Margin = new Thickness(0, 0, 4, 4);
        button.IsEnabled = path is not null;
        button.ToolTip = path is null ? $"{description}\n\n{SearchTexts.QuickPathUnavailable}" : $"{description}\n\n{path}\n\n{SearchTexts.QuickPathDragHint}";
        _fixed.Children.Add(button);
    }

    /// <summary>Indizes der eigenen Knöpfe, die im aktiven Spiel angezeigt werden (die Liste enthält auch die der anderen Spiele).</summary>
    private List<int> VisibleCustom() =>
        Enumerable.Range(0, _settings.CustomButtons.Count).Where(i => _settings.CustomButtons[i].AppliesTo(_profile?.Id)).ToList();

    /// <summary>Eigene Knöpfe des aktiven Spiels, beliebig viele; Rechtsklick bearbeitet, verschiebt oder entfernt, "+" legt einen neuen an.</summary>
    private void BuildCustom()
    {
        _custom.Children.Clear();
        foreach (var i in VisibleCustom())
        {
            var index = i;
            var state = _settings.CustomButtons[index];
            var button = PathButton(state.Label, () => _settings.CustomButtons[index].Path);
            button.Margin = new Thickness(0, 0, 4, 4);
            button.ToolTip = $"{state.Path}\n\n{SearchTexts.QuickPathDragHint}";
            button.ContextMenu = CustomMenu(index);
            _custom.Children.Add(button);
        }

        var add = new Button { Content = "+", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 4, 4), ToolTip = SearchTexts.QuickPathAddTip };
        add.Click += (_, _) => Add();
        _custom.Children.Add(add);
    }

    private ContextMenu CustomMenu(int index)
    {
        var menu = new ContextMenu();
        void Item(string header, bool enabled, Action action)
        {
            var item = new MenuItem { Header = header, IsEnabled = enabled };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        // Verschoben wird innerhalb der angezeigten Knöpfe: Knöpfe anderer Spiele dazwischen springt der Knopf mit über.
        var visible = VisibleCustom();
        var position = visible.IndexOf(index);
        Item(SearchTexts.QuickPathMenuEdit, true, () => Edit(index));
        Item(SearchTexts.QuickPathMenuGames, GameChoices.Count > 1, () => ChangeGames(index));
        Item(SearchTexts.QuickPathMenuLeft, position > 0, () => Change(() => _settings.Move(index, visible[position - 1] - index)));
        Item(SearchTexts.QuickPathMenuRight, position < visible.Count - 1, () => Change(() => _settings.Move(index, visible[position + 1] - index)));
        menu.Items.Add(new Separator());
        Item(SearchTexts.QuickPathMenuRemove, true, () => Change(() => _settings.CustomButtons.RemoveAt(index)));
        return menu;
    }

    private void Change(Action change)
    {
        change();
        Save();
    }

    /// <summary>"+": Name und Ordner abfragen; ohne Ordner entsteht kein Knopf.</summary>
    private void Add()
    {
        if (AskLabel(string.Empty) is not { } label || AskFolder(label, null) is not { } folder)
        {
            return;
        }

        var games = AskGames(label, new List<string>());
        if (games is null)
        {
            return;
        }

        Change(() => _settings.CustomButtons.Add(new QuickPathButtonState { Label = label, Path = folder, Games = games }));
    }

    private static IReadOnlyList<(string Id, string Name)> GameChoices => GameContext.Profiles.Select(entry => (entry.Profile.Id, entry.Profile.DisplayName)).ToList();

    /// <summary>
    /// Fragt, in welchen Spielen der Knopf erscheint; <see langword="null"/> bei Abbruch, leer für alle Spiele. Mit nur einem Spiel gibt es nichts zu wählen
    /// (der Knopf gilt dann für alle).
    /// </summary>
    private List<string>? AskGames(string label, List<string> current)
    {
        var games = GameChoices;
        return games.Count <= 1
            ? current
            : GamePickDialog.Ask(Window.GetWindow(this), SearchTexts.QuickPathGamesTitle, SearchTexts.QuickPathGamesPrompt(label), SearchTexts.QuickPathGamesAll, games, current);
    }

    /// <summary>"Spiele festlegen…": nur die Spielauswahl eines bestehenden Knopfs ändern.</summary>
    private void ChangeGames(int index)
    {
        var current = _settings.CustomButtons[index];
        if (AskGames(current.Label, current.Games) is { } games)
        {
            current.Games = games;
            Save();
        }
    }

    /// <summary>Bearbeiten: Abbruch beim Namen bricht alles ab; Abbruch nur beim Ordner behält den bisherigen Ordner.</summary>
    private void Edit(int index)
    {
        var current = _settings.CustomButtons[index];
        if (AskLabel(current.Label) is not { } label)
        {
            return;
        }

        current.Label = label;
        current.Path = AskFolder(label, current.Path) ?? current.Path;
        current.Games = AskGames(label, current.Games) ?? current.Games;
        Save();
    }

    private static string? AskLabel(string current)
    {
        var label = Microsoft.VisualBasic.Interaction.InputBox(SearchTexts.QuickPathNamePrompt, SearchTexts.QuickPathConfigureTitle, current);
        return string.IsNullOrWhiteSpace(label) ? null : label.Trim();
    }

    private string? AskFolder(string label, string? current)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = SearchTexts.QuickPathFolderPrompt(label) };
        if (!string.IsNullOrEmpty(current) && Directory.Exists(current))
        {
            dialog.SelectedPath = ParadoxModTool.Explorer.PathNormalization.ToWindowsPath(current);
        }

        dialog.ClientGuid = GameDialogMemory.For("search.quickPath");
        var owner = Window.GetWindow(this) is { } window ? new Win32DialogOwner(new WindowInteropHelper(window).Handle) : null;
        return dialog.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    /// <summary>
    /// Knopf, der beim Klick seinen Ordner meldet und sich mit gedrückter linker Maustaste auf ein Pfadfeld ziehen lässt
    /// (<see cref="AcceptDrops"/>). Erst eine Bewegung über die Windows-Ziehschwelle gilt als Ziehen; danach löst der Knopf keinen Klick aus.
    /// </summary>
    private Button PathButton(string label, Func<string?> path)
    {
        var button = new Button { Content = label, Padding = new Thickness(6, 2, 6, 2) };
        Point? start = null;
        var dragged = false;
        button.PreviewMouseLeftButtonDown += (_, e) =>
        {
            start = e.GetPosition(this);
            dragged = false;
        };
        button.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || start is not { } from || path() is not { } folder)
            {
                return;
            }

            var now = e.GetPosition(this);
            if (Math.Abs(now.X - from.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(now.Y - from.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            start = null;
            dragged = true;
            var data = new DataObject(DragFormat, folder);
            data.SetText(folder);
            DragDrop.DoDragDrop(button, data, DragDropEffects.Copy);
        };
        button.Click += (_, _) =>
        {
            if (dragged)
            {
                dragged = false;
                return;
            }

            if (path() is { } folder)
            {
                PathChosen?.Invoke(this, folder);
            }
        };
        return button;
    }

    /// <summary>Datenformat eines gezogenen Schnellzugriff-Knopfs (der Ordner als Text).</summary>
    private const string DragFormat = "ParadoxModTool.QuickPath";

    /// <summary>
    /// Macht ein Pfadfeld zum Ziel für gezogene Schnellzugriff-Knöpfe: der Ordner ersetzt den Text (statt an der Einfügemarke
    /// eingefügt zu werden, wie ein TextBox gezogenen Text sonst behandelt).
    /// </summary>
    public static void AcceptDrops(TextBox box)
    {
        box.AllowDrop = true;
        DragEventHandler over = (_, e) =>
        {
            if (e.Data.GetDataPresent(DragFormat))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        };
        box.PreviewDragEnter += over;
        box.PreviewDragOver += over;
        box.PreviewDrop += (_, e) =>
        {
            if (e.Data.GetData(DragFormat) is string folder)
            {
                box.Text = folder;
                box.Focus();
                box.CaretIndex = box.Text.Length;
                e.Handled = true;
            }
        };
    }

    private void Save()
    {
        try
        {
            QuickPathStore.Save(_settings, SettingsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Einstellung bleibt für die laufende Sitzung wirksam, wird nur nicht dauerhaft gespeichert.
        }

        // Alle Leisten bauen neu auf; die anderen lesen den gespeicherten Stand, diese behält ihren.
        _saving = true;
        try
        {
            SettingsChanged?.Invoke(null, EventArgs.Empty);
        }
        finally
        {
            _saving = false;
        }
    }

    private sealed class Win32DialogOwner(IntPtr handle) : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }
}
