using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Klassische Fenster: nur Blöcke direkt in einem obersten <c>guiTypes</c> zählen; Gewinner nach der Ladereihenfolge des Spiels.</summary>
public class ClassicWindowsTests
{
    private const string Window = "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"win\"\n\t}\n}\n";

    internal static GuiLayout LayoutOf(string game) =>
        GuiRules.FromProfile(GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", game + ".json"))).Layout;

    internal static GuiIndex Build(GuiLayout layout, params (GuiSourceFile File, string Text)[] files) =>
        GuiIndex.FromDocuments(files.Select(f => f.File).ToList(), files.Select(f => GuiParser.Parse(f.Text)).ToList(), layout);

    private static (GuiSourceFile, string) Mod(string path, string text = Window) => (GuiIndexTests.F(path, GuiLayer.Mod), text);

    private static (GuiSourceFile, string) Vanilla(string path, string text = Window) => (GuiIndexTests.F(path), text);

    [Fact]
    public void Windows_are_named_blocks_in_guiTypes()
    {
        var document = GuiParser.Parse(
            "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"a\"\n\t}\n\ticonType = {\n\t\tName = \"b\"\n\t}\n\twindowType = {\n\t\tsize = { x = 1 y = 1 }\n\t}\n}\n"
            + "guitypes = {\n\twindowType = {\n\t\tname = \"c\"\n\t}\n}\n"
            + "other = {\n\twindowType = {\n\t\tname = \"d\"\n\t}\n}\n");

        var windows = ClassicWindows.Of(document);

        Assert.Equal(new[] { "a", "b", "c" }, windows.Select(ClassicWindows.NameOf));
        Assert.Same(document.Root.Children[0], ClassicWindows.ContainerOf(document));
    }

    [Fact]
    public void Document_without_guiTypes_has_no_windows()
    {
        var document = GuiParser.Parse("spriteTypes = {\n\tspriteType = {\n\t\tname = \"s\"\n\t}\n}\n");

        Assert.Empty(ClassicWindows.Of(document));
        Assert.Null(ClassicWindows.ContainerOf(document));
    }

    [Fact]
    public void Nested_same_name_is_not_a_window()
    {
        var index = Build(
            LayoutOf("hoi4"),
            Mod("interface/a.gui", "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"a\"\n\t\ticonType = {\n\t\t\tname = \"a\"\n\t\t}\n\t}\n}\n"));

        var found = ClassicWindows.FindAll(index, "a");

        Assert.Single(found);
        Assert.True(found[0].IsTopLevel);
        Assert.Equal("containerWindowType", found[0].WidgetKey);
    }

    [Fact]
    public void Winner_ignores_nested_elements()
    {
        var index = Build(
            LayoutOf("hoi4"),
            Vanilla("interface/topbar.gui", "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"a\"\n\t}\n}\n"),
            Mod("interface/zz_mod.gui", "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"b\"\n\t\ticonType = {\n\t\t\tname = \"a\"\n\t\t}\n\t}\n}\n"));

        Assert.Equal("interface/topbar.gui", ClassicWindows.Winner(index, "a")!.File.RelativePath);
        Assert.Null(ClassicWindows.Winner(index, "missing"));
    }

    [Fact]
    public void Hoi4_winner_is_last_loaded_root_then_subfolder()
    {
        var files = new[]
        {
            Mod("interface/00_pmt_order_a.gui"),
            Mod("interface/zz_pmt_order_b.gui"),
            Vanilla("interface/frontendmainview.gui"),
        };

        Assert.Equal("interface/zz_pmt_order_b.gui", ClassicWindows.Winner(Build(LayoutOf("hoi4"), files), "win")!.File.RelativePath);

        var withSubfolder = Build(LayoutOf("hoi4"), files.Append(Mod("interface/pmt_sub/000_pmt_sub_a.gui")).ToArray());

        Assert.Equal("interface/pmt_sub/000_pmt_sub_a.gui", ClassicWindows.Winner(withSubfolder, "win")!.File.RelativePath);
    }

    [Fact]
    public void Eu4_winner_is_first_loaded()
    {
        var index = Build(
            LayoutOf("eu4"),
            Mod("interface/00_pmt_gui_a.gui"),
            Mod("interface/00_pmt_gui_b.gui"),
            Vanilla("interface/frontend.gui"),
            Mod("interface/zz_pmt_gui_a.gui"),
            Mod("interface/pmt_sub/000_pmt_sub_a.gui"));

        Assert.Equal("interface/00_pmt_gui_a.gui", ClassicWindows.Winner(index, "win")!.File.RelativePath);
    }
}
