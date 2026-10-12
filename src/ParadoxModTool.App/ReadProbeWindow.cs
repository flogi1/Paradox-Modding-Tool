using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ParadoxModTool.Generators.Editing.Package;
using Button = System.Windows.Controls.Button;
using FontFamily = System.Windows.Media.FontFamily;
using TabControl = System.Windows.Controls.TabControl;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>Ergebnis des Probelaufs (Spec Teil 5b, Abschnitt 1) mit drei Reitern: Treffer je Muster, eingelesener Stand, Hinweise.</summary>
internal sealed class ReadProbeWindow : Window
{
    public ReadProbeWindow(Window owner, string key, ReadProbeResult result)
    {
        Owner = owner;
        Title = PackageEditorTexts.ProbeTitle(key);
        Width = 820;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new DockPanel { Margin = new Thickness(12) };
        var close = new Button { Content = new TextBlock { Text = PackageEditorTexts.ProbeClose }, Width = 100, IsCancel = true, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Bottom);
        panel.Children.Add(close);
        if (result.Error is { } error)
        {
            var text = new TextBlock { Text = error, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            text.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");
            DockPanel.SetDock(text, Dock.Top);
            panel.Children.Add(text);
        }

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = new TextBlock { Text = PackageEditorTexts.ProbeMatches }, Content = Matches(result.Patterns) });
        tabs.Items.Add(new TabItem { Header = new TextBlock { Text = PackageEditorTexts.ProbeResult }, Content = Code(result.StateText) });
        tabs.Items.Add(new TabItem
        {
            Header = new TextBlock { Text = PackageEditorTexts.ProbeNotes },
            Content = Code(result.Notes.Count == 0 ? PackageEditorTexts.ProbeNoNotes : string.Join("\n", result.Notes)),
        });
        panel.Children.Add(tabs);
        Content = panel;
    }

    private static FrameworkElement Matches(IReadOnlyList<ProbePatternReport> reports)
    {
        var stack = new StackPanel { Margin = new Thickness(6) };
        foreach (var report in reports)
        {
            var label = Label(report.Where);
            var line = new TextBlock
            {
                Text = report.Count == 0 ? PackageEditorTexts.ProbeNoMatch(label) : PackageEditorTexts.ProbeLine(label, report.Count),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 2),
            };
            if (report.Count == 0)
            {
                line.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
            }

            stack.Children.Add(line);
            if (report.Problem is { } problem)
            {
                var text = new TextBlock { Text = problem, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 0, 0, 0) };
                text.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
                stack.Children.Add(text);
            }

            foreach (var example in report.Examples)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = example.Captures.Length == 0 ? example.Text : $"{example.Text}   ({example.Captures})",
                    FontFamily = new FontFamily("Consolas"),
                    Margin = new Thickness(12, 0, 0, 0),
                });
            }
        }

        return new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>Lesbare Bezeichnung eines Musters aus seinem Ort (<c>read.flags[2].block</c> → "Ja/Nein-Merkmal, Zeile 2").</summary>
    private static string Label(string where)
    {
        var row = where.IndexOf('[') is var open and >= 0 && int.TryParse(where[(open + 1)..where.IndexOf(']')], out var number) ? number : 0;
        return where switch
        {
            "read.keys" => PackageEditorTexts.ProbePatternKeys,
            "read.elements.block" => PackageEditorTexts.ProbePatternElements,
            _ when where.StartsWith("read.localization", StringComparison.Ordinal) => PackageEditorTexts.ProbePatternLocalization(row),
            _ when where.StartsWith("read.flags", StringComparison.Ordinal) => PackageEditorTexts.ProbePatternFlag(row),
            _ when where.StartsWith("read.assets", StringComparison.Ordinal) => PackageEditorTexts.ProbePatternAsset(row),
            _ => where,
        };
    }

    private static TextBox Code(string text) => new()
    {
        Text = text,
        IsReadOnly = true,
        FontFamily = new FontFamily("Consolas"),
        TextWrapping = TextWrapping.NoWrap,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Margin = new Thickness(6),
    };
}
