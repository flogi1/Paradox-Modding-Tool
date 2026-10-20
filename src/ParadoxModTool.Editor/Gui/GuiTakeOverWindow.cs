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
/// Übernehmen verhindert. Klassisch (HOI IV, EU IV) dazu, warum ein Fenster in seine Datei kommt, Hinweise und umziehende Fenster sowie
/// "Ganze Datei kopieren…". Schreibt selbst nichts; "Übernehmen" schließt mit <c>DialogResult = true</c>.
/// </summary>
public sealed class GuiTakeOverWindow : Window
{
    /// <summary>Zeile der Liste (Texte fertig für die Anzeige).</summary>
    internal sealed record Row(string Action, string Name, string File, string Rule, string Usages);

    /// <param name="openModified">Zieldateien, die gerade geöffnet sind und ungespeicherte Änderungen haben (blockieren wie ein Planfehler).</param>
    /// <param name="classic">HOI IV, EU IV: klassische Texte, umziehende Fenster und "Ganze Datei kopieren…".</param>
    /// <param name="firstWins">Klassisch: Die zuerst geladene Fassung gewinnt (EU IV); bestimmt den Text zu umziehenden Fenstern.</param>
    public GuiTakeOverWindow(GuiOverridePlan plan, string modRoot, IReadOnlyList<string> openModified, bool classic = false, bool firstWins = false)
    {
        Title = GuiTexts.TakeOverTitle;
        Width = 820;
        Height = 520;
        MinWidth = 520;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Moves = classic ? ClassicOverridePlanner.Moves(plan, modRoot) : Array.Empty<(string, string, string, string?)>();

        var panel = new DockPanel { Margin = new Thickness(12) };
        var header = new StackPanel();
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);

        // Jomini behält die bisherigen Texte; klassisch heißen Einheit und Kopiergrund anders.
        var blockers = plan.Blockers.Select(blocker => classic ? GuiTexts.TakeOverBlocker(blocker, classic: true) : GuiTexts.TakeOverBlocker(blocker))
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
            var text = classic ? GuiTexts.TakeOverReason(reason, classic: true) : GuiTexts.TakeOverReason(reason);
            var why = new Border { Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 0, 6), Child = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } };
            why.SetResourceReference(Border.BackgroundProperty, "Theme.NoticeBackground");
            header.Children.Add(why);
        }

        // Hinweise verhindern nichts: umziehende Fenster (Florian 2026-10-17), gewinnende Abhängigkeiten, unsichere DLC-Reihenfolge.
        var notes = Moves.Select(m => GuiTexts.ClassicMoveWarning(m.Window, m.FromFile, m.ToFile, firstWins, m.Dependency))
            .Concat(plan.Warnings.Select(GuiTexts.TakeOverWarning))
            .ToList();
        if (notes.Count > 0)
        {
            var box = new StackPanel();
            box.Children.Add(new TextBlock { Text = GuiTexts.TakeOverWarnings, FontWeight = FontWeights.SemiBold });
            foreach (var text in notes)
            {
                box.Children.Add(new TextBlock { Text = "• " + text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 2, 0, 0) });
            }

            var border = new Border { Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 0, 6), Child = box };
            border.SetResourceReference(Border.BackgroundProperty, "Theme.NoticeBackground");
            header.Children.Add(border);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };

        // Klassisch: Statt der geänderten Fenster die ganze Datei kopieren (die App plant neu). Kopiert der Plan schon die ganze Datei, entfällt die Wahl;
        // ohne Änderung kopierte sie eine unveränderte Vanilla-Datei in die Mod, mit Syntaxfehlern ginge sie nicht.
        if (classic
            && plan.CopyReason is null
            && plan.Entries.Count > 0
            && !plan.Blockers.Any(b => b.Kind == GuiPlanBlockerKind.DraftErrors))
        {
            var copy = new Button { Content = GuiTexts.TakeOverCopyFile, MinWidth = 100, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 8, 0) };
            copy.Click += (_, _) =>
            {
                if (MessageBox.Show(this, GuiTexts.TakeOverCopyFileConfirm, GuiTexts.TakeOverTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    CopyFileRequested = true;
                    DialogResult = false;
                }
            };
            buttons.Children.Add(copy);
        }

        var apply = new Button
        {
            Content = GuiTexts.TakeOverApply,
            IsDefault = true,
            MinWidth = 100,
            Padding = new Thickness(8, 2, 8, 2),
            IsEnabled = plan.CanApply && openModified.Count == 0,
        };
        apply.Click += (_, _) =>
        {
            // Ziehen Fenster um, fragt die App vorher nach (Besitzer ist diese Übersicht); "Nein" bleibt hier.
            if (Moves.Count > 0 && ConfirmMoves is { } confirm && !confirm(this, Moves))
            {
                return;
            }

            DialogResult = true;
        };
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

        var view = new GridView
        {
            Columns =
            {
                new GridViewColumn { Header = GuiTexts.TakeOverColumnAction, DisplayMemberBinding = new Binding(nameof(Row.Action)), Width = 190 },
                new GridViewColumn { Header = GuiTexts.TakeOverColumnName, DisplayMemberBinding = new Binding(nameof(Row.Name)), Width = 230 },
                new GridViewColumn { Header = GuiTexts.TakeOverColumnFile, DisplayMemberBinding = new Binding(nameof(Row.File)), Width = 250 },
            },
        };

        // Klassisch: warum das Fenster in diese Datei kommt (gemessene Ladereihenfolge); nur, wenn ein Eintrag eine Regel hat.
        if (plan.Entries.Any(e => e.Rule is not null))
        {
            view.Columns.Add(new GridViewColumn { Header = GuiTexts.TakeOverColumnRule, DisplayMemberBinding = new Binding(nameof(Row.Rule)), Width = 260 });
        }

        view.Columns.Add(new GridViewColumn { Header = GuiTexts.TakeOverColumnUsages, DisplayMemberBinding = new Binding(nameof(Row.Usages)), Width = 90 });
        var list = new ListView
        {
            ItemsSource = plan.Entries.Select(entry => new Row(
                classic ? GuiTexts.TakeOverKind(entry.Kind, entry.Detail, entry.DefinitionKind) : GuiTexts.TakeOverKind(entry.Kind, entry.Detail),
                NameOf(entry),
                Relative(modRoot, entry.TargetPath),
                entry.Rule is { } rule ? GuiTexts.TakeOverRule(rule) : string.Empty,
                entry.Usages > 0 ? GuiTexts.PaletteUsages(entry.Usages) : string.Empty)).ToList(),
            View = view,
            Visibility = plan.Entries.Count > 0 ? Visibility.Visible : Visibility.Collapsed,
        };
        panel.Children.Add(list);
        Content = panel;
    }

    /// <summary>Klassisch: Fenster, die aus einer verlierenden Mod-Datei in die Zieldatei umziehen (<see cref="ClassicOverridePlanner.Moves"/>).</summary>
    public IReadOnlyList<(string Window, string FromFile, string ToFile, string? Dependency)> Moves { get; }

    /// <summary>
    /// Rückfrage vor dem Übernehmen, wenn Fenster umziehen: bekommt diese Übersicht als Besitzer und die Umzüge, liefert, ob geschrieben werden darf.
    /// Null = keine Rückfrage. Die App setzt sie (MessageBox).
    /// </summary>
    public Func<Window, IReadOnlyList<(string Window, string FromFile, string ToFile, string? Dependency)>, bool>? ConfirmMoves { get; set; }

    /// <summary>Klassisch: "Ganze Datei kopieren…" wurde bestätigt; die Übersicht schließt mit <c>DialogResult = false</c>, die App plant neu.</summary>
    public bool CopyFileRequested { get; private set; }

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
