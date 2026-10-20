using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Klassisches Format (HOI IV, EU IV): Gewinnerregel mit den Dateinamen der Messung vom 2026-10-17 (PMT Override Probe). Die Dateien eines Ordners
/// laden vor seinen Unterordnern; HOI IV: zuletzt geladen gewinnt, EU IV: bei Fenstern zuerst geladen.
/// </summary>
public class ClassicIndexTests
{
    private const string Window = "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"mainmenu_panel_upperleft\"\n\t}\n}\n";

    private static readonly GuiLayout Hoi4 = new(
        Array.Empty<string>(), FirstWins: false, ModsBeforeVanilla: false, Dialect: GuiDialect.Classic, CaseSensitive: true);

    private static readonly GuiLayout Eu4 = new(
        Array.Empty<string>(), FirstWins: true, ModsBeforeVanilla: false, Dialect: GuiDialect.Classic);

    private static GuiIndex Build(GuiLayout layout, params (GuiSourceFile File, string Text)[] files) =>
        GuiIndex.FromDocuments(
            files.Select(f => f.File).ToList(), files.Select(f => GuiParser.Parse(f.Text)).ToList(), layout);

    private static (GuiSourceFile, string) Mod(string path, string text = Window) => (GuiIndexTests.F(path, GuiLayer.Mod), text);

    private static (GuiSourceFile, string) Vanilla(string path, string text = Window) => (GuiIndexTests.F(path), text);

    [Fact]
    public void Hoi4_last_root_file_loses_to_subfolder()
    {
        var index = Build(
            Hoi4,
            Mod("interface/00_pmt_order_a.gui"),
            Mod("interface/zz_pmt_order_b.gui"),
            Vanilla("interface/frontendmainview.gui"),
            Mod("interface/pmt_sub/000_pmt_sub_a.gui"));

        Assert.Equal(
            "interface/pmt_sub/000_pmt_sub_a.gui",
            index.Find("mainmenu_panel_upperleft", GuiDefinitionKind.NamedWidget)!.File.RelativePath);
    }

    [Fact]
    public void Hoi4_00_mod_file_loses_to_vanilla()
    {
        var index = Build(Hoi4, Mod("interface/00_pmt_order_a.gui"), Vanilla("interface/frontendmainview.gui"));

        Assert.Equal(
            "interface/frontendmainview.gui",
            index.Find("mainmenu_panel_upperleft", GuiDefinitionKind.NamedWidget)!.File.RelativePath);
    }

    [Fact]
    public void Hoi4_same_path_higher_layer_wins()
    {
        var index = Build(Hoi4, Vanilla("interface/frontendmainview.gui"), Mod("interface/frontendmainview.gui"));

        Assert.Equal(GuiLayer.Mod, index.Find("mainmenu_panel_upperleft", GuiDefinitionKind.NamedWidget)!.File.Layer);
    }

    [Fact]
    public void Last_loaded_uses_the_case_of_the_game()
    {
        // "Z" liegt bei Groß-/Kleinschreibung vor "a", sonst nach "a".
        var upper = Build(Hoi4, Mod("interface/Z.gui"), Mod("interface/a.gui")).FindAll("mainmenu_panel_upperleft", GuiDefinitionKind.NamedWidget);
        var lastCaseSensitive = GuiIndex.LastLoaded(upper, caseSensitive: true)!;
        var lastIgnoringCase = GuiIndex.LastLoaded(upper, caseSensitive: false)!;

        Assert.Equal("interface/a.gui", lastCaseSensitive.File.RelativePath);
        Assert.Equal("interface/Z.gui", lastIgnoringCase.File.RelativePath);
    }

    [Fact]
    public void Eu4_first_root_file_wins()
    {
        var index = Build(
            Eu4,
            Mod("interface/00_pmt_gui_a.gui"),
            Mod("interface/00_pmt_gui_b.gui"),
            Vanilla("interface/frontend.gui"),
            Mod("interface/zz_pmt_gui_a.gui"),
            Mod("interface/pmt_sub/000_pmt_sub_a.gui"));

        Assert.Equal(
            "interface/00_pmt_gui_a.gui",
            index.Find("mainmenu_panel_upperleft", GuiDefinitionKind.NamedWidget)!.File.RelativePath);
    }

    [Fact]
    public void Classic_window_under_guiTypes_is_top_level()
    {
        var index = Build(
            Hoi4,
            Mod("interface/a.gui", "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"win\"\n\t\ticonType = {\n\t\t\tname = \"icon\"\n\t\t}\n\t}\n}\n"));

        Assert.True(index.Find("win", GuiDefinitionKind.NamedWidget)!.IsTopLevel);
        Assert.False(index.Find("icon", GuiDefinitionKind.NamedWidget)!.IsTopLevel);
    }

    [Fact]
    public void Classic_guiTypes_in_other_case_is_top_level()
    {
        var index = Build(Hoi4, Mod("interface/a.gui", "GUITYPES = {\n\tcontainerWindowType = {\n\t\tname = \"win\"\n\t}\n}\n"));

        Assert.True(index.Find("win", GuiDefinitionKind.NamedWidget)!.IsTopLevel);
    }

    [Fact]
    public void Classic_Name_key_in_other_case_is_found()
    {
        var index = Build(Hoi4, Mod("interface/a.gui", "guiTypes = {\n\tcontainerWindowType = {\n\t\tName = \"x\"\n\t}\n}\n"));

        Assert.NotNull(index.Find("x", GuiDefinitionKind.NamedWidget));
    }

    [Fact]
    public void Jomini_Name_key_in_other_case_is_not_a_name()
    {
        var index = Build(GuiLayout.Default, Mod("in_game/gui/a.gui", "window = {\n\tName = \"x\"\n}\n"));

        Assert.Null(index.Find("x", GuiDefinitionKind.NamedWidget));
    }

    [Fact]
    public void Jomini_guiTypes_block_is_not_top_level()
    {
        var index = Build(GuiLayout.Default, Mod("in_game/gui/a.gui", Window));

        Assert.False(index.Find("mainmenu_panel_upperleft", GuiDefinitionKind.NamedWidget)!.IsTopLevel);
    }

    [Fact]
    public void Eu5_find_unchanged()
    {
        var text = "window = {\n\tname = \"w\"\n}\n";
        var index = Build(GuiLayout.Default, Vanilla("in_game/gui/a.gui", text), Mod("in_game/gui/a.gui", text));

        Assert.Equal(GuiLayer.Mod, index.Find("w", GuiDefinitionKind.NamedWidget)!.File.Layer);
    }
}
