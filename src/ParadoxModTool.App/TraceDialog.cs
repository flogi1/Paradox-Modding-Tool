using System.Windows;
using ParadoxModTool.ErrorLog;
using Controls = System.Windows.Controls;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// "Scope verfolgen" (Spec Scope-Verfolgung, Oberfläche): Datei und Zeile, die Zeile vorher und nachher, die Art der Spur (mit Bedingung
/// oder bei jedem Aufruf) und die Hinweise zu laufendem Spiel und Größe von debug.log.
/// </summary>
public sealed class TraceDialog : Window
{
    private TraceDialog(string file, TracePlan plan, RenameLine preview, string? guardLimit, bool gameRunning)
    {
        Title = ErrorLogTexts.TraceDialogTitle;
        Width = 900;
        SizeToContent = SizeToContent.Height;
        MinWidth = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new Controls.StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(Bold($"{file}:{plan.Line}"));
        panel.Children.Add(Text(guardLimit is null ? ErrorLogTexts.TraceUnguardedKind : ErrorLogTexts.TraceGuardedKind(guardLimit)));
        panel.Children.Add(Code(preview.Before));
        panel.Children.Add(Code(preview.After));
        if (gameRunning)
        {
            panel.Children.Add(Bold(ErrorLogTexts.TraceGameRunning));
        }

        if (guardLimit is null)
        {
            panel.Children.Add(Muted(ErrorLogTexts.TraceBigLogHint));
        }

        panel.Children.Add(Muted(ErrorLogTexts.TraceWhenEffective));

        var ok = new Controls.Button { Content = ErrorLogTexts.TraceInsert, Width = 130, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
        var cancel = new Controls.Button { Content = ErrorLogTexts.Cancel, Width = 110, IsCancel = true };
        ok.Click += (_, _) => DialogResult = true;
        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;
    }

    public static bool Ask(Window owner, string file, TracePlan plan, RenameLine preview, string? guardLimit, bool gameRunning) =>
        new TraceDialog(file, plan, preview, guardLimit, gameRunning) { Owner = owner }.ShowDialog() == true;

    private static Controls.TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

    private static Controls.TextBlock Bold(string text)
    {
        var block = Text(text);
        block.FontWeight = FontWeights.SemiBold;
        return block;
    }

    private static Controls.TextBlock Muted(string text)
    {
        var block = Text(text);
        block.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        return block;
    }

    private static Controls.TextBox Code(string text) => new()
    {
        Text = text,
        IsReadOnly = true,
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily("Consolas"),
        Margin = new Thickness(0, 6, 0, 0),
    };
}
