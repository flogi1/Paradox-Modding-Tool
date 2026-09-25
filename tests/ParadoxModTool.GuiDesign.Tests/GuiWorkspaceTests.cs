using System.Buffers.Binary;
using System.Diagnostics;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

public sealed class GuiWorkspaceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-gui-" + Guid.NewGuid().ToString("N"));

    public GuiWorkspaceTests(ITestOutputHelper output)
    {
        _output = output;
        Write(@"install\game\in_game\gui\base.gui", "types V { type v = icon { } }");
        Write(@"dep\in_game\gui\d.gui", "types D { type d = widget { } }");
        Write(@"mod\in_game\gui\m.gui", "types M { type m = v { } }");
        var dds = new byte[128];
        "DDS "u8.CopyTo(dds);
        BinaryPrimitives.WriteInt32LittleEndian(dds.AsSpan(12), 32);
        BinaryPrimitives.WriteInt32LittleEndian(dds.AsSpan(16), 64);
        var ddsPath = Path.Combine(_temp, @"install\game\in_game\gfx\interface\a.dds");
        Directory.CreateDirectory(Path.GetDirectoryName(ddsPath)!);
        File.WriteAllBytes(ddsPath, dds);
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_temp, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private GuiWorkspaceOptions Options(bool relearn = false) => new(
        Path.Combine(_temp, "install"),
        Path.Combine(_temp, "mod"),
        new[] { Path.Combine(_temp, "dep") },
        GuiResolverTests.Rules,
        Path.Combine(_temp, "cache", "catalog.json"),
        relearn);

    [Fact]
    public void CreateIndexesVanillaDependencyAndMod()
    {
        var workspace = GuiWorkspace.Create(Options());

        Assert.Equal(GuiLayer.Vanilla, workspace.Index.Find("v", GuiDefinitionKind.Type)!.File.Layer);
        Assert.Equal(GuiLayer.Dependency, workspace.Index.Find("d", GuiDefinitionKind.Type)!.File.Layer);
        Assert.Equal(GuiLayer.Mod, workspace.Index.Find("m", GuiDefinitionKind.Type)!.File.Layer);
        Assert.Equal((64, 32), workspace.Textures.SizeOf("gfx/interface/a.dds"));
        Assert.False(workspace.CatalogFromCache);
        Assert.True(File.Exists(Path.Combine(_temp, "cache", "catalog.json")));
        Assert.Equal("icon", workspace.CreateResolver().BaseWidgetOf("m"));
    }

    [Fact]
    public void SecondCreateReusesDocumentsAndTheCatalog()
    {
        var first = GuiWorkspace.Create(Options());
        var second = GuiWorkspace.Create(Options(), first);

        Assert.Equal(3, second.ReusedDocuments);
        Assert.True(second.CatalogFromCache);
        Assert.Same(first.Index.DocumentOf(first.Index.Files[0]), second.Index.DocumentOf(second.Index.Files[0]));

        var fromDisk = GuiWorkspace.Create(Options());
        Assert.True(fromDisk.CatalogFromCache);
        Assert.False(GuiWorkspace.Create(Options(relearn: true), first).CatalogFromCache);
    }

    [Fact]
    public void WithModFileUpdatesTheMod()
    {
        var workspace = GuiWorkspace.Create(Options());

        var updated = workspace.WithModFile(Path.Combine(_temp, @"mod\in_game\gui\m.gui"), "types M { type m2 = widget { } }");

        Assert.NotNull(updated.Index.Find("m2", GuiDefinitionKind.Type));
        Assert.Null(updated.Index.Find("m", GuiDefinitionKind.Type));
    }

    [Fact]
    public void WithModFileIgnoresPathsOutsideTheModRoots()
    {
        var workspace = GuiWorkspace.Create(Options());

        Assert.Same(workspace, workspace.WithModFile(Path.Combine(_temp, @"elsewhere\x.gui"), "types X { type x = widget { } }"));
        Assert.Same(workspace, workspace.WithModFile(Path.Combine(_temp, @"install\game\in_game\gui\base.gui"), "types V { }"));
        Assert.Same(workspace, workspace.WithModFile(Path.Combine(_temp, @"mod\in_game\common\x.txt"), "a = 1"));
    }

    [Fact]
    public void BuildsOverVanillaAndReportsTimeAndMemory()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var before = GC.GetTotalMemory(forceFullCollection: true);
        var watch = Stopwatch.StartNew();
        var options = new GuiWorkspaceOptions(VanillaGui.InstallDirectory, null, Array.Empty<string>(), GuiRulesVanillaTests.ProfileRules(), null);
        var workspace = GuiWorkspace.Create(options);
        var buildMs = watch.ElapsedMilliseconds;
        var after = GC.GetTotalMemory(forceFullCollection: true);
        watch.Restart();
        var again = GuiWorkspace.Create(options, workspace);
        _output.WriteLine($"Aufbau {buildMs} ms, erneut mit Wiederverwendung {watch.ElapsedMilliseconds} ms, Speicher {(after - before) / 1024 / 1024} MB, {again.ReusedDocuments} Dateien wiederverwendet");

        Assert.Equal(workspace.Index.Files.Count, again.ReusedDocuments);
        GC.KeepAlive(workspace);
    }

    [Fact]
    public void CreateReportsProgressUpToAllFiles()
    {
        var progress = new Collect();

        var workspace = GuiWorkspace.Create(Options(), null, CancellationToken.None, progress);

        Assert.Equal((workspace.Index.Files.Count, workspace.Index.Files.Count), progress.Items[^1]);
    }

    [Fact]
    public void CreateCanBeCancelled()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => GuiWorkspace.Create(Options(), null, source.Token));
    }

    private sealed class Collect : IProgress<(int Done, int Total)>
    {
        public List<(int Done, int Total)> Items { get; } = new();

        public void Report((int Done, int Total) value) => Items.Add(value);
    }
}
