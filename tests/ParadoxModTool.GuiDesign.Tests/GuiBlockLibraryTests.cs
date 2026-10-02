using ParadoxModTool.GuiDesign.Blocks;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public sealed class GuiBlockLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-blocks-" + Guid.NewGuid().ToString("N"));

    public GuiBlockLibraryTests()
    {
        Directory.CreateDirectory(Shipped);
        Directory.CreateDirectory(Game);
        Directory.CreateDirectory(Mod);
    }

    private string Shipped => Path.Combine(_root, "shipped");

    private string Game => Path.Combine(_root, "game");

    private string Mod => Path.Combine(_root, "mod");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static void Write(string folder, string file, string text) => File.WriteAllText(Path.Combine(folder, file), text);

    private GuiBlockLibrary Library() => new(Shipped, Game, Mod);

    private static IEnumerable<GuiBlock> Own(GuiBlockLibrary library) => library.Effective.Where(b => !b.IsBuiltIn);

    [Theory]
    [InlineData("My Window", "my_window")]
    [InlineData("trade-screen.gui", "trade_screen")]
    [InlineData("  ", "new_window")]
    public void WidgetNameFor_MakesAWidgetName(string fileName, string expected)
    {
        Assert.Equal(expected, GuiBlockNames.WidgetNameFor(fileName));
    }

    [Fact]
    public void FileNameFor_ReplacesInvalidCharacters()
    {
        Assert.Equal("Knopf_ groß_.guiblock", GuiBlockNames.FileNameFor("Knopf: groß?"));
        Assert.Equal("block.guiblock", GuiBlockNames.FileNameFor("  "));
    }

    [Fact]
    public void Layers_ModBeatsGameBeatsShipped()
    {
        Write(Shipped, "x.guiblock", "# block: X\ns\n");
        Write(Game, "x.guiblock", "# block: X\ng\n");
        Write(Mod, "x.guiblock", "# block: x\nm\n");

        var library = Library();

        var x = Assert.Single(Own(library));
        Assert.Equal(GuiBlockLayer.Mod, x.Layer);
        Assert.Same(x, library.Find("X"));
        Assert.Equal(new[] { GuiBlockLayer.Game, GuiBlockLayer.Shipped }, library.HiddenBy(x).Select(b => b.Layer));
    }

    [Fact]
    public void EmptyFile_IsBuiltInAndAlwaysThere()
    {
        var library = new GuiBlockLibrary(null, null, null);

        var empty = Assert.Single(library.Effective);
        Assert.Equal("Leere Datei", GuiTexts.BlocksEmptyFileName);
        Assert.Equal(GuiTexts.BlocksEmptyFileName, empty.Name);
        Assert.Equal("Fenster", empty.Group);
        Assert.True(empty.IsBuiltIn);
        Assert.True(empty.IsValid);
        Assert.Equal(GuiBlockLayer.Shipped, empty.Layer);
        Assert.Equal(string.Empty, GuiBlockRenderer.Render(empty, GuiBlockValues.Defaults(empty)));
    }

    [Fact]
    public void EmptyFile_CanBeHiddenByAFile()
    {
        Write(Game, "leer.guiblock", $"# block: {GuiTexts.BlocksEmptyFileName}\nx\n");

        var library = Library();

        var empty = Assert.Single(library.Effective, b => b.Name == GuiTexts.BlocksEmptyFileName);
        Assert.False(empty.IsBuiltIn);
        Assert.True(Assert.Single(library.HiddenBy(empty)).IsBuiltIn);
    }

    [Fact]
    public void Effective_SortsStandardGroupsFirst()
    {
        Write(Game, "a.guiblock", "# block: A\n# group: Zeug\nx\n");
        Write(Game, "b.guiblock", "# block: B\n# group: Anzeige\nx\n");
        Write(Game, "c.guiblock", "# block: C\n# group: Fenster\nx\n");
        Write(Game, "d.guiblock", "# block: D\n# group: Bedienelemente\nx\n");

        var library = Library();

        Assert.Equal(new[] { "Fenster", "Bedienelemente", "Anzeige" }, GuiBlockLibrary.StandardGroups);
        Assert.Equal(new[] { "C", GuiTexts.BlocksEmptyFileName, "D", "B", "A" }, library.Effective.Select(b => b.Name));
    }

    [Fact]
    public void DuplicateNameInOneLayer_IsAnError()
    {
        Write(Game, "a.guiblock", "# block: X\nx\n");
        Write(Game, "b.guiblock", "# block: X\ny\n");

        var library = Library();

        var x = Assert.Single(Own(library));
        Assert.EndsWith("a.guiblock", x.SourcePath);
        Assert.True(x.IsValid);
        var hidden = Assert.Single(library.HiddenBy(x));
        Assert.EndsWith("b.guiblock", hidden.SourcePath);
        Assert.Contains(hidden.Errors, e => e.Kind == GuiBlockErrorKind.DuplicateName);
    }

    [Fact]
    public void UnreadableFile_ShowsAsError()
    {
        var path = Path.Combine(Game, "Gesperrt.guiblock");
        File.WriteAllText(path, "# block: Egal\nx\n");
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var library = Library();

        var block = Assert.Single(Own(library));
        Assert.Equal("Gesperrt", block.Name);
        Assert.Contains(block.Errors, e => e.Kind == GuiBlockErrorKind.Unreadable);
    }

    [Fact]
    public void Save_WritesFileAndOverwritesSameName()
    {
        Write(Game, "anders.guiblock", "# block: Knopf\nalt\n");
        var library = Library();

        var path = library.Save("# block: Knopf\nneu\n", "Knopf", GuiBlockLayer.Game);
        var second = library.Save("# block: Neu: X\nx\n", "Neu: X", GuiBlockLayer.Mod);

        Assert.Equal(Path.Combine(Game, "anders.guiblock"), path);
        Assert.Equal("# block: Knopf\nneu\n", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(Game));
        Assert.Equal(Path.Combine(Mod, "Neu_ X.guiblock"), second);
        Assert.True(library.Reload().Exists("Neu: X", GuiBlockLayer.Mod));
        Assert.False(library.Reload().Exists("Neu: X", GuiBlockLayer.Game));
        Assert.Throws<InvalidOperationException>(() => library.Save("# block: S\n", "S", GuiBlockLayer.Shipped));
        Assert.Throws<InvalidOperationException>(() => new GuiBlockLibrary(Shipped, Game, null).Save("# block: S\n", "S", GuiBlockLayer.Mod));
    }

    [Fact]
    public void Save_CreatesTheFolder()
    {
        var game = Path.Combine(_root, "neu", "guiblocks");
        var library = new GuiBlockLibrary(null, game, null);

        library.Save("# block: A\nx\n", "A", GuiBlockLayer.Game);

        Assert.True(File.Exists(Path.Combine(game, "A.guiblock")));
        Assert.Equal(game, library.DirectoryOf(GuiBlockLayer.Game));
    }

    [Fact]
    public void Delete_RemovesOwnBlocksOnly()
    {
        Write(Game, "g.guiblock", "# block: G\nx\n");
        Write(Shipped, "s.guiblock", "# block: S\nx\n");
        var library = Library();

        library.Delete(library.Find("G")!);

        Assert.False(File.Exists(Path.Combine(Game, "g.guiblock")));
        Assert.Throws<InvalidOperationException>(() => library.Delete(library.Find("S")!));
        Assert.Throws<InvalidOperationException>(() => library.Delete(library.Find(GuiTexts.BlocksEmptyFileName)!));
        Assert.Null(library.Reload().Find("G"));
    }

    [Fact]
    public void CopyToLayer_CopiesTheText()
    {
        Write(Shipped, "s.guiblock", "# block: S\n# group: Anzeige\nx = 1\n");
        var library = Library();

        var path = library.CopyToLayer(library.Find("S")!, GuiBlockLayer.Mod);
        var builtIn = library.CopyToLayer(library.Find(GuiTexts.BlocksEmptyFileName)!, GuiBlockLayer.Game);

        Assert.Equal("# block: S\n# group: Anzeige\nx = 1\n", File.ReadAllText(path));
        Assert.Equal(GuiBlockLayer.Mod, library.Reload().Find("S")!.Layer);
        Assert.Equal($"# block: {GuiTexts.BlocksEmptyFileName}\n# group: Fenster\n", File.ReadAllText(builtIn));
    }

    [Fact]
    public void MissingRequirements_ChecksTypesAndTemplates()
    {
        var index = GuiIndexTests.Build((GuiIndexTests.F("in_game/gui/a.gui"), "types T {\n\ttype a = widget { }\n}\ntemplate b { }\n"));
        var block = GuiBlockParser.Parse("# block: A\n# requires: a, b, c\nx\n", GuiBlockLayer.Game, null);

        Assert.Equal(new[] { "c" }, GuiBlockLibrary.MissingRequirements(block, index));
    }

    [Fact]
    public void MissingRequirements_WithoutIndex_IsEmpty()
    {
        var block = GuiBlockParser.Parse("# block: A\n# requires: a\nx\n", GuiBlockLayer.Game, null);

        Assert.Empty(GuiBlockLibrary.MissingRequirements(block, null));
    }
}
