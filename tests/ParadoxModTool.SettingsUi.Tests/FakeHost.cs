using System.Windows;
using System.Windows.Media;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.Settings;

namespace ParadoxModTool.SettingsUi.Tests;

/// <summary>Host ohne Dialoge: merkt sich jeden Aufruf; lange Aufgaben liefern <see cref="Pending"/> (standardmäßig schon fertig).</summary>
internal sealed class FakeHost : ISettingsHost
{
    public AppSettings Settings { get; } = new();

    public List<string> Calls { get; } = new();

    public TaskCompletionSource Pending { get; set; } = Done();

    public string? GameExecutablePath { get; set; }

    public string? PlaysetsFilePath { get; set; }

    public string CurrentThemeId { get; set; } = "builtin-light";

    public string CurrentThemeName { get; set; } = "Hell";

    public string SelectedThemeId { get; set; } = "system";

    public List<ThemeChoice> Themes { get; } = new() { new("system", "Wie Windows"), new("builtin-light", "Hell"), new("builtin-dark", "Dunkel") };

    public Color? NextPickedColor { get; set; }

    public bool UpdatesAvailable { get; set; } = true;

    public string GameDisplayName { get; set; } = "EU V";

    public bool CanRegisterGameEditor { get; set; } = true;

    public string GameEditorStatus { get; set; } = string.Empty;

    public IReadOnlyList<UiLanguageOption> Languages { get; } = new[] { new UiLanguageOption("de", "Deutsch"), new UiLanguageOption("en", "English") };

    public event EventHandler? ThemeChanged;

    public int ThemeChangedSubscribers => ThemeChanged?.GetInvocationList().Length ?? 0;

    public static TaskCompletionSource Done()
    {
        var source = new TaskCompletionSource();
        source.SetResult();
        return source;
    }

    public void RaiseThemeChanged() => ThemeChanged?.Invoke(this, EventArgs.Empty);

    public void ChooseGameExecutable()
    {
        Calls.Add("game");
        GameExecutablePath = @"C:\Spiel\game.exe";
    }

    public void ChoosePlaysetsFile()
    {
        Calls.Add("playsets");
        PlaysetsFilePath = @"C:\Docs\playsets.json";
    }

    public void OpenStorageSettings() => Calls.Add("storage");

    public Task RelearnLocalizationProfilesAsync()
    {
        Calls.Add("relearnLoc");
        return Pending.Task;
    }

    public Task RelearnScopeRulesAsync()
    {
        Calls.Add("relearnScope");
        return Pending.Task;
    }

    public void OpenCategories() => Calls.Add("categories");

    public void SetGuiToolsEnabled(bool enabled)
    {
        Calls.Add($"gui:{enabled}");
        GuiSettings.SetToolsEnabled(enabled);
    }

    public Task RebuildGuiIndexAsync()
    {
        Calls.Add("rebuildGui");
        return Pending.Task;
    }

    public Color IconColor(string iconId) =>
        Settings.GetIconColor(CurrentThemeId, iconId) is { } hex ? (Color)ColorConverter.ConvertFromString(hex) : Colors.Gray;

    public Color? PickColor(Color current)
    {
        Calls.Add("pick");
        return NextPickedColor;
    }

    public IReadOnlyList<ThemeChoice> ThemeChoices() => Themes.ToList();

    public void SelectTheme(string selectionId)
    {
        Calls.Add($"theme:{selectionId}");
        SelectedThemeId = selectionId;
        RaiseThemeChanged();
    }

    public void OpenThemeEditor(Window? owner) => Calls.Add("themeEditor");

    public void RegisterGameEditor()
    {
        Calls.Add("registerEditor");
        GameEditorStatus = "PMT";
    }

    public void RestoreGameEditor() => Calls.Add("restoreEditor");

    public Task CheckForUpdatesAsync()
    {
        Calls.Add("updates");
        return Pending.Task;
    }
}
