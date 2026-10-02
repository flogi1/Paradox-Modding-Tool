using System.Windows;
using ParadoxModTool.ErrorLog;
using Controls = System.Windows.Controls;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// "`set_variable` auskommentieren" (Spielstand-Explorer Plan 2b): Vorschau je Mod und Datei mit den geänderten Zeilen, Mods abwählbar,
/// Warnung zu zusammengesetzten Namen, ausgelassene Stellen (anderer Code in derselben Zeile) und Sperre, solange der Name außerhalb der
/// gefundenen Stellen noch vorkommt. Beim Bestätigen liest der Dialog die gewählten Mods neu, damit genau das geschrieben wird, was jetzt dort
/// steht (wie beim Umbenennen).
/// </summary>
public sealed class CommentOutDialog : Window
{
    private const int MaxLinesPerFile = 20;
    private const int MaxListed = 30;

    private readonly string _name;
    private readonly IReadOnlyList<string> _setEffects;
    private readonly SymbolIndex _index;
    private readonly Func<string, string> _snapshotDirectory;
    private readonly List<ModChoice> _choices = new();
    private readonly Controls.TextBlock _summary = new() { Margin = new Thickness(0, 6, 0, 0), FontWeight = FontWeights.SemiBold };
    private readonly Controls.StackPanel _problems = new();
    private readonly Controls.StackPanel _preview = new();
    private readonly Controls.Button _ok = new() { Content = ErrorLogTexts.CommentOutConfirm, Width = 130, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
    private readonly Controls.Button _cancel = new() { Content = ErrorLogTexts.Cancel, Width = 110, IsCancel = true };

    private CommentOutDialog(string name, IReadOnlyList<string> setEffects, IReadOnlyList<ModSource> mods, SymbolIndex index, Func<string, string> snapshotDirectory)
    {
        _name = name;
        _setEffects = setEffects;
        _index = index;
        _snapshotDirectory = snapshotDirectory;
        Title = ErrorLogTexts.CommentOutTitle(setEffects[0], name);
        Width = 860;
        Height = 640;
        MinWidth = 520;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        foreach (var mod in mods)
        {
            var scan = ScanOf(mod);
            if (scan.Statements.Count == 0 && scan.Skipped.Count == 0 && scan.Unreadable.Count == 0)
            {
                continue;
            }

            var include = new Controls.CheckBox { IsChecked = true, Margin = new Thickness(0, 8, 0, 2) };
            include.Click += (_, _) => Refresh();
            _choices.Add(new ModChoice(mod, scan, include));
        }

        var top = new Controls.StackPanel();
        var warning = new Controls.TextBlock { Text = ErrorLogTexts.CommentOutWarning(name), TextWrapping = TextWrapping.Wrap };
        warning.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        top.Children.Add(warning);
        top.Children.Add(_problems);
        top.Children.Add(_summary);

        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(_ok);
        buttons.Children.Add(_cancel);

        var root = new Controls.DockPanel { Margin = new Thickness(12) };
        Controls.DockPanel.SetDock(top, Controls.Dock.Top);
        Controls.DockPanel.SetDock(buttons, Controls.Dock.Bottom);
        root.Children.Add(top);
        root.Children.Add(buttons);
        root.Children.Add(new Controls.ScrollViewer { Content = _preview, Margin = new Thickness(0, 8, 0, 0), VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto });
        Content = root;

        _ok.Click += (_, _) => Confirm();
        Refresh();
    }

    public IReadOnlyList<ModRenamePlan> Plans { get; private set; } = Array.Empty<ModRenamePlan>();

    /// <summary>Pläne der gewählten Mods nach Bestätigung; <see langword="null"/> bei Abbruch.</summary>
    public static IReadOnlyList<ModRenamePlan>? Ask(Window owner, string name, IReadOnlyList<string> setEffects, IReadOnlyList<ModSource> mods, SymbolIndex index, Func<string, string> snapshotDirectory)
    {
        var dialog = new CommentOutDialog(name, setEffects, mods, index, snapshotDirectory) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Plans : null;
    }

    private SetEffectScan ScanOf(ModSource mod) => SetEffectEdits.Scan(mod, _name, _setEffects, new[] { _snapshotDirectory(mod.Directory) });

    private void Confirm()
    {
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
        var chosen = _choices.Where(c => c.Include.IsChecked == true).ToList();
        // Vorkommen in abgewählten Mods zählen als Lesestellen: Ihre set_variable-Stellen bleiben ja stehen.
        var reads = SetEffectEdits.ReadsOutside(_index, _name, chosen.Select(c => c.Scan));
        var unreadable = chosen.SelectMany(c => c.Scan.Unreadable).ToList();
        var skipped = chosen.SelectMany(c => c.Scan.Skipped).ToList();
        Plans = chosen.Select(c => c.Scan.Plan).Where(p => p.Files.Count > 0).ToList();
        var files = Plans.SelectMany(p => p.Files).ToList();

        _problems.Children.Clear();
        if (reads.Count > 0)
        {
            AddProblem(ErrorLogTexts.CommentOutReads(_name), reads.Take(MaxListed).Select(ErrorLogTexts.Occurrence));
        }

        if (unreadable.Count > 0)
        {
            AddProblem(ErrorLogTexts.RenameUnreadable(string.Join(", ", unreadable.Select(System.IO.Path.GetFileName))), Array.Empty<string>());
        }

        if (skipped.Count > 0)
        {
            AddProblem(ErrorLogTexts.CommentOutSkipped, skipped.Take(MaxListed).Select(s => $"{System.IO.Path.GetFileName(s.Path)}:{s.Line}"));
        }

        _summary.Text = files.Count == 0
            ? ErrorLogTexts.CommentOutNothing(_setEffects[0], _name)
            : ErrorLogTexts.RenameSummary(files.Sum(f => f.Count), files.Count, Plans.Count);

        _preview.Children.Clear();
        foreach (var choice in _choices)
        {
            choice.Include.Content = new Controls.TextBlock { Text = ErrorLogTexts.RenameModHeading(choice.Mod.Name, choice.Scan.Plan.Files.Count), FontWeight = FontWeights.Bold };
            _preview.Children.Add(choice.Include);
            if (choice.Include.IsChecked != true)
            {
                continue;
            }

            foreach (var file in choice.Scan.Plan.Files)
            {
                _preview.Children.Add(new Controls.TextBlock
                {
                    Text = ErrorLogTexts.RenameFileHeading(System.IO.Path.GetRelativePath(choice.Mod.Directory, file.Path), file.Count),
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

        _ok.IsEnabled = files.Count > 0 && reads.Count == 0 && unreadable.Count == 0;
    }

    private void AddProblem(string heading, IEnumerable<string> lines)
    {
        _problems.Children.Add(new Controls.TextBlock { Text = heading, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 2) });
        foreach (var line in lines)
        {
            _problems.Children.Add(new Controls.TextBlock { Text = line, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 0, 0, 0) });
        }
    }

    /// <summary>Eine Mod in der Vorschau: gelesener Stand und ob sie bearbeitet wird.</summary>
    private sealed record ModChoice(ModSource Mod, SetEffectScan Scan, Controls.CheckBox Include);
}
