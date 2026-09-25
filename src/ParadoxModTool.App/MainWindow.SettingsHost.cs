using System.IO;
using System.Windows;
using ParadoxModTool.Editor;
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

    string ISettingsHost.CurrentThemeName => ThemeManager.Current.Name;

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

    IReadOnlyList<UiLanguageOption> ISettingsHost.Languages => LanguageCatalog.Find(Path.Combine(AppContext.BaseDirectory, "locales"));

    bool ISettingsHost.UpdatesAvailable => AppInfo.HasRepository;

    Task ISettingsHost.CheckForUpdatesAsync() => CheckForUpdatesNowAsync();

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
