using System.Windows;
using ParadoxModTool.BugTracking;
using ParadoxModTool.Mods;
using ParadoxModTool.Notes;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Forum verknüpfen: Server und Forum-Channel aus dem, was der Bot sieht, die drei Status-Tags, optionale Filter-Tags und die Archiv-Option.
/// "Speichern" prüft vorher die Rechte des Bots im Channel und nennt fehlende.
/// </summary>
public sealed class DiscordForumDialog : Window
{
    private readonly DiscordClient _client;
    private readonly Controls.ComboBox _serverBox = new() { Width = 360, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.ComboBox _forumBox = new() { Width = 360, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.ComboBox _openBox = new() { Width = 240, Margin = new Thickness(0, 0, 0, 4) };
    private readonly Controls.ComboBox _doneBox = new() { Width = 240, Margin = new Thickness(0, 0, 0, 4) };
    private readonly Controls.ComboBox _rejectedBox = new() { Width = 240, Margin = new Thickness(0, 0, 0, 4) };
    private readonly Controls.WrapPanel _filterPanel = new() { Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.CheckBox _archiveBox = new() { IsChecked = true, Margin = new Thickness(0, 4, 0, 4) };
    private readonly Controls.TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.Button _saveButton = new() { Width = 90, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
    private bool _filling;

    public DiscordForumDialog(DiscordClient client)
    {
        _client = client;
        Title = BugTexts.ForumTitle;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;

        var root = new Controls.StackPanel { Margin = new Thickness(12) };
        root.Children.Add(Caption(BugTexts.ForumServer));
        root.Children.Add(_serverBox);
        root.Children.Add(Caption(BugTexts.ForumChannel));
        root.Children.Add(_forumBox);
        root.Children.Add(Caption(BugTexts.ForumTag(BugStatus.Open)));
        root.Children.Add(_openBox);
        root.Children.Add(Caption(BugTexts.ForumTag(BugStatus.Done)));
        root.Children.Add(_doneBox);
        root.Children.Add(Caption(BugTexts.ForumTag(BugStatus.Rejected)));
        root.Children.Add(_rejectedBox);
        root.Children.Add(Caption(BugTexts.ForumFilter));
        root.Children.Add(_filterPanel);
        _archiveBox.Content = BugTexts.ForumArchive;
        root.Children.Add(_archiveBox);
        _status.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        root.Children.Add(_status);

        _saveButton.Content = BugTexts.Save;
        _saveButton.Click += async (_, _) => await SaveAsync();
        var cancel = new Controls.Button { Content = BugTexts.Cancel, Width = 90, IsCancel = true };
        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(_saveButton);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);
        Content = root;

        _serverBox.SelectionChanged += async (_, _) =>
        {
            if (!_filling)
            {
                await LoadForumsAsync();
            }
        };
        _forumBox.SelectionChanged += (_, _) =>
        {
            if (!_filling)
            {
                FillTags();
            }
        };
        Loaded += async (_, _) => await LoadServersAsync();
    }

    /// <summary>Die verknüpfte Quelle nach "Speichern"; sonst <see langword="null"/>.</summary>
    public BugSourceEntry? Result { get; private set; }

    private DiscordGuild? SelectedServer => (_serverBox.SelectedItem as Controls.ComboBoxItem)?.Tag as DiscordGuild;

    private DiscordChannel? SelectedForum => (_forumBox.SelectedItem as Controls.ComboBoxItem)?.Tag as DiscordChannel;

    private static Controls.TextBlock Caption(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 2) };

    private static string? TagId(Controls.ComboBox box) => (box.SelectedItem as Controls.ComboBoxItem)?.Tag as string;

    private async Task LoadServersAsync()
    {
        _status.Text = BugTexts.ForumLoading;
        try
        {
            var guilds = await _client.GetGuildsAsync(CancellationToken.None);
            _filling = true;
            foreach (var guild in guilds.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
            {
                _serverBox.Items.Add(new Controls.ComboBoxItem { Content = guild.Name, Tag = guild });
            }

            _filling = false;
            _status.Text = guilds.Count == 0 ? BugTexts.ForumNoServers : string.Empty;
            if (guilds.Count > 0)
            {
                _serverBox.SelectedIndex = 0;
            }
        }
        catch (DiscordException ex)
        {
            _filling = false;
            _status.Text = DiscordBugSource.MessageFor(ex);
        }
    }

    private async Task LoadForumsAsync()
    {
        if (SelectedServer is not { } server)
        {
            return;
        }

        _status.Text = BugTexts.ForumLoading;
        try
        {
            var forums = (await _client.GetGuildChannelsAsync(server.Id, CancellationToken.None))
                .Where(channel => channel.Type == DiscordClient.ForumChannelType)
                .OrderBy(channel => channel.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _filling = true;
            _forumBox.Items.Clear();
            foreach (var forum in forums)
            {
                _forumBox.Items.Add(new Controls.ComboBoxItem { Content = "#" + forum.Name, Tag = forum });
            }

            _filling = false;
            _status.Text = forums.Count == 0 ? BugTexts.ForumNoForums : string.Empty;
            if (forums.Count > 0)
            {
                _forumBox.SelectedIndex = 0;
            }
            else
            {
                FillTags();
            }
        }
        catch (DiscordException ex)
        {
            _filling = false;
            _status.Text = DiscordBugSource.MessageFor(ex);
        }
    }

    /// <summary>Tags des Forums in die drei Auswahllisten und die Filter; vorbelegt, wenn ein Tag wie der Status heißt.</summary>
    private void FillTags()
    {
        var tags = SelectedForum?.Tags ?? Array.Empty<DiscordTag>();
        foreach (var (box, status) in new[] { (_openBox, BugStatus.Open), (_doneBox, BugStatus.Done), (_rejectedBox, BugStatus.Rejected) })
        {
            box.Items.Clear();
            foreach (var tag in tags)
            {
                var item = new Controls.ComboBoxItem { Content = tag.Name, Tag = tag.Id };
                box.Items.Add(item);
                if (tag.Name.Equals(BugTexts.Status(status), StringComparison.OrdinalIgnoreCase))
                {
                    box.SelectedItem = item;
                }
            }
        }

        _filterPanel.Children.Clear();
        foreach (var tag in tags)
        {
            _filterPanel.Children.Add(new Controls.CheckBox { Content = new Controls.TextBlock { Text = tag.Name }, Tag = tag.Id, Margin = new Thickness(0, 2, 10, 2) });
        }
    }

    private async Task SaveAsync()
    {
        var ids = new[] { TagId(_openBox), TagId(_doneBox), TagId(_rejectedBox) };
        if (SelectedServer is not { } server || SelectedForum is not { } forum || ids.Any(id => id is null) || ids.Distinct().Count() != 3)
        {
            _status.Text = BugTexts.ForumChooseAll;
            return;
        }

        _saveButton.IsEnabled = false;
        try
        {
            var missing = await DiscordSetup.MissingPermissionsAsync(_client, forum, CancellationToken.None);
            if (missing.Count > 0)
            {
                _status.Text = BugTexts.ForumMissingRights(string.Join(", ", missing.Select(BugTexts.PermissionName)));
                return;
            }
        }
        catch (DiscordException ex)
        {
            _status.Text = DiscordBugSource.MessageFor(ex);
            return;
        }
        finally
        {
            _saveButton.IsEnabled = true;
        }

        Result = new BugSourceEntry
        {
            Kind = BugSourceKinds.Discord,
            Key = $"{server.Id}/{forum.Id}",
            Name = $"{server.Name} / #{forum.Name}",
            OpenTagId = ids[0]!,
            DoneTagId = ids[1]!,
            RejectedTagId = ids[2]!,
            ArchiveClosed = _archiveBox.IsChecked == true,
            Labels = _filterPanel.Children.OfType<Controls.CheckBox>().Where(box => box.IsChecked == true).Select(box => (string)box.Tag).ToList(),
        };
        DialogResult = true;
    }
}
