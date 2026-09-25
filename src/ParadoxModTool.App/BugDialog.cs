using System.IO;
using System.Windows;
using ParadoxModTool.Notes;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Dialog "Bug": neuen Bug anlegen oder die aktuelle Stelle an eine bestehende offene Meldung des Projekts hängen (<see cref="ForCreate"/>),
/// oder Titel und Text eines lokalen Bugs bearbeiten (<see cref="ForEdit"/>).
/// </summary>
public sealed class BugDialog : Window
{
    private readonly Controls.RadioButton _newRadio = new() { Margin = new Thickness(0, 0, 12, 0) };
    private readonly Controls.RadioButton _attachRadio = new();
    private readonly Controls.TextBox _titleBox = new() { MaxLength = 200 };
    private readonly Controls.TextBox _descriptionBox = new()
    {
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        Height = 110,
        VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto,
    };
    private readonly Controls.TextBox _searchBox = new();
    private readonly Controls.ListBox _reportList = new() { Height = 170 };
    private readonly Controls.TextBlock _noReportsText = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly Controls.CheckBox _lineCheckBox = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly Controls.Button _saveButton = new() { Width = 90, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
    private readonly Controls.StackPanel _newPanel = new();
    private readonly Controls.StackPanel _attachPanel = new();
    private readonly IReadOnlyList<BugReport> _candidates;

    private BugDialog(string? filePath, string title, string description, string? lineText, IReadOnlyList<BugReport>? candidates, bool attach)
    {
        Title = BugTexts.DialogTitle;
        Width = 480;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        _candidates = candidates ?? Array.Empty<BugReport>();

        var root = new Controls.StackPanel { Margin = new Thickness(12) };
        if (filePath is not null)
        {
            var file = new Controls.TextBlock { Text = Path.GetFileName(filePath), ToolTip = filePath, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 8) };
            file.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
            root.Children.Add(file);
        }

        if (candidates is not null)
        {
            _newRadio.Content = BugTexts.DialogNew;
            _attachRadio.Content = BugTexts.DialogAttach;
            _newRadio.Checked += (_, _) => UpdateMode();
            _attachRadio.Checked += (_, _) => UpdateMode();
            var modes = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            modes.Children.Add(_newRadio);
            modes.Children.Add(_attachRadio);
            root.Children.Add(modes);
        }

        _newPanel.Children.Add(new Controls.TextBlock { Text = BugTexts.DialogBugTitle, Margin = new Thickness(0, 0, 0, 2) });
        _newPanel.Children.Add(_titleBox);
        _newPanel.Children.Add(new Controls.TextBlock { Text = BugTexts.DialogDescription, Margin = new Thickness(0, 8, 0, 2) });
        _newPanel.Children.Add(_descriptionBox);
        root.Children.Add(_newPanel);

        _attachPanel.Children.Add(new Controls.TextBlock { Text = BugTexts.DialogSearch, Margin = new Thickness(0, 0, 0, 2) });
        _attachPanel.Children.Add(_searchBox);
        _attachPanel.Children.Add(new Controls.Border { Height = 6 });
        _attachPanel.Children.Add(_reportList);
        _noReportsText.Text = BugTexts.DialogNoReports;
        _attachPanel.Children.Add(_noReportsText);
        root.Children.Add(_attachPanel);

        if (lineText is not null)
        {
            _lineCheckBox.Content = BugTexts.DialogUseLine(lineText);
            _lineCheckBox.IsChecked = true;
            root.Children.Add(_lineCheckBox);
        }
        else
        {
            _lineCheckBox.Visibility = Visibility.Collapsed;
        }

        _saveButton.Content = BugTexts.Save;
        _saveButton.Click += (_, _) => DialogResult = true;
        var cancel = new Controls.Button { Content = BugTexts.Cancel, Width = 90, IsCancel = true };
        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(_saveButton);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);
        Content = root;

        _titleBox.Text = title;
        _descriptionBox.Text = description;
        _titleBox.TextChanged += (_, _) => UpdateSaveEnabled();
        _searchBox.TextChanged += (_, _) => FillReports();
        _reportList.SelectionChanged += (_, _) => UpdateSaveEnabled();
        _reportList.MouseDoubleClick += (_, _) =>
        {
            if (_reportList.SelectedItem is not null)
            {
                DialogResult = true;
            }
        };

        FillReports();
        if (attach && candidates is not null)
        {
            _attachRadio.IsChecked = true;
        }
        else
        {
            _newRadio.IsChecked = true;
        }

        UpdateMode();
        Loaded += (_, _) =>
        {
            if (AttachMode)
            {
                _searchBox.Focus();
            }
            else
            {
                _titleBox.Focus();
                _titleBox.CaretIndex = _titleBox.Text.Length;
            }
        };
    }

    /// <summary>Neuer Bug bzw. Anhängen an eine der <paramref name="candidates"/> (offene Meldungen des Projekts).</summary>
    public static BugDialog ForCreate(string filePath, string? lineText, IReadOnlyList<BugReport> candidates, bool attach) =>
        new(filePath, string.Empty, string.Empty, lineText, candidates, attach);

    /// <summary>Titel und Text eines lokalen Bugs bearbeiten; ohne Modus-Auswahl und ohne Zeilenbezug (Bezüge pflegt der Bugtracker).</summary>
    public static BugDialog ForEdit(string title, string description) =>
        new(null, title, description, null, null, attach: false);

    public bool AttachMode => _attachRadio.IsChecked == true;

    public string BugTitle => _titleBox.Text.Trim();

    public string Description => _descriptionBox.Text.Trim();

    public bool UseLine => _lineCheckBox.Visibility == Visibility.Visible && _lineCheckBox.IsChecked == true;

    public string? SelectedReportId => (_reportList.SelectedItem as Controls.ListBoxItem)?.Tag as string;

    private void UpdateMode()
    {
        _newPanel.Visibility = AttachMode ? Visibility.Collapsed : Visibility.Visible;
        _attachPanel.Visibility = AttachMode ? Visibility.Visible : Visibility.Collapsed;
        UpdateSaveEnabled();
    }

    private void FillReports()
    {
        var search = _searchBox.Text.Trim();
        var selected = SelectedReportId;
        _reportList.Items.Clear();
        foreach (var report in _candidates.Where(r => search.Length == 0
            || r.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
            || r.Body.Contains(search, StringComparison.OrdinalIgnoreCase)))
        {
            var item = new Controls.ListBoxItem { Content = new Controls.TextBlock { Text = $"{BugListRow.IconFor(report.Source)} {report.Title}", TextTrimming = TextTrimming.CharacterEllipsis }, Tag = report.Id };
            _reportList.Items.Add(item);
            if (report.Id == selected)
            {
                _reportList.SelectedItem = item;
            }
        }

        _noReportsText.Visibility = _candidates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateSaveEnabled();
    }

    private void UpdateSaveEnabled() =>
        _saveButton.IsEnabled = AttachMode ? SelectedReportId is not null : BugTitle.Length > 0;
}
