using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Mods;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>Ein Repository (<c>owner/name</c>) mit Tag-Format anlegen oder ändern.</summary>
public sealed class GitHubRepoDialog : Window
{
    private readonly TextBox _repo = new();
    private readonly TextBox _format = new();
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly bool _enabled;

    private GitHubRepoDialog(GitHubRepoEntry? initial)
    {
        _enabled = initial?.Enabled ?? true;
        Title = initial is null ? "Repository hinzufügen" : "Repository bearbeiten";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;

        _repo.Text = initial?.Repo ?? string.Empty;
        _format.Text = initial?.TagFormat ?? GitHubNames.DefaultTagFormat;
        _error.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");

        var ok = new Button { Content = "OK", Width = 90, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
        ok.Click += (_, _) => Confirm();
        var cancel = new Button { Content = "Abbrechen", Width = 90, IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(new TextBlock { Text = "Repository (owner/name):" });
        panel.Children.Add(_repo);
        panel.Children.Add(new TextBlock { Text = "Tag-Format:", Margin = new Thickness(0, 10, 0, 0) });
        panel.Children.Add(_format);
        panel.Children.Add(new TextBlock
        {
            Text = "Platzhalter: {version} (Pflicht) und {mod}. Beispiel: v{version} ergibt v2.0.5.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        });
        panel.Children.Add(_error);
        panel.Children.Add(buttons);
        Content = panel;
    }

    private GitHubRepoEntry? Result { get; set; }

    /// <summary>Zeigt den Dialog; <see langword="null"/> bei Abbruch.</summary>
    public static GitHubRepoEntry? Ask(Window owner, GitHubRepoEntry? initial = null, string? suggestion = null)
    {
        var dialog = new GitHubRepoDialog(initial) { Owner = owner };
        if (initial is null && suggestion is not null)
        {
            dialog._repo.Text = suggestion;
        }

        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private void Confirm()
    {
        var repo = _repo.Text.Trim();
        var format = string.IsNullOrWhiteSpace(_format.Text) ? GitHubNames.DefaultTagFormat : _format.Text.Trim();
        if (!GitHubNames.IsValidRepo(repo))
        {
            _error.Text = "Das Repository muss die Form owner/name haben (Buchstaben, Ziffern, - _ .).";
            return;
        }

        if (!format.Contains("{version}", StringComparison.Ordinal))
        {
            _error.Text = "Das Tag-Format braucht den Platzhalter {version}.";
            return;
        }

        Result = new GitHubRepoEntry { Repo = repo, TagFormat = format, Enabled = _enabled };
        DialogResult = true;
    }
}
