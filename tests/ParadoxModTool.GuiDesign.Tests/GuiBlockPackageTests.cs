using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ParadoxModTool.GuiDesign.Blocks;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public sealed class GuiBlockPackageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-package-" + Guid.NewGuid().ToString("N"));

    public GuiBlockPackageTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private GuiBlock Own(string name, string body = "x = 1\n")
    {
        var path = Path.Combine(_root, "own", GuiBlockNames.FileNameFor(name));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"# block: {name}\n# group: Anzeige\n{body}");
        return GuiBlockParser.Parse(File.ReadAllText(path), GuiBlockLayer.Game, path);
    }

    private string Zip(params (string Name, byte[] Content)[] entries)
    {
        var path = Path.Combine(_root, "test" + GuiBlockPackage.Extension);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            using var stream = zip.CreateEntry(name).Open();
            stream.Write(content);
        }

        return path;
    }

    private static byte[] Block(string name) => Encoding.UTF8.GetBytes($"# block: {name}\nx = 1\n");

    [Fact]
    public void Package_RoundTripsUmlauts()
    {
        var path = Path.Combine(_root, "paket" + GuiBlockPackage.Extension);
        var blocks = new[] { Own("Größe"), Own("Knopf") };

        GuiBlockPackage.Write(path, blocks, "eu5", "1.2.3");
        var content = GuiBlockPackage.Read(path);

        Assert.Equal("eu5", content.Game);
        Assert.Equal("1.2.3", content.PmtVersion);
        Assert.False(content.ManifestProblem);
        Assert.Equal(new[] { "Größe", "Knopf" }, content.Items.Select(i => i.Block.Name));
        Assert.Equal(File.ReadAllText(blocks[0].SourcePath!), content.Items[0].Text);
        Assert.Equal("Größe.guiblock", content.Items[0].FileName);
        Assert.Empty(content.Skipped);
    }

    [Fact]
    public void Package_ManifestListsTheBlocks()
    {
        var path = Path.Combine(_root, "paket" + GuiBlockPackage.Extension);
        GuiBlockPackage.Write(path, new[] { Own("Knopf") }, "eu5", "1.2.3");

        using var zip = ZipFile.OpenRead(path);
        using var reader = new StreamReader(zip.GetEntry("manifest.json")!.Open());
        using var json = JsonDocument.Parse(reader.ReadToEnd());

        Assert.Equal(1, json.RootElement.GetProperty("format").GetInt32());
        Assert.Equal("eu5", json.RootElement.GetProperty("game").GetString());
        Assert.Equal("1.2.3", json.RootElement.GetProperty("pmtVersion").GetString());
        var block = Assert.Single(json.RootElement.GetProperty("blocks").EnumerateArray());
        Assert.Equal("Knopf", block.GetProperty("name").GetString());
        Assert.Equal("Anzeige", block.GetProperty("group").GetString());
        Assert.Equal("Knopf.guiblock", block.GetProperty("file").GetString());
    }

    [Fact]
    public void Read_SingleGuiblock()
    {
        var path = Own("Einzeln").SourcePath!;

        var content = GuiBlockPackage.Read(path);

        Assert.Null(content.Game);
        Assert.False(content.ManifestProblem);
        Assert.Equal("Einzeln", Assert.Single(content.Items).Block.Name);
    }

    [Fact]
    public void Read_SingleGuiblockTooLarge_Throws()
    {
        var path = Path.Combine(_root, "gross.guiblock");
        File.WriteAllBytes(path, new byte[GuiBlockPackage.MaxFileBytes + 1]);

        Assert.Throws<InvalidDataException>(() => GuiBlockPackage.Read(path));
    }

    [Fact]
    public void Read_MissingOrBrokenManifest_WarnsButReads()
    {
        var missing = GuiBlockPackage.Read(Zip(("a.guiblock", Block("A"))));
        File.Delete(Path.Combine(_root, "test" + GuiBlockPackage.Extension));
        var broken = GuiBlockPackage.Read(Zip(("manifest.json", Encoding.UTF8.GetBytes("{")), ("a.guiblock", Block("A"))));

        Assert.True(missing.ManifestProblem);
        Assert.Single(missing.Items);
        Assert.True(broken.ManifestProblem);
        Assert.Single(broken.Items);
    }

    [Fact]
    public void Read_HostilePackage_StaysWithinLimits()
    {
        var entries = new List<(string, byte[])>
        {
            ("sub/a.guiblock", Block("Sub")),
            ("../b.guiblock", Block("Out")),
            ("c.GUIBLOCK", Block("C")),
            ("big.guiblock", new byte[GuiBlockPackage.MaxFileBytes + 1]),
            ("d1.guiblock", Block("Doppelt")),
            ("d2.guiblock", Block("Doppelt")),
            ("notes.txt", Encoding.UTF8.GetBytes("x")),
        };
        entries.AddRange(Enumerable.Range(0, 600).Select(i => ($"small{i:D3}.guiblock", Block($"Klein {i}"))));
        var before = Directory.GetFileSystemEntries(_root, "*", SearchOption.AllDirectories).Length;

        var content = GuiBlockPackage.Read(Zip(entries.ToArray()));

        Assert.Equal(GuiBlockPackage.MaxFiles, content.Items.Count);
        Assert.Contains("c.GUIBLOCK", content.Items.Select(i => i.FileName));
        Assert.Contains("sub/a.guiblock", content.Skipped);
        Assert.Contains("../b.guiblock", content.Skipped);
        Assert.Contains("big.guiblock", content.Skipped);
        Assert.Contains("d2.guiblock", content.Skipped);
        Assert.Contains("small599.guiblock", content.Skipped);
        Assert.DoesNotContain(content.Items, i => i.Block.Name is "Sub" or "Out");
        Assert.Equal(before + 1, Directory.GetFileSystemEntries(_root, "*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public void Read_BrokenZip_Throws()
    {
        var path = Path.Combine(_root, "kaputt" + GuiBlockPackage.Extension);
        File.WriteAllText(path, "kein zip");

        Assert.Throws<InvalidDataException>(() => GuiBlockPackage.Read(path));
    }

    [Fact]
    public void Write_IsAtomic()
    {
        var path = Path.Combine(_root, "paket" + GuiBlockPackage.Extension);
        File.WriteAllText(path, "alt");

        GuiBlockPackage.Write(path, new[] { Own("Knopf") }, "eu5", "1.0.0");

        Assert.Equal("Knopf", Assert.Single(GuiBlockPackage.Read(path).Items).Block.Name);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }
}
