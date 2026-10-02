using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Localization;
using ParadoxModTool.Mods;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>Eine ZIP-Variante anlegen oder ändern (Name, Dateiname-Vorlage, Ausschlüsse, Ordnerauswahl, Wurzelordner). Listenfelder: ein Eintrag je Zeile.</summary>
public sealed class ZipVariantDialog : Window
{
    private readonly TextBox _name = new();
    private readonly TextBox _format = new();
    private readonly TextBox _folders = Multi();
    private readonly TextBox _patterns = Multi();
    private readonly TextBox _only = Multi();
    private readonly TextBox _root = new();
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly bool _enabled;

    private ZipVariantDialog(GitHubZipVariant? initial)
    {
        _enabled = initial?.Enabled ?? true;
        Title = initial is null ? PublishTexts.ZipAddTitle : PublishTexts.ZipEditTitle;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;

        _name.Text = initial?.Name ?? string.Empty;
        _format.Text = initial?.FileNameFormat ?? GitHubNames.DefaultFileNameFormat;
        _folders.Text = string.Join("\n", initial?.ExcludedFolders ?? new List<string>());
        _patterns.Text = string.Join("\n", initial?.ExcludedFilePatterns ?? new List<string>());
        _only.Text = string.Join("\n", initial?.OnlyFolders ?? new List<string>());
        _root.Text = initial?.RootFolder ?? string.Empty;
        _error.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");

        var ok = new Button { Content = "OK", Width = 90, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
        ok.Click += (_, _) => Confirm();
        var cancel = new Button { Content = CommonTexts.Cancel, Width = 90, IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(12) };
        Add(panel, "Name:", _name, null);
        Add(panel, PublishTexts.ZipFileName, _format, PublishTexts.ZipFileNameHint);
        Add(panel, PublishTexts.ZipExcludedFolders, _folders, PublishTexts.ZipExcludedFoldersHint);
        Add(panel, PublishTexts.ZipExcludedFiles, _patterns, PublishTexts.ZipExcludedFilesHint);
        Add(panel, PublishTexts.ZipOnlyFolders, _only, PublishTexts.ZipOnlyFoldersHint);
        Add(panel, PublishTexts.ZipRootFolder, _root, PublishTexts.ZipRootFolderHint);
        panel.Children.Add(_error);
        panel.Children.Add(buttons);
        Content = panel;
    }

    private GitHubZipVariant? Result { get; set; }

    /// <summary>Zeigt den Dialog; <see langword="null"/> bei Abbruch. Die Prüfung auf doppelte Namen usw. macht das Speichern in <see cref="ModRegistry"/>.</summary>
    public static GitHubZipVariant? Ask(Window owner, GitHubZipVariant? initial = null)
    {
        var dialog = new ZipVariantDialog(initial) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private static TextBox Multi() => new() { AcceptsReturn = true, Height = 54, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

    private static void Add(StackPanel panel, string label, TextBox box, string? hint)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 2), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(box);
        if (hint is not null)
        {
            panel.Children.Add(new TextBlock { Text = hint, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        }
    }

    private static List<string> Lines(TextBox box) =>
        box.Text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            _error.Text = PublishTexts.ZipNameMissing;
            return;
        }

        Result = new GitHubZipVariant
        {
            Name = _name.Text.Trim(),
            FileNameFormat = string.IsNullOrWhiteSpace(_format.Text) ? GitHubNames.DefaultFileNameFormat : _format.Text.Trim(),
            ExcludedFolders = Lines(_folders),
            ExcludedFilePatterns = Lines(_patterns),
            OnlyFolders = Lines(_only),
            RootFolder = string.IsNullOrWhiteSpace(_root.Text) ? null : _root.Text.Trim(),
            Enabled = _enabled,
        };
        DialogResult = true;
    }
}
