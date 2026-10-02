using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class ColumnResolverTests
{
    private const string History = "1785 = {\n\tculture = x\n\t867.1.1 = { special_building_slot = alcazar_01 }\n}\n";
    private const string Titles = "e_x = { k_x = { d_x = { c_x = { b_segovia = { province = 1785 } } } } }\n";

    private static PatchColumn Provinz(bool optional = true) => new()
    {
        Name = "provinz",
        Lookup = new DerivedLookup { Files = "history/provinces/*.txt", Match = "special_building_slot = {key}", Take = LookupTake.TopLevelKey, Optional = optional },
    };

    private static PatchColumn Barony() => new()
    {
        Name = "barony",
        TitleKey = true,
        Lookup = new DerivedLookup { Files = "common/landed_titles/**/*.txt", Match = "province = {provinz}", Take = LookupTake.EnclosingKey, Optional = true },
    };

    private static PatchRecipe Recipe(params string[] keys)
    {
        var recipe = new PatchRecipe { Id = "r" };
        recipe.Columns.Add(Provinz());
        recipe.Columns.Add(Barony());
        foreach (var key in keys)
        {
            recipe.Rows.Add(new PatchRow { Key = key });
        }

        return recipe;
    }

    private static ResolvedTable Resolve(TempMod mod, PatchRecipe recipe) =>
        ColumnResolver.Resolve(recipe, new PatchWorkspace(mod.ModRoot, mod.VanillaRoot));

    private const string ThreeProvinces =
        "1 = { special_building_slot = x_01 }\n2 = { special_building = x_01 }\n3 = { 1100.1.1 = { add_special_building_slot = x_01 } }\n";

    private const string ThreeTitles = "e_x = { k_x = { d_x = { c_x = {\n b_a = { province = 1 }\n b_b = { province = 2 }\n b_c = { province = 3 }\n} } } }\n";

    private static PatchRecipe AllHitsRecipe(string key, string? baronyMatch = null)
    {
        var recipe = new PatchRecipe { Id = "r", Rows = new() { new PatchRow { Key = key } } };
        recipe.Columns.Add(new PatchColumn
        {
            Name = "provinz",
            Lookup = new DerivedLookup
            {
                Files = "history/provinces/*.txt",
                Match = "special_building_slot = {key}; special_building = {key}; add_special_building_slot = {key}",
                Take = LookupTake.TopLevelKey, Optional = true, All = true,
            },
        });
        recipe.Columns.Add(new PatchColumn
        {
            Name = "barony",
            TitleKey = true,
            Lookup = new DerivedLookup { Files = "common/landed_titles/**/*.txt", Match = baronyMatch ?? "province = {provinz}", Take = LookupTake.EnclosingKey, Optional = true, All = true },
        });
        return recipe;
    }

    [Fact]
    public void Lookup_alternatives_find_add_forms()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("history/provinces/k_x.txt", "1 = { 1066.1.1 = { add_special_building_slot = x_01 } }\n");
        var recipe = Recipe("x_01");
        recipe.Columns[0].Lookup!.Match = "special_building_slot = {key}; add_special_building_slot = {key}";

        Assert.Equal("1", Resolve(mod, recipe).Rows[0].Values["provinz"]);
    }

    [Fact]
    public void All_hits_make_a_list()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("history/provinces/k_x.txt", ThreeProvinces);
        mod.WriteVanilla("common/landed_titles/00_landed_titles.txt", ThreeTitles);

        var table = Resolve(mod, AllHitsRecipe("x_01"));

        Assert.Equal("1, 2, 3", table.Rows[0].Values["provinz"]);
        Assert.Contains("provinz", table.ListColumns);
    }

    [Fact]
    public void All_hits_follow_file_order_not_search_order()
    {
        using var mod = new TempMod();
        // Provinz 3 setzt das Gebäude (zweite Suchform), 1 nur den Slot (erste Suchform); die Datei nennt 3 zuerst.
        mod.WriteVanilla("history/provinces/k_x.txt", "3 = { special_building = x_01 }\n1 = { special_building_slot = x_01 }\n");
        mod.WriteVanilla("common/landed_titles/00_landed_titles.txt", ThreeTitles);

        var row = Resolve(mod, AllHitsRecipe("x_01")).Rows[0];

        Assert.Equal("3, 1", row.Values["provinz"]);
        Assert.Equal("b_a, b_c", row.Values["barony"]);
    }

    [Fact]
    public void Lookup_expands_list_valued_column()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("history/provinces/k_x.txt", ThreeProvinces);
        mod.WriteVanilla("common/landed_titles/00_landed_titles.txt", ThreeTitles);

        var row = Resolve(mod, AllHitsRecipe("x_01")).Rows[0];

        Assert.Equal("b_a, b_b, b_c", row.Values["barony"]);
        Assert.False(row.HasErrors);
    }

    [Fact]
    public void Title_check_checks_each_list_value()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("history/provinces/k_x.txt", ThreeProvinces);
        mod.WriteVanilla("common/landed_titles/00_landed_titles.txt", ThreeTitles);
        var recipe = AllHitsRecipe("x_01");
        recipe.Columns[1].Lookup = null;
        recipe.Rows[0].Values["barony"] = "b_a, b_bamiyan, b_c";

        var row = Resolve(mod, recipe).Rows[0];

        Assert.Equal(new[] { PatchTexts.UnknownTitle("b_bamiyan") }, row.Errors.ToArray());
    }

    [Fact]
    public void Derives_province_from_nested_history_block_and_barony_from_landed_titles()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("history/provinces/k_x.txt", History);
        mod.WriteVanilla("common/landed_titles/00_landed_titles.txt", Titles);

        var row = Assert.Single(Resolve(mod, Recipe("alcazar_01")).Rows);

        Assert.Equal("alcazar_01", row.Values["key"]);
        Assert.Equal("1785", row.Values["provinz"]);
        Assert.Equal("b_segovia", row.Values["barony"]);
        Assert.False(row.HasErrors);
    }

    [Fact]
    public void Vanilla_source_ignores_commented_slot_in_mod_copy()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("history/provinces/k_x.txt", History);
        mod.WriteMod("history/provinces/k_x.txt", History.Replace("special_building_slot", "#special_building_slot"));
        mod.WriteVanilla("common/landed_titles/00_landed_titles.txt", Titles);

        Assert.Equal("1785", Resolve(mod, Recipe("alcazar_01")).Rows[0].Values["provinz"]);
    }

    [Fact]
    public void Mod_over_vanilla_prefers_the_mod_file()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("history/provinces/k_x.txt", History);
        mod.WriteMod("history/provinces/k_x.txt", History.Replace("1785", "1786"));
        var recipe = Recipe("alcazar_01");
        recipe.Columns[0].Lookup!.Source = LookupSource.ModOverVanilla;

        Assert.Equal("1786", Resolve(mod, recipe).Rows[0].Values["provinz"]);
    }

    [Fact]
    public void Missing_required_value_is_a_row_error_and_optional_is_empty()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("history/provinces/k_x.txt", History);
        var recipe = Recipe("unknown_01");

        var optional = Resolve(mod, recipe).Rows[0];
        Assert.Equal("", optional.Values["provinz"]);
        Assert.False(optional.HasErrors);

        recipe.Columns[0] = Provinz(optional: false);
        var required = Resolve(mod, recipe).Rows[0];
        Assert.Contains("provinz: nicht gefunden", required.Errors);
    }

    [Fact]
    public void Unknown_title_is_a_row_error()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("history/provinces/k_x.txt", History);
        mod.WriteVanilla("common/landed_titles/00_landed_titles.txt", Titles);
        var recipe = Recipe("alcazar_01");
        recipe.Columns[1] = new PatchColumn { Name = "barony", TitleKey = true };
        recipe.Rows[0].Values["barony"] = "b_bamiyan";

        var row = Resolve(mod, recipe).Rows[0];

        Assert.Contains("Titel „b_bamiyan“ gibt es nicht", row.Errors);
    }

    [Fact]
    public void Title_defined_only_in_the_mod_is_accepted()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/landed_titles/01_mod.txt", "e_y = { b_new = { province = 9 } }\n");
        var recipe = new PatchRecipe { Id = "r", Columns = { new PatchColumn { Name = "barony", TitleKey = true } } };
        recipe.Rows.Add(new PatchRow { Key = "a", Values = { ["barony"] = "b_new" } });

        Assert.False(Resolve(mod, recipe).Rows[0].HasErrors);
    }

    [Fact]
    public void Duplicate_key_marks_the_second_row()
    {
        using var mod = new TempMod();
        var rows = Resolve(mod, Recipe("a_01", "a_01")).Rows;

        Assert.False(rows[0].HasErrors);
        Assert.Contains("Doppelter Schlüssel", rows[1].Errors);
    }

    [Fact]
    public void Empty_cell_takes_the_column_default()
    {
        using var mod = new TempMod();
        var recipe = new PatchRecipe { Id = "r", Columns = { new PatchColumn { Name = "holdings", Type = ColumnType.List, Default = "castle_holding" } } };
        recipe.Rows.Add(new PatchRow { Key = "a", Values = { ["holdings"] = " " } });
        recipe.Rows.Add(new PatchRow { Key = "b", Values = { ["holdings"] = "city_holding" } });

        var table = Resolve(mod, recipe);

        Assert.Equal("castle_holding", table.Rows[0].Values["holdings"]);
        Assert.Equal("city_holding", table.Rows[1].Values["holdings"]);
        Assert.Contains("holdings", table.ListColumns);
    }

    [Fact]
    public void Unparsable_lookup_file_is_a_warning_not_a_crash()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("history/provinces/k_bad.txt", "1 = {\n");
        mod.WriteVanilla("history/provinces/k_x.txt", History);

        var table = Resolve(mod, Recipe("alcazar_01"));

        Assert.Equal("1785", table.Rows[0].Values["provinz"]);
        Assert.Contains(table.Warnings, w => w.Contains("history/provinces/k_bad.txt", StringComparison.Ordinal));
    }
}
