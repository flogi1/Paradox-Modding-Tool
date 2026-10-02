using System.IO;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Blocks;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

public sealed class GuiBlockOwnCopyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-owncopy-" + Guid.NewGuid().ToString("N"));

    public GuiBlockOwnCopyTests()
    {
        Directory.CreateDirectory(Shipped);
        Directory.CreateDirectory(Game);
        File.WriteAllText(Path.Combine(Shipped, "S.guiblock"), "# block: S\n# group: Anzeige\nx = 1\n");
        File.WriteAllText(Path.Combine(Game, "G.guiblock"), "# block: G\ny = 1\n");
    }

    private string Shipped => Path.Combine(_root, "shipped");

    private string Game => Path.Combine(_root, "game");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private GuiBlockWindow Window(string select) =>
        new(new GuiBlockLibrary(Shipped, Game, null), null, null, new GuiBlockWindowOptions(select, null, "Einfügen", true));

    [Fact]
    public void OwnCopy_OfShippedBlock_WritesGameFile() => Sta.Run(() =>
    {
        var window = Window("S");

        Assert.True(window.OwnCopyOffered);
        var path = window.MakeOwnCopy(GuiBlockLayer.Game, _ => true);

        Assert.Equal(Path.Combine(Game, "S.guiblock"), path);
        Assert.Equal("# block: S\n# group: Anzeige\nx = 1\n", File.ReadAllText(path!));
    });

    [Fact]
    public void OwnCopy_NotOfferedForBuiltInOrOwnBlocks() => Sta.Run(() =>
    {
        Assert.False(Window(GuiTexts.BlocksEmptyFileName).OwnCopyOffered);
        Assert.False(Window("G").OwnCopyOffered);
    });

    [Fact]
    public void OwnCopy_ExistingName_RespectsTheAnswer() => Sta.Run(() =>
    {
        // Eine eigene Kopie gleichen Namens entsteht, während das Fenster offen ist (z.B. in einem zweiten PMT-Fenster).
        var window = Window("S");
        File.WriteAllText(Path.Combine(Game, "eigen.guiblock"), "# block: S\nalt\n");
        var asked = new List<string>();

        Assert.Null(window.MakeOwnCopy(GuiBlockLayer.Game, name => { asked.Add(name); return false; }));
        Assert.Equal("# block: S\nalt\n", File.ReadAllText(Path.Combine(Game, "eigen.guiblock")));

        var path = window.MakeOwnCopy(GuiBlockLayer.Game, name => { asked.Add(name); return true; });
        Assert.Equal(Path.Combine(Game, "eigen.guiblock"), path);
        Assert.Equal("# block: S\n# group: Anzeige\nx = 1\n", File.ReadAllText(path!));
        Assert.Equal(new[] { "S", "S" }, asked);
        Assert.False(File.Exists(Path.Combine(Game, "S.guiblock")));
    });

    [Fact]
    public void OwnCopy_WhenOwnBlockHidesShipped_IsNotOffered() => Sta.Run(() =>
    {
        File.WriteAllText(Path.Combine(Game, "eigen.guiblock"), "# block: S\nalt\n");

        Assert.False(Window("S").OwnCopyOffered);
    });

    [Fact]
    public void OwnCopy_ModLayerNeedsAnActiveMod() => Sta.Run(() =>
    {
        var window = Window("S");

        Assert.False(window.OwnCopyModAvailable);
        Assert.Null(window.MakeOwnCopy(GuiBlockLayer.Mod, _ => true));
    });
}
