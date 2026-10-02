using System.Windows;
using ParadoxModTool.BugTracking;
using ParadoxModTool.Mods;
using ParadoxModTool.Notes;
using Controls = System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Quellen für Bugreports eines Projekts: GitHub-Repositories mit Schalter "Abrufen" und Label-Filter. Quellen anderer Arten (Discord, Plan 3)
/// bleiben unverändert erhalten. Gespeichert wird erst mit "Speichern".
/// </summary>
public sealed class BugSourcesDialog : Window
{
    private sealed class Row
    {
        public required BugSourceEntry Entry { get; init; }

        public required Controls.CheckBox Enabled { get; init; }

        public required Controls.TextBox? Labels { get; init; }
    }

    private readonly List<Row> _rows = new();
    private readonly List<BugSourceEntry> _otherKinds;
    private readonly Controls.StackPanel _list = new();
    private readonly Controls.TextBlock _emptyText = new() { Margin = new Thickness(0, 4, 0, 4) };
    private readonly Controls.ComboBox _repoBox = new() { IsEditable = true, Width = 260, Margin = new Thickness(0, 0, 6, 0) };
    private readonly Func<string, Task<IReadOnlyList<string>>> _loadLabels;

    public BugSourcesDialog(string projectName, IReadOnlyList<BugSourceEntry> current, IReadOnlyList<string> suggestedRepos, Func<string, Task<IReadOnlyList<string>>> loadLabels, Action openSettings, Func<Window, BugSourceEntry?> addDiscord)
    {
        _loadLabels = loadLabels;
        _otherKinds = current.Where(source => source.Kind != BugSourceKinds.GitHub && source.Kind != BugSourceKinds.Discord).ToList();
        Title = BugTexts.SourcesTitle;
        Width = 620;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;

        var root = new Controls.StackPanel { Margin = new Thickness(12) };
        root.Children.Add(new Controls.TextBlock { Text = BugTexts.SourcesProject(projectName), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        _emptyText.Text = BugTexts.SourcesEmpty;
        _emptyText.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        root.Children.Add(_emptyText);
        root.Children.Add(new Controls.ScrollViewer { Content = _list, MaxHeight = 360, VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto });

        root.Children.Add(new Controls.TextBlock { Text = BugTexts.SourcesRepo, Margin = new Thickness(0, 12, 0, 2) });
        foreach (var repo in suggestedRepos)
        {
            _repoBox.Items.Add(repo);
        }

        var add = new Controls.Button { Content = BugTexts.SourcesAdd, Padding = new Thickness(8, 2, 8, 2) };
        add.Click += (_, _) => AddRepo();
        var addRow = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        addRow.Children.Add(_repoBox);
        addRow.Children.Add(add);
        root.Children.Add(addRow);
        var addDiscordButton = new Controls.Button { Content = BugTexts.SourcesAddDiscord, Padding = new Thickness(8, 2, 8, 2), HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) };
        addDiscordButton.Click += (_, _) =>
        {
            if (addDiscord(this) is not { } entry)
            {
                return;
            }

            if (_rows.Any(row => row.Entry.Kind == entry.Kind && row.Entry.Key == entry.Key))
            {
                MessageBox.Show(this, BugTexts.SourcesDuplicate(entry.Name), BugTexts.HintCaption);
                return;
            }

            AddRow(entry);
            UpdateEmpty();
        };
        root.Children.Add(addDiscordButton);

        var hint = new Controls.TextBlock { Text = BugTexts.SourcesSetupHint, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 2) };
        hint.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        root.Children.Add(hint);
        var settings = new Controls.Button { Content = BugTexts.SourcesOpenSettings, Padding = new Thickness(8, 2, 8, 2), HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
        settings.Click += (_, _) => openSettings();
        root.Children.Add(settings);

        var save = new Controls.Button { Content = BugTexts.Save, Width = 90, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
        save.Click += (_, _) => DialogResult = true;
        var cancel = new Controls.Button { Content = BugTexts.Cancel, Width = 90, IsCancel = true };
        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);
        Content = root;

        foreach (var source in current.Where(source => source.Kind is BugSourceKinds.GitHub or BugSourceKinds.Discord))
        {
            AddRow(source);
        }

        UpdateEmpty();
    }

    /// <summary>Alle Quellen nach der Bearbeitung: GitHub- und Discord-Zeilen plus unveränderte Quellen unbekannter Arten.</summary>
    public IReadOnlyList<BugSourceEntry> Sources => _rows
        .Select(row =>
        {
            var entry = row.Entry.Clone();
            entry.Enabled = row.Enabled.IsChecked == true;
            if (row.Labels is not null)
            {
                entry.Labels = SplitLabels(row.Labels.Text);
            }

            return entry;
        })
        .Concat(_otherKinds)
        .ToList();

    private static List<string> SplitLabels(string text) =>
        text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private void AddRepo()
    {
        var repo = _repoBox.Text.Trim();
        if (!GitHubNames.IsValidRepo(repo))
        {
            MessageBox.Show(this, BugTexts.SourcesInvalidRepo(repo), BugTexts.HintCaption);
            return;
        }

        if (_rows.Any(row => row.Entry.Kind == BugSourceKinds.GitHub && row.Entry.Key.Equals(repo, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, BugTexts.SourcesDuplicate(repo), BugTexts.HintCaption);
            return;
        }

        AddRow(new BugSourceEntry { Kind = BugSourceKinds.GitHub, Key = repo });
        _repoBox.Text = string.Empty;
        UpdateEmpty();
    }

    private void AddRow(BugSourceEntry source)
    {
        var panel = new Controls.StackPanel { Margin = new Thickness(0, 4, 0, 8) };
        var header = new Controls.DockPanel();
        var remove = new Controls.Button { Content = BugTexts.SourcesRemove, Padding = new Thickness(8, 0, 8, 0) };
        Controls.DockPanel.SetDock(remove, Controls.Dock.Right);
        header.Children.Add(remove);
        var enabled = new Controls.CheckBox { Content = BugTexts.SourcesEnabled, IsChecked = source.Enabled, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        header.Children.Add(enabled);
        var discord = source.Kind == BugSourceKinds.Discord;
        header.Children.Add(new Controls.TextBlock
        {
            Text = (discord ? "💬 " : "🐙 ") + (discord && source.Name.Length > 0 ? source.Name : source.Key),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        panel.Children.Add(header);

        Controls.TextBox? labels = null;
        Controls.Button? pick = null;
        if (discord)
        {
            if (source.Labels.Count > 0)
            {
                panel.Children.Add(new Controls.TextBlock { Text = BugTexts.SourcesDiscordFilter(source.Labels.Count), FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });
            }
        }
        else
        {
            panel.Children.Add(new Controls.TextBlock { Text = BugTexts.SourcesLabels, FontSize = 11, Margin = new Thickness(0, 4, 0, 2) });
            labels = new Controls.TextBox { Text = string.Join(", ", source.Labels), Width = 380, Margin = new Thickness(0, 0, 6, 0) };
            pick = new Controls.Button { Content = BugTexts.SourcesPickLabels, Padding = new Thickness(8, 0, 8, 0) };
            var labelRow = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
            labelRow.Children.Add(labels);
            labelRow.Children.Add(pick);
            panel.Children.Add(labelRow);
        }

        var row = new Row { Entry = source, Enabled = enabled, Labels = labels };
        remove.Click += (_, _) =>
        {
            _rows.Remove(row);
            _list.Children.Remove(panel);
            UpdateEmpty();
        };
        if (pick is not null)
        {
            pick.Click += async (_, _) => await PickLabelsAsync(row, pick);
        }

        _rows.Add(row);
        _list.Children.Add(panel);
    }

    private async Task PickLabelsAsync(Row row, Controls.Button button)
    {
        if (row.Labels is not { } box)
        {
            return;
        }

        button.IsEnabled = false;
        try
        {
            var available = await _loadLabels(row.Entry.Key);
            var dialog = new LabelPickDialog(row.Entry.Key, available, SplitLabels(box.Text)) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                box.Text = string.Join(", ", dialog.Selected);
            }
        }
        catch (BugSourceException ex)
        {
            MessageBox.Show(this, ex.Message, BugTexts.ErrorCaption);
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void UpdateEmpty() => _emptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
}
