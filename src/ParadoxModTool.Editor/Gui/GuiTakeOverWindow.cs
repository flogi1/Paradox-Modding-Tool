using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Übersicht "In Mod übernehmen" (Spec "Vanilla-Datei ändern" Schritt 4): was der Plan schreibt, warum eine Datei ganz kopiert wird, was das
/// Übernehmen verhindert. Schreibt selbst nichts; "Übernehmen" schließt mit <c>DialogResult = true</c>.
/// </summary>
public sealed class GuiTakeOverWindow : Window
{
    /// <summary>Zeile der Liste (Texte fertig für die Anzeige).</summary>
    private sealed record Row(string Action, string Name, string File, string Usages);

    /// <param name="openModified">Zieldateien, die gerade geöffnet sind und ungespeicherte Änderungen haben (blockieren wie ein Planfehler).</param>
    public GuiTakeOverWindow(GuiOverridePlan plan, string modRoot, IReadOnlyList<string> openModified)
    {
        Title = GuiTexts.TakeOverTitle;
        Width = 820;
        Height = 520;
        MinWidth = 520;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new DockPanel { Margin = new Thickness(12) };
        var header = new StackPanel();
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);

        var blockers = plan.Blockers.Select(GuiTexts.TakeOverBlocker)
            .Concat(openModified.Select(path => GuiTexts.TakeOverOpenModified(Relative(modRoot, path))))
            .ToList();
        if (blockers.Count > 0)
        {
            var box = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            box.Children.Add(new TextBlock { Text = GuiTexts.TakeOverBlocked, FontWeight = FontWeights.SemiBold });
            foreach (var text in blockers)
            {
                var line = new TextBlock { Text = "• " + text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 2, 0, 0) };
                line.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");
                box.Children.Add(line);
            }

            header.Children.Add(box);
        }

        var empty = plan.Entries.Count == 0 && plan.Blockers.Count == 0;
        header.Children.Add(new TextBlock
        {
            Text = empty ? GuiTexts.TakeOverEmpty : GuiTexts.TakeOverIntro(modRoot),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6),
        });
        if (plan.CopyReason is { } reason)
        {
            var why = new Border { Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 0, 6), Child = new TextBlock { Text = GuiTexts.TakeOverReason(reason), TextWrapping = TextWrapping.Wrap } };
            why.SetResourceReference(Border.BackgroundProperty, "Theme.NoticeBackground");
            header.Children.Add(why);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var apply = new Button
        {
            Content = GuiTexts.TakeOverApply,
            IsDefault = true,
            MinWidth = 100,
            Padding = new Thickness(8, 2, 8, 2),
            IsEnabled = plan.CanApply && openModified.Count == 0,
        };
        apply.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = GuiTexts.TakeOverCancel, IsCancel = true, MinWidth = 100, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        buttons.Children.Add(apply);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);

        var files = new TextBlock
        {
            Text = GuiTexts.TakeOverFiles(plan.Files.Count(f => f.IsNew), plan.Files.Count(f => !f.IsNew)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
            Visibility = plan.Files.Count > 0 ? Visibility.Visible : Visibility.Collapsed,
        };
        files.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        DockPanel.SetDock(files, Dock.Bottom);
        panel.Children.Add(files);

        var list = new ListView
        {
            ItemsSource = plan.Entries.Select(entry => new Row(
                GuiTexts.TakeOverKind(entry.Kind, entry.Detail),
                NameOf(entry),
                Relative(modRoot, entry.TargetPath),
                entry.Usages > 0 ? GuiTexts.PaletteUsages(entry.Usages) : string.Empty)).ToList(),
            View = new GridView
            {
                Columns =
                {
                    new GridViewColumn { Header = GuiTexts.TakeOverColumnAction, DisplayMemberBinding = new Binding(nameof(Row.Action)), Width = 190 },
                    new GridViewColumn { Header = GuiTexts.TakeOverColumnName, DisplayMemberBinding = new Binding(nameof(Row.Name)), Width = 230 },
                    new GridViewColumn { Header = GuiTexts.TakeOverColumnFile, DisplayMemberBinding = new Binding(nameof(Row.File)), Width = 250 },
                    new GridViewColumn { Header = GuiTexts.TakeOverColumnUsages, DisplayMemberBinding = new Binding(nameof(Row.Usages)), Width = 90 },
                },
            },
            Visibility = plan.Entries.Count > 0 ? Visibility.Visible : Visibility.Collapsed,
        };
        panel.Children.Add(list);
        Content = panel;
    }

    /// <summary>Name mit Art (<c>type x</c>, <c>template y</c>, <c>@z</c>); Dateikopie: der relative Pfad.</summary>
    private static string NameOf(GuiPlanEntry entry) => entry.DefinitionKind switch
    {
        GuiDefinitionKind.Type => "type " + entry.Name,
        GuiDefinitionKind.Template => "template " + entry.Name,
        _ => entry.Kind is GuiPlanEntryKind.CopyConstant or GuiPlanEntryKind.RenameConstant ? "@" + entry.Name : entry.Name,
    };

    private static string Relative(string modRoot, string path)
    {
        var relative = Path.GetRelativePath(modRoot, path);
        return relative.StartsWith("..", StringComparison.Ordinal) ? path : relative.Replace('\\', '/');
    }
}
