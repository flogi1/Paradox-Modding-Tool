using System.IO;
using System.Windows;
using ParadoxModTool.SearchReplace.ReplaceLists;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Vorschau einer Ersetzungsliste (Spec 2026-09-30-replace-lists, Abschnitt 3): links die geänderten Dateien mit Anzahl, rechts Vorher/Nachher
/// der gewählten Datei. "Übernehmen" setzt <see cref="Applied"/>; geschrieben wird vom Aufrufer.
/// </summary>
public sealed class ReplaceListPreviewWindow : Window
{
    private readonly Controls.ListBox _files = new() { Margin = new Thickness(0, 0, 6, 0) };
    private readonly DiffComparePane _pane = new();

    public ReplaceListPreviewWindow(ReplaceListRun run, string modRoot, string listName)
    {
        Title = ReplaceListTexts.PreviewTitle(listName);
        Width = 1000;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _pane.Initialize(() => false, _ => { }, _ => true);
        foreach (var change in run.Changes)
        {
            _files.Items.Add(new Controls.ListBoxItem { Content = ReplaceListTexts.PreviewFile(Path.GetRelativePath(modRoot, change.Path), change.Count), Tag = change });
        }

        _files.SelectionChanged += (_, _) =>
        {
            if (_files.SelectedItem is Controls.ListBoxItem { Tag: ReplaceFileChange change })
            {
                _pane.LoadTexts(change.OldText, change.NewText, ReplaceListTexts.PreviewBefore, ReplaceListTexts.PreviewAfter, change.Path);
            }
        };

        var grid = new Controls.Grid { Margin = new Thickness(8) };
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(280), MinWidth = 140 });
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new Controls.RowDefinition { Height = GridLength.Auto });
        Controls.Grid.SetColumn(_files, 0);
        grid.Children.Add(_files);
        var splitter = new Controls.GridSplitter { Width = 6, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
        Controls.Grid.SetColumn(splitter, 1);
        grid.Children.Add(splitter);
        Controls.Grid.SetColumn(_pane, 2);
        grid.Children.Add(_pane);

        var bottom = new Controls.DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        var apply = new Controls.Button { Content = ReplaceListTexts.Apply, Width = 110, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        apply.Click += (_, _) =>
        {
            Applied = true;
            DialogResult = true;
        };
        buttons.Children.Add(apply);
        buttons.Children.Add(new Controls.Button { Content = ReplaceListTexts.Cancel, Width = 110, IsCancel = true });
        Controls.DockPanel.SetDock(buttons, Controls.Dock.Right);
        bottom.Children.Add(buttons);
        if (run.Skipped.Count > 0)
        {
            var skipped = new Controls.TextBlock { Text = ReplaceListTexts.Skipped(run.Skipped.Count), VerticalAlignment = VerticalAlignment.Center, ToolTip = string.Join(Environment.NewLine, run.Skipped) };
            skipped.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
            bottom.Children.Add(skipped);
        }

        Controls.Grid.SetRow(bottom, 1);
        Controls.Grid.SetColumnSpan(bottom, 3);
        grid.Children.Add(bottom);
        Content = grid;

        Loaded += (_, _) =>
        {
            if (_files.Items.Count > 0)
            {
                _files.SelectedIndex = 0;
            }
        };
    }

    public bool Applied { get; private set; }
}
