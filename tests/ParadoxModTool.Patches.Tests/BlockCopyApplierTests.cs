using ParadoxModTool.VanillaDiff;
using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class BlockCopyApplierTests
{
    private const string Source = "common/buildings/00_special_buildings.txt";
    private const string Target = "common/buildings/pmt_special_buildings.txt";

    private static RuleRunContext Context(TempMod mod, PatchMemory? memory = null, params (string Key, string Barony)[] rows)
    {
        var recipe = new PatchRecipe { Id = "r", Columns = { new PatchColumn { Name = "barony" } } };
        foreach (var (key, barony) in rows)
        {
            recipe.Rows.Add(new PatchRow { Key = key, Values = { ["barony"] = barony } });
        }

        var workspace = new PatchWorkspace(mod.ModRoot, mod.VanillaRoot);
        return new RuleRunContext(recipe, workspace, ColumnResolver.Resolve(recipe, workspace), memory ?? new PatchMemory());
    }

    private static BlockCopyRule Rule(bool followNext = false) => new()
    {
        Id = "defs",
        SourceFiles = Source,
        Target = Target,
        FollowNext = followNext,
        Edits =
        {
            new BlockEdit { Kind = BlockEditKind.Remove, Key = "type", Value = "special" },
            new BlockEdit { Kind = BlockEditKind.InsertInto, Key = "can_construct_potential", Text = "barony = title:{barony}" },
        },
    };

    private static string Unit(RuleRunContext ctx, string key) =>
        GeneratedRegion.Read(ctx.Workspace.Get(Target)!, "r", "defs").Units.Single(u => u.UnitKey == key).Text;

    private const string ChainVanilla =
        "x_01 = {\n\ttype = special\n\tcan_construct_potential = {\n\t\ta = yes\n\t}\n\tnext_building = x_02\n}\n\nx_02 = {\n\ttype = special\n\tcost = 1\n}\n";

    private const string OwnFile = "common/buildings/01_own.txt";

    private const string OwnBlocks =
        "@c = 5\n\nkeep_me = {\n\tcost = 9\n}\n\nx_01 = {\n\ttype = special\n\tcost = @c\n\tcan_construct_potential = {\n\t\ta = yes\n\t}\n\tnext_building = x_02\n}\n";

    [Fact]
    public void Adopted_block_skips_insert_when_the_statement_is_already_there()
    {
        const string block = "a_01 = {\n\tcan_construct_potential = {\n\t\tOR = {\n\t\t\tbarony = title:b_x\n\t\t}\n\t}\n}";
        var edit = new BlockEdit { Kind = BlockEditKind.InsertInto, Key = "can_construct_potential", Text = "OR = {\n\tbarony   =  title:{barony}\n}" };
        var values = new Dictionary<string, string> { ["barony"] = "b_x" };

        Assert.Equal(block, BlockCopyApplier.ApplyEdit(block, edit, values, skipExisting: true));
        Assert.NotEqual(block, BlockCopyApplier.ApplyEdit(block, edit, values));
    }

    [Fact]
    public void Without_adoption_duplicate_is_skipped_and_marked()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, ChainVanilla);
        mod.WriteMod(OwnFile, OwnBlocks, bom: false);
        var ctx = Context(mod, null, ("x_01", "b_a"));

        BlockCopyApplier.Apply(Rule(followNext: true), ctx);

        Assert.True(Assert.Single(ctx.Skipped).AlreadyDefined);
    }

    [Fact]
    public void Adoption_converts_and_moves_the_block()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, ChainVanilla);
        mod.WriteMod(OwnFile, OwnBlocks, bom: false);
        var ctx = Context(mod, null, ("x_01", "b_a"));
        var rule = Rule(followNext: true);
        rule.AdoptExisting = true;

        BlockCopyApplier.Apply(rule, ctx);

        Assert.Empty(ctx.Skipped);
        var unit = Unit(ctx, "x_01");
        Assert.StartsWith("x_01 = {\n\tcost = 5\n", unit, StringComparison.Ordinal);
        Assert.DoesNotContain("type = special", unit, StringComparison.Ordinal);
        Assert.Contains("barony = title:b_a", unit, StringComparison.Ordinal);
        Assert.Contains("x_02 = {\n\tcost = 1\n", unit, StringComparison.Ordinal);
        var own = ctx.Workspace.Get(OwnFile)!;
        Assert.DoesNotContain("x_01", own, StringComparison.Ordinal);
        Assert.Contains("keep_me = {", own, StringComparison.Ordinal);
        Assert.Contains("@c = 5", own, StringComparison.Ordinal);
        Assert.Equal(new[] { "1 Blöcke in die Patch-Region übernommen" }, ctx.Workspace.NotesOf(OwnFile));
        Assert.DoesNotContain("cost = 5", ctx.NewMemory.Units["defs|x_01"], StringComparison.Ordinal);
    }

    private static BlockCopyRule AdoptRule() => new()
    {
        Id = "defs", SourceFiles = Source, Target = Target, FollowNext = true, AdoptExisting = true,
        Edits = { new BlockEdit { Kind = BlockEditKind.Remove, Key = "type", Value = "special" } },
    };

    private static PatchRecipe AdoptRecipe(BlockCopyRule rule, params string[] keys)
    {
        var recipe = new PatchRecipe { Id = "r", Rules = { rule } };
        recipe.Rows.AddRange(keys.Select(k => new PatchRow { Key = k }));
        return recipe;
    }

    private static PlannedFile? PlanFile(PatchPlan plan, string rel) => plan.Files.FirstOrDefault(f => f.RelativePath == rel);

    [Fact]
    public void Taking_back_an_adopted_unit_but_not_its_removal_is_reported_as_lost()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "x_01 = {\n\ttype = special\n\tcost = 1\n}\n");
        mod.WriteMod(OwnFile, "keep = {\n}\n\nx_01 = {\n\ttype = special\n\tcost = 7\n}\n", bom: false);
        var plan = PatchRunner.Plan(PatchRunnerTests.Context(mod), AdoptRecipe(AdoptRule(), "x_01"));

        Assert.Empty(PatchPlanEdits.LostAdoptions(plan));

        // U in der Vorschau: das Ziel bleibt wie vorher, die Quelldatei verliert den Block trotzdem.
        var edited = PatchPlanEdits.WithEditedFile(plan, Target, PlanFile(plan, Target)!.OriginalText ?? string.Empty);
        Assert.Equal(new[] { "x_01" }, PatchPlanEdits.LostAdoptions(edited).ToArray());

        // Wird auch das Entfernen zurückgenommen, ist nichts verloren.
        var both = PatchPlanEdits.WithEditedFile(edited, OwnFile, PlanFile(plan, OwnFile)!.OriginalText!);
        Assert.Empty(PatchPlanEdits.LostAdoptions(both));
    }

    [Fact]
    public void Conflict_in_target_keeps_adopted_block_in_its_file()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "x_01 = {\n\ttype = special\n\tcost = 1\n}\n\ny_01 = {\n\ttype = special\n\tcost = 1\n}\n");
        var rule = AdoptRule();
        PatchPlanWriter.CreateAction(PatchRunnerTests.Context(mod), PatchRunner.Plan(PatchRunnerTests.Context(mod), AdoptRecipe(rule, "y_01"))).Do();
        File.WriteAllText(mod.ModPath(Target), mod.ReadMod(Target).Replace("cost = 1", "cost = 2"), new System.Text.UTF8Encoding(true));
        mod.WriteVanilla(Source, "x_01 = {\n\ttype = special\n\tcost = 1\n}\n\ny_01 = {\n\ttype = special\n\tcost = 3\n}\n");
        mod.WriteMod(OwnFile, "keep = {\n}\n\nx_01 = {\n\ttype = special\n\tcost = 7\n}\n", bom: false);

        var plan = PatchRunner.Plan(PatchRunnerTests.Context(mod), AdoptRecipe(rule, "x_01", "y_01"));
        Assert.Equal(PlannedFileStatus.Conflict, PlanFile(plan, Target)!.Status);
        Assert.True(PlanFile(plan, OwnFile) is null or { Status: PlannedFileStatus.Unchanged });
        PatchPlanWriter.CreateAction(PatchRunnerTests.Context(mod), plan).Do();

        Assert.Contains("cost = 7", mod.ReadMod(OwnFile), StringComparison.Ordinal);
    }

    [Fact]
    public void Region_already_holding_the_key_refuses_adoption()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "x_01 = {\n\ttype = special\n\tcost = 1\n}\n");
        var rule = AdoptRule();
        PatchPlanWriter.CreateAction(PatchRunnerTests.Context(mod), PatchRunner.Plan(PatchRunnerTests.Context(mod), AdoptRecipe(rule, "x_01"))).Do();
        mod.WriteMod(OwnFile, "keep = {\n}\n\nx_01 = {\n\ttype = special\n\tcost = 7\n}\n", bom: false);

        var plan = PatchRunner.Plan(PatchRunnerTests.Context(mod), AdoptRecipe(rule, "x_01"));

        Assert.True(PlanFile(plan, OwnFile) is null or { Status: PlannedFileStatus.Unchanged });
        var skip = Assert.Single(plan.Skipped);
        Assert.Contains("01_own.txt", skip.Reason, StringComparison.Ordinal);
        Assert.False(skip.AlreadyDefined);
    }

    [Fact]
    public void Key_defined_in_two_files_refuses_adoption()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "x_01 = {\n\ttype = special\n\tcost = 1\n}\n");
        mod.WriteMod("common/buildings/01_a.txt", "x_01 = {\n\ttype = special\n\tcost = 7\n}\n", bom: false);
        mod.WriteMod("common/buildings/02_b.txt", "x_01 = {\n\ttype = special\n\tcost = 8\n}\n", bom: false);

        var plan = PatchRunner.Plan(PatchRunnerTests.Context(mod), AdoptRecipe(AdoptRule(), "x_01"));

        Assert.True(PlanFile(plan, "common/buildings/01_a.txt") is null or { Status: PlannedFileStatus.Unchanged });
        Assert.True(PlanFile(plan, "common/buildings/02_b.txt") is null or { Status: PlannedFileStatus.Unchanged });
        var skip = Assert.Single(plan.Skipped);
        Assert.Contains("01_a.txt", skip.Reason, StringComparison.Ordinal);
        Assert.Contains("02_b.txt", skip.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Key_defined_twice_in_one_file_refuses_adoption()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "x_01 = {\n\ttype = special\n\tcost = 1\n}\n");
        mod.WriteMod(OwnFile, "x_01 = {\n\ttype = special\n\tcost = 7\n}\n\nx_01 = {\n\ttype = special\n\tcost = 8\n}\n", bom: false);

        var plan = PatchRunner.Plan(PatchRunnerTests.Context(mod), AdoptRecipe(AdoptRule(), "x_01"));

        Assert.True(PlanFile(plan, OwnFile) is null or { Status: PlannedFileStatus.Unchanged });
        Assert.Single(plan.Skipped);
    }

    [Fact]
    public void Insert_is_kept_when_the_text_only_appears_inside_another_statement()
    {
        const string withNot = "a_01 = {\n\tcan_construct_potential = {\n\t\tNOT = { is_coastal = yes }\n\t}\n}";
        const string withLonger = "a_01 = {\n\tcan_construct_potential = {\n\t\tbarony = title:b_ab\n\t}\n}";
        var values = new Dictionary<string, string>();

        foreach (var skipExisting in new[] { false, true })
        {
            Assert.Contains("\t\tis_coastal = yes\n", BlockCopyApplier.ApplyEdit(withNot, new BlockEdit { Kind = BlockEditKind.InsertInto, Key = "can_construct_potential", Text = "is_coastal = yes" }, values, skipExisting), StringComparison.Ordinal);
            Assert.Contains("\t\tbarony = title:b_a\n", BlockCopyApplier.ApplyEdit(withLonger, new BlockEdit { Kind = BlockEditKind.InsertInto, Key = "can_construct_potential", Text = "barony = title:b_a" }, values, skipExisting), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Adopted_block_gets_only_the_statements_that_are_still_missing()
    {
        const string partial = "a_01 = {\n\ton_start = {\n\t\tfx_a = yes\n\t\tfx_b = yes\n\t}\n}";
        var edit = new BlockEdit { Kind = BlockEditKind.InsertInto, Key = "on_start", Text = "fx_a = yes\nfx_b = yes\nfx_c = yes" };
        var values = new Dictionary<string, string>();

        var result = BlockCopyApplier.ApplyEdit(partial, edit, values, skipExisting: true);

        Assert.Equal("a_01 = {\n\ton_start = {\n\t\tfx_a = yes\n\t\tfx_b = yes\n\t\tfx_c = yes\n\t}\n}", result);
        Assert.Equal(result, BlockCopyApplier.ApplyEdit(result, edit, values, skipExisting: true));
    }

    [Fact]
    public void Adopted_block_without_the_target_block_gets_it_with_all_statements()
    {
        var edit = new BlockEdit { Kind = BlockEditKind.InsertInto, Key = "on_cancelled", Text = "fx_a = yes\nfx_b = yes" };

        var result = BlockCopyApplier.ApplyEdit("a_01 = {\n\tcost = 1\n}", edit, new Dictionary<string, string>(), skipExisting: true);

        Assert.Equal("a_01 = {\n\tcost = 1\n\ton_cancelled = {\n\t\tfx_a = yes\n\t\tfx_b = yes\n\t}\n}", result);
    }

    [Fact]
    public void Adoption_follows_the_mods_own_next_building()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "x_01 = {\n\ttype = special\n\t#next_building = x_02\n}\n");
        mod.WriteMod(OwnFile, "x_01 = {\n\ttype = special\n\tnext_building = x_02\n}\n\nx_02 = {\n\ttype = special\n\tcost = 2\n}\n", bom: false);
        var ctx = Context(mod, null, ("x_01", "b_a"));
        var rule = Rule(followNext: true);
        rule.AdoptExisting = true;

        BlockCopyApplier.Apply(rule, ctx);

        var unit = Unit(ctx, "x_01");
        Assert.Contains("x_02 = {\n\tcost = 2\n", unit, StringComparison.Ordinal);
        Assert.DoesNotContain("type = special", unit, StringComparison.Ordinal);
        Assert.DoesNotContain("x_02", ctx.Workspace.Get(OwnFile)!, StringComparison.Ordinal);

        var recipe = new PatchRecipe { Id = "r", Columns = { new PatchColumn { Name = "barony" } }, Rows = { new PatchRow { Key = "x_01", Values = { ["barony"] = "b_a" } } }, Rules = { rule } };
        PatchPlanWriter.CreateAction(PatchRunnerTests.Context(mod), PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe)).Do();
        Assert.All(PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe).Files, f => Assert.Equal(PlannedFileStatus.Unchanged, f.Status));
    }

    [Fact]
    public void Adoption_then_second_run_changes_nothing()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, ChainVanilla);
        mod.WriteMod(OwnFile, OwnBlocks, bom: false);
        var rule = Rule(followNext: true);
        rule.AdoptExisting = true;
        var recipe = new PatchRecipe { Id = "r", Columns = { new PatchColumn { Name = "barony" } }, Rows = { new PatchRow { Key = "x_01", Values = { ["barony"] = "b_a" } } }, Rules = { rule } };

        var first = PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe);
        Assert.Contains(first.Files, f => f.RelativePath == OwnFile && f.Status == PlannedFileStatus.Changed);
        PatchPlanWriter.CreateAction(PatchRunnerTests.Context(mod), first).Do();
        var second = PatchRunner.Plan(PatchRunnerTests.Context(mod), recipe);

        Assert.All(second.Files, f => Assert.Equal(PlannedFileStatus.Unchanged, f.Status));
        Assert.Empty(second.Skipped);
        Assert.Contains("cost = 5", mod.ReadMod(Target), StringComparison.Ordinal);
    }

    [Fact]
    public void Copies_vanilla_block_removes_type_special_and_inserts_trigger()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "angkor_wat_01 = {\n\ttype = special\n\tconstruction_time = 10\n}\n");
        var ctx = Context(mod, null, ("angkor_wat_01", "b_x"));

        BlockCopyApplier.Apply(Rule(), ctx);

        Assert.Equal("angkor_wat_01 = {\n\tconstruction_time = 10\n\tcan_construct_potential = {\n\t\tbarony = title:b_x\n\t}\n}", Unit(ctx, "angkor_wat_01"));
        Assert.Equal("angkor_wat_01 = {\n\tconstruction_time = 10\n\tcan_construct_potential = {\n\t\tbarony = title:b_x\n\t}\n}", ctx.NewMemory.Units["defs|angkor_wat_01"]);
        Assert.Equal(new[] { "1 Bausteine erzeugt" }, ctx.Workspace.NotesOf(Target));
        Assert.True(ctx.Workspace.HasBomOf(Target));
    }

    [Fact]
    public void Insert_into_existing_sub_block_adds_before_its_closing_brace()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "a_01 = {\n\ttype = special\n\tcan_construct_potential = {\n\t\tculture = x\n\t}\n}\n");
        var ctx = Context(mod, null, ("a_01", "b_x"));

        BlockCopyApplier.Apply(Rule(), ctx);

        Assert.Equal("a_01 = {\n\tcan_construct_potential = {\n\t\tculture = x\n\t\tbarony = title:b_x\n\t}\n}", Unit(ctx, "a_01"));
    }

    [Fact]
    public void Empty_rendered_insert_leaves_no_trace()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "a_01 = {\n\ttype = special\n\tx = 1\n}\nb_01 = {\n\ttype = special\n\tcan_construct_potential = {\n\t\tculture = y\n\t}\n}\n");
        var ctx = Context(mod, null, ("a_01", ""), ("b_01", ""));
        var rule = Rule();
        rule.Edits[1].Text = "{?barony}barony = title:{barony}{/barony}";

        BlockCopyApplier.Apply(rule, ctx);

        Assert.Equal("a_01 = {\n\tx = 1\n}", Unit(ctx, "a_01"));
        Assert.Equal("b_01 = {\n\tcan_construct_potential = {\n\t\tculture = y\n\t}\n}", Unit(ctx, "b_01"));
    }

    [Fact]
    public void Insert_after_and_at_start_and_end()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "a_01 = {\n\tx = 1\n\ty = 2\n}\n");
        var ctx = Context(mod, null, ("a_01", ""));
        var rule = new BlockCopyRule
        {
            Id = "defs",
            SourceFiles = Source,
            Target = Target,
            Edits =
            {
                new BlockEdit { Kind = BlockEditKind.InsertAfter, Key = "x", Text = "after_x = yes" },
                new BlockEdit { Kind = BlockEditKind.InsertAtStart, Text = "first = yes" },
                new BlockEdit { Kind = BlockEditKind.InsertAtEnd, Text = "last = {\n\tz = 1\n}" },
            },
        };

        BlockCopyApplier.Apply(rule, ctx);

        Assert.Equal("a_01 = {\n\tfirst = yes\n\tx = 1\n\tafter_x = yes\n\ty = 2\n\tlast = {\n\t\tz = 1\n\t}\n}", Unit(ctx, "a_01"));
    }

    [Fact]
    public void Follow_next_copies_the_upgrade_chain_into_one_unit()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "a_01 = {\n\ttype = special\n\tnext_building = a_02\n}\na_02 = {\n\ttype = special\n}\nb_01 = { }\n");
        var ctx = Context(mod, null, ("a_01", "b_x"));

        BlockCopyApplier.Apply(Rule(followNext: true), ctx);

        var unit = Unit(ctx, "a_01");
        Assert.StartsWith("a_01 = {", unit);
        Assert.Contains("\n\na_02 = {", unit);
        Assert.DoesNotContain("b_01", unit);
        Assert.DoesNotContain("type = special", unit);
        ScriptDocument.Parse(unit);
    }

    [Fact]
    public void File_level_variable_is_inlined()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "@illustration_islamic = \"gfx/a.dds\"\nx_01 = {\n\tillustration = @illustration_islamic\n}\n");
        var ctx = Context(mod, null, ("x_01", ""));

        BlockCopyApplier.Apply(new BlockCopyRule { Id = "defs", SourceFiles = Source, Target = Target }, ctx);

        Assert.Equal("x_01 = {\n\tillustration = \"gfx/a.dds\"\n}", Unit(ctx, "x_01"));
    }

    [Fact]
    public void Undefined_variable_is_warned_and_kept()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "x_01 = {\n\tvalue = @missing\n}\n");
        var ctx = Context(mod, null, ("x_01", ""));

        BlockCopyApplier.Apply(new BlockCopyRule { Id = "defs", SourceFiles = Source, Target = Target }, ctx);

        Assert.Contains("@missing", Unit(ctx, "x_01"));
        Assert.Contains("@missing ist in common/buildings/00_special_buildings.txt nicht definiert", ctx.Warnings);
    }

    [Fact]
    public void Missing_vanilla_block_skips_the_row_with_reason()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "a_01 = { }\n");
        var ctx = Context(mod, null, ("nope_01", ""));

        BlockCopyApplier.Apply(Rule(), ctx);

        Assert.Contains(new SkippedRow("defs", "nope_01", "Vanilla-Block „nope_01“ nicht gefunden"), ctx.Skipped);
        Assert.Null(ctx.Workspace.Get(Target));
    }

    [Fact]
    public void Key_defined_elsewhere_in_the_mod_is_skipped_as_already_defined()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "angkor_wat_01 = { type = special }\n");
        mod.WriteMod("common/buildings/01_Flogis.txt", "# a\n\nangkor_wat_01 = { type = special }\n");
        var ctx = Context(mod, null, ("angkor_wat_01", "b_x"));

        BlockCopyApplier.Apply(Rule(), ctx);

        Assert.Contains(new SkippedRow("defs", "angkor_wat_01", "bereits definiert in common/buildings/01_Flogis.txt:3", AlreadyDefined: true), ctx.Skipped);
        Assert.Null(ctx.Workspace.Get(Target));
    }

    [Fact]
    public void Second_run_changes_nothing()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "a_01 = {\n\ttype = special\n\tx = 1\n}\n");
        var first = Context(mod, null, ("a_01", "b_x"));
        BlockCopyApplier.Apply(Rule(), first);
        mod.WriteMod(Target, first.Workspace.Get(Target)!);

        var second = Context(mod, first.NewMemory, ("a_01", "b_x"));
        BlockCopyApplier.Apply(Rule(), second);

        Assert.DoesNotContain(Target, second.Workspace.Touched);
        Assert.Empty(second.Skipped);
    }

    [Fact]
    public void Hand_edit_in_region_survives_a_vanilla_change()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "a_01 = {\n\ttype = special\n\tconstruction_time = 10\n\tcost_gold = 100\n}\n");
        var first = Context(mod, null, ("a_01", "b_x"));
        BlockCopyApplier.Apply(Rule(), first);
        mod.WriteMod(Target, first.Workspace.Get(Target)!.Replace("construction_time = 10", "construction_time = 99"));
        mod.WriteVanilla(Source, "a_01 = {\n\ttype = special\n\tconstruction_time = 10\n\tcost_gold = 500\n}\n");

        var second = Context(mod, first.NewMemory, ("a_01", "b_x"));
        BlockCopyApplier.Apply(Rule(), second);

        var unit = Unit(second, "a_01");
        Assert.Contains("construction_time = 99", unit);
        Assert.Contains("cost_gold = 500", unit);
        Assert.Contains("1 Bausteine mit Handänderungen zusammengeführt", second.Workspace.NotesOf(Target));
    }

    [Fact]
    public void Skipped_row_keeps_its_unit_when_vanilla_renames_the_block()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "a_01 = {\n\ttype = special\n\tx = 1\n}\n");
        var first = Context(mod, null, ("a_01", "b_x"));
        BlockCopyApplier.Apply(Rule(), first);
        mod.WriteMod(Target, first.Workspace.Get(Target)!);
        mod.WriteVanilla(Source, "a_01_renamed = {\n\ttype = special\n}\n");

        var second = Context(mod, first.NewMemory, ("a_01", "b_x"));
        BlockCopyApplier.Apply(Rule(), second);

        Assert.Contains(second.Skipped, s => s.RowKey == "a_01");
        Assert.DoesNotContain(Target, second.Workspace.Touched);
        Assert.Equal(first.NewMemory.Units["defs|a_01"], second.NewMemory.Units["defs|a_01"]);
    }

    [Fact]
    public void Skipped_row_keeps_its_unit_when_defined_elsewhere_later()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "a_01 = {\n\ttype = special\n\tx = 1\n}\n");
        var first = Context(mod, null, ("a_01", "b_x"));
        BlockCopyApplier.Apply(Rule(), first);
        mod.WriteMod(Target, first.Workspace.Get(Target)!);
        mod.WriteMod("common/buildings/zz_other.txt", "a_01 = { y = 2 }\n");

        var second = Context(mod, first.NewMemory, ("a_01", "b_x"));
        BlockCopyApplier.Apply(Rule(), second);

        Assert.Contains(second.Skipped, s => s.RowKey == "a_01" && s.Reason.StartsWith("bereits definiert", StringComparison.Ordinal));
        Assert.DoesNotContain(Target, second.Workspace.Touched);
    }

    [Fact]
    public void Hand_deletion_survives_a_skipped_run()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "a_01 = {\n\ttype = special\n\tx = 1\n}\n");
        var first = Context(mod, null, ("a_01", "b_x"));
        BlockCopyApplier.Apply(Rule(), first);
        mod.WriteMod(Target, "# PMT-Patch r/defs begin\n# PMT-Patch r/defs end\n");
        mod.WriteVanilla(Source, "other = { }\n");
        var second = Context(mod, first.NewMemory, ("a_01", "b_x"));
        BlockCopyApplier.Apply(Rule(), second);
        mod.WriteVanilla(Source, "a_01 = {\n\ttype = special\n\tx = 1\n}\n");

        var third = Context(mod, second.NewMemory, ("a_01", "b_x"));
        BlockCopyApplier.Apply(Rule(), third);

        Assert.DoesNotContain(Target, third.Workspace.Touched);
    }

    [Fact]
    public void Removed_units_are_reported()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "a_01 = { type = special }\nb_01 = { type = special }\n");
        var first = Context(mod, null, ("a_01", "b_x"), ("b_01", "b_x"));
        BlockCopyApplier.Apply(Rule(), first);
        mod.WriteMod(Target, first.Workspace.Get(Target)!);

        var second = Context(mod, first.NewMemory, ("a_01", "b_x"));
        BlockCopyApplier.Apply(Rule(), second);

        Assert.Contains("−1 Bausteine entfernt", second.Workspace.NotesOf(Target));
    }

    [Fact]
    public void Line_replace_rules_also_apply_to_fresh_units_of_the_same_recipe()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "a_01 = {\n\ttype = special\n\tis_enabled = { any_special_building_province = { } }\n}\n");
        var ctx = Context(mod, null, ("a_01", "b_x"));
        ctx.Recipe.Rules.Add(new LineRule { Id = "it", Scope = RuleScope.Global, Files = "common/**/*.txt", Match = "any_special_building_province", Action = LineAction.Replace, Replacement = "any_province" });

        BlockCopyApplier.Apply(Rule(), ctx);

        Assert.Contains("any_province = { }", Unit(ctx, "a_01"));
        Assert.DoesNotContain("special_building_province", Unit(ctx, "a_01"));
    }

    [Fact]
    public void Broken_target_file_is_skipped_with_parse_message()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "a_01 = { type = special }\n");
        mod.WriteMod(Target, "x = {\n");
        var ctx = Context(mod, null, ("a_01", "b_x"));

        BlockCopyApplier.Apply(Rule(), ctx);

        Assert.Equal("x = {\n", ctx.Workspace.Get(Target));
        Assert.Contains(ctx.Warnings, w => w.StartsWith(Target + " übersprungen", StringComparison.Ordinal));
    }

    [Fact]
    public void Broken_region_is_skipped_and_memory_kept()
    {
        using var mod = new TempMod();
        mod.WriteVanilla(Source, "a_01 = { type = special }\n");
        mod.WriteMod(Target, "# PMT-Patch r/defs begin\n# PMT-Unit a_01\na_01 = { }\n");
        var memory = new PatchMemory();
        memory.Units["defs|a_01"] = "a_01 = { }";
        var ctx = Context(mod, memory, ("a_01", "b_x"));

        BlockCopyApplier.Apply(Rule(), ctx);

        Assert.DoesNotContain(Target, ctx.Workspace.Touched);
        Assert.Contains(ctx.Warnings, w => w.Contains("beschädigt", StringComparison.Ordinal));
        Assert.Equal("a_01 = { }", ctx.NewMemory.Units["defs|a_01"]);
    }
}
