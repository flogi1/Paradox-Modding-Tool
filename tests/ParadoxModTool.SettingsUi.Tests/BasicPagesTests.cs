using ParadoxModTool.Settings;
using ParadoxModTool.SettingsUi.Pages;
using Xunit;

namespace ParadoxModTool.SettingsUi.Tests;

public sealed class BasicPagesTests
{
    [Fact]
    public void LanguagePageSelectsCurrentAndWritesChoice() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new LanguagePage(host);

        Assert.True(UiFind.Radio(page, "Deutsch").IsChecked);
        UiFind.Choose(UiFind.Radio(page, "English"));

        Assert.Equal("en", host.Settings.UiLanguage);
        Assert.Contains(SettingsTexts.LanguageHint, UiFind.Texts(page));
    });

    [Fact]
    public void LanguagePageFollowsModelChange() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new LanguagePage(host);

        host.Settings.UiLanguage = "en";

        Assert.True(UiFind.Radio(page, "English").IsChecked);
        Assert.False(UiFind.Radio(page, "Deutsch").IsChecked);
    });

    [Fact]
    public void HotkeysPageListsEveryEntry() => Sta.Run(() =>
    {
        using var page = new HotkeysPage(new FakeHost());
        var texts = UiFind.Texts(page).ToList();

        Assert.Contains("Strg+Umschalt+P", texts);
        Assert.Contains(SettingsTexts.HotkeyDescription("main.scopeFinder"), texts);
        Assert.Contains(SettingsTexts.HotkeyGroupLabel(HotkeyGroup.CuTool), texts);
        Assert.Equal(HotkeyCatalog.Entries.Count, texts.Count(text => HotkeyCatalog.Entries.Any(entry => SettingsTexts.Gesture(entry.Modifiers, entry.Key) == text)));
    });

    [Fact]
    public void UpdatePageWritesCheckboxAndRunsCheck() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new UpdatePage(host);

        UiFind.Toggle(UiFind.CheckBox(page, SettingsTexts.UpdateOnStartup));
        UiFind.Click(UiFind.Button(page, SettingsTexts.UpdateNow));

        Assert.False(host.Settings.CheckForUpdatesOnStartup);
        Assert.Equal(new[] { "updates" }, host.Calls);
    });

    [Fact]
    public void UpdatePageWithoutRepositoryIsDisabled() => Sta.Run(() =>
    {
        using var page = new UpdatePage(new FakeHost { UpdatesAvailable = false });

        Assert.False(UiFind.CheckBox(page, SettingsTexts.UpdateOnStartup).IsEnabled);
        Assert.False(UiFind.Button(page, SettingsTexts.UpdateNow).IsEnabled);
        Assert.Contains(SettingsTexts.UpdatesUnavailable, UiFind.Texts(page));
    });

    [Fact]
    public void TaskButtonIsDisabledWhileTheTaskRuns() => Sta.Run(() =>
    {
        var host = new FakeHost { Pending = new TaskCompletionSource() };
        using var page = new UpdatePage(host);
        var button = UiFind.Button(page, SettingsTexts.UpdateNow);

        UiFind.Click(button);
        Assert.False(button.IsEnabled);

        host.Pending.SetResult();
        Assert.True(button.IsEnabled);
    });

    [Fact]
    public void DisposedPageNoLongerFollowsTheModel() => Sta.Run(() =>
    {
        var host = new FakeHost();
        var page = new LanguagePage(host);
        page.Dispose();

        host.Settings.UiLanguage = "en";

        Assert.True(page.IsDisposed);
        Assert.True(UiFind.Radio(page, "Deutsch").IsChecked);
    });
}
