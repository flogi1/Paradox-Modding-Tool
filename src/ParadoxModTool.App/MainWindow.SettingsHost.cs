using System.IO;
using System.Security.Cryptography;
using ParadoxModTool.BugTracking;
using System.Windows;
using ParadoxModTool.Editor;
using ParadoxModTool.GitIntegration;
using ParadoxModTool.Localization;
using ParadoxModTool.Settings;
using ParadoxModTool.SettingsUi;
using ParadoxModTool.Theming;
using Color = System.Windows.Media.Color;

namespace ParadoxModTool.App;

/// <summary>
/// Einstellungsfenster: ein Fenster pro Hauptfenster (erneuter Aufruf holt es nach vorn). Das Hauptfenster ist sein Host
/// (<see cref="ISettingsHost"/>): Dialoge, Theme, Farbwahl und Neu-Lernen laufen hier.
/// </summary>
public partial class MainWindow : ISettingsHost
{
    private SettingsWindow? _settingsWindow;

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => OpenSettingsWindow();

    private void OpenSettingsWindow()
    {
        if (_settingsWindow is { } open)
        {
            if (open.WindowState == WindowState.Minimized)
            {
                open.WindowState = WindowState.Normal;
            }

            open.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(this) { Owner = this };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    AppSettings ISettingsHost.Settings => _settings;

    string? ISettingsHost.GameExecutablePath => _gameExecutablePath;

    string? ISettingsHost.PlaysetsFilePath => _playsetsFilePath;

    void ISettingsHost.ChooseGameExecutable() => PromptForGameExecutablePath();

    void ISettingsHost.ChoosePlaysetsFile() => ChoosePlaysetsFile();

    void ISettingsHost.OpenStorageSettings() => OpenStorageSettingsDialog();

    Task ISettingsHost.RelearnLocalizationProfilesAsync() => RelearnLocProfilesAsync();

    Task ISettingsHost.RelearnScopeRulesAsync() => RelearnScopeRulesAsync();

    void ISettingsHost.OpenCategories() => OpenCategoriesDialog();

    void ISettingsHost.SetGuiToolsEnabled(bool enabled) => SetGuiToolsEnabled(enabled);

    Task ISettingsHost.RebuildGuiIndexAsync() => RebuildGuiIndexAsync();

    string ISettingsHost.GameDisplayName => _activeProfile?.DisplayName ?? GameContext.GameId;

    bool ISettingsHost.CanRegisterGameEditor => PdxSettingsPath is { } path && File.Exists(path);

    string ISettingsHost.GameEditorStatus => GameEditorStatusText();

    void ISettingsHost.RegisterGameEditor() => RegisterGameEditor();

    void ISettingsHost.RestoreGameEditor() => RestoreGameEditor();

    string ISettingsHost.CurrentThemeId => ThemeManager.Current.Id;

    string ISettingsHost.CurrentThemeName => ParadoxModTool.Theming.ThemingTexts.DisplayName(ThemeManager.Current);

    Color ISettingsHost.IconColor(string iconId) => TabIconBrushes.CurrentColor(IconBrushKey(iconId));

    Color? ISettingsHost.PickColor(Color current)
    {
        var dialog = new System.Windows.Forms.ColorDialog { Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B), FullOpen = true };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return null;
        }

        return Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
    }

    IReadOnlyList<ThemeChoice> ISettingsHost.ThemeChoices() =>
        new[]
        {
            new ThemeChoice(ThemeSelection.SystemId, SettingsTexts.ThemeSystem),
            new ThemeChoice(ThemeDefinition.LightId, SettingsTexts.ThemeLight),
            new ThemeChoice(ThemeDefinition.DarkId, SettingsTexts.ThemeDark),
        }
        .Concat(ThemeManager.CustomThemes.Select(theme => new ThemeChoice(theme.Id, theme.Name)))
        .ToList();

    string ISettingsHost.SelectedThemeId => ThemeManager.SelectionId;

    void ISettingsHost.SelectTheme(string selectionId) => ThemeManager.SelectTheme(selectionId);

    void ISettingsHost.OpenThemeEditor(Window? owner) => new ThemeEditorWindow { Owner = owner ?? this }.ShowDialog();

    event EventHandler? ISettingsHost.ThemeChanged
    {
        add => ThemeManager.ThemeChanged += value;
        remove => ThemeManager.ThemeChanged -= value;
    }

    IReadOnlyList<UiLanguageOption> ISettingsHost.Languages => LanguageCatalog.Find(LocaleSources.Default());

    LocaleSources ISettingsHost.LocaleSources => LocaleSources.Default();

    string ISettingsHost.TranslationGuideUrl => AppInfo.RepositoryUrl + "/blob/main/TRANSLATING.md";

    void ISettingsHost.OpenTranslationFolder()
    {
        if (LocaleSources.Default().User is not { } folder)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start("explorer.exe", $"\"{ParadoxModTool.Explorer.PathNormalization.ToWindowsPath(folder)}\"");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            // Ordner nicht anlegbar oder Explorer nicht startbar: nichts zu tun.
        }
    }

    bool ISettingsHost.UpdatesAvailable => AppInfo.HasRepository;

    Task ISettingsHost.CheckForUpdatesAsync() => CheckForUpdatesNowAsync();

    async Task<string> ISettingsHost.CheckGitHubStatusAsync()
    {
        if (FindGhPath() is not { } path)
        {
            return SettingsTexts.BugTrackerGhMissing;
        }

        return await new GhClient(_bugProcessRunner, path).IsLoggedInAsync(CancellationToken.None)
            ? SettingsTexts.BugTrackerLoggedIn(path)
            : SettingsTexts.BugTrackerNotLoggedIn(path);
    }

    string ISettingsHost.StartGitHubLogin()
    {
        if (FindGhPath() is not { } path)
        {
            return SettingsTexts.BugTrackerGhMissing;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
            {
                UseShellExecute = true,
                WorkingDirectory = System.IO.Path.GetDirectoryName(path)!,
                Arguments = "auth login --web --hostname github.com --git-protocol https",
            });
            return SettingsTexts.BugTrackerLoginStarted;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return SettingsTexts.BugTrackerLoginFailed(ex.Message);
        }
    }

    bool ISettingsHost.HasDiscordToken => DiscordTokenStore.Load(DiscordTokenPath) is not null;

    string ISettingsHost.SaveDiscordToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return SettingsTexts.DiscordTokenEmpty;
        }

        try
        {
            DiscordTokenStore.Save(DiscordTokenPath, token);
            return SettingsTexts.DiscordTokenSaved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return SettingsTexts.DiscordTokenSaveFailed(ex.Message);
        }
    }

    void ISettingsHost.RemoveDiscordToken()
    {
        try
        {
            DiscordTokenStore.Delete(DiscordTokenPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Beim nächsten Versuch erneut.
        }
    }

    string? ISettingsHost.ReadDiscordToken() => DiscordTokenStore.Load(DiscordTokenPath);

    async Task<DiscordSetupState> ISettingsHost.CheckDiscordSetupAsync()
    {
        if (CreateDiscordClient() is not { } client)
        {
            return new DiscordSetupState(false, false, string.Empty, false, string.Empty, Array.Empty<string>(), null);
        }

        var result = await DiscordSetup.CheckAsync(client, CancellationToken.None);
        return new DiscordSetupState(true, result.TokenValid, result.BotName, result.IntentEnabled, result.InviteUrl, result.Servers, result.Error);
    }

    void ISettingsHost.OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Kein Browser: nichts zu tun.
        }
    }

    /// <summary>Öffnet die Einstellungen (oder holt sie nach vorn) auf einer bestimmten Kategorie.</summary>
    private void OpenSettingsAt(SettingsCategory category)
    {
        OpenSettingsWindow();
        _settingsWindow?.Select(category);
    }

    /// <summary>Ressourcen-Schlüssel des Symbol-Pinsels zu einer Symbol-Kennung.</summary>
    private static string IconBrushKey(string iconId) => iconId switch
    {
        IconIds.Note => TabIconBrushes.NoteKey,
        IconIds.Bookmark => TabIconBrushes.BookmarkKey,
        IconIds.Bug => TabIconBrushes.BugKey,
        IconIds.Vanilla => TabIconBrushes.VanillaKey,
        IconIds.Mod => TabIconBrushes.ModKey,
        _ => throw new ArgumentException($"Unbekanntes Symbol '{iconId}'.", nameof(iconId)),
    };
}
