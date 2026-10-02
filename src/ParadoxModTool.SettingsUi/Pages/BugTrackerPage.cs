using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Pages;

/// <summary>
/// Kategorie Bugtracker: GitHub-Zeile mit gh-Status und Anmeldung; Discord als How-To in fünf Schritten mit Token-Feld und Prüfzeilen
/// (✓/✗), die PMT selbst über die Discord-API prüft.
/// </summary>
public sealed class BugTrackerPage : SettingsPage
{
    private const string PortalUrl = "https://discord.com/developers/applications";

    private readonly PasswordBox _tokenBox = new() { Width = 360, Margin = new Thickness(0, 2, 6, 2) };
    private readonly TextBox _tokenPlain = new() { Width = 360, Margin = new Thickness(0, 2, 6, 2), Visibility = Visibility.Collapsed };
    private readonly TextBlock _tokenLine;
    private readonly TextBlock _intentLine;
    private readonly TextBlock _serversLine;
    private readonly Button _inviteButton;
    private string? _inviteUrl;

    public BugTrackerPage(ISettingsHost host)
        : base(host)
    {
        AddHeading(SettingsTexts.BugTrackerGitHubHeading);
        AddHint(SettingsTexts.BugTrackerGitHubHint);
        var status = AddHint(SettingsTexts.BugTrackerChecking);
        AddTaskButton(SettingsTexts.BugTrackerCheck, null, () => ShowStatusAsync(status));
        AddButton(SettingsTexts.BugTrackerLogin, null, () => status.Text = Host.StartGitHubLogin());
        _ = ShowStatusAsync(status);

        AddHeading(SettingsTexts.DiscordHeading);
        AddHint(SettingsTexts.DiscordHint);
        AddStep(SettingsTexts.DiscordStep1);
        AddButton(SettingsTexts.DiscordOpenPortal, null, () => Host.OpenUrl(PortalUrl));

        AddStep(SettingsTexts.DiscordStep2);
        var tokenRow = new StackPanel { Orientation = Orientation.Horizontal };
        tokenRow.Children.Add(_tokenBox);
        tokenRow.Children.Add(_tokenPlain);
        Root.Children.Add(tokenRow);
        var tokenButtons = new StackPanel { Orientation = Orientation.Horizontal };
        Root.Children.Add(tokenButtons);
        AddButton(SettingsTexts.DiscordSaveToken, null, SaveToken, tokenButtons).Margin = new Thickness(0, 4, 6, 4);
        AddButton(SettingsTexts.DiscordShowToken, null, ToggleShowToken, tokenButtons).Margin = new Thickness(0, 4, 6, 4);
        AddButton(SettingsTexts.DiscordRemoveToken, null, RemoveToken, tokenButtons);
        _tokenLine = AddHint(string.Empty);

        AddStep(SettingsTexts.DiscordStep3);
        _intentLine = AddHint(string.Empty);

        AddStep(SettingsTexts.DiscordStep4);
        _inviteButton = AddButton(SettingsTexts.DiscordOpenInvite, null, () =>
        {
            if (_inviteUrl is { } url)
            {
                Host.OpenUrl(url);
            }
        });
        _serversLine = AddHint(string.Empty);

        AddStep(SettingsTexts.DiscordStep5);
        AddTaskButton(SettingsTexts.DiscordCheck, null, CheckDiscordAsync);

        ShowDiscord(null);
        if (Host.HasDiscordToken)
        {
            _ = CheckDiscordAsync();
        }
    }

    private async Task ShowStatusAsync(TextBlock status) => status.Text = await Host.CheckGitHubStatusAsync();

    private void AddStep(string text)
    {
        var step = Label(text);
        step.Margin = new Thickness(0, 8, 0, 2);
        Root.Children.Add(step);
    }

    private void SaveToken()
    {
        var token = _tokenPlain.Visibility == Visibility.Visible ? _tokenPlain.Text : _tokenBox.Password;
        _tokenBox.Password = string.Empty;
        _tokenPlain.Text = string.Empty;
        _tokenLine.Text = Host.SaveDiscordToken(token);
        if (Host.HasDiscordToken)
        {
            _ = CheckDiscordAsync();
        }
    }

    private void ToggleShowToken()
    {
        var show = _tokenPlain.Visibility != Visibility.Visible;
        _tokenPlain.Text = show ? (_tokenBox.Password.Length > 0 ? _tokenBox.Password : Host.ReadDiscordToken() ?? string.Empty) : string.Empty;
        _tokenPlain.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        _tokenBox.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RemoveToken()
    {
        Host.RemoveDiscordToken();
        _tokenBox.Password = string.Empty;
        _tokenPlain.Text = string.Empty;
        ShowDiscord(null);
        _tokenLine.Text = SettingsTexts.DiscordTokenRemoved;
    }

    private async Task CheckDiscordAsync() => ShowDiscord(await Host.CheckDiscordSetupAsync());

    /// <summary>Prüfzeilen; <see langword="null"/> = noch nicht geprüft.</summary>
    private void ShowDiscord(DiscordSetupState? state)
    {
        _inviteUrl = state is { TokenValid: true, InviteUrl.Length: > 0 } ? state.InviteUrl : null;
        _inviteButton.IsEnabled = _inviteUrl is not null;
        if (state is null || !state.TokenSaved)
        {
            _tokenLine.Text = Host.HasDiscordToken && state is null ? string.Empty : SettingsTexts.DiscordNoToken;
            _intentLine.Text = string.Empty;
            _serversLine.Text = string.Empty;
            return;
        }

        _tokenLine.Text = state.Error is { } error ? "✗ " + SettingsTexts.DiscordCheckFailed(error)
            : state.TokenValid ? "✓ " + SettingsTexts.DiscordTokenValid(state.BotName)
            : "✗ " + SettingsTexts.DiscordTokenInvalid;
        _intentLine.Text = !state.TokenValid ? string.Empty : state.IntentEnabled ? "✓ " + SettingsTexts.DiscordIntentOn : "✗ " + SettingsTexts.DiscordIntentOff;
        _serversLine.Text = !state.TokenValid ? string.Empty
            : state.Servers.Count > 0 ? "✓ " + SettingsTexts.DiscordServers(state.Servers.Count, string.Join(", ", state.Servers))
            : "✗ " + SettingsTexts.DiscordNoServers;
    }
}
