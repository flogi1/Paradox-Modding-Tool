using ParadoxModTool.GuiDesign.Designer;
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
