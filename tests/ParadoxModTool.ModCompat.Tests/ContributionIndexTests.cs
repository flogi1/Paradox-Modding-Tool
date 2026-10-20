using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class ContributionIndexTests : CompatTestBase
{
    private static ContributionIndex Build(IReadOnlyList<CompatParticipant> mods, ParseCache? cache = null) =>
        ContributionIndex.Build(mods, Rules(), cache ?? new ParseCache(), null, CancellationToken.None);

    [Fact]
    public void Same_key_in_different_file_names_lands_in_one_group()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "in_game/common/building_types/a.txt", "farm = { cost = 1 }\n");
        Put(b, "in_game/common/building_types/b.txt", "farm = { cost = 2 }\nmine = { cost = 3 }\n");
        var index = Build(new[] { a, b });
        var group = index.KeyGroups["in_game/common/building_types|farm"];
        Assert.Equal(2, group.Contributions.Count);
        Assert.Equal(new[] { "A", "B" }, group.Contributions.Select(c => c.Participant.Name));
        Assert.Single(index.KeyGroups["in_game/common/building_types|mine"].Contributions);
    }

    [Fact]
    public void Prefix_is_split_into_operator_and_key()
    {
        var a = Mod("A", 1);
        Put(a, "in_game/common/building_types/a.txt", "INJECT:farm = { x = 1 }\nREPLACE:mine = { y = 1 }\n");
        var index = Build(new[] { a });
        Assert.Equal(OverrideOperator.Inject, index.KeyGroups["in_game/common/building_types|farm"].Contributions[0].Operator);
        Assert.Equal(OverrideOperator.Replace, index.KeyGroups["in_game/common/building_types|mine"].Contributions[0].Operator);
    }

    [Fact]
    public void File_replaced_by_higher_mod_at_same_path_contributes_no_keys()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "in_game/common/building_types/x.txt", "farm = { cost = 1 }\nonly_a = { cost = 1 }\n");
        Put(b, "in_game/common/building_types/x.txt", "farm = { cost = 2 }\n");
        var index = Build(new[] { a, b });
        Assert.True(index.Overridden.Contains(("A", "in_game/common/building_types/x.txt")));
        Assert.False(index.KeyGroups.ContainsKey("in_game/common/building_types|only_a"));
        Assert.Single(index.KeyGroups["in_game/common/building_types|farm"].Contributions);
        Assert.Equal(new[] { "A", "B" }, index.PathGroups["in_game/common/building_types/x.txt"].Select(p => p.Name));
    }

    [Fact]
    public void Folder_operators_decide_how_a_prefix_is_read()
    {
        var a = Mod("A", 1);
        Put(a, "common/scripted_effects/a.txt", "INJECT:foo = { x = 1 }\n");
        Put(a, "common/coat_of_arms/a.txt", "INJECT:bar = { x = 1 }\n");
        var rules = new OverrideRules(new[]
        {
            new OverrideRule("common/*", OverrideBehavior.LastWins, Operators: Array.Empty<string>()),
            new OverrideRule("common/scripted_effects", OverrideBehavior.PerKey, Operators: new[] { "REPLACE", "TRY_REPLACE", "REPLACE_OR_CREATE" }),
        });

        var index = ContributionIndex.Build(new[] { a }, rules, new ParseCache(), null, CancellationToken.None);

        Assert.Equal(OverrideOperator.Replace, index.KeyGroups["common/scripted_effects|foo"].Contributions[0].Operator);
        Assert.Equal(OverrideOperator.None, index.KeyGroups["common/coat_of_arms|INJECT:bar"].Contributions[0].Operator);
    }

    [Fact]
    public void Contribution_carries_text_line_and_path()
    {
        var a = Mod("A", 1);
        var full = Put(a, "in_game/common/building_types/a.txt", "# Kopf\nfarm = {\n  cost = 1\n}\n");
        var c = Build(new[] { a }).KeyGroups["in_game/common/building_types|farm"].Contributions[0];
        Assert.Equal(full, c.FullPath, ignoreCase: true);
        Assert.Equal(2, c.Line);
        Assert.Contains("cost = 1", c.Text);
        Assert.Equal("in_game/common/building_types/a.txt", c.RelativePath);
    }

    [Fact]
    public void Events_skip_namespace_and_use_event_id_as_key()
    {
        var a = Mod("A", 1);
        Put(a, "in_game/events/e.txt", "namespace = my\nmy.1 = { type = country_event }\n");
        var index = Build(new[] { a });
        Assert.Contains("in_game/events|my.1", index.KeyGroups.Keys);
        Assert.DoesNotContain("in_game/events|namespace", index.KeyGroups.Keys);
        Assert.Equal(OverrideBehavior.FirstWins, index.KeyGroups["in_game/events|my.1"].Behavior);
    }

    /// <summary>Eine Einbindung auf oberster Ebene (Stellaris-Mods in districts, pop_jobs) ist keine Definition und damit kein Schlüssel.</summary>
    [Fact]
    public void Top_level_inline_script_is_no_key()
    {
        var a = Mod("A", 1);
        Put(a, "in_game/common/building_types/a.txt", "inline_script = { script = x }\nfarm = { cost = 1 }\n");
        var index = Build(new[] { a });
        Assert.Contains("in_game/common/building_types|farm", index.KeyGroups.Keys);
        Assert.DoesNotContain("in_game/common/building_types|inline_script", index.KeyGroups.Keys);
    }

    [Fact]
    public void Top_level_inline_script_is_no_key_with_a_key_field()
    {
        var a = Mod("A", 1);
        Put(a, "events/e.txt", "inline_script = { script = x }\ncountry_event = { id = my.1 }\n");
        var rules = new OverrideRules(new[] { new OverrideRule("events", OverrideBehavior.FirstWins, KeyField: "id") });
        var index = ContributionIndex.Build(new[] { a }, rules, new ParseCache(), null, CancellationToken.None);
        Assert.Contains("events|my.1", index.KeyGroups.Keys);
        Assert.Single(index.KeyGroups.Keys, k => k.StartsWith("events|", StringComparison.Ordinal));
    }

    [Fact]
    public void Defines_use_top_and_child_key()
    {
        var a = Mod("A", 1);
        Put(a, "in_game/common/defines/a.txt", "NGame = { START_YEAR = 1444 }\n");
        var index = Build(new[] { a });
        Assert.Contains("in_game/common/defines|NGame.START_YEAR", index.KeyGroups.Keys);
    }

    [Fact]
    public void Localization_keys_are_per_language_and_replace_folder_is_an_override()
    {
        var a = Mod("A", 1);
        Put(a, "in_game/localization/english/a_l_english.yml", "l_english:\n foo:0 \"A\"\n");
        Put(a, "in_game/localization/english/replace/r_l_english.yml", "l_english:\n bar:0 \"B\"\n");
        var index = Build(new[] { a });
        Assert.Equal(OverrideOperator.None, index.KeyGroups["in_game/localization|english/foo"].Contributions[0].Operator);
        Assert.Equal(OverrideOperator.Replace, index.KeyGroups["in_game/localization|english/bar"].Contributions[0].Operator);
    }

    [Fact]
    public void Unparsable_script_is_reported_and_does_not_stop_the_build()
    {
        var a = Mod("A", 1);
        Put(a, "in_game/common/building_types/broken.txt", "farm = { cost = 1\n");
        Put(a, "in_game/common/building_types/ok.txt", "mine = { cost = 1 }\n");
        var index = Build(new[] { a });
        Assert.Single(index.Unreadable);
        Assert.Equal("in_game/common/building_types/broken.txt", index.Unreadable[0].RelativePath);
        Assert.Contains("in_game/common/building_types|mine", index.KeyGroups.Keys);
    }

    [Fact]
    public void Missing_mod_folder_is_listed_and_skipped()
    {
        var a = Mod("A", 1);
        var ghost = new CompatParticipant("G", "Geist", Path.Combine(Dir, "gibt-es-nicht"), 2, false);
        Put(a, "in_game/common/building_types/a.txt", "farm = { cost = 1 }\n");
        var index = Build(new[] { a, ghost });
        Assert.Equal(new[] { "Geist" }, index.MissingFolders.Select(m => m.Name));
        Assert.Single(index.KeyGroups["in_game/common/building_types|farm"].Contributions);
    }

    [Fact]
    public void Non_script_files_and_whole_file_folders_contribute_no_keys()
    {
        var a = Mod("A", 1);
        Put(a, "in_game/gfx/x.txt", "foo = 1\n");
        Put(a, "in_game/common/building_types/readme.md", "farm = 1\n");
        Assert.Empty(Build(new[] { a }).KeyGroups);
    }

    [Fact]
    public void Cancellation_throws()
    {
        var a = Mod("A", 1);
        Put(a, "in_game/common/building_types/a.txt", "farm = { }\n");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => ContributionIndex.Build(new[] { a }, Rules(), new ParseCache(), null, cts.Token));
    }

    [Fact]
    public void Cache_reparses_only_when_the_file_changed()
    {
        var a = Mod("A", 1);
        var full = Put(a, "in_game/common/building_types/a.txt", "farm = { cost = 1 }\n");
        var cache = new ParseCache();
        var first = cache.Get(full);
        Assert.Same(first, cache.Get(full));
        File.WriteAllText(full, "farm = { cost = 1 }\nmine = { }\n");
        File.SetLastWriteTimeUtc(full, DateTime.UtcNow.AddMinutes(5));
        Assert.NotSame(first, cache.Get(full));
    }
}
