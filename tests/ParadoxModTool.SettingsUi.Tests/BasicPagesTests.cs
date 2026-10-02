using System.IO;
using ParadoxModTool.Localization;
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

        Assert.True(UiFind.Radio(page, "English").IsChecked);
        UiFind.Choose(UiFind.Radio(page, "Deutsch"));

        Assert.Equal("de", host.Settings.UiLanguage);
        Assert.Contains(SettingsTexts.LanguageHint, UiFind.Texts(page));
    });

    [Fact]
    public void LanguagePageFollowsModelChange() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new LanguagePage(host);

        host.Settings.UiLanguage = "de";

        Assert.True(UiFind.Radio(page, "Deutsch").IsChecked);
        Assert.False(UiFind.Radio(page, "English").IsChecked);
    });

    [Fact]
    public void LanguagePageShowsStatusPerLanguage() => WithLocales("{\"a\":\"x\"}", host =>
    {
        using var page = new LanguagePage(host);

        Assert.Contains(
            SettingsTexts.LanguagePercent(50) + " (" + SettingsTexts.LanguageMissing(1) + ") · " + SettingsTexts.LanguageOriginUser,
            UiFind.Texts(page));
    });

    [Fact]
    public void LanguagePageListsErrors() => WithLocales("{\"a\":\"x\",\"b\":\"y\",\"zz\":\"q\"}", host =>
    {
        using var page = new LanguagePage(host);
        var texts = UiFind.Texts(page).ToList();

        Assert.Contains(SettingsTexts.LanguageErrors(2), texts);
        Assert.Contains(SettingsTexts.LanguageUnknownKey("zz"), texts);
        Assert.Contains(SettingsTexts.LanguagePlaceholderError("b", "{0}", "–"), texts);
    });

    [Fact]
    public void LanguagePageCapsErrorListAtTwenty()
    {
        var unknown = string.Join(",", Enumerable.Range(1, 25).Select(i => $"\"zz{i}\":\"q\""));
        WithLocales("{\"a\":\"x\"," + unknown + "}", host =>
        {
            using var page = new LanguagePage(host);
            var texts = UiFind.Texts(page).ToList();

            Assert.Equal(20, texts.Count(text => text.StartsWith(SettingsTexts.LanguageUnknownKey(string.Empty), StringComparison.Ordinal)));
            Assert.Contains(SettingsTexts.LanguageMoreErrors(5), texts);
        });
    }

    [Fact]
    public void RecheckReadsTheFileAgain() => WithLocales("{\"a\":\"x\"}", host =>
    {
        using var page = new LanguagePage(host);
        File.WriteAllText(Path.Combine(host.LocaleSources.User!, "fr.json"), "{\"a\":\"x\",\"b\":\"y {0}\"}");

        UiFind.Click(UiFind.Button(page, SettingsTexts.LanguageRecheck));

        Assert.Contains(SettingsTexts.LanguagePercent(100) + " · " + SettingsTexts.LanguageOriginUser, UiFind.Texts(page));
    });

    [Fact]
    public void FolderAndGuideButtonsUseTheHost() => WithLocales("{}", host =>
    {
        using var page = new LanguagePage(host);

        UiFind.Click(UiFind.Button(page, SettingsTexts.LanguageOpenFolder));
        UiFind.Click(UiFind.Button(page, SettingsTexts.LanguageGuide));

        Assert.Contains("openTranslationFolder", host.Calls);
        Assert.Contains("openUrl:https://example.test/TRANSLATING.md", host.Calls);
    });

    [Fact]
    public void RestartButtonAsksTheHostToRestart() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new LanguagePage(host);

        var button = UiFind.Button(page, SettingsTexts.LanguageRestart);
        Assert.Equal(SettingsTexts.LanguageRestartTip, button.ToolTip);
        UiFind.Click(button);

        Assert.Contains("restartApp", host.Calls);
    });

    /// <summary>Host mit Programmordner (de, en mit Keys a und b) und User-Ordner mit <c>fr.json</c> = <paramref name="french"/>.</summary>
    private static void WithLocales(string french, Action<FakeHost> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "pmt-langpage-" + Guid.NewGuid().ToString("N"));
        var program = Directory.CreateDirectory(Path.Combine(root, "program")).FullName;
        var user = Directory.CreateDirectory(Path.Combine(root, "user")).FullName;
        try
        {
            File.WriteAllText(Path.Combine(program, "de.json"), "{\"a\":\"A\",\"b\":\"B {0}\"}");
            File.WriteAllText(Path.Combine(program, "en.json"), "{\"a\":\"A\",\"b\":\"B {0}\"}");
            File.WriteAllText(Path.Combine(user, "fr.json"), french);
            var host = new FakeHost
            {
                LocaleSources = new LocaleSources(program, user),
                Languages = new[]
                {
                    new UiLanguageOption("de", "Deutsch"),
                    new UiLanguageOption("en", "English"),
                    new UiLanguageOption("fr", "Français", LocaleOrigin.User),
                },
            };
            Sta.Run(() => test(host));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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

        host.Settings.UiLanguage = "de";

        Assert.True(page.IsDisposed);
        Assert.True(UiFind.Radio(page, "English").IsChecked);
    });

    [Fact]
    public void BugTrackerPageShowsGitHubStatusAndStartsLogin() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new BugTrackerPage(host);

        Assert.Contains(host.GitHubStatusText, UiFind.Texts(page));

        UiFind.Click(UiFind.Button(page, SettingsTexts.BugTrackerLogin));
        Assert.Contains("Anmeldung gestartet", UiFind.Texts(page));

        UiFind.Click(UiFind.Button(page, SettingsTexts.BugTrackerCheck));
        Assert.Contains(host.GitHubStatusText, UiFind.Texts(page));
        Assert.Equal(new[] { "githubStatus", "githubLogin", "githubStatus" }, host.Calls);
    });

    [Fact]
    public void BugTrackerPageChecksDiscordSetupAndSavesTheToken() => Sta.Run(() =>
    {
        var host = new FakeHost { HasDiscordToken = true };
        using var page = new BugTrackerPage(host);

        Assert.Contains("✓ " + SettingsTexts.DiscordTokenValid("PMT-Bot"), UiFind.Texts(page));
        Assert.Contains("✓ " + SettingsTexts.DiscordIntentOn, UiFind.Texts(page));
        Assert.Contains("✓ " + SettingsTexts.DiscordServers(1, "Meine Mod"), UiFind.Texts(page));

        UiFind.All<System.Windows.Controls.PasswordBox>(page.View).Single().Password = "neuer.token";
        UiFind.Click(UiFind.Button(page, SettingsTexts.DiscordSaveToken));
        UiFind.Click(UiFind.Button(page, SettingsTexts.DiscordOpenPortal));
        UiFind.Click(UiFind.Button(page, SettingsTexts.DiscordOpenInvite));

        Assert.Contains("saveDiscordToken:neuer.token", host.Calls);
        Assert.Contains("openUrl:https://discord.com/developers/applications", host.Calls);
        Assert.Contains("openUrl:https://discord.com/oauth2/authorize?client_id=555", host.Calls);
        Assert.Equal(string.Empty, UiFind.All<System.Windows.Controls.PasswordBox>(page.View).Single().Password);
    });

    [Fact]
    public void BugTrackerPageWithoutTokenDoesNotCallDiscord() => Sta.Run(() =>
    {
        var host = new FakeHost();
        using var page = new BugTrackerPage(host);

        Assert.DoesNotContain("discordState", host.Calls);
        Assert.Contains(SettingsTexts.DiscordNoToken, UiFind.Texts(page));
        Assert.False(UiFind.Button(page, SettingsTexts.DiscordOpenInvite).IsEnabled);
    });
}
