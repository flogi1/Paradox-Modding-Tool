using System.Windows;
using System.Windows.Threading;
using ParadoxModTool.ErrorLog;
using Controls = System.Windows.Controls;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// "Überall umbenennen" (Fehlerprotokoll Plan 3): neuer Name, Vorschau je Mod und Datei mit den geänderten Zeilen (Spec: Vorschau je Datei,
/// geschrieben erst nach Bestätigung). Die Mods werden beim Öffnen einmal gelesen, beim Tippen nur neu gerechnet; jede Mod lässt sich
/// abwählen (Plan-3-Ruling 10). Beim Bestätigen liest der Dialog die gewählten Mods neu, bevor das Hauptfenster sichert und schreibt
/// (Ruling 4). Dateien, die den Namen enthalten, aber kein gültiges UTF-8 sind, sperren das Umbenennen (Ruling 9).
/// </summary>
public sealed class RenameDialog : Window
{
    private const int MaxLinesPerFile = 20;

    private readonly string _oldName;
    private readonly Func<string, string> _snapshotDirectory;
    private readonly Func<string, bool> _nameExists;
    private readonly List<ModChoice> _choices = new();
    private readonly DispatcherTimer _delay = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly Controls.TextBox _name = new() { Width = 320, Margin = new Thickness(6, 0, 0, 0) };
    private readonly Controls.TextBlock _warning = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.TextBlock _summary = new() { Margin = new Thickness(0, 6, 0, 0), FontWeight = FontWeights.SemiBold };
    private readonly Controls.StackPanel _preview = new();
    private readonly Controls.Button _ok = new() { Content = ErrorLogTexts.RenameConfirm, Width = 110, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
    private readonly Controls.Button _cancel = new() { Content = ErrorLogTexts.Cancel, Width = 110, IsCancel = true };

    private RenameDialog(string oldName, IReadOnlyList<ModSource> mods, IReadOnlyList<string> candidates, Func<string, string> snapshotDirectory, Func<string, bool> nameExists)
    {
        _oldName = oldName;
        _snapshotDirectory = snapshotDirectory;
        _nameExists = nameExists;
        Title = ErrorLogTexts.RenameTitle(oldName);
        Width = 820;
        Height = 620;
        MinWidth = 520;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        _warning.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");

        foreach (var mod in mods)
        {
            var scan = ScanOf(mod);
            if (scan.Files.Count == 0 && scan.Unreadable.Count == 0)
            {
                continue;
            }

            var include = new Controls.CheckBox { IsChecked = true, Margin = new Thickness(0, 8, 0, 2) };
            include.Click += (_, _) => Refresh();
            _choices.Add(new ModChoice(mod, scan, include));
        }

        var top = new Controls.StackPanel();
        var nameRow = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        nameRow.Children.Add(new Controls.TextBlock { Text = ErrorLogTexts.RenameNewName, VerticalAlignment = VerticalAlignment.Center });
        nameRow.Children.Add(_name);
        top.Children.Add(nameRow);
        if (candidates.Count > 0)
        {
            var row = new Controls.WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            row.Children.Add(new Controls.TextBlock { Text = ErrorLogTexts.RenameCandidates, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            foreach (var candidate in candidates)
            {
                // TextBlock statt Zeichenkette: WPF würde "_" im Namen als Zugriffstaste lesen.
                var button = new Controls.Button { Content = new Controls.TextBlock { Text = candidate }, Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(0, 0, 6, 4) };
                button.Click += (_, _) => _name.Text = candidate;
                row.Children.Add(button);
            }

            top.Children.Add(row);
        }

        top.Children.Add(_warning);
        top.Children.Add(_summary);

        var bottom = new Controls.StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        bottom.Children.Add(new Controls.TextBlock { Text = ErrorLogTexts.RenameSnapshotHint, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(_ok);
        buttons.Children.Add(_cancel);
        bottom.Children.Add(buttons);

        var root = new Controls.DockPanel { Margin = new Thickness(12) };
        Controls.DockPanel.SetDock(top, Controls.Dock.Top);
        Controls.DockPanel.SetDock(bottom, Controls.Dock.Bottom);
        root.Children.Add(top);
        root.Children.Add(bottom);
        root.Children.Add(new Controls.ScrollViewer { Content = _preview, Margin = new Thickness(0, 8, 0, 0), VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto });
        Content = root;

        _name.Text = candidates.FirstOrDefault() ?? oldName;
        _name.TextChanged += (_, _) =>
        {
            _delay.Stop();
            _delay.Start();
        };
        _delay.Tick += (_, _) =>
        {
            _delay.Stop();
            Refresh();
        };
        _ok.Click += (_, _) => Confirm();
        Closed += (_, _) => _delay.Stop();
        Loaded += (_, _) =>
        {
            _name.Focus();
            _name.SelectAll();
        };
        Refresh();
    }

    public IReadOnlyList<ModRenamePlan> Plans { get; private set; } = Array.Empty<ModRenamePlan>();

    public string NewName => _name.Text.Trim();

    /// <summary>Neuer Name und Plan der gewählten Mods nach Bestätigung; <see langword="null"/> bei Abbruch.</summary>
    public static (string NewName, IReadOnlyList<ModRenamePlan> Plans)? Ask(
        Window owner,
        string oldName,
        IReadOnlyList<ModSource> mods,
        IReadOnlyList<string> candidates,
        Func<string, string> snapshotDirectory,
        Func<string, bool> nameExists)
    {
        var dialog = new RenameDialog(oldName, mods, candidates, snapshotDirectory, nameExists) { Owner = owner };
        return dialog.ShowDialog() == true ? (dialog.NewName, dialog.Plans) : null;
    }

    private NameScan ScanOf(ModSource mod) => NameEdits.Scan(mod.Directory, _oldName, new[] { _snapshotDirectory(mod.Directory) });

    /// <summary>Plan-3-Ruling 4: vor dem Sichern von der Platte neu lesen, damit genau das geschrieben wird, was jetzt dort steht.</summary>
    private void Confirm()
    {
        _delay.Stop();
        for (var i = 0; i < _choices.Count; i++)
        {
            if (_choices[i].Include.IsChecked == true)
            {
                _choices[i] = _choices[i] with { Scan = ScanOf(_choices[i].Mod) };
            }
        }

        Refresh();
        if (_ok.IsEnabled)
        {
            DialogResult = true;
        }
    }

    private void Refresh()
    {
        var newName = NewName;
        var valid = NameEdits.IsValidName(newName) && newName != _oldName;
        var chosen = _choices.Where(c => c.Include.IsChecked == true).ToList();
        var unreadable = chosen.SelectMany(c => c.Scan.Unreadable).ToList();
        Plans = valid
            ? chosen.Select(c => new ModRenamePlan(c.Mod.Name, c.Mod.Directory, NameEdits.Apply(c.Scan, _oldName, newName))).Where(p => p.Files.Count > 0).ToList()
            : Array.Empty<ModRenamePlan>();

        _warning.Text = unreadable.Count > 0 ? ErrorLogTexts.RenameUnreadable(string.Join(", ", unreadable.Select(path => System.IO.Path.GetFileName(path))))
            : !valid ? ErrorLogTexts.RenameInvalid
            : _nameExists(newName) ? ErrorLogTexts.RenameExists(newName)
            : string.Empty;
        _warning.Visibility = _warning.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        var files = Plans.SelectMany(p => p.Files).ToList();
        _summary.Text = !valid ? string.Empty
            : files.Count == 0 ? ErrorLogTexts.RenameNothing(_oldName)
            : ErrorLogTexts.RenameSummary(files.Sum(f => f.Count), files.Count, Plans.Count);

        _preview.Children.Clear();
        foreach (var choice in _choices)
        {
            choice.Include.Content = new Controls.TextBlock { Text = ErrorLogTexts.RenameModHeading(choice.Mod.Name, choice.Scan.Files.Count), FontWeight = FontWeights.Bold };
            _preview.Children.Add(choice.Include);
            var plan = choice.Include.IsChecked == true ? Plans.FirstOrDefault(p => string.Equals(p.Folder, choice.Mod.Directory, StringComparison.Ordinal)) : null;
            if (plan is null)
            {
                continue;
            }

            foreach (var file in plan.Files)
            {
                _preview.Children.Add(new Controls.TextBlock
                {
                    Text = ErrorLogTexts.RenameFileHeading(System.IO.Path.GetRelativePath(plan.Folder, file.Path), file.Count),
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(20, 4, 0, 0),
                });
                foreach (var line in file.Lines.Take(MaxLinesPerFile))
                {
                    _preview.Children.Add(new Controls.TextBlock
                    {
                        Text = ErrorLogTexts.RenameLineText(line.Number, line.Before.Trim(), line.After.Trim()),
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(28, 0, 0, 0),
                    });
                }

                if (file.Lines.Count > MaxLinesPerFile)
                {
                    _preview.Children.Add(new Controls.TextBlock { Text = ErrorLogTexts.More(file.Lines.Count - MaxLinesPerFile), Margin = new Thickness(28, 0, 0, 0) });
                }
            }
        }

        _ok.IsEnabled = valid && files.Count > 0 && unreadable.Count == 0;
    }

    /// <summary>Eine Mod in der Vorschau: gelesener Stand und ob sie umbenannt wird.</summary>
    private sealed record ModChoice(ModSource Mod, NameScan Scan, Controls.CheckBox Include);
}
