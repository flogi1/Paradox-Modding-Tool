using System.Text.Json;
using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class LocalizationSettingsMigrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-locmig-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private SavedTemplate LoadOld(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "alt.json"), json);
        return new TemplateStore(_dir).Load("alt")!;
    }

    [Fact]
    public void The_default_suffix_list_needs_nothing()
    {
        var loaded = LoadOld("{ \"version\": 2, \"localization\": { \"enabled\": true, \"suffixes\": [\"\", \"_desc\"] } }");

        Assert.Null(loaded.Localization.Suffixes);
        Assert.Empty(loaded.Localization.ExtraSuffixes);
        Assert.Empty(loaded.Localization.ProfileChoices);
        Assert.True(loaded.Localization.Enabled);
    }

    [Fact]
    public void Other_suffixes_become_extra_suffixes()
    {
        var loaded = LoadOld("{ \"version\": 2, \"localization\": { \"suffixes\": [\"\", \"_desc\", \"_tooltip\", \" \"] } }");

        Assert.Equal(new[] { "_tooltip" }, loaded.Localization.ExtraSuffixes);
        Assert.Empty(loaded.Localization.ProfileChoices);
    }

    [Fact]
    public void A_list_without_desc_switches_desc_off_for_every_profile()
    {
        var loaded = LoadOld("{ \"version\": 2, \"localization\": { \"suffixes\": [\"\"] } }");

        var key = LocalizationProfileResolver.ObjectKey(LocalizationProfileResolver.AllProfiles, "", "_desc");
        Assert.False(loaded.Localization.ProfileChoices[key]);
        var profile = LocalizationProfileResolver.Resolve(null, loaded.Localization, null);
        Assert.Equal(new[] { "" }, profile.Objects.Where(o => o.Active).Select(o => o.Suffix));
    }

    [Fact]
    public void A_template_without_a_suffix_list_is_left_alone()
    {
        var loaded = LoadOld("{ \"version\": 2, \"localization\": { \"profilePath\": \"gui\" } }");

        Assert.Equal("gui", loaded.Localization.ProfilePath);
        Assert.Empty(loaded.Localization.ExtraSuffixes);
        Assert.Empty(loaded.Localization.ProfileChoices);
    }

    [Fact]
    public void The_saved_file_has_no_suffix_list_and_keeps_the_new_settings()
    {
        var store = new TemplateStore(_dir);
        var loaded = LoadOld("{ \"version\": 2, \"localization\": { \"suffixes\": [\"\", \"_desc\", \"_x\"], \"profilePath\": \"gui\" } }");
        loaded.Localization.ProfileChoices[LocalizationProfileResolver.FieldKey("gui", "text")] = true;

        store.Save("neu", loaded);
        var json = File.ReadAllText(Path.Combine(_dir, "neu.json"));
        var again = store.Load("neu")!;

        using var document = JsonDocument.Parse(json);
        var localization = document.RootElement.GetProperty("localization");
        Assert.False(localization.TryGetProperty("suffixes", out _));
        Assert.Equal("gui", localization.GetProperty("profilePath").GetString());
        Assert.Equal(new[] { "_x" }, again.Localization.ExtraSuffixes);
        Assert.True(again.Localization.ProfileChoices[LocalizationProfileResolver.FieldKey("gui", "text")]);
    }
}
