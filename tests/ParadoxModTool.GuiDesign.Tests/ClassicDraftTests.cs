using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Klassische Entwürfe: das oberste Fenster ist die Einheit, die die Mod-Fassung ersetzt.</summary>
public class ClassicDraftTests
{
    private const string VanillaText =
        "guiTypes = {\n"
        + "\tcontainerWindowType = {\n\t\tname = \"topbar\"\n\t\tsize = { width = 10 height = 10 }\n\t}\n"
        + "\tcontainerWindowType = {\n\t\tname = \"x\"\n\t\tsize = { width = 1 height = 1 }\n\t}\n"
        + "}\n";

    private const string ModTopbar =
        "guitypes = {\n"
        + "\tcontainerWindowType = {\n\t\tname = \"topbar\"\n\t\tsize = { width = 99 height = 10 }\n\t}\n"
        + "}\n";

    private static GuiSourceFile VanillaFile => GuiIndexTests.F("interface/topbar.gui");

    private static GuiIndex Index(string game, params (GuiSourceFile File, string Text)[] extra) =>
        ClassicWindowsTests.Build(ClassicWindowsTests.LayoutOf(game), extra.Prepend((VanillaFile, VanillaText)).ToArray());

    private static (GuiSourceFile, string) Mod(string path, string text) => (GuiIndexTests.F(path, GuiLayer.Mod), text);

    private static string Substituted(string text) => text.Replace("width = 10 height = 10", "width = 99 height = 10");

    [Fact]
    public void Draft_substitutes_mod_window()
    {
        var index = Index("hoi4", Mod("interface/zz_mod.gui", ModTopbar));

        var start = GuiDraft.Start(VanillaFile, VanillaText, index);

        Assert.Null(start.ModCopy);
        Assert.Equal(new[] { "topbar" }, start.Substituted);
        Assert.Equal(Substituted(VanillaText), start.Text);
    }

    [Fact]
    public void Draft_substitutes_losing_mod_window_too()
    {
        // interface/aaa.gui lädt vor topbar.gui und verliert in HOI IV; der Entwurf zeigt die Mod-Fassung trotzdem.
        var index = Index("hoi4", Mod("interface/aaa.gui", ModTopbar));

        var start = GuiDraft.Start(VanillaFile, VanillaText, index);

        Assert.Equal(new[] { "topbar" }, start.Substituted);
        Assert.Equal(Substituted(VanillaText), start.Text);
    }

    [Fact]
    public void Draft_is_indented_like_vanilla_when_mod_file_uses_other_indent()
    {
        var spaced = "guiTypes = {\n    containerWindowType = {\n        name = \"topbar\"\n        size = { width = 99 height = 10 }\n    }\n}\n";
        var index = Index("hoi4", Mod("interface/zz_mod.gui", spaced));

        var start = GuiDraft.Start(VanillaFile, VanillaText, index);

        Assert.Equal(Substituted(VanillaText), start.Text);
    }

    [Fact]
    public void Unchanged_classic_draft_has_no_changes()
    {
        var other = "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"other\"\n\t}\n}\n";
        var index = Index("hoi4", Mod("interface/zz_mod.gui", other));

        var start = GuiDraft.Start(VanillaFile, VanillaText, index);

        Assert.Null(start.ModCopy);
        Assert.Empty(start.Substituted);
        Assert.Equal(VanillaText, start.Text);
    }

    [Fact]
    public void Mod_window_equal_to_vanilla_is_not_a_change()
    {
        var index = Index("hoi4", Mod("interface/zz_mod.gui", VanillaText));

        var start = GuiDraft.Start(VanillaFile, VanillaText, index);

        Assert.Empty(start.Substituted);
        Assert.Equal(VanillaText, start.Text);
    }

    [Fact]
    public void Mod_copy_of_whole_file_opens_mod_file()
    {
        var index = Index("hoi4", Mod("interface/topbar.gui", ModTopbar));

        var start = GuiDraft.Start(VanillaFile, VanillaText, index);

        Assert.Equal("interface/topbar.gui", start.ModCopy!.RelativePath);
        Assert.Equal(GuiLayer.Mod, start.ModCopy.Layer);
        Assert.Empty(start.Substituted);
    }

    [Fact]
    public void Mod_window_follows_the_rule_of_the_game()
    {
        var files = new[] { Mod("interface/aa_mod.gui", ModTopbar), Mod("interface/zz_mod.gui", ModTopbar.Replace("width = 99", "width = 55")) };
        var window = ClassicWindows.Of(GuiParser.Parse(VanillaText))[0];

        Assert.Equal("interface/zz_mod.gui", GuiDraft.ModDefinition(Index("hoi4", files), window)!.File.RelativePath);
        Assert.Equal("interface/aa_mod.gui", GuiDraft.ModDefinition(Index("eu4", files), window)!.File.RelativePath);
    }

    [Fact]
    public void Mod_definition_is_null_without_mod_or_dependency_copy()
    {
        var index = Index("hoi4");

        Assert.Null(GuiDraft.ModDefinition(index, ClassicWindows.Of(GuiParser.Parse(VanillaText))[0]));
    }

    [Fact]
    public void Nested_element_of_the_same_name_is_not_a_mod_window()
    {
        var nested = "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"other\"\n\t\ticonType = {\n\t\t\tname = \"topbar\"\n\t\t}\n\t}\n}\n";
        var index = Index("hoi4", Mod("interface/zz_mod.gui", nested));

        var start = GuiDraft.Start(VanillaFile, VanillaText, index);

        Assert.Empty(start.Substituted);
        Assert.Equal(VanillaText, start.Text);
    }

    [Fact]
    public void Dependency_window_is_used_when_mod_has_none()
    {
        var index = Index("hoi4", (GuiIndexTests.F("interface/aaa.gui", GuiLayer.Dependency), ModTopbar));

        var start = GuiDraft.Start(VanillaFile, VanillaText, index);

        Assert.Equal(new[] { "topbar" }, start.Substituted);
        Assert.Equal(Substituted(VanillaText), start.Text);
    }

    [Fact]
    public void Definitions_kind_and_name_follow_the_layout()
    {
        var layout = ClassicWindowsTests.LayoutOf("hoi4");
        var document = GuiParser.Parse(VanillaText);

        var windows = GuiDraft.Definitions(document, layout);

        Assert.Equal(new[] { "topbar", "x" }, windows.Select(w => GuiDraft.NameOf(w, layout)));
        Assert.All(windows, w => Assert.Equal(GuiDefinitionKind.NamedWidget, GuiDraft.KindOf(w, layout)));
        Assert.Equal("containerWindowType", windows[0].Name);
        Assert.Empty(GuiDraft.Definitions(document));
    }

    [Fact]
    public void Line_in_copy_follows_the_window_by_name_when_windows_are_reordered()
    {
        var layout = ClassicWindowsTests.LayoutOf("hoi4");
        var vanilla = GuiParser.Parse(VanillaText);
        var copy = GuiParser.Parse(
            "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"x\"\n\t\tsize = { width = 3 height = 3 }\n\t}\n"
            + "\tcontainerWindowType = {\n\t\tname = \"topbar\"\n\t}\n}\n");
        var offset = VanillaText.IndexOf("width = 10 ", StringComparison.Ordinal);

        // Der Pfad (containerWindowType Nr. 1, size) träfe das falsche Fenster "x"; erwartet wird das Fenster "topbar".
        Assert.Equal(6, GuiDraft.LineInCopy(vanilla, offset, copy, layout));
    }

    [Fact]
    public void Line_in_copy_uses_the_exact_element_in_the_same_window()
    {
        var layout = ClassicWindowsTests.LayoutOf("hoi4");
        var vanilla = GuiParser.Parse(VanillaText);
        var offset = VanillaText.IndexOf("width = 1 ", StringComparison.Ordinal);

        Assert.Equal(8, GuiDraft.LineInCopy(vanilla, offset, GuiParser.Parse(VanillaText), layout));
    }
}
