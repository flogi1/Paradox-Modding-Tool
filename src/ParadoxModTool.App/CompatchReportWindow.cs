using System.Text;
using System.Windows;
using ParadoxModTool.Compatch;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>Ergebnis nach dem Schreiben: Zusammenfassung, Snapshot, Hinweise, Einträge mit Ladereihenfolge-Abhängigkeit.</summary>
public sealed class CompatchReportWindow : Window
{
    private CompatchReportWindow(WriteResult result, PreparedWrite prepared)
    {
        Title = CompatchTexts.ReportTitle;
        Width = 720;
        Height = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var text = new StringBuilder();
        text.AppendLine(CompatchTexts.ReportSummary(result.Written, result.Deleted, result.SnapshotPath ?? "-"));
        text.AppendLine();
        text.AppendLine(CompatchTexts.IssuesHeader + ":");
        if (prepared.Issues.Count == 0)
        {
            text.AppendLine(CompatchTexts.NoIssues);
        }

        foreach (var issue in prepared.Issues)
        {
            text.AppendLine("- " + issue.Message);
        }

        if (prepared.OrderDependent.Count > 0)
        {
            text.AppendLine();
            text.AppendLine(CompatchTexts.OrderDependentHeader);
            foreach (var entry in prepared.OrderDependent)
            {
                text.AppendLine("- " + entry);
            }
        }

        var panel = new Controls.DockPanel { Margin = new Thickness(12) };
        var close = new Controls.Button { Content = CompatchTexts.Close, Width = 100, IsDefault = true, IsCancel = true, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        close.Click += (_, _) => Close();
        Controls.DockPanel.SetDock(close, Controls.Dock.Bottom);
        panel.Children.Add(close);
        panel.Children.Add(new Controls.TextBox { Text = text.ToString(), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto });
        Content = panel;
    }

    public static void Show(Window owner, WriteResult result, PreparedWrite prepared) =>
        new CompatchReportWindow(result, prepared) { Owner = owner }.ShowDialog();
}
