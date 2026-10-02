using System.Windows;
using ParadoxModTool.ModOverview;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Dialog "Links bearbeiten" der Übersicht "Meine Mods": Forum-Link, Discord-Einladung und eigener GitHub-Link. Nur http- und https-Adressen; ein leeres
/// Feld entfernt den Link. Kommt GitHub aus einem eingetragenen Repo, ist das Feld nur zur Anzeige.
/// </summary>
internal sealed class ModLinksDialog : Window
{
    private readonly Controls.TextBox _forum = Field();
    private readonly Controls.TextBox _discord = Field();
    private readonly Controls.TextBox _gitHub = Field();
    private readonly Controls.TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private readonly bool _gitHubFromRepo;

    private ModLinksDialog(string modName, ModLinkEntry? current, EffectiveLinks effective)
    {
        Title = OverviewTexts.LinksTitle(modName);
        Width = 520;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _gitHubFromRepo = effective.GitHubFromRepo;

        _forum.Text = current?.ForumUrl ?? string.Empty;
        _discord.Text = current?.DiscordUrl ?? string.Empty;
        _gitHub.Text = _gitHubFromRepo ? effective.GitHub ?? string.Empty : current?.GitHubUrl ?? string.Empty;
        _gitHub.IsReadOnly = _gitHubFromRepo;

        var panel = new Controls.StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(Label(OverviewTexts.LinksForum));
        panel.Children.Add(_forum);
        panel.Children.Add(Label(OverviewTexts.LinksDiscord));
        panel.Children.Add(_discord);
        panel.Children.Add(Label(OverviewTexts.LinksGitHub));
        panel.Children.Add(_gitHub);
        if (_gitHubFromRepo)
        {
            var hint = new Controls.TextBlock { Text = OverviewTexts.LinksGitHubFromRepo, Margin = new Thickness(0, 2, 0, 0) };
            hint.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
            panel.Children.Add(hint);
        }

        _error.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.Danger");
        panel.Children.Add(_error);

        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var save = new Controls.Button { Content = OverviewTexts.LinksSave, IsDefault = true, Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 0, 6, 0) };
        var cancel = new Controls.Button { Content = OverviewTexts.LinksCancel, IsCancel = true, Padding = new Thickness(12, 2, 12, 2) };
        save.Click += (_, _) => Save();
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => _forum.Focus();
    }

    /// <summary>Zeigt den Dialog; bei "Speichern" die drei Werte (leer = entfernen, GitHub <see langword="null"/> = aus dem Repo, nicht speichern).</summary>
    public static bool Edit(Window owner, string modName, ModLinkEntry? current, EffectiveLinks effective, out (string? Forum, string? Discord, string? GitHub) result)
    {
        var dialog = new ModLinksDialog(modName, current, effective) { Owner = owner };
        if (dialog.ShowDialog() != true)
        {
            result = default;
            return false;
        }

        result = (dialog._forum.Text.Trim(), dialog._discord.Text.Trim(), dialog._gitHubFromRepo ? current?.GitHubUrl : dialog._gitHub.Text.Trim());
        return true;
    }

    private void Save()
    {
        var fields = _gitHubFromRepo ? new[] { _forum, _discord } : new[] { _forum, _discord, _gitHub };
        if (fields.FirstOrDefault(f => f.Text.Trim().Length > 0 && !ModLinkStore.IsValidUrl(f.Text)) is { } invalid)
        {
            _error.Text = OverviewTexts.LinksInvalid(invalid.Text.Trim());
            _error.Visibility = Visibility.Visible;
            invalid.Focus();
            invalid.SelectAll();
            return;
        }

        DialogResult = true;
    }

    private static Controls.TextBox Field() => new() { Margin = new Thickness(0, 2, 0, 0) };

    private static Controls.TextBlock Label(string text) => new() { Text = text, Margin = new Thickness(0, 8, 0, 0) };
}
