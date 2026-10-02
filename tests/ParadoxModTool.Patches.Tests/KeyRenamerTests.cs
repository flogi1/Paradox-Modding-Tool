using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class KeyRenamerTests
{
    private static readonly IReadOnlySet<string> NoKeys = new HashSet<string>();

    [Fact]
    public void Renames_whole_tokens_and_derived_forms()
    {
        var map = new Dictionary<string, string> { ["castle_1"] = "castle_01" };
        var text = "castle_1 = { }\nbuilding_castle_1:0 \"x\"\ncastle_1_desc\nflogi_is_special_castle_1\nhas_building = \"castle_1\"\n";
        var result = KeyRenamer.RenameTokens(text, map, NoKeys, out var count);
        Assert.Equal("castle_01 = { }\nbuilding_castle_01:0 \"x\"\ncastle_01_desc\nflogi_is_special_castle_01\nhas_building = \"castle_01\"\n", result);
        Assert.Equal(5, count);
    }

    [Theory]
    [InlineData("castle_1_0")]
    [InlineData("big_castle_10")]
    [InlineData("castle_1x")]
    [InlineData("castle_10_desc")]
    public void Leaves_longer_keys_alone(string token)
    {
        var result = KeyRenamer.RenameTokens(token, new Dictionary<string, string> { ["castle_1"] = "castle_01" }, NoKeys, out var count);
        Assert.Equal(token, result);
        Assert.Equal(0, count);
    }

    [Fact]
    public void Prefix_and_suffix_together_are_renamed()
    {
        var result = KeyRenamer.RenameTokens("building_castle_1_desc", new Dictionary<string, string> { ["castle_1"] = "castle_01" }, NoKeys, out var count);
        Assert.Equal("building_castle_01_desc", result);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Defined_longer_key_is_not_a_derived_form()
    {
        var defined = new HashSet<string> { "gold_mine_1", "mine_1" };
        var result = KeyRenamer.RenameTokens("gold_mine_1 building_mine_1", new Dictionary<string, string> { ["mine_1"] = "mine_01" }, defined, out var count);
        Assert.Equal("gold_mine_1 building_mine_01", result);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Longest_old_key_wins()
    {
        var map = new Dictionary<string, string> { ["a_1"] = "x", ["b_a_1"] = "y" };
        Assert.Equal("y z_x", KeyRenamer.RenameTokens("b_a_1 z_a_1", map, NoKeys, out _));
    }

    private const string Buildings = "common/buildings/01_mod.txt";

    private static readonly KeyRename[] Renames = { new("m_1_0", "holy_01") };

    private static readonly PatchRecipe Recipe = new() { Id = "r", Name = "R", GameId = "ck3" };

    private static TempMod PlanFixture()
    {
        var mod = new TempMod();
        mod.WriteVanilla("common/buildings/00_special_buildings.txt", "holy_01 = {\n\ttype = special\n}\n");
        mod.WriteVanilla("localization/english/b_l_english.yml", "l_english:\n building_type_holy_01:0 \"Holy\"\n");
        mod.WriteMod("common/buildings/00_special_buildings.txt", "# leer\n");
        mod.WriteMod(Buildings, "m_1_0 = {\n\tcost_gold = 1\n}\nm_2 = {\n\tnext = m_1_0\n\tflag = building_m_1_0\n}\n");
        mod.WriteMod("common/holdings/00_holdings.txt", "castle_holding = {\n\tbuildings = {\n\t\tm_1_0\n\t}\n}\n");
        mod.WriteMod("gui/x.gui", "text = \"[Has('m_1_0')]\"\n");
        mod.WriteMod("history/provinces/k.txt", "1 = {\n\tbuildings = { m_1_0 }\n}\n");
        mod.WriteMod("localization/english/m_l_english.yml", "l_english:\n building_type_m_1_0:0 \"$building_type_holy_01$\"\n");
        return mod;
    }

    private static PlannedFile File(PatchPlan plan, string rel) => plan.Files.Single(f => f.RelativePath == rel);

    [Fact]
    public void Plan_renames_in_scripts_gui_and_localization()
    {
        using var mod = PlanFixture();

        var plan = KeyRenamer.Plan(PatchRunnerTests.Context(mod), Recipe, Renames, "common/buildings", wasComments: false);

        Assert.Equal("holy_01 = {\n\tcost_gold = 1\n}\nm_2 = {\n\tnext = holy_01\n\tflag = building_holy_01\n}\n", File(plan, Buildings).NewText);
        Assert.Contains("3 Stellen umbenannt", File(plan, Buildings).Notes);
        Assert.Equal(PlannedFileStatus.Changed, File(plan, Buildings).Status);
        Assert.Equal("castle_holding = {\n\tbuildings = {\n\t\tholy_01\n\t}\n}\n", File(plan, "common/holdings/00_holdings.txt").NewText);
        Assert.Equal("text = \"[Has('holy_01')]\"\n", File(plan, "gui/x.gui").NewText);
        Assert.Equal("1 = {\n\tbuildings = { holy_01 }\n}\n", File(plan, "history/provinces/k.txt").NewText);
        Assert.Equal("l_english:\n", File(plan, "localization/english/m_l_english.yml").NewText);
        Assert.Contains("1 Selbstverweise entfernt", File(plan, "localization/english/m_l_english.yml").Notes);
        Assert.Equal("Umbenennung (R)", plan.RecipeName);
        Assert.Equal("r", plan.RecipeId);
        Assert.Empty(plan.Skipped);
        Assert.Empty(plan.ConflictUnitIds);
        Assert.DoesNotContain(plan.Files, f => f.RelativePath == "common/buildings/00_special_buildings.txt" && f.Status != PlannedFileStatus.Unchanged);
    }

    [Fact]
    public void Was_comment_goes_before_renamed_definition_only()
    {
        using var mod = PlanFixture();
        mod.WriteMod(Buildings, "m_1_0 = {\n\tcost_gold = 1\n}\n\tm_x = { }\n");

        var with = KeyRenamer.Plan(PatchRunnerTests.Context(mod), Recipe, Renames, "common/buildings", wasComments: true);
        var without = KeyRenamer.Plan(PatchRunnerTests.Context(mod), Recipe, Renames, "common/buildings", wasComments: false);

        // Der Kommentar steht im Block, damit er beim Übernehmen in den erzeugten Bereich mitwandert.
        Assert.Equal("holy_01 = {\n\t# was: m_1_0\n\tcost_gold = 1\n}\n\tm_x = { }\n", File(with, Buildings).NewText);
        Assert.DoesNotContain("# was:", File(with, "common/holdings/00_holdings.txt").NewText, StringComparison.Ordinal);
        Assert.DoesNotContain("# was:", File(with, "history/provinces/k.txt").NewText, StringComparison.Ordinal);
        Assert.Equal("holy_01 = {\n\tcost_gold = 1\n}\n\tm_x = { }\n", File(without, Buildings).NewText);
    }

    [Fact]
    public void Was_comment_keeps_crlf_and_indent()
    {
        using var mod = PlanFixture();
        mod.WriteMod(Buildings, "# Kopf\r\n  m_1_0 = {\r\n  }\r\nm_x = { a = 1 }\r\n");
        var renames = new[] { new KeyRename("m_1_0", "holy_01"), new KeyRename("m_x", "x_01") };

        var plan = KeyRenamer.Plan(PatchRunnerTests.Context(mod), Recipe, renames, "common/buildings", wasComments: true);

        Assert.Equal("# Kopf\r\n  holy_01 = {\r\n  \t# was: m_1_0\r\n  }\r\nx_01 = {\r\n\t# was: m_x\r\n a = 1 }\r\n", File(plan, Buildings).NewText);
    }

    [Fact]
    public void Windows1252_file_keeps_encoding_and_bom_state()
    {
        using var mod = PlanFixture();
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var cp1252 = System.Text.Encoding.GetEncoding(1252);
        System.IO.File.WriteAllBytes(mod.ModPath(Buildings), cp1252.GetBytes("# Södermanland\r\nm_1_0 = {\r\n}\r\n"));

        var plan = KeyRenamer.Plan(PatchRunnerTests.Context(mod), Recipe, Renames, "common/buildings", wasComments: false);
        PatchPlanWriter.CreateAction(PatchRunnerTests.Context(mod), plan).Do();

        Assert.Equal("windows-1252", File(plan, Buildings).EncodingName);
        Assert.False(File(plan, Buildings).HasBom);
        Assert.Equal(cp1252.GetBytes("# Södermanland\r\nholy_01 = {\r\n}\r\n"), System.IO.File.ReadAllBytes(mod.ModPath(Buildings)));
    }

    [Fact]
    public void Pmt_and_git_folders_are_skipped()
    {
        using var mod = PlanFixture();
        mod.WriteMod(".pmt-versions/a.txt", "m_1_0 = { }\n");
        mod.WriteMod(".git/b.txt", "m_1_0\n");

        var plan = KeyRenamer.Plan(PatchRunnerTests.Context(mod), Recipe, Renames, "common/buildings", wasComments: false);

        Assert.DoesNotContain(plan.Files, f => f.RelativePath.StartsWith('.'));
    }

    [Fact]
    public void Plan_keeps_memory_and_applies_once()
    {
        using var mod = PlanFixture();
        var context = PatchRunnerTests.Context(mod);

        var plan = KeyRenamer.Plan(context, Recipe, Renames, "common/buildings", wasComments: true);
        PatchPlanWriter.CreateAction(context, plan).Do();
        var again = KeyRenamer.Plan(context, Recipe, Renames, "common/buildings", wasComments: true);

        Assert.Same(plan.OldMemory, plan.NewMemory);
        Assert.All(again.Files, f => Assert.Equal(PlannedFileStatus.Unchanged, f.Status));
        Assert.StartsWith("holy_01 = {\n\t# was: m_1_0\n", mod.ReadMod(Buildings), StringComparison.Ordinal);
    }
}
