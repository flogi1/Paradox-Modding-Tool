using System.Text;
using ParadoxModTool.Patches;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Der Planer mit dem Test-Paket "iv2-slice": alle sechs Ausgabe-Arten, beide Ziele, zweiter Lauf, Kodierung, leere Gruppe.</summary>
public sealed class GeneratorPlannerTests : IDisposable
{
    private readonly SlicePackage _slice = new();

    public void Dispose() => _slice.Dispose();

    private static GeneratorPlannedFile File(GeneratorPlan plan, string relative) => plan.Files.Single(f => f.RelativePath == relative);

    [Fact]
    public void First_run_creates_all_files_and_patches_base_files()
    {
        var plan = _slice.Plan(_slice.State("BYZ", 2, 1));

        Assert.Equal(_slice.ModRoot, plan.WriteRoot);
        Assert.Equal(PlannedFileStatus.New, File(plan, "in_game/common/scripted_guis/02_IV2_sgui_BYZ.txt").Status);
        Assert.Equal(PlannedFileStatus.New, File(plan, "main_menu/localization/english/04_IV2_BYZ_l_english.yml").Status);
        Assert.Equal(PlannedFileStatus.New, File(plan, "main_menu/localization/german/04_IV2_BYZ_l_german.yml").Status);
        Assert.Equal(PlannedFileStatus.New, File(plan, "main_menu/common/static_modifiers/02_IV2_BYZ.txt").Status);
        Assert.Equal(PlannedFileStatus.Changed, File(plan, "in_game/gui/setup.gui").Status);
        Assert.Equal(PlannedFileStatus.Changed, File(plan, "in_game/common/scripted_triggers/setup.txt").Status);
        var coa = File(plan, "main_menu/gfx/interface/coa/BYZ.dds");
        Assert.Equal(PlannedFileStatus.New, coa.Status);
        Assert.Equal(new byte[] { 0x44, 0x44, 0x53, 1 }, coa.Bytes);

        var sgui = File(plan, "in_game/common/scripted_guis/02_IV2_sgui_BYZ.txt");
        Assert.Contains("iv2_sgui_historic_BYZ_1 = {\r\n\tscope = country", sgui.NewText);
        Assert.Contains("iv2_sgui_historic_BYZ_31 = {", sgui.NewText);
        Assert.True(sgui.HasBom);

        var loc = File(plan, "main_menu/localization/english/04_IV2_BYZ_l_english.yml");
        Assert.StartsWith("l_english:\n", loc.NewText);
        Assert.Contains(" iv2_ga_select_historic_BYZ_1_desc: \"Line one\\nline \\\"two\\\"\"\n", loc.NewText);

        var setup = File(plan, "in_game/gui/setup.gui").NewText!;
        Assert.Contains("\t# === IV2Generator:BYZ:START ===\n\tiv2_historic_idea_select_BYZ_1 = {}\n\tiv2_historic_idea_select_BYZ_2 = {}\n\t# === IV2Generator:BYZ:END ===", setup);
        Assert.Contains("\tiv2_historic_idea_select_FRA_1 = {}", setup);
        Assert.Contains("iv2_historic_shell_navy = {\n\t# PMT-Generator iv2-slice/select/BYZ/navy begin\n\tiv2_historic_idea_select_BYZ_31 = {}\n\t# PMT-Generator iv2-slice/select/BYZ/navy end\n}", setup);

        Assert.Contains("\t\thas_or_had_tag = BYZ\n", File(plan, "in_game/common/scripted_triggers/setup.txt").NewText);
        Assert.Contains("national_idea_modifier_BYZ_31 = {", File(plan, "main_menu/common/static_modifiers/02_IV2_BYZ.txt").NewText);
    }

    [Fact]
    public void Markers_use_marker_id_not_package_id()
    {
        var copy = _slice.Package with { Id = "my-copy", MarkerId = "iv2-slice" };
        SlicePackage.Write(_slice.Plan(_slice.State("BYZ", 2, 1)));

        var plan = GeneratorPlanner.Plan(copy, _slice.State("BYZ", 2, 1), new GeneratorTarget(_slice.ModRoot, null, SlicePackage.Languages));

        Assert.All(plan.Files, f => Assert.Equal(PlannedFileStatus.Unchanged, f.Status));
    }

    [Fact]
    public void Marker_id_defaults_to_package_id()
    {
        Assert.Equal("iv2-slice", _slice.Package.MarkerId);
    }

    [Fact]
    public void Second_run_is_unchanged()
    {
        var state = _slice.State("BYZ", 2, 1);
        SlicePackage.Write(_slice.Plan(state));

        var again = _slice.Plan(state);

        Assert.All(again.Files, f => Assert.Equal(PlannedFileStatus.Unchanged, f.Status));
    }

    [Fact]
    public void Output_target_copies_base_files_and_accumulates_two_tags()
    {
        var output = _slice.Temp.PathOf("out");
        var byz = _slice.Plan(_slice.State("BYZ", 1, 0), output);
        Assert.Equal(output, byz.WriteRoot);
        Assert.Equal(PlannedFileStatus.New, File(byz, "in_game/gui/setup.gui").Status);
        SlicePackage.Write(byz);

        var fra = _slice.Plan(_slice.State("FRA", 0, 1), output);
        SlicePackage.Write(fra);

        var setup = System.IO.File.ReadAllText(Path.Combine(output, "in_game", "gui", "setup.gui"));
        Assert.Contains("iv2_historic_idea_select_BYZ_1 = {}", setup);
        Assert.Contains("iv2_historic_idea_select_FRA_31 = {}", setup);
        var triggers = System.IO.File.ReadAllText(Path.Combine(output, "in_game", "common", "scripted_triggers", "setup.txt"));
        Assert.Contains("has_or_had_tag = BYZ", triggers);
        Assert.Contains("has_or_had_tag = FRA", triggers);
        Assert.Equal(SlicePackage.SetupGui, _slice.ReadMod("in_game/gui/setup.gui"));
        Assert.Equal(SlicePackage.Triggers, _slice.ReadMod("in_game/common/scripted_triggers/setup.txt"));
    }

    [Fact]
    public void Output_target_keeps_the_mod_file_as_base_for_the_preview()
    {
        var output = _slice.Temp.PathOf("out");

        var plan = _slice.Plan(_slice.State("BYZ", 1, 0), output);

        var setup = File(plan, "in_game/gui/setup.gui");
        Assert.Equal(PlannedFileStatus.New, setup.Status);
        Assert.Null(setup.OriginalText);
        Assert.Equal(SlicePackage.SetupGui, setup.ModBaseText);
        Assert.All(plan.Files.Where(f => f.Bytes is not null), f => Assert.Null(f.ModBaseText));
    }

    [Fact]
    public void Output_target_with_a_copy_there_has_no_mod_base()
    {
        var output = _slice.Temp.PathOf("out");
        SlicePackage.Write(_slice.Plan(_slice.State("BYZ", 1, 0), output));

        var again = _slice.Plan(_slice.State("BYZ", 1, 0), output);

        Assert.All(again.Files, f => Assert.Null(f.ModBaseText));
    }

    [Fact]
    public void Mod_target_has_no_mod_base()
    {
        var plan = _slice.Plan(_slice.State("BYZ", 1, 0));

        Assert.All(plan.Files, f => Assert.Null(f.ModBaseText));
    }

    [Fact]
    public void Base_file_encoding_and_line_endings_are_kept()
    {
        var latin = Encoding.Latin1;
        System.IO.File.WriteAllBytes(_slice.Temp.PathOf("mod/in_game/common/scripted_triggers/setup.txt"), latin.GetBytes("# Café\n" + SlicePackage.Triggers));

        var file = File(_slice.Plan(_slice.State("BYZ", 1, 0)), "in_game/common/scripted_triggers/setup.txt");

        Assert.Equal(TextFile.Windows1252, file.EncodingName);
        Assert.False(file.HasBom);
        Assert.DoesNotContain("\r\n", file.NewText);
        Assert.StartsWith("# Café\n", file.NewText);
    }

    [Fact]
    public void Group_without_items_is_stable()
    {
        var state = _slice.State("BYZ", 1, 0);
        var first = _slice.Plan(state);
        Assert.DoesNotContain("iv2-slice/select/BYZ/navy", File(first, "in_game/gui/setup.gui").NewText);
        SlicePackage.Write(first);

        Assert.All(_slice.Plan(state).Files, f => Assert.Equal(PlannedFileStatus.Unchanged, f.Status));
    }

    [Fact]
    public void Removed_idea_lists_removed_static_modifier_blocks()
    {
        SlicePackage.Write(_slice.Plan(_slice.State("BYZ", 2, 0)));

        var plan = _slice.Plan(_slice.State("BYZ", 1, 0));

        var modifiers = File(plan, "main_menu/common/static_modifiers/02_IV2_BYZ.txt");
        Assert.Equal(new[] { "national_idea_modifier_BYZ_2" }, modifiers.RemovedBlocks);
        Assert.Contains(plan.Warnings, w => w.Contains("national_idea_modifier_BYZ_2"));
    }

    [Fact]
    public void Missing_base_file_error_names_the_file()
    {
        System.IO.File.Delete(_slice.Temp.PathOf("mod/in_game/common/scripted_triggers/setup.txt"));

        var error = Assert.Throws<GeneratorOutputException>(() => _slice.Plan(_slice.State("BYZ", 1, 0)));

        Assert.Contains("in_game/common/scripted_triggers/setup.txt", error.Message);
    }

    [Fact]
    public void Missing_asset_library_skips_copy_with_note()
    {
        System.IO.File.Delete(Path.Combine(_slice.Package.Folder, "assets", "coa.zip"));

        var plan = _slice.Plan(_slice.State("BYZ", 1, 0));

        Assert.DoesNotContain(plan.Files, f => f.RelativePath.EndsWith(".dds", StringComparison.Ordinal));
        Assert.Contains(plan.Warnings, w => w == GeneratorsTexts.AssetLibraryNotProvided("coa"));
        Assert.Contains(plan.Files, f => f.RelativePath == "in_game/common/scripted_guis/02_IV2_sgui_BYZ.txt");
    }

    [Fact]
    public void Ensure_line_uses_skeleton_for_missing_file()
    {
        _slice.Temp.Write("own/templates/sk.sbn", "list = {\n\tOR = {\n\t}\n}\n");
        var package = SamplePackages.Ideas(folder: _slice.Temp.PathOf("own"), outputs: new[]
        {
            new OutputDef("t", OutputKind.EnsureLine, "x.txt", Block: "list.OR", Line: "has_or_had_tag = {{ tag }}", Skeleton: "sk.sbn"),
        });
        var state = GeneratorState.CreateEmpty(package, new[] { "english" });
        state.Head.Values["tag"] = "BYZ";

        var plan = GeneratorPlanner.Plan(package, state, new GeneratorTarget(_slice.Temp.PathOf("emptymod"), null));

        var file = File(plan, "x.txt");
        Assert.Equal(PlannedFileStatus.New, file.Status);
        Assert.Equal("list = {\r\n\tOR = {\r\n\t\thas_or_had_tag = BYZ\r\n\t}\r\n}\r\n", file.NewText);
    }

    [Fact]
    public void Plan_refuses_output_path_outside_root()
    {
        _slice.Temp.Write("own/templates/a.sbn", "x = 1\n");
        var package = SamplePackages.Ideas(folder: _slice.Temp.PathOf("own"), outputs: new[]
        {
            new OutputDef("escape", OutputKind.OwnFile, "../{{ tag }}.txt", Template: "a.sbn"),
        });
        var state = GeneratorState.CreateEmpty(package, new[] { "english" });
        state.Head.Values["tag"] = "BYZ";

        var error = Assert.Throws<GeneratorOutputException>(() =>
            GeneratorPlanner.Plan(package, state, new GeneratorTarget(_slice.Temp.PathOf("emptymod"), null)));
        Assert.Contains("escape", error.Message);
        Assert.False(System.IO.File.Exists(_slice.Temp.PathOf("BYZ.txt")));
    }

    [Fact]
    public void Template_error_stops_planning()
    {
        System.IO.File.WriteAllText(Path.Combine(_slice.Package.Folder, "templates", "select.sbn"), "{{ nope }}");
        var package = GeneratorPackageLoader.Load(_slice.Package.Folder);

        var error = Assert.Throws<GeneratorTemplateException>(() =>
            GeneratorPlanner.Plan(package, _slice.State("BYZ", 1, 0), new GeneratorTarget(_slice.ModRoot, null, SlicePackage.Languages)));
        Assert.Equal("select.sbn", error.TemplateName);
    }
}
