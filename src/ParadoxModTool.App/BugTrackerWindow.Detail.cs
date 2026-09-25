using System.Diagnostics;
using System.Windows;
using ParadoxModTool.Notes;
using Controls = System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Rechte Seite des Bugtrackers: Kopfdaten, Text (bei lokalen Bugs bearbeitbar), Status-Knöpfe und die Bezüge der Meldung.</summary>
internal sealed partial class BugTrackerWindow
{
    private readonly Controls.TextBlock _noSelectionText = new() { Margin = new Thickness(4) };
    private readonly Controls.DockPanel _detailPanel = new();
    private readonly Controls.TextBox _titleBox = new() { FontWeight = FontWeights.SemiBold, FontSize = 14, TextWrapping = TextWrapping.Wrap };
    private readonly Controls.TextBlock _metaText = new() { FontSize = 11, Margin = new Thickness(0, 4, 0, 4), TextWrapping = TextWrapping.Wrap };
    private readonly Controls.Button _browserButton = new() { Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
    private readonly Controls.TextBox _bodyBox = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto, MinHeight = 120 };
    private readonly Controls.StackPanel _statusButtons = new() { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 6) };
    private readonly Controls.Button _saveButton = new() { Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
    private readonly Controls.Button _deleteButton = new() { Padding = new Thickness(8, 2, 8, 2) };
    private readonly Controls.ListBox _linkList = new() { Height = 140 };
    private readonly Controls.TextBlock _noLinksText = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
    private BugReport? _shown;

    private Controls.Grid BuildDetail()
    {
        var host = new Controls.Grid();
        _noSelectionText.Text = BugTexts.WindowNoSelection;
        _noSelectionText.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        host.Children.Add(_noSelectionText);

        var top = new Controls.StackPanel();
        top.Children.Add(_titleBox);
        _metaText.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        top.Children.Add(_metaText);
        var actions = new Controls.WrapPanel();
        _browserButton.Content = BugTexts.WindowOpenInBrowser;
        _browserButton.Click += (_, _) => OpenInBrowser();
        actions.Children.Add(_browserButton);
        _saveButton.Content = BugTexts.Save;
        _saveButton.Click += (_, _) => SaveLocal();
        actions.Children.Add(_saveButton);
        _deleteButton.Content = BugTexts.WindowDelete;
        _deleteButton.ToolTip = BugTexts.DeleteTip;
        _deleteButton.Click += (_, _) => DeleteLocal();
        actions.Children.Add(_deleteButton);
        top.Children.Add(actions);
        top.Children.Add(_statusButtons);
        Controls.DockPanel.SetDock(top, Controls.Dock.Top);
        _detailPanel.Children.Add(top);

        var links = new Controls.StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        links.Children.Add(new Controls.TextBlock { Text = BugTexts.WindowLinks, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        _noLinksText.Text = BugTexts.WindowNoLinks;
        _noLinksText.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        links.Children.Add(_noLinksText);
        _linkList.ToolTip = BugTexts.WindowLinksTip;
        _linkList.MouseDoubleClick += (_, _) =>
        {
            if ((_linkList.SelectedItem as Controls.ListBoxItem)?.Tag is BugLink link)
            {
                _host.OpenBugLink(link);
            }
        };
        links.Children.Add(_linkList);
        var linkButtons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        var add = new Controls.Button { Content = BugTexts.WindowAddCurrent, ToolTip = BugTexts.WindowAddCurrentTip, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
        add.Click += (_, _) => AddCurrentFileLink();
        var remove = new Controls.Button { Content = BugTexts.WindowRemoveLink, Padding = new Thickness(8, 2, 8, 2) };
        remove.Click += (_, _) => RemoveSelectedLink();
        linkButtons.Children.Add(add);
        linkButtons.Children.Add(remove);
        links.Children.Add(linkButtons);
        Controls.DockPanel.SetDock(links, Controls.Dock.Bottom);
        _detailPanel.Children.Add(links);

        _detailPanel.Children.Add(_bodyBox);
        host.Children.Add(_detailPanel);
        return host;
    }

    private void ShowDetail(BugReport? report)
    {
        _shown = report;
        _noSelectionText.Visibility = report is null ? Visibility.Visible : Visibility.Collapsed;
        _detailPanel.Visibility = report is null ? Visibility.Collapsed : Visibility.Visible;
        if (report is null)
        {
            return;
        }

        var local = report.Source == BugSource.Local;
        _titleBox.Text = report.Title;
        _titleBox.IsReadOnly = !local;
        _bodyBox.Text = report.Body;
        _bodyBox.IsReadOnly = !local;
        var meta = new List<string> { BugTexts.Source(report.Source), BugTexts.Status(report.Status), report.CreatedUtc.ToLocalTime().ToString("g") };
        if (report.Author.Length > 0)
        {
            meta.Insert(1, BugTexts.WindowAuthor(report.Author));
        }

        if (report.Removed)
        {
            meta.Add(BugTexts.Removed);
        }

        _metaText.Text = string.Join(" · ", meta);
        _browserButton.Visibility = report.Url.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _saveButton.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        _deleteButton.Visibility = local ? Visibility.Visible : Visibility.Collapsed;

        _statusButtons.Children.Clear();
        if (report.IsOpen)
        {
            _statusButtons.Children.Add(StatusButton(BugTexts.Done, BugTexts.DoneTip, BugStatus.Done));
            _statusButtons.Children.Add(StatusButton(BugTexts.Reject, BugTexts.RejectTip, BugStatus.Rejected));
        }
        else
        {
            _statusButtons.Children.Add(StatusButton(BugTexts.Reopen, BugTexts.ReopenTip, BugStatus.Open));
        }

        _linkList.Items.Clear();
        foreach (var link in report.Links)
        {
            var text = link.LineText.Length == 0 ? link.DisplayPath : $"{link.DisplayPath}, {link.LineText}";
            _linkList.Items.Add(new Controls.ListBoxItem { Content = new Controls.TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis }, ToolTip = link.Key, Tag = link });
        }

        _noLinksText.Visibility = report.Links.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _linkList.Visibility = report.Links.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private Controls.Button StatusButton(string content, string tip, BugStatus status)
    {
        var button = new Controls.Button { Content = content, ToolTip = tip, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
        button.Click += (_, _) =>
        {
            if (_shown is { } report)
            {
                SetStatus(report.Id, status);
            }
        };
        return button;
    }

    private void SaveLocal()
    {
        if (_shown is not { Source: BugSource.Local } report || _titleBox.Text.Trim().Length == 0)
        {
            return;
        }

        var title = _titleBox.Text.Trim();
        var body = _bodyBox.Text.Trim();
        _host.ChangeBugs(data => BugEditing.UpdateLocal(data, report.Id, title, body));
    }

    private void DeleteLocal()
    {
        if (_shown is not { Source: BugSource.Local } report
            || MessageBox.Show(this, BugTexts.DeleteConfirm(report.Title), BugTexts.HintCaption, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _selectedId = null;
        _host.ChangeBugs(data => BugEditing.Delete(data, report.Id));
    }

    private void OpenInBrowser()
    {
        if (_shown is { Url.Length: > 0 } report && Uri.TryCreate(report.Url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    }

    private void AddCurrentFileLink()
    {
        if (_shown is not { } report)
        {
            return;
        }

        if (_host.CurrentFileLink() is not { } link)
        {
            MessageBox.Show(this, BugTexts.WindowNoActiveFile, BugTexts.HintCaption);
            return;
        }

        _host.ChangeBugs(data => BugEditing.AddLink(data, report.Id, link));
    }

    private void RemoveSelectedLink()
    {
        if (_shown is { } report && (_linkList.SelectedItem as Controls.ListBoxItem)?.Tag is BugLink link)
        {
            _host.ChangeBugs(data => BugEditing.RemoveLink(data, report.Id, link));
        }
    }
}
