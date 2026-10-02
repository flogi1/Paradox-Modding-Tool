using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class SpecialBuildingsPresetTests
{
    [Fact]
    public void Preset_has_the_spec_rules_in_order()
    {
        var recipe = SpecialBuildingsPreset.Create("Special Buildings", Array.Empty<string>());

        Assert.Equal("ck3", recipe.GameId);
        Assert.Equal(SpecialBuildingsPreset.Id, recipe.Preset);
        Assert.Equal(new[] { "holdings", "provinz", "barony" }, recipe.Columns.Select(c => c.Name).ToArray());
        Assert.True(recipe.Columns[1].Lookup!.All);
        Assert.Contains("add_special_building_slot = {key}", recipe.Columns[1].Lookup!.Match, StringComparison.Ordinal);
        Assert.True(recipe.Columns[2].TitleKey);
        Assert.Equal(
            new[] { "slots", "iterators", "originals", "buildings", "holdings", "trigger_level_01", "trigger_level_02", "trigger_level_03", "trigger_level_04" },
            recipe.Rules.Select(r => r.Id).ToArray());

        var slots = Assert.IsType<LineRule>(recipe.Rules[0]);
        Assert.Equal(MatchKind.Regex, slots.MatchKind);
        Assert.True(slots.CreateCopy);
        var iterators = Assert.IsType<LineRule>(recipe.Rules[1]);
        Assert.Equal(RuleScope.Global, iterators.Scope);
        Assert.Equal("$1_province", iterators.Replacement);
        Assert.Equal(string.Empty, iterators.Files);
        var originals = Assert.IsType<LineRule>(recipe.Rules[2]);
        Assert.Equal(LineAction.RemoveBlock, originals.Action);
        Assert.True(originals.VanillaPathsOnly);
        var buildings = Assert.IsType<BlockCopyRule>(recipe.Rules[3]);
        Assert.Equal("common/buildings/*.txt", buildings.SourceFiles);
        Assert.True(buildings.AdoptExisting);
        Assert.Equal("common/buildings/pmt_special_buildings.txt", buildings.Target);
        Assert.IsType<ListEntryRule>(recipe.Rules[4]);
        Assert.Equal(8, recipe.Watch.Patterns.Count);
    }

    [Theory]
    [InlineData(0, "01", 2)]
    [InlineData(1, "02", 4)]
    [InlineData(2, "03", 6)]
    [InlineData(3, "04", 8)]
    public void Trigger_rules_turn_the_castle_church_requirement_into_the_main_building_requirement(int index, string level, int mainLevel)
    {
        var recipe = SpecialBuildingsPreset.Create("S", Array.Empty<string>());

        var rule = Assert.IsType<LineRule>(recipe.Rules[5 + index]);

        Assert.Equal(LineAction.Replace, rule.Action);
        Assert.Equal(MatchKind.Text, rule.MatchKind);
        Assert.Equal(RuleScope.Global, rule.Scope);
        Assert.Equal("common/buildings/pmt_special_buildings.txt", rule.Files);
        Assert.False(rule.CreateCopy);
        Assert.True(rule.Enabled);
        Assert.Equal($"building_requirement_castle_city_church = {{ LEVEL = {level} }}", rule.Match);
        Assert.Equal($"building_requirement_main_building = {{ LEVEL = {mainLevel} }}", rule.Replacement);
    }

    [Fact]
    public void Buildings_rule_adds_the_resource_hooks_to_every_converted_building()
    {
        var buildings = (BlockCopyRule)SpecialBuildingsPreset.Create("S", Array.Empty<string>()).Rules[3];

        var start = buildings.Edits.Single(e => e.Kind == BlockEditKind.InsertInto && e.Key == "on_start");
        var cancelled = buildings.Edits.Single(e => e.Kind == BlockEditKind.InsertInto && e.Key == "on_cancelled");

        Assert.Equal(
            "flogis_techmod_subtract_wood_250 = yes\nflogis_techmod_subtract_stone_250 = yes\nflogis_techmod_subtract_metal_250 = yes\nflogis_techmod_subtract_luxury_250 = yes",
            start.Text);
        Assert.Equal(
            "flogis_techmod_add_wood_250 = yes\nflogis_techmod_add_stone_250 = yes\nflogis_techmod_add_metal_250 = yes\nflogis_techmod_add_luxury_250 = yes",
            cancelled.Text);
    }

    [Fact]
    public void A_switched_off_edit_is_not_applied_and_the_other_edits_still_are()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("common/buildings/00_special_mines.txt", "x_01 = {\n\ttype = special\n\tcan_construct_potential = {\n\t\ta = yes\n\t}\n}\n");
        mod.WriteVanilla("history/provinces/k_x.txt", "1 = {\n\tspecial_building_slot = x_01\n}\n");
        mod.WriteVanilla("common/landed_titles/00_landed_titles.txt", "e_x = { k_x = { d_x = { c_x = {\n\tb_a = { province = 1 }\n} } } }\n");
        mod.WriteVanilla("common/holdings/00_holdings.txt", "castle_holding = {\n\tbuildings = {\n\t\tcastle_01\n\t}\n}\ncity_holding = {\n\tbuildings = {\n\t\tcity_01\n\t}\n}\nchurch_holding = {\n\tbuildings = {\n\t\ttemple_01\n\t}\n}\n");
        var recipe = SpecialBuildingsPreset.Create("S", Array.Empty<string>());
        recipe.Rows.Add(new PatchRow { Key = "x_01" });
        var buildings = (BlockCopyRule)recipe.Rules[3];
        buildings.Edits.Single(e => e.Key == "on_start").Enabled = false;

        var plan = PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe);
        var unit = Assert.Single(GeneratedRegion.Read(plan.Files.Single(f => f.RelativePath == "common/buildings/pmt_special_buildings.txt").NewText, recipe.Id, "buildings").Units).Text;

        Assert.DoesNotContain("on_start", unit, StringComparison.Ordinal);
        Assert.DoesNotContain("subtract", unit, StringComparison.Ordinal);
        Assert.Contains("on_cancelled = {", unit, StringComparison.Ordinal);
        Assert.Contains("barony = title:b_a", unit, StringComparison.Ordinal);
    }

    [Fact]
    public void Edits_are_enabled_by_default_and_old_recipe_files_load_them_enabled()
    {
        Assert.True(new BlockEdit().Enabled);

        using var mod = new TempMod();
        var folder = PatchRecipeStore.Folder(mod.ModRoot);
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            PatchRecipeStore.RecipePath(folder, "r"),
            "{\"id\":\"r\",\"name\":\"R\",\"gameId\":\"ck3\",\"rules\":[{\"kind\":\"blockCopy\",\"id\":\"b\",\"edits\":[{\"kind\":\"insertInto\",\"key\":\"on_start\",\"text\":\"a = yes\"}]}]}");

        var recipe = Assert.Single(PatchRecipeStore.LoadAll(folder, new List<string>()));
        Assert.True(((BlockCopyRule)recipe.Rules.Single()).Edits.Single().Enabled);

        var copy = PatchRecipeStore.Clone(PatchRecipeStore.Clone(recipe));
        ((BlockCopyRule)copy.Rules.Single()).Edits.Single().Enabled = false;
        Assert.False(((BlockCopyRule)PatchRecipeStore.Clone(copy).Rules.Single()).Edits.Single().Enabled);
    }

    [Fact]
    public void Preset_adds_hooks_and_rewrites_triggers_in_the_generated_buildings_and_a_second_run_changes_nothing()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(
            "common/buildings/00_special_mines.txt",
            "x_01 = {\n\ttype = special\n\tnext_building = x_02\n\ton_start = {\n\t\tvanilla_effect = yes\n\t}\n\tcan_construct_potential = {\n\t\tbuilding_requirement_castle_city_church = { LEVEL = 01 }\n\t}\n}\n"
            + "x_02 = {\n\ttype = special\n\tcan_construct_potential = {\n\t\tbuilding_requirement_castle_city_church =\n\t\t{\n\t\t\tLEVEL = 03\n\t\t}\n\t}\n}\n");
        mod.WriteVanilla("history/provinces/k_x.txt", "1 = {\n\tspecial_building_slot = x_01\n}\n");
        mod.WriteVanilla("common/landed_titles/00_landed_titles.txt", "e_x = { k_x = { d_x = { c_x = {\n\tb_a = { province = 1 }\n} } } }\n");
        mod.WriteVanilla("common/holdings/00_holdings.txt", "castle_holding = {\n\tbuildings = {\n\t\tcastle_01\n\t}\n}\ncity_holding = {\n\tbuildings = {\n\t\tcity_01\n\t}\n}\nchurch_holding = {\n\tbuildings = {\n\t\ttemple_01\n\t}\n}\n");
        var recipe = SpecialBuildingsPreset.Create("S", Array.Empty<string>());
        recipe.Rows.Add(new PatchRow { Key = "x_01" });

        var plan = PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe);
        var units = GeneratedRegion.Read(plan.Files.Single(f => f.RelativePath == "common/buildings/pmt_special_buildings.txt").NewText, recipe.Id, "buildings").Units;
        var unit = Assert.Single(units).Text;
        var split = unit.IndexOf("x_02 = {", StringComparison.Ordinal);
        var first = unit[..split];
        var second = unit[split..];

        Assert.Empty(plan.Skipped);

        // on_start: vanilla hat schon einen Block, die vier Zeilen kommen dazu; on_cancelled gab es nicht und wird angelegt (beide Stufen der Kette).
        Assert.Contains("vanilla_effect = yes", first, StringComparison.Ordinal);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(first, "on_start = \\{").Count);
        Assert.Contains("flogis_techmod_subtract_wood_250 = yes", first, StringComparison.Ordinal);
        Assert.Contains("on_cancelled = {", first, StringComparison.Ordinal);
        Assert.Contains("flogis_techmod_add_luxury_250 = yes", first, StringComparison.Ordinal);
        Assert.Contains("on_start = {", second, StringComparison.Ordinal);
        Assert.Contains("flogis_techmod_subtract_metal_250 = yes", second, StringComparison.Ordinal);
        Assert.Contains("on_cancelled = {", second, StringComparison.Ordinal);

        // Trigger: Einzeilform (01 -> 2) und Mehrzeilform (03 -> 6), nichts Altes bleibt.
        Assert.Contains("building_requirement_main_building = { LEVEL = 2 }", first, StringComparison.Ordinal);
        Assert.Contains("building_requirement_main_building = { LEVEL = 6 }", second, StringComparison.Ordinal);
        Assert.DoesNotContain("castle_city_church", first + second, StringComparison.Ordinal);

        PatchPlanWriter.CreateAction(PatchRunnerTests.Context(mod), plan).Do();
        var again = PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe);
        Assert.All(again.Files, f => Assert.Equal(PlannedFileStatus.Unchanged, f.Status));
    }

    [Fact]
    public void Iterator_rule_replaces_all_four_iterators()
    {
        using var mod = new TempMod();
        mod.WriteMod("events/a.txt", "every_special_building_province = {\n\tlimit = { any_special_building_province = { } }\n}\nrandom_special_building_province = { }\nordered_special_building_province = { }\n", bom: false);
        var recipe = SpecialBuildingsPreset.Create("S", Array.Empty<string>());
        var iterators = (LineRule)recipe.Rules[1];
        iterators.Files = "events/a.txt";
        recipe.Rules.RemoveAll(r => r != iterators);

        var plan = PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe);

        Assert.Equal(
            "every_province = {\n\tlimit = { any_province = { } }\n}\nrandom_province = { }\nordered_province = { }\n",
            plan.Files.Single(f => f.RelativePath == "events/a.txt").NewText);
    }

    [Fact]
    public void Vanilla_specials_are_chain_starts_with_mod_state()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("common/buildings/00_special_buildings.txt",
            "x_01 = {\n\ttype = special\n\tnext_building = x_02\n}\nx_02 = {\n\ttype = special\n}\ny = {\n\ttype = special\n}\nz = {\n\ttype = regular\n}\n");
        mod.WriteMod("common/buildings/01_own.txt", "y = {\n\ttype = special\n}\nx_01 = {\n\tcost = 1\n}\n", bom: false);

        var specials = SpecialBuildingsPreset.VanillaSpecials(new PatchWorkspace(mod.ModRoot, mod.VanillaRoot));

        Assert.Equal(new[] { new VanillaSpecial("x_01", false), new VanillaSpecial("y", true) }, specials.ToArray());
    }

    [Fact]
    public void Preset_converts_whole_rows_and_adopts_by_default()
    {
        var recipe = SpecialBuildingsPreset.Create("S", Array.Empty<string>());

        Assert.True(recipe.WholeRows);
        Assert.True(((BlockCopyRule)recipe.Rules[3]).AdoptExisting);
    }

    [Fact]
    public void Row_skipped_by_one_rule_is_skipped_by_all_rules()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("common/buildings/00_special_buildings.txt", "x_01 = {\n\ttype = special\n}\ny_01 = {\n\ttype = special\n}\n");
        mod.WriteVanilla("history/provinces/k_x.txt", "1 = {\n\tspecial_building_slot = x_01\n}\n2 = {\n\tspecial_building_slot = y_01\n}\n");
        mod.WriteVanilla("common/holdings/00_holdings.txt", "castle_holding = {\n\tbuildings = {\n\t\tcastle_01\n\t}\n}\ncity_holding = {\n\tbuildings = {\n\t\tcity_01\n\t}\n}\nchurch_holding = {\n\tbuildings = {\n\t\ttemple_01\n\t}\n}\n");
        mod.WriteMod("common/buildings/01_own.txt", "x_01 = {\n\ttype = special\n}\n\nx_01 = {\n\ttype = special\n}\n", bom: false);
        var recipe = SpecialBuildingsPreset.Create("S", Array.Empty<string>());
        recipe.Rows.Add(new PatchRow { Key = "x_01" });
        recipe.Rows.Add(new PatchRow { Key = "y_01" });

        var plan = PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe);
        string Text(string rel) => plan.Files.Single(f => f.RelativePath == rel).NewText;

        // x_01 ist in der Mod doppelt definiert: die Block-Kopie überspringt es, also lassen es auch alle anderen Regeln aus.
        var history = Text("history/provinces/k_x.txt");
        Assert.Contains("\tspecial_building_slot = x_01", history, StringComparison.Ordinal);
        Assert.Contains("#special_building_slot = y_01", history, StringComparison.Ordinal);
        var holdings = Text("common/holdings/00_holdings.txt");
        Assert.DoesNotContain("x_01", holdings, StringComparison.Ordinal);
        Assert.Contains("y_01", holdings, StringComparison.Ordinal);
        Assert.Contains(plan.Skipped, s => s.RuleId == "slots" && s.RowKey == "x_01" && s.Reason.Contains("buildings", StringComparison.Ordinal));
        Assert.Contains(plan.Skipped, s => s.RuleId == "holdings" && s.RowKey == "x_01");
        Assert.DoesNotContain(plan.Skipped, s => s.RowKey == "y_01");
    }

    [Fact]
    public void Special_without_place_in_history_stays_special()
    {
        // Slot kommt aus einem Script (Event, Entscheidung, Großprojekt): ohne Baronie wäre das umgewandelte Gebäude überall baubar.
        using var mod = new TempMod();
        mod.WriteVanilla("common/buildings/00_special_mines.txt", "m_01 = {\n\ttype = special\n}\n");
        mod.WriteVanilla("history/provinces/k_x.txt", "1 = {\n\tholding = castle_holding\n}\n");
        mod.WriteVanilla("common/landed_titles/00_landed_titles.txt", "e_x = { k_x = { d_x = { c_x = {\n\tb_a = { province = 1 }\n} } } }\n");
        mod.WriteVanilla("common/holdings/00_holdings.txt", "castle_holding = {\n\tbuildings = {\n\t\tcastle_01\n\t}\n}\ncity_holding = {\n\tbuildings = {\n\t\tcity_01\n\t}\n}\nchurch_holding = {\n\tbuildings = {\n\t\ttemple_01\n\t}\n}\n");
        mod.WriteMod("common/buildings/01_own.txt", "m_01 = {\n\ttype = special\n}\n", bom: false);
        var recipe = SpecialBuildingsPreset.Create("S", Array.Empty<string>());
        recipe.Rows.Add(new PatchRow { Key = "m_01" });

        var plan = PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe);

        Assert.DoesNotContain(plan.Files, f => f.RelativePath == "common/buildings/pmt_special_buildings.txt" && f.NewText.Contains("m_01", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Files, f => f.RelativePath == "common/buildings/01_own.txt" && f.Status != PlannedFileStatus.Unchanged);
        Assert.DoesNotContain(plan.Files, f => f.RelativePath == "common/holdings/00_holdings.txt" && f.NewText.Contains("m_01", StringComparison.Ordinal));
        Assert.Contains(plan.Skipped, s => s.RuleId == "buildings" && s.RowKey == "m_01");
    }

    [Fact]
    public void Preset_end_to_end_on_fixture()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("common/buildings/00_special_mines.txt", "x_01 = {\n\ttype = special\n\tcan_construct_potential = {\n\t\ta = yes\n\t}\n}\n");
        mod.WriteVanilla("history/provinces/k_x.txt", "1 = {\n\tspecial_building_slot = x_01\n}\n2 = {\n\t1100.1.1 = {\n\t\tadd_special_building_slot = x_01\n\t}\n}\n");
        mod.WriteVanilla("common/landed_titles/00_landed_titles.txt", "e_x = { k_x = { d_x = { c_x = {\n\tb_a = { province = 1 }\n\tb_b = { province = 2 }\n} } } }\n");
        mod.WriteVanilla("common/holdings/00_holdings.txt", "castle_holding = {\n\tbuildings = {\n\t\tcastle_01\n\t}\n}\ncity_holding = {\n\tbuildings = {\n\t\tcity_01\n\t}\n}\nchurch_holding = {\n\tbuildings = {\n\t\ttemple_01\n\t}\n}\n");
        mod.WriteMod("common/buildings/01_own.txt", "x_01 = {\n\ttype = special\n\tcost = 7\n\tcan_construct_potential = {\n\t\ta = yes\n\t}\n}\n", bom: false);
        var recipe = SpecialBuildingsPreset.Create("S", Array.Empty<string>());
        recipe.Rows.Add(new PatchRow { Key = "x_01" });
        ((BlockCopyRule)recipe.Rules[3]).AdoptExisting = true;

        var plan = PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe);
        string Text(string rel) => plan.Files.Single(f => f.RelativePath == rel).NewText;

        Assert.Empty(plan.Skipped);
        var history = Text("history/provinces/k_x.txt");
        Assert.Contains("#special_building_slot = x_01", history, StringComparison.Ordinal);
        Assert.Contains("#add_special_building_slot = x_01", history, StringComparison.Ordinal);
        var unit = GeneratedRegion.Read(Text("common/buildings/pmt_special_buildings.txt"), recipe.Id, "buildings").Units.Single().Text;
        Assert.Contains("cost = 7", unit, StringComparison.Ordinal);
        Assert.DoesNotContain("type = special", unit, StringComparison.Ordinal);
        Assert.Contains("OR = {", unit, StringComparison.Ordinal);
        Assert.Contains("barony = title:b_a", unit, StringComparison.Ordinal);
        Assert.Contains("barony = title:b_b", unit, StringComparison.Ordinal);
        Assert.DoesNotContain("x_01", Text("common/buildings/01_own.txt"), StringComparison.Ordinal);
        Assert.Contains("x_01", Text("common/holdings/00_holdings.txt"), StringComparison.Ordinal);

        PatchPlanWriter.CreateAction(PatchRunnerTests.Context(mod), plan).Do();
        var second = PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe);
        Assert.All(second.Files, f => Assert.Equal(PlannedFileStatus.Unchanged, f.Status));
    }

    private static TempMod AdoptFixture()
    {
        var mod = new TempMod();
        mod.WriteVanilla("common/buildings/00_special_buildings.txt",
            "a_01 = {\n\ttype = special\n\tnext_building = a_02\n}\na_02 = {\n\ttype = special\n}\nb_01 = {\n\ttype = special\n}\nw_01 = {\n\ttype = duchy_capital\n}\n");
        mod.WriteVanilla("history/provinces/k_x.txt", "1 = {\n\tspecial_building_slot = a_01\n}\n");
        mod.WriteVanilla("common/landed_titles/00_landed_titles.txt", "e_x = { k_x = { d_x = { c_x = {\n\tb_a = { province = 1 }\n} } } }\n");
        mod.WriteMod("common/buildings/00_special_buildings.txt",
            "a_01 = {\n\tnext_building = a_02\n}\na_02 = { }\nb_01 = { }\nw_01 = { }\n");
        return mod;
    }

    [Fact]
    public void Rows_to_adopt_are_special_chain_starts_with_place()
    {
        using var mod = AdoptFixture();
        var recipe = SpecialBuildingsPreset.Create("S", Array.Empty<string>());
        var renamed = new[] { "a_01", "a_02", "b_01", "w_01" };

        var rows = SpecialBuildingsPreset.RowsToAdopt(recipe, new PatchWorkspace(mod.ModRoot, mod.VanillaRoot), renamed);

        Assert.Equal(new[] { "a_01" }, rows.Select(r => r.Key).ToArray());
        recipe.Rows.Add(new PatchRow { Key = "a_01" });
        Assert.Empty(SpecialBuildingsPreset.RowsToAdopt(recipe, new PatchWorkspace(mod.ModRoot, mod.VanillaRoot), renamed));
    }

    [Fact]
    public void Adopted_row_takes_holdings_from_mod()
    {
        using var mod = AdoptFixture();
        var recipe = SpecialBuildingsPreset.Create("S", Array.Empty<string>());

        var without = SpecialBuildingsPreset.RowsToAdopt(recipe, new PatchWorkspace(mod.ModRoot, mod.VanillaRoot), new[] { "a_01" });
        mod.WriteMod("common/holdings/00_holdings.txt",
            "castle_holding = {\n\tbuildings = {\n\t\ta_01\n\t\tcastle_01\n\t}\n}\ncity_holding = {\n\tbuildings = {\n\t\tcity_01\n\t}\n}\ntemple_citadel_holding = {\n\tbuildings = { a_01 }\n}\n");
        var with = SpecialBuildingsPreset.RowsToAdopt(recipe, new PatchWorkspace(mod.ModRoot, mod.VanillaRoot), new[] { "a_01" });

        Assert.False(without.Single().Values.ContainsKey("holdings"));
        Assert.Equal("castle_holding, temple_citadel_holding", with.Single().Values["holdings"]);
    }
}
