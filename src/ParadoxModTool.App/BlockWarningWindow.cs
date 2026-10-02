using System.Windows;
using ParadoxModTool.Localization;
using ParadoxModTool.SearchReplace;
using Button = System.Windows.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;

namespace ParadoxModTool.App;

/// <summary>Eine Fundstelle in der Warnliste: Anzeigename der Datei (oder "Aktuelles Dokument"), der Block und der Pfad der Datei (<see langword="null"/> = aktives Dokument).</summary>
public sealed record BlockWarningHit(string File, LogicBlock Block, string? FilePath);

/// <summary>
/// Meldet die Warnungen nach einem Ersetzen (OR-, NOR- oder AND-Blöcke mit höchstens einem Eintrag, siehe <see cref="SingleEntryBlockFinder"/>) an das
/// Hauptfenster, das sie im Notiz-Panel oder in einem eigenen Fenster zeigt. Suchfenster und Ersetzungslisten kennen das Hauptfenster nicht; eine leere Liste
/// räumt die Anzeige des letzten Ersetzens ab.
/// </summary>
public static class ReplaceWarningHub
{
    public static event Action<IReadOnlyList<BlockWarningHit>>? Reported;

    public static void Report(IReadOnlyList<BlockWarningHit> hits) => Reported?.Invoke(hits);
}

/// <summary>
/// Hinweisfenster nach dem Ersetzen (Einstellung "Warnungen nach dem Ersetzen in eigenem Fenster zeigen" oder ausgeschaltetes Notiz-Panel).
/// Nicht modal, damit man beim Aufräumen im Editor weiterarbeiten kann; ein Doppelklick auf eine Zeile öffnet die Stelle.
/// </summary>
public sealed class BlockWarningWindow : Window
{
    private const int MaxRows = 200;

    private BlockWarningWindow(IReadOnlyList<BlockWarningHit> hits, Action<BlockWarningHit> open)
    {
        Title = SearchTexts.BlockWarningTitle;
        Width = 560;
        Height = 360;
        MinWidth = 380;
        MinHeight = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new System.Windows.Controls.DockPanel { Margin = new Thickness(14) };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var ok = new Button { Content = CommonTexts.Ok, Width = 80, IsDefault = true, IsCancel = true };
        ok.Click += (_, _) => Close();
        buttons.Children.Add(ok);
        System.Windows.Controls.DockPanel.SetDock(buttons, System.Windows.Controls.Dock.Bottom);
        panel.Children.Add(buttons);

        var text = new TextBlock { Text = SearchTexts.BlockWarningIntro(hits.Count) + " " + SearchTexts.BlockWarningOpenHint, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        System.Windows.Controls.DockPanel.SetDock(text, System.Windows.Controls.Dock.Top);
        panel.Children.Add(text);

        var list = new ListBox();
        foreach (var hit in hits.Take(MaxRows))
        {
            var item = new ListBoxItem { Content = SearchTexts.BlockWarningText(hit), Tag = hit };
            item.MouseDoubleClick += (_, _) => open(hit);
            list.Items.Add(item);
        }

        if (hits.Count > MaxRows)
        {
            list.Items.Add(new ListBoxItem { Content = SearchTexts.BlockWarningMore(hits.Count - MaxRows), IsEnabled = false });
        }

        panel.Children.Add(list);
        Content = panel;
    }

    /// <summary>Zeigt das Fenster, wenn es Fundstellen gibt, und gibt es zurück; sonst nichts (<see langword="null"/>).</summary>
    public static BlockWarningWindow? ShowIfAny(Window? owner, IReadOnlyList<BlockWarningHit> hits, Action<BlockWarningHit> open)
    {
        if (hits.Count == 0)
        {
            return null;
        }

        var window = new BlockWarningWindow(hits, open) { Owner = owner };
        window.Show();
        return window;
    }
}
