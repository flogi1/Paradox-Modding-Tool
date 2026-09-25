using System.Text;
using ParadoxModTool.Mods;
using ParadoxModTool.Templates;
using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class LocalizationBuilderTests
{
    private static ResolvedLocProfile Fallback(LocalizationSettings settings) => LocalizationProfileResolver.Resolve(null, settings, null);

    /// <summary>Ein gelerntes Profil mit den angegebenen Feldern und Paaren; die Einstellungen wählen es aus.</summary>
    private static ResolvedLocProfile Profile(LocalizationSettings settings, string[] fields, params (string Prefix, string Suffix)[] objects)
    {
        var folder = new LocalizationFolderProfile { Path = "common/test", ObjectCount = 10 };
        folder.Objects.AddRange(objects.Select(o => new LocalizationObjectPattern { Prefix = o.Prefix, Suffix = o.Suffix, Share = 1, Preselected = true }));
        folder.Fields.AddRange(fields.Select(f => new LocalizationFieldPattern { Name = f, Hits = 20, Share = 1, Preselected = true }));
        settings.ProfilePath = "common/test";
        return LocalizationProfileResolver.Resolve(new LocalizationProfiles { Profiles = { folder } }, settings, null);
    }

    private static List<LocalizationRow> Suggest(TemplateModel model, LocalizationSettings settings) =>
        LocalizationBuilder.Suggest(model, settings, Fallback(settings));

    [Fact]
    public void Values_in_fields_the_profile_does_not_name_make_no_rows()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = {\n\tname = idea_1_name\n\tdesc = idea_1_desc\n}");
        var settings = new LocalizationSettings();

        var rows = Suggest(model, settings);

        Assert.Equal(new[] { "idea_{n}", "idea_{n}_desc" }, rows.Select(r => r.KeyPattern));
        Assert.All(rows, r => Assert.True(r.FromBlockKey && r.Selected));
        Assert.Equal(new[] { "Objektname", "Objektname · Endung _desc" }, rows.Select(r => r.Origin));
    }

    [Fact]
    public void Profile_fields_make_rows_for_their_values()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = {\n\tname = idea_1_name\n\tdesc = idea_1_desc\n}");
        var settings = new LocalizationSettings();

        var rows = LocalizationBuilder.Suggest(model, settings, Profile(settings, new[] { "name", "Desc" }, ("", "")));

        Assert.Equal(new[] { "idea_{n}", "idea_{n}_name", "idea_{n}_desc" }, rows.Select(r => r.KeyPattern));
        Assert.Equal(new[] { "Objektname", "Feld name", "Feld desc" }, rows.Select(r => r.Origin));
        Assert.Equal(new[] { true, false, false }, rows.Select(r => r.FromBlockKey));
        Assert.All(rows, r => Assert.True(r.Selected));
    }

    [Fact]
    public void Quoted_keys_without_spaces_become_rows_but_texts_with_spaces_do_not()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = { name = \"iv2_ga_ARA_1_1\" title = \"Idee 1\" }");
        var settings = new LocalizationSettings();

        var rows = LocalizationBuilder.Suggest(model, settings, Profile(settings, new[] { "name", "title" }));

        Assert.Contains(rows, r => r.KeyPattern == "iv2_ga_ARA_{n}_1" && r.Selected);
        Assert.DoesNotContain(rows, r => r.KeyPattern.Contains("Idee"));
    }

    [Fact]
    public void Block_keys_with_the_profile_pairs_are_selected()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = { cost = 5 }");
        var settings = new LocalizationSettings();

        var rows = Suggest(model, settings);

        Assert.Equal(new[] { "idea_{n}", "idea_{n}_desc" }, rows.Select(r => r.KeyPattern));
        Assert.All(rows, r => Assert.True(r.Selected));
    }

    [Fact]
    public void A_prefix_pair_builds_the_key_with_the_prefix()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = { cost = 5 }");
        var settings = new LocalizationSettings();

        var rows = LocalizationBuilder.Suggest(model, settings, Profile(settings, Array.Empty<string>(), ("hint_", ""), ("", "")));

        Assert.Equal(new[] { "hint_idea_{n}", "idea_{n}" }, rows.Select(r => r.KeyPattern));
        Assert.Equal(new[] { "Präfix hint_ · Objektname", "Objektname" }, rows.Select(r => r.Origin));
    }

    [Fact]
    public void Only_block_keys_on_the_top_level_make_rows()
    {
        var model = TemplateAnalyzer.Analyze("group = {\n\tidea_1 = { }\n}");
        var settings = new LocalizationSettings();

        Assert.Empty(Suggest(model, settings));
    }

    [Fact]
    public void A_pair_that_is_switched_off_makes_no_row()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = { cost = 5 }");
        var settings = new LocalizationSettings();
        settings.ProfileChoices[LocalizationProfileResolver.ObjectKey(LocalizationProfileResolver.Fallback, "", "_desc")] = false;

        var rows = Suggest(model, settings);

        Assert.Equal(new[] { "idea_{n}" }, rows.Select(r => r.KeyPattern));
    }

    [Fact]
    public void Extra_suffixes_of_the_template_add_rows()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = { cost = 5 }");
        var settings = new LocalizationSettings();
        settings.ExtraSuffixes.Add("_tooltip");

        var rows = Suggest(model, settings);

        Assert.Equal(new[] { "idea_{n}", "idea_{n}_desc", "idea_{n}_tooltip" }, rows.Select(r => r.KeyPattern));
        Assert.Equal("Objektname · Endung _tooltip", rows[2].Origin);
    }

    [Fact]
    public void Build_creates_entries_per_run_with_placeholders_in_the_text()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = { cost = 5 }");
        var settings = new LocalizationSettings { Enabled = true };
        settings.Rows = Suggest(model, settings);
        settings.Rows[0].TextPattern = "Idee {n}";
        settings.Rows[1].TextPattern = "Beschreibung von {key}";

        var entries = LocalizationBuilder.Build(model, settings, 2);

        Assert.Equal(
            new[] { "idea_1=Idee 1", "idea_1_desc=Beschreibung von idea_1_desc", "idea_2=Idee 2", "idea_2_desc=Beschreibung von idea_2_desc" },
            entries.Select(e => e.Key + "=" + e.Text));
    }

    [Fact]
    public void Overrides_win_over_the_pattern()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = { cost = 5 }");
        var settings = new LocalizationSettings();
        settings.Rows = Suggest(model, settings);
        settings.Overrides["idea_2"] = "Zweite Idee";

        var entries = LocalizationBuilder.Build(model, settings, 2);

        Assert.Equal("Zweite Idee", entries.Single(e => e.Key == "idea_2").Text);
        Assert.Equal("TODO", entries.Single(e => e.Key == "idea_1").Text);
    }

    [Fact]
    public void Known_rows_keep_their_selection_and_text_when_suggested_again()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = { cost = 5 }");
        var settings = new LocalizationSettings();
        settings.Rows = Suggest(model, settings);
        settings.Rows[0].Selected = false;
        settings.Rows[1].TextPattern = "Mein Text";

        var again = Suggest(model, settings);

        Assert.False(again[0].Selected);
        Assert.Equal("Mein Text", again[1].TextPattern);
    }

    [Fact]
    public void Rows_of_disabled_points_are_dropped_from_suggestions_and_builds()
    {
        var model = TemplateAnalyzer.Analyze("idea_1 = { name = idea_1 }");
        var settings = new LocalizationSettings();
        settings.Rows = Suggest(model, settings);
        model.Points[0].Enabled = false;

        Assert.Empty(Suggest(model, settings));
        Assert.Empty(LocalizationBuilder.Build(model, settings, 2));
    }

    [Fact]
    public void Repeat_groups_produce_entries_per_repetition()
    {
        var model = TemplateAnalyzer.Analyze("e_1 = {\n\toption = { name = e_opt_1 }\n\toption = { name = e_opt_2 }\n}");
        var settings = new LocalizationSettings();
        settings.Rows = LocalizationBuilder.Suggest(model, settings, Profile(settings, new[] { "name" }, ("", "")));

        var entries = LocalizationBuilder.Build(model, settings, 2);

        Assert.Equal(new[] { "e_opt_1", "e_opt_2" }, entries.Select(e => e.Key).Where(k => k.StartsWith("e_opt", StringComparison.Ordinal)).Distinct());
    }

    [Fact]
    public void Yaml_uses_the_game_format_and_escapes_quotes()
    {
        var yaml = LocalizationBuilder.ToYaml(
            new[] { new LocalizationEntry("a_1", "Sag \"hallo\""), new LocalizationEntry("a_2", "Zeile\nzwei") },
            "german");

        Assert.Equal("l_german:\n a_1: \"Sag \\\"hallo\\\"\"\n a_2: \"Zeile\\nzwei\"\n", yaml);
    }
}

public class TemplateFileWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-templates-" + Guid.NewGuid().ToString("N"));

    public TemplateFileWriterTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Code_is_appended_after_a_blank_line_keeping_windows_newlines()
    {
        var result = TemplateFileWriter.AppendCodeText("a = 1\r\n", "b = 2\nc = 3");

        Assert.Equal("a = 1\r\n\r\nb = 2\r\nc = 3\r\n", result);
    }

    [Fact]
    public void Code_into_empty_text_has_no_leading_blank_line()
    {
        Assert.Equal("b = 2\n", TemplateFileWriter.AppendCodeText(string.Empty, "b = 2"));
    }

    [Fact]
    public void Localization_gets_a_header_when_the_text_is_empty()
    {
        var (text, result) = TemplateFileWriter.AppendLocalizationText(string.Empty, new[] { new LocalizationEntry("a", "x") }, "english", true);

        Assert.Equal("l_english:\n a: \"x\"\n", text);
        Assert.Equal(1, result.Added);
    }

    [Fact]
    public void Localization_skips_existing_keys_and_reports_the_language()
    {
        var existing = "l_german:\n a: \"alt\"\n # Kommentar: b\n";

        var (text, result) = TemplateFileWriter.AppendLocalizationText(
            existing,
            new[] { new LocalizationEntry("a", "neu"), new LocalizationEntry("c", "neu") },
            "english",
            skipDuplicates: true);

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Skipped);
        Assert.Equal("german", result.ExistingLanguage);
        Assert.Contains(" c: \"neu\"", text);
        Assert.DoesNotContain("neu\"\n a", text);
        Assert.Equal(1, text.Split(" a: ").Length - 1);
    }

    [Fact]
    public void Localization_can_add_duplicates_when_asked()
    {
        var (_, result) = TemplateFileWriter.AppendLocalizationText("l_english:\n a: \"alt\"\n", new[] { new LocalizationEntry("a", "neu") }, "english", skipDuplicates: false);

        Assert.Equal(1, result.Added);
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public void Existing_keys_ignore_header_and_comments()
    {
        var keys = TemplateFileWriter.ExistingKeys("﻿l_english:\n # x: y\n one: \"1\"\n two:0 \"2\"\n");

        Assert.Equal(new[] { "one", "two" }, keys.OrderBy(k => k));
    }

    [Fact]
    public void New_files_get_utf8_bom_and_are_created_with_their_folder()
    {
        var path = Path.Combine(_dir, "localization", "english", "x_l_english.yml");

        TemplateFileWriter.AppendLocalizationToFile(path, new[] { new LocalizationEntry("a", "ü") }, "english", true);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        Assert.Contains("ü", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void Existing_files_keep_their_missing_bom()
    {
        var path = Path.Combine(_dir, "a.txt");
        File.WriteAllText(path, "a = 1\n", new UTF8Encoding(false));

        TemplateFileWriter.AppendCodeToFile(path, "b = 2");

        var bytes = File.ReadAllBytes(path);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal("a = 1\n\nb = 2\n", File.ReadAllText(path));
    }
}

public class TemplateStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-templstore-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Save_and_load_roundtrip_including_localization()
    {
        var store = new TemplateStore(_dir);
        var template = new SavedTemplate { Snippet = "idea_1 = { }", Count = 5 };
        template.PointOverrides.Add(new PointOverride("k", Enabled: true, Step: 3));
        template.Localization.Enabled = true;
        template.Localization.Rows.Add(new LocalizationRow { KeyPattern = "idea_{n}", TextPattern = "Idee {n}", Selected = true, PointKeys = { "k" } });

        store.Save("Meine Vorlage", template);
        var loaded = store.Load("Meine Vorlage");

        Assert.NotNull(loaded);
        Assert.Equal("idea_1 = { }", loaded!.Snippet);
        Assert.Equal(5, loaded.Count);
        Assert.Equal(3, loaded.PointOverrides.Single().Step);
        Assert.True(loaded.Localization.Enabled);
        Assert.Equal("Idee {n}", loaded.Localization.Rows.Single().TextPattern);
    }

    [Fact]
    public void Older_files_without_the_new_settings_get_the_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "alt.json"), "{ \"snippet\": \"a_1 = { }\", \"count\": 4 }");

        var loaded = new TemplateStore(_dir).Load("alt");

        Assert.NotNull(loaded);
        Assert.Null(loaded!.Ignore);
        Assert.Empty(loaded.DisabledRuleIds);
        Assert.True(loaded.HideIgnored);
        Assert.True(loaded.MarkChanges);
        Assert.False(loaded.HideSingleNumbers);
        Assert.Equal(0, loaded.ShowMode);
    }

    [Theory]
    [InlineData(0, new[] { 0 })]
    [InlineData(1, new[] { 1 })]
    [InlineData(2, new[] { 2 })]
    [InlineData(3, new[] { 0, 1, 2 })]
    [InlineData(4, new int[0])]
    public void Old_rules_per_shape_become_checked_numbers(int legacyRule, int[] expected)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "alt.json"), "{ \"shapeRules\": { \"iv#_a_#_#\": " + legacyRule + " } }");

        var loaded = new TemplateStore(_dir).Load("alt");

        Assert.Equal(expected, loaded!.ShapeNumbers["iv#_a_#_#"]);
        Assert.Empty(loaded.ShapeRules);
    }

    [Fact]
    public void List_hides_the_last_state_and_sorts_names()
    {
        var store = new TemplateStore(_dir);
        store.Save("b", new SavedTemplate());
        store.Save("A", new SavedTemplate());
        store.Save(TemplateStore.LastName, new SavedTemplate());

        Assert.Equal(new[] { "A", "b" }, store.List());
    }

    [Fact]
    public void Unsafe_names_are_cleaned_and_missing_or_broken_files_load_as_null()
    {
        var store = new TemplateStore(_dir);

        store.Save("a/b:c", new SavedTemplate());

        Assert.Equal(new[] { "a_b_c" }, store.List());
        Assert.Null(store.Load("gibt es nicht"));
        File.WriteAllText(Path.Combine(_dir, "kaputt.json"), "{ nicht json");
        Assert.Null(store.Load("kaputt"));
    }

    [Fact]
    public void Delete_removes_the_file()
    {
        var store = new TemplateStore(_dir);
        store.Save("x", new SavedTemplate());

        store.Delete("x");

        Assert.Empty(store.List());
    }

    private string ModDir => _dir + "-mod";

    [Fact]
    public void Existing_templates_stay_on_the_game_layer()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "alt.json"), "{ \"snippet\": \"a_1 = { }\" }");

        var entries = new TemplateStore(_dir).ListEntries();

        Assert.Equal(new[] { new LayeredEntry("alt", StoreLayer.Game) }, entries);
    }

    [Fact]
    public void The_mod_layer_is_kept_apart_from_the_game_layer()
    {
        try
        {
            var store = new TemplateStore(_dir, ModDir);

            store.Save("x", new SavedTemplate { Snippet = "mod" }, StoreLayer.Mod);
            store.Save("x", new SavedTemplate { Snippet = "spiel" });

            Assert.Equal("spiel", store.Load("x")!.Snippet);
            Assert.Equal("mod", store.Load("x", StoreLayer.Mod)!.Snippet);
            Assert.Equal(new[] { "x" }, store.List(StoreLayer.Mod));
            Assert.Equal(2, store.ListEntries().Count);
            Assert.Equal(StoreLayer.Mod, store.Resolve("x")!.Value.Layer);
            Assert.True(File.Exists(Path.Combine(ModDir, "x.json")));
        }
        finally
        {
            if (Directory.Exists(ModDir))
            {
                Directory.Delete(ModDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Without_a_mod_directory_the_mod_layer_is_empty_and_cannot_be_written()
    {
        var store = new TemplateStore(_dir);

        Assert.Empty(store.List(StoreLayer.Mod));
        Assert.Null(store.Load("x", StoreLayer.Mod));
        Assert.Throws<InvalidOperationException>(() => store.Save("x", new SavedTemplate(), StoreLayer.Mod));
    }

    [Fact]
    public void The_mod_directory_can_be_changed_while_the_store_is_in_use()
    {
        var other = _dir + "-other";
        try
        {
            var store = new TemplateStore(_dir, ModDir);
            store.Save("x", new SavedTemplate(), StoreLayer.Mod);

            store.ModDirectory = other;

            Assert.Empty(store.List(StoreLayer.Mod));

            store.ModDirectory = null;

            Assert.Empty(store.ListEntries());
        }
        finally
        {
            foreach (var directory in new[] { ModDir, other })
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void Old_rules_are_migrated_on_the_mod_layer_too()
    {
        try
        {
            Directory.CreateDirectory(ModDir);
            File.WriteAllText(Path.Combine(ModDir, "alt.json"), "{ \"shapeRules\": { \"iv#_a_#_#\": 1 } }");

            var loaded = new TemplateStore(_dir, ModDir).Load("alt", StoreLayer.Mod);

            Assert.Equal(new[] { 1 }, loaded!.ShapeNumbers["iv#_a_#_#"]);
        }
        finally
        {
            if (Directory.Exists(ModDir))
            {
                Directory.Delete(ModDir, recursive: true);
            }
        }
    }

    [Fact]
    public void The_last_state_stays_on_the_game_layer_and_out_of_the_lists()
    {
        var store = new TemplateStore(_dir, ModDir);

        store.Save(TemplateStore.LastName, new SavedTemplate { Snippet = "zuletzt" });

        Assert.Empty(store.ListEntries());
        Assert.Equal("zuletzt", store.Load(TemplateStore.LastName)!.Snippet);
        Assert.False(Directory.Exists(ModDir));
    }

    [Fact]
    public void DiffOverrides_only_contains_changes_against_the_defaults()
    {
        var defaults = TemplateAnalyzer.Analyze("idea_1 = { cost = 10 }");
        var edited = TemplateAnalyzer.Analyze("idea_1 = { cost = 10 }");
        edited.Points[1].Enabled = true;
        edited.Points[1].Step = 5;

        var diff = TemplateStore.DiffOverrides(defaults, edited);

        var change = Assert.Single(diff);
        Assert.Equal(edited.Points[1].Key, change.Key);
        Assert.True(change.Enabled);
        Assert.Equal(5, change.Step);
        Assert.Null(change.Start);
    }
}
