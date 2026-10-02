using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class PatchRecipeStoreTests
{
    private static PatchRecipe SampleRecipe() => new()
    {
        Id = "special_buildings",
        Name = "Special Buildings in normale Gebäude",
        GameId = "ck3",
        Columns =
        {
            new PatchColumn { Name = "holdings", Type = ColumnType.List, Default = "castle_holding, city_holding" },
            new PatchColumn
            {
                Name = "barony",
                Type = ColumnType.Text,
                TitleKey = true,
                Lookup = new DerivedLookup
                {
                    Files = "common/landed_titles/**/*.txt",
                    Source = LookupSource.Vanilla,
                    Match = "province = {provinz}",
                    Take = LookupTake.EnclosingKey,
                    Optional = true,
                },
            },
        },
        Rows = { new PatchRow { Key = "angkor_wat_01", Values = { ["holdings"] = "castle_holding" } } },
        Rules =
        {
            new LineRule { Id = "remove", Name = "Blöcke entfernen", Files = "common/buildings/00_special_buildings.txt", Match = "{key}", Action = LineAction.RemoveBlock, FollowNext = true, CreateCopy = true },
            new BlockCopyRule
            {
                Id = "defs",
                Name = "Definitionen",
                SourceFiles = "common/buildings/00_special_buildings.txt",
                FollowNext = true,
                Target = "common/buildings/pmt_special_buildings.txt",
                Edits =
                {
                    new BlockEdit { Kind = BlockEditKind.Remove, Key = "type", Value = "special" },
                    new BlockEdit { Kind = BlockEditKind.InsertInto, Key = "can_construct_potential", Text = "barony = title:{barony}" },
                },
            },
            new TemplateRule { Id = "sort", Name = "Sortieren", Target = "common/scripted_effects/sort.txt", Inside = "a/b", Text = "if = { }", Scope = RuleScope.Global },
            new ListEntryRule { Id = "holdings", Name = "Holdings", Target = "common/holdings/00_holdings.txt", ListPath = "{holdings}/buildings", Entry = "{key}", CreateCopy = true, Enabled = false },
        },
        Watch = { Patterns = { "has_special_building" }, Ignored = { new IgnoredWatchFile { Path = "events/x.txt", Note = "egal" } } },
    };

    [Fact]
    public void Old_recipe_json_without_new_fields_loads()
    {
        using var mod = new TempMod();
        mod.WriteMod(".pmt-patches/alt.json", """
            {
              "id": "alt", "name": "Alt", "gameId": "ck3",
              "columns": [ { "name": "provinz", "lookup": { "files": "history/provinces/*.txt", "match": "special_building_slot = {key}" } } ],
              "rows": [ { "key": "x_01" } ],
              "rules": [
                { "kind": "lines", "id": "l", "files": "a/*.txt", "match": "{key}" },
                { "kind": "blockCopy", "id": "b", "sourceFiles": "common/buildings/*.txt", "target": "common/buildings/t.txt" }
              ]
            }
            """, bom: false);

        var errors = new List<string>();
        var recipe = Assert.Single(PatchRecipeStore.LoadAll(PatchRecipeStore.Folder(mod.ModRoot), errors));

        Assert.Empty(errors);
        Assert.False(recipe.Columns[0].Lookup!.All);
        Assert.False(Assert.IsType<LineRule>(recipe.Rules[0]).VanillaPathsOnly);
        Assert.False(Assert.IsType<BlockCopyRule>(recipe.Rules[1]).AdoptExisting);
        Assert.Null(recipe.Preset);
    }

    [Fact]
    public void Recipe_with_all_rule_kinds_round_trips()
    {
        using var mod = new TempMod();
        PatchRecipeStore.Save(PatchRecipeStore.Folder(mod.ModRoot), SampleRecipe());

        Assert.True(mod.ModExists(".pmt-patches/special_buildings.json"));
        var errors = new List<string>();
        var loaded = Assert.Single(PatchRecipeStore.LoadAll(PatchRecipeStore.Folder(mod.ModRoot), errors));
        Assert.Empty(errors);

        Assert.Equal("special_buildings", loaded.Id);
        Assert.Equal("Special Buildings in normale Gebäude", loaded.Name);
        Assert.Equal("ck3", loaded.GameId);
        Assert.Equal(ColumnType.List, loaded.Columns[0].Type);
        Assert.Equal("castle_holding, city_holding", loaded.Columns[0].Default);
        var lookup = loaded.Columns[1].Lookup!;
        Assert.True(loaded.Columns[1].TitleKey);
        Assert.Equal(LookupTake.EnclosingKey, lookup.Take);
        Assert.True(lookup.Optional);
        Assert.Equal("province = {provinz}", lookup.Match);
        Assert.Equal("castle_holding", loaded.Rows[0].Values["holdings"]);

        Assert.Collection(
            loaded.Rules,
            r =>
            {
                var line = Assert.IsType<LineRule>(r);
                Assert.Equal(LineAction.RemoveBlock, line.Action);
                Assert.True(line.FollowNext);
                Assert.True(line.CreateCopy);
            },
            r =>
            {
                var copy = Assert.IsType<BlockCopyRule>(r);
                Assert.Equal(2, copy.Edits.Count);
                Assert.Equal(BlockEditKind.Remove, copy.Edits[0].Kind);
                Assert.Equal("special", copy.Edits[0].Value);
                Assert.Equal("barony = title:{barony}", copy.Edits[1].Text);
            },
            r =>
            {
                var template = Assert.IsType<TemplateRule>(r);
                Assert.Equal("a/b", template.Inside);
                Assert.Equal(RuleScope.Global, template.Scope);
            },
            r =>
            {
                var list = Assert.IsType<ListEntryRule>(r);
                Assert.False(list.Enabled);
                Assert.Equal("{holdings}/buildings", list.ListPath);
            });
        Assert.Equal("egal", loaded.Watch.Ignored[0].Note);
        Assert.Equal("has_special_building", loaded.Watch.Patterns[0]);
    }

    [Fact]
    public void LoadAll_without_folder_returns_empty_and_no_errors()
    {
        using var mod = new TempMod();
        var errors = new List<string>();

        Assert.Empty(PatchRecipeStore.LoadAll(PatchRecipeStore.Folder(mod.ModRoot), errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void LoadAll_skips_broken_json_and_reports_the_file_name()
    {
        using var mod = new TempMod();
        mod.WriteMod(".pmt-patches/broken.json", "{");
        var errors = new List<string>();

        Assert.Empty(PatchRecipeStore.LoadAll(PatchRecipeStore.Folder(mod.ModRoot), errors));
        Assert.Contains("broken.json", Assert.Single(errors));
    }

    [Fact]
    public void LoadAll_ignores_memory_files()
    {
        using var mod = new TempMod();
        PatchRecipeStore.Save(PatchRecipeStore.Folder(mod.ModRoot), SampleRecipe());
        mod.WriteMod(".pmt-patches/special_buildings.memory.json", PatchRecipeStore.SerializeMemory(new PatchMemory()));
        var errors = new List<string>();

        Assert.Single(PatchRecipeStore.LoadAll(PatchRecipeStore.Folder(mod.ModRoot), errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void Memory_is_empty_when_file_missing_and_round_trips()
    {
        using var mod = new TempMod();
        var empty = PatchRecipeStore.LoadMemory(PatchRecipeStore.Folder(mod.ModRoot), "special_buildings");
        Assert.Empty(empty.Units);
        Assert.Empty(empty.ListEntries);

        var memory = new PatchMemory();
        memory.Units["defs|angkor_wat_01"] = "angkor_wat_01 = { }";
        memory.ListEntries["holdings"] = new List<ListEntryRecord> { new("common/holdings/00_holdings.txt", "castle_holding/buildings", "angkor_wat_01") };
        mod.WriteMod(".pmt-patches/special_buildings.memory.json", PatchRecipeStore.SerializeMemory(memory));

        var loaded = PatchRecipeStore.LoadMemory(PatchRecipeStore.Folder(mod.ModRoot), "special_buildings");
        Assert.Equal("angkor_wat_01 = { }", loaded.Units["defs|angkor_wat_01"]);
        Assert.Equal("castle_holding/buildings", loaded.ListEntries["holdings"][0].ListPath);
    }

    [Fact]
    public void HasRecipes_is_true_only_with_a_recipe_file()
    {
        using var mod = new TempMod();
        Assert.False(PatchRecipeStore.HasRecipes(PatchRecipeStore.Folder(mod.ModRoot)));

        mod.WriteMod(".pmt-patches/x.memory.json", "{}");
        Assert.False(PatchRecipeStore.HasRecipes(PatchRecipeStore.Folder(mod.ModRoot)));

        PatchRecipeStore.Save(PatchRecipeStore.Folder(mod.ModRoot), SampleRecipe());
        Assert.True(PatchRecipeStore.HasRecipes(PatchRecipeStore.Folder(mod.ModRoot)));
    }

    [Fact]
    public void Recipes_move_out_of_the_mod_folder()
    {
        using var mod = new TempMod();
        mod.WriteMod(".pmt-patches/a.json", "{\"id\":\"a\"}");
        mod.WriteMod(".pmt-patches/a.memory.json", "{}");
        var target = Path.Combine(mod.VanillaRoot, "..", "recipes");

        var result = PatchRecipeStore.MoveFromModFolder(mod.ModRoot, target);

        Assert.Equal(2, result.Moved);
        Assert.Empty(result.Kept);
        Assert.True(File.Exists(Path.Combine(target, "a.json")));
        Assert.True(File.Exists(Path.Combine(target, "a.memory.json")));
        Assert.False(Directory.Exists(PatchRecipeStore.Folder(mod.ModRoot)));
    }

    [Fact]
    public void Moving_never_overwrites_a_recipe_at_the_target()
    {
        using var mod = new TempMod();
        mod.WriteMod(".pmt-patches/a.json", "{\"id\":\"old\"}");
        mod.WriteMod(".pmt-patches/b.json", "{\"id\":\"b\"}");
        var target = Path.Combine(mod.VanillaRoot, "..", "recipes");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "a.json"), "{\"id\":\"new\"}");

        var result = PatchRecipeStore.MoveFromModFolder(mod.ModRoot, target);

        Assert.Equal(1, result.Moved);
        Assert.Equal(new[] { "a.json" }, result.Kept.ToArray());
        Assert.Equal("{\"id\":\"new\"}", File.ReadAllText(Path.Combine(target, "a.json")));
        Assert.True(mod.ModExists(".pmt-patches/a.json"));
        Assert.False(mod.ModExists(".pmt-patches/b.json"));
    }

    [Fact]
    public void Moving_to_the_mod_folder_itself_does_nothing()
    {
        using var mod = new TempMod();
        mod.WriteMod(".pmt-patches/a.json", "{\"id\":\"a\"}");

        var result = PatchRecipeStore.MoveFromModFolder(mod.ModRoot, PatchRecipeStore.Folder(mod.ModRoot));

        Assert.Equal(0, result.Moved);
        Assert.True(mod.ModExists(".pmt-patches/a.json"));
    }

    [Fact]
    public void Memory_lives_in_the_context_recipes_folder()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("history/provinces/k_x.txt", "1 = {\n\tspecial_building_slot = a_01\n}\n");
        var folder = Path.Combine(mod.VanillaRoot, "..", "recipes");
        var context = new PatchContext(mod.ModRoot, mod.VanillaRoot, null, null, folder);
        var recipe = new PatchRecipe
        {
            Id = "r",
            Rows = { new PatchRow { Key = "a_01" } },
            Rules = { new LineRule { Id = "l", Files = "history/provinces/*.txt", Match = "special_building_slot = {key}", CreateCopy = true } },
        };

        PatchPlanWriter.CreateAction(context, PatchRunner.Plan(context, recipe)).Do();

        Assert.True(File.Exists(PatchRecipeStore.MemoryPath(folder, "r")));
        Assert.False(Directory.Exists(PatchRecipeStore.Folder(mod.ModRoot)));
    }

    [Fact]
    public void Broken_memory_warns_and_is_kept_as_broken_copy()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("history/provinces/k_x.txt", "1 = {\n\tspecial_building_slot = a_01\n}\n");
        var folder = Path.Combine(mod.VanillaRoot, "..", "recipes");
        Directory.CreateDirectory(folder);
        File.WriteAllText(PatchRecipeStore.MemoryPath(folder, "r"), "{ kaputt");
        var context = new PatchContext(mod.ModRoot, mod.VanillaRoot, null, null, folder);
        var recipe = new PatchRecipe
        {
            Id = "r",
            Rows = { new PatchRow { Key = "a_01" } },
            Rules = { new LineRule { Id = "l", Files = "history/provinces/*.txt", Match = "special_building_slot = {key}", CreateCopy = true } },
        };

        var plan = PatchRunner.Plan(context, recipe);

        // Zurücknehmen mit U erzeugt einen neuen Plan; die Sicherung darf dabei nicht verloren gehen.
        plan = PatchPlanEdits.WithEditedFile(plan, plan.Files[0].RelativePath, plan.Files[0].NewText);
        var action = PatchPlanWriter.CreateAction(context, plan);
        action.Do();

        Assert.Contains(plan.Warnings, w => w.Contains("r.memory.json", StringComparison.Ordinal));
        Assert.Equal("{ kaputt", File.ReadAllText(PatchRecipeStore.MemoryPath(folder, "r") + ".broken"));
        action.Undo();
        Assert.False(File.Exists(PatchRecipeStore.MemoryPath(folder, "r") + ".broken"));
        Assert.Equal("{ kaputt", File.ReadAllText(PatchRecipeStore.MemoryPath(folder, "r")));
    }

    [Fact]
    public void Delete_removes_recipe_and_memory()
    {
        using var mod = new TempMod();
        PatchRecipeStore.Save(PatchRecipeStore.Folder(mod.ModRoot), SampleRecipe());
        mod.WriteMod(".pmt-patches/special_buildings.memory.json", "{}");

        PatchRecipeStore.Delete(PatchRecipeStore.Folder(mod.ModRoot), "special_buildings");

        Assert.False(mod.ModExists(".pmt-patches/special_buildings.json"));
        Assert.False(mod.ModExists(".pmt-patches/special_buildings.memory.json"));
    }
}
