using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Index;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Crusader Kings III: Innerhalb einer Ebene gilt die zuerst geladene Definition (gemessen 2026-09-30), die Mod geht vor Vanilla.</summary>
public class GuiIndexFirstWinsTests
{
    private static readonly GuiLayout Ck3 = new(Array.Empty<string>(), FirstWins: true);

    private static string Type(int size) => $"types T {{\n\ttype t = button {{\n\t\tsize = {{ {size} {size} }}\n\t}}\n}}\n";

    private static GuiIndex Build(GuiLayout? layout, params (GuiSourceFile File, string Text)[] files) =>
        GuiIndex.Build(files.Select(f => f.File).ToList(), file => files.First(f => f.File == file).Text, layout);

    private static (GuiSourceFile, string)[] VanillaAndTwoModFiles() => new[]
    {
        (GuiIndexTests.F("gui/shared/x.gui"), Type(45)),
        (GuiIndexTests.F("gui/00_a.gui", GuiLayer.Mod), Type(90)),
        (GuiIndexTests.F("gui/zz_b.gui", GuiLayer.Mod), Type(20)),
    };

    [Fact]
    public void First_wins_takes_the_first_mod_file() =>
        Assert.Equal("gui/00_a.gui", Build(Ck3, VanillaAndTwoModFiles()).Find("t", GuiDefinitionKind.Type)!.File.RelativePath);

    [Fact]
    public void Last_wins_takes_the_last_mod_file() =>
        Assert.Equal("gui/zz_b.gui", Build(null, VanillaAndTwoModFiles()).Find("t", GuiDefinitionKind.Type)!.File.RelativePath);

    [Fact]
    public void Mod_beats_vanilla_under_first_wins()
    {
        var index = Build(Ck3, (GuiIndexTests.F("gui/shared/x.gui"), Type(45)), (GuiIndexTests.F("gui/zz_b.gui", GuiLayer.Mod), Type(20)));

        Assert.Equal("gui/zz_b.gui", index.Find("t", GuiDefinitionKind.Type)!.File.RelativePath);
    }

    // Victoria 3 (gemessen 2026-10-11 mit PMT Order Probe A/B/C): Vanilla und Mods zählen gemeinsam nach Ladereihenfolge; Unterordner laden nach den
    // Dateien des Ordners darüber (Wiki "Mod compatibility", mit Probe C bestätigt).
    private static readonly GuiLayout Vic3 = new(Array.Empty<string>(), FirstWins: true, ModsBeforeVanilla: false);

    [Fact]
    public void Vic3_vanilla_file_that_loads_first_beats_the_mod()
    {
        var index = Build(Vic3, (GuiIndexTests.F("gui/shared/button_icons.gui"), Type(45)), (GuiIndexTests.F("gui/shared/zz_a.gui", GuiLayer.Mod), Type(20)));

        Assert.Equal("gui/shared/button_icons.gui", index.Find("t", GuiDefinitionKind.Type)!.File.RelativePath);
    }

    [Fact]
    public void Vic3_mod_file_that_loads_first_beats_vanilla()
    {
        var index = Build(Vic3, (GuiIndexTests.F("gui/shared/button_icons.gui"), Type(45)), (GuiIndexTests.F("gui/00_b.gui", GuiLayer.Mod), Type(20)));

        Assert.Equal("gui/00_b.gui", index.Find("t", GuiDefinitionKind.Type)!.File.RelativePath);
    }

    [Fact]
    public void Vic3_files_of_a_folder_load_before_its_subfolders()
    {
        var index = Build(Vic3, (GuiIndexTests.F("gui/shared/x.gui"), Type(45)), (GuiIndexTests.F("gui/zz_c.gui", GuiLayer.Mod), Type(20)));

        Assert.Equal("gui/zz_c.gui", index.Find("t", GuiDefinitionKind.Type)!.File.RelativePath);
    }

    [Fact]
    public void Vic3_mod_definition_that_loads_after_vanilla_is_not_the_mod_definition_in_effect()
    {
        var index = Build(Vic3, (GuiIndexTests.F("gui/block_windows.gui"), Type(45)), (GuiIndexTests.F("gui/shared/zz_mod.gui", GuiLayer.Mod), Type(20)));
        var vanilla = index.FindAll("t", GuiDefinitionKind.Type).Single(d => d.File.Layer == GuiLayer.Vanilla);

        Assert.Null(GuiDraft.ModDefinition(index, vanilla.Node));
    }

    [Fact]
    public void Vic3_mod_definition_follows_the_load_order()
    {
        var index = Build(
            Vic3,
            (GuiIndexTests.F("gui/shared/x.gui"), Type(45)),
            (GuiIndexTests.F("gui/shared/00_b.gui", GuiLayer.Mod), Type(20)),
            (GuiIndexTests.F("gui/zz_a.gui", GuiLayer.Mod), Type(90)));
        var vanilla = index.FindAll("t", GuiDefinitionKind.Type).Single(d => d.File.Layer == GuiLayer.Vanilla);

        Assert.Equal("gui/zz_a.gui", GuiDraft.ModDefinition(index, vanilla.Node)!.File.RelativePath);
    }

    [Fact]
    public void Vic3_editing_a_type_whose_mod_definition_loads_after_vanilla_is_blocked()
    {
        var source = GuiOverridePlannerTests.V("gui/shared/buttons.gui");
        var mod = GuiIndexTests.F("gui/shared/zz_mod.gui", GuiLayer.Mod);
        var files = new List<(GuiSourceFile File, string Text)> { (source, GuiOverridePlannerTests.Vanilla), (mod, GuiOverridePlannerTests.ModOverrides) };
        var index = Build(Vic3, files.ToArray());
        var draft = GuiDraft.Start(source, GuiOverridePlannerTests.Vanilla, index).Text.Replace("size = { 20 20 }", "size = { 21 21 }");
        var request = new GuiOverrideRequest(source, GuiOverridePlannerTests.Vanilla, draft, index, GuiOverridePlannerTests.Targets,
            path => string.Equals(path, mod.FullPath, StringComparison.OrdinalIgnoreCase) ? GuiOverridePlannerTests.ModOverrides : null);

        var blocker = Assert.Single(GuiOverridePlanner.Plan(request).Blockers);

        Assert.Equal(GuiPlanBlockerKind.ModLoadsAfterVanilla, blocker.Kind);
        Assert.Equal("button_b", blocker.Name);
    }

    [Fact]
    public void Vic3_mod_settings_hint_warns_that_own_names_must_load_before_vanilla()
    {
        Assert.Contains("Vanilla", GuiTexts.ModSettingsFirstWinsHintFor(Vic3));
        Assert.Equal(GuiTexts.ModSettingsFirstWinsHint, GuiTexts.ModSettingsFirstWinsHintFor(Ck3));
    }

    [Fact]
    public void Vic3_profile_keeps_vanilla_in_load_order()
    {
        var profile = ParadoxModTool.GameProfiles.GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "vic3.json"));

        var layout = ParadoxModTool.GuiDesign.Resolve.GuiRules.FromProfile(profile).Layout;

        Assert.True(layout.FirstWins);
        Assert.False(layout.ModsBeforeVanilla);
    }

    [Fact]
    public void Dependency_file_00_beats_own_mod_file_zz()
    {
        // Gemessen 2026-09-30: Die Dateien aller Mods werden gemeinsam alphabetisch sortiert, die Playset-Reihenfolge zählt nicht.
        var index = Build(
            Ck3,
            (GuiIndexTests.F("gui/shared/x.gui"), Type(45)),
            (GuiIndexTests.F("gui/00_dep.gui", GuiLayer.Dependency), Type(20)),
            (GuiIndexTests.F("gui/zz_own.gui", GuiLayer.Mod), Type(90)));

        Assert.Equal("gui/00_dep.gui", index.Find("t", GuiDefinitionKind.Type)!.File.RelativePath);
    }

    [Fact]
    public void Own_mod_file_00_beats_dependency_file_zz()
    {
        var index = Build(
            Ck3,
            (GuiIndexTests.F("gui/zz_dep.gui", GuiLayer.Dependency), Type(20)),
            (GuiIndexTests.F("gui/00_own.gui", GuiLayer.Mod), Type(90)));

        Assert.Equal("gui/00_own.gui", index.Find("t", GuiDefinitionKind.Type)!.File.RelativePath);
    }

    [Fact]
    public void Same_path_in_both_mods_takes_the_own_mod()
    {
        var index = Build(
            Ck3,
            (GuiIndexTests.F("gui/00_same.gui", GuiLayer.Dependency), Type(20)),
            (GuiIndexTests.F("gui/00_same.gui", GuiLayer.Mod), Type(90)));

        Assert.Equal(GuiLayer.Mod, index.Find("t", GuiDefinitionKind.Type)!.File.Layer);
    }

    [Fact]
    public void ModDefinition_uses_the_combined_mod_order()
    {
        var index = Build(
            Ck3,
            (GuiIndexTests.F("gui/shared/x.gui"), Type(45)),
            (GuiIndexTests.F("gui/00_dep.gui", GuiLayer.Dependency), Type(20)),
            (GuiIndexTests.F("gui/zz_own.gui", GuiLayer.Mod), Type(90)));
        var vanilla = index.FindAll("t", GuiDefinitionKind.Type).Single(d => d.File.Layer == GuiLayer.Vanilla);

        Assert.Equal("gui/00_dep.gui", GuiDraft.ModDefinition(index, vanilla.Node)!.File.RelativePath);
    }

    [Fact]
    public void WithFile_keeps_layout()
    {
        var files = VanillaAndTwoModFiles();
        var index = Build(Ck3, files).WithFile(files[2].Item1, Type(30));

        Assert.True(index.Layout.FirstWins);
        Assert.Equal("gui/00_a.gui", index.Find("t", GuiDefinitionKind.Type)!.File.RelativePath);
    }

    [Fact]
    public void ModDefinition_follows_first_wins()
    {
        var index = Build(Ck3, VanillaAndTwoModFiles());
        var vanilla = index.FindAll("t", GuiDefinitionKind.Type).Single(d => d.File.Layer == GuiLayer.Vanilla);

        Assert.Equal("gui/00_a.gui", GuiDraft.ModDefinition(index, vanilla.Node)!.File.RelativePath);
    }
}
