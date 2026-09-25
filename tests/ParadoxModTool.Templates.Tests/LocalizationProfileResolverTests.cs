using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class LocalizationProfileResolverTests
{
    private static LocalizationProfiles Learned() => new()
    {
        Profiles =
        {
            new LocalizationFolderProfile
            {
                Path = "common/building_types",
                ObjectCount = 465,
                Objects =
                {
                    new LocalizationObjectPattern { Suffix = "", Share = 1, Preselected = true },
                    new LocalizationObjectPattern { Suffix = "_desc", Share = 1, Preselected = true },
                    new LocalizationObjectPattern { Suffix = "_maintenance", Share = 0.47, Preselected = false },
                },
            },
            new LocalizationFolderProfile
            {
                Path = "gui",
                Fields =
                {
                    new LocalizationFieldPattern { Name = "text", Hits = 20, Share = 0.48, Preselected = false },
                    new LocalizationFieldPattern { Name = "title", Hits = 30, Share = 0.9, Preselected = true },
                },
            },
        },
    };

    private static string[] Active(ResolvedLocProfile profile) => profile.Objects.Where(o => o.Active).Select(o => o.Prefix + "|" + o.Suffix).ToArray();

    [Fact]
    public void Without_learned_profiles_the_fallback_applies()
    {
        var profile = LocalizationProfileResolver.Resolve(null, new LocalizationSettings(), "common/x");

        Assert.True(profile.IsFallback);
        Assert.Equal(LocalizationProfileResolver.Fallback, profile.Path);
        Assert.Equal(new[] { "|", "|_desc" }, Active(profile));
        Assert.Empty(profile.Fields);
        Assert.Equal("automatisch: Rückfall (Name + _desc)", profile.Description);
    }

    [Fact]
    public void Automatic_picks_the_profile_of_the_source_folder()
    {
        var profile = LocalizationProfileResolver.Resolve(Learned(), new LocalizationSettings(), "common/building_types/sub");

        Assert.False(profile.IsFallback);
        Assert.Equal("common/building_types", profile.Path);
        Assert.Equal("automatisch: common/building_types", profile.Description);
        Assert.Equal(new[] { "|", "|_desc" }, Active(profile));
        Assert.Equal(3, profile.Objects.Count);
        Assert.Equal("common/building_types", LocalizationProfileResolver.FindAutomatic(Learned(), "common/building_types/sub"));
    }

    [Fact]
    public void Automatic_without_a_source_or_a_matching_profile_falls_back()
    {
        Assert.True(LocalizationProfileResolver.Resolve(Learned(), new LocalizationSettings(), null).IsFallback);
        Assert.True(LocalizationProfileResolver.Resolve(Learned(), new LocalizationSettings(), "events").IsFallback);
    }

    [Fact]
    public void An_explicit_path_wins_over_the_source_folder()
    {
        var settings = new LocalizationSettings { ProfilePath = "gui" };

        var profile = LocalizationProfileResolver.Resolve(Learned(), settings, "common/building_types");

        Assert.Equal("gui", profile.Path);
        Assert.Equal("gui", profile.Description);
        Assert.Equal(new[] { "title" }, profile.Fields.Where(f => f.Active).Select(f => f.Name));
    }

    [Fact]
    public void An_explicit_path_that_was_not_learned_falls_back_and_so_does_the_fallback_choice()
    {
        var gone = LocalizationProfileResolver.Resolve(Learned(), new LocalizationSettings { ProfilePath = "common/gone" }, null);
        var chosen = LocalizationProfileResolver.Resolve(Learned(), new LocalizationSettings { ProfilePath = LocalizationProfileResolver.Fallback }, "common/building_types");

        Assert.True(gone.IsFallback);
        Assert.True(chosen.IsFallback);
        Assert.Equal("Rückfall (Name + _desc)", chosen.Description);
    }

    [Fact]
    public void Choices_of_the_template_override_the_preselection()
    {
        var settings = new LocalizationSettings();
        settings.ProfileChoices[LocalizationProfileResolver.ObjectKey("common/building_types", "", "_maintenance")] = true;
        settings.ProfileChoices[LocalizationProfileResolver.ObjectKey("common/building_types", "", "_desc")] = false;

        var profile = LocalizationProfileResolver.Resolve(Learned(), settings, "common/building_types");

        Assert.Equal(new[] { "|", "|_maintenance" }, Active(profile));
    }

    [Fact]
    public void A_choice_for_all_profiles_applies_everywhere_but_a_specific_one_wins()
    {
        var settings = new LocalizationSettings();
        settings.ProfileChoices[LocalizationProfileResolver.ObjectKey(LocalizationProfileResolver.AllProfiles, "", "_desc")] = false;

        Assert.Equal(new[] { "|" }, Active(LocalizationProfileResolver.Resolve(Learned(), settings, "common/building_types")));
        Assert.Equal(new[] { "|" }, Active(LocalizationProfileResolver.Resolve(null, settings, null)));

        settings.ProfileChoices[LocalizationProfileResolver.ObjectKey("common/building_types", "", "_desc")] = true;

        Assert.Equal(new[] { "|", "|_desc" }, Active(LocalizationProfileResolver.Resolve(Learned(), settings, "common/building_types")));
    }

    [Fact]
    public void Fields_follow_their_preselection_and_the_choices()
    {
        var settings = new LocalizationSettings { ProfilePath = "gui" };
        settings.ProfileChoices[LocalizationProfileResolver.FieldKey("gui", "text")] = true;
        settings.ProfileChoices[LocalizationProfileResolver.FieldKey("gui", "title")] = false;

        var profile = LocalizationProfileResolver.Resolve(Learned(), settings, null);

        Assert.Equal(new[] { "text" }, profile.Fields.Where(f => f.Active).Select(f => f.Name));
    }

    [Fact]
    public void Extra_suffixes_are_added_once_and_only_when_the_profile_lacks_them()
    {
        var settings = new LocalizationSettings();
        settings.ExtraSuffixes.AddRange(new[] { "_tooltip", "_desc", " ", "_tooltip" });

        var profile = LocalizationProfileResolver.Resolve(null, settings, null);

        Assert.Equal(new[] { "|", "|_desc", "|_tooltip" }, Active(profile));
        Assert.Equal(new[] { false, false, true }, profile.Objects.Select(o => o.Extra));
    }

    [Fact]
    public void The_signature_changes_with_the_profile_and_with_the_choices()
    {
        var settings = new LocalizationSettings();
        var first = LocalizationProfileResolver.Resolve(Learned(), settings, "common/building_types").Signature;
        var other = LocalizationProfileResolver.Resolve(Learned(), settings, "gui").Signature;
        settings.ProfileChoices[LocalizationProfileResolver.ObjectKey("common/building_types", "", "_desc")] = false;
        var changed = LocalizationProfileResolver.Resolve(Learned(), settings, "common/building_types").Signature;

        Assert.NotEqual(first, other);
        Assert.NotEqual(first, changed);
        Assert.Equal(first, LocalizationProfileResolver.Resolve(Learned(), new LocalizationSettings(), "common/building_types").Signature);
    }

    [Theory]
    [InlineData(@"C:\g\game\in_game\common\building_types\x.txt", @"C:\g\game", "common/building_types")]
    [InlineData(@"C:\g\game\gui\x.gui", @"C:\g\game", "gui")]
    [InlineData(@"c:\G\GAME\in_game\common\ideas\sub\y.txt", @"C:\g\game", "common/ideas/sub")]
    [InlineData(@"C:\mods\my\in_game\common\ideas\sub\y.txt", @"C:\g\game", "common/ideas/sub")]
    [InlineData(@"C:\mods\my\events\e.txt", @"C:\g\game", "events")]
    [InlineData(@"C:\elsewhere\z.txt", @"C:\g\game", null)]
    [InlineData(@"C:\g\game\z.txt", @"C:\g\game", null)]
    [InlineData(null, @"C:\g\game", null)]
    [InlineData("", @"C:\g\game", null)]
    public void The_relative_folder_comes_from_a_root_or_from_the_folder_names(string? file, string root, string? expected)
    {
        Assert.Equal(expected, LocalizationProfileResolver.RelativeFolder(file, new[] { root }));
    }
}
