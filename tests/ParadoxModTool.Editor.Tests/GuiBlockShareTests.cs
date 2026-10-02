using System.IO;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Blocks;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

public sealed class GuiBlockShareTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-share-" + Guid.NewGuid().ToString("N"));

    public GuiBlockShareTests()
    {
        Directory.CreateDirectory(Shipped);
        Directory.CreateDirectory(Game);
        Directory.CreateDirectory(Mod);
    }

    private string Shipped => Path.Combine(_root, "shipped");

    private string Game => Path.Combine(_root, "game");

    private string Mod => Path.Combine(_root, "mod");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static void Write(string folder, string name, string body = "x = 1\n") =>
        File.WriteAllText(Path.Combine(folder, GuiBlockNames.FileNameFor(name)), $"# block: {name}\n{body}");

    private static GuiBlockPackageItem Item(string name, string body = "x = 1\n")
    {
        var text = $"# block: {name}\n{body}";
        return new GuiBlockPackageItem(GuiBlockNames.FileNameFor(name), text, GuiBlockParser.Parse(text, GuiBlockLayer.Game, GuiBlockNames.FileNameFor(name)));
    }

    [Fact]
    public void Export_OffersOnlyOwnEffectiveBlocks()
    {
        Write(Shipped, "S");
        Write(Game, "G");
        Write(Game, "Beide");
        Write(Mod, "Beide", "y = 2\n");

        var own = GuiBlockExportWindow.OwnBlocks(new GuiBlockLibrary(Shipped, Game, Mod));

        Assert.Equal(new[] { ("Beide", GuiBlockLayer.Mod), ("G", GuiBlockLayer.Game) }, own.Select(b => (b.Name, b.Layer)).OrderBy(p => p.Name));
    }

    [Fact]
    public void ExportWindow_SearchAndAllNone() => Sta.Run(() =>
    {
        Write(Game, "Knopf");
        Write(Game, "Liste");
        var window = new GuiBlockExportWindow(GuiBlockExportWindow.OwnBlocks(new GuiBlockLibrary(null, Game, null)), "eu5", "1.0.0");

        Assert.Equal(2, window.Chosen.Count);
        window.SearchText = "kno";
        window.SetAll(false);
        Assert.Equal(new[] { "Liste" }, window.Chosen.Select(b => b.Name));
    });

    [Fact]
    public void ImportWindow_PresetsChecksAndWarnings() => Sta.Run(() =>
    {
        Write(Game, "Konflikt");
        var content = new GuiBlockPackageContent("eu5", "1.0.0", false, new[] { Item("Neu"), Item("Kaputt", "x = $q$\n"), Item("Konflikt") }, Array.Empty<string>());

        var window = new GuiBlockImportWindow(content, "ck3", new GuiBlockLibrary(null, Game, null));

        Assert.True(window.GameWarningShown);
        Assert.Equal(GuiBlockLayer.Game, window.Layer);
        var rows = window.Rows.ToDictionary(r => r.Name);
        Assert.True(rows["Neu"].Checked);
        Assert.False(rows["Kaputt"].Checked);
        Assert.True(rows["Konflikt"].Checked);
        Assert.True(rows["Konflikt"].IsConflict);
        Assert.False(rows["Konflikt"].Overwrite);
        Assert.Equal(2, window.Chosen.Count);
    });

    [Fact]
    public void ImportWindow_SameGameShowsNoWarning() => Sta.Run(() =>
    {
        var content = new GuiBlockPackageContent("eu5", "1.0.0", false, new[] { Item("Neu") }, Array.Empty<string>());

        Assert.False(new GuiBlockImportWindow(content, "eu5", new GuiBlockLibrary(null, Game, null)).GameWarningShown);
    });

    [Fact]
    public void Import_SkipsConflictsUnlessOverwrite()
    {
        Write(Game, "Konflikt", "alt = 1\n");
        var library = new GuiBlockLibrary(null, Game, null);
        var conflictFile = Path.Combine(Game, GuiBlockNames.FileNameFor("Konflikt"));

        var first = GuiBlockImportWindow.Import(library, new[] { (Item("Neu"), false), (Item("Konflikt", "neu = 1\n"), false) }, GuiBlockLayer.Game);

        Assert.Equal((1, 1), first);
        Assert.Contains("alt = 1", File.ReadAllText(conflictFile));
        Assert.True(File.Exists(Path.Combine(Game, "Neu.guiblock")));

        var second = GuiBlockImportWindow.Import(library.Reload(), new[] { (Item("Konflikt", "neu = 1\n"), true) }, GuiBlockLayer.Game);

        Assert.Equal((1, 0), second);
        Assert.Contains("neu = 1", File.ReadAllText(conflictFile));
    }

    [Fact]
    public void ImportWindow_ModLayerNeedsAnActiveMod() => Sta.Run(() =>
    {
        var content = new GuiBlockPackageContent(null, null, true, new[] { Item("Neu") }, new[] { "sub/x.guiblock" });

        var without = new GuiBlockImportWindow(content, "eu5", new GuiBlockLibrary(null, Game, null));
        var with = new GuiBlockImportWindow(content, "eu5", new GuiBlockLibrary(null, Game, Mod));

        Assert.False(without.ModOption.IsEnabled);
        Assert.True(with.ModOption.IsEnabled);
    });

    [Fact]
    public void ShareWindows_KeepTheirSize() => Sta.Run(() =>
    {
        Write(Game, "Knopf");
        var library = new GuiBlockLibrary(null, Game, Mod);
        var content = new GuiBlockPackageContent("eu5", "1.0.0", true, new[] { Item("Knopf"), Item("Neu") }, new[] { "big.guiblock" });

        WpfWindows.AssertKeepsItsSizeWhenChoicesAreUsed(new GuiBlockExportWindow(GuiBlockExportWindow.OwnBlocks(library), "eu5", "1.0.0"));
        WpfWindows.AssertKeepsItsSizeWhenChoicesAreUsed(new GuiBlockImportWindow(content, "ck3", library));
    });
}
