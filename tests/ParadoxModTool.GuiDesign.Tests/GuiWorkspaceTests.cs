using System.Buffers.Binary;
using System.Diagnostics;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.GuiDesign.Text;
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
    public void JominiTextServiceFollowsSavedModFiles()
    {
        var workspace = GuiWorkspace.Create(Options());
        Assert.NotNull(workspace.Text);

        // Ein gespeichertes Textformat der Mod gilt in der nächsten Vorschau: WithModFile gibt dem Textdienst den neuen Index.
        var updated = workspace.WithModFile(
            Path.Combine(_temp, @"mod\in_game\gui\m.gui"),
            "types M { type m = v { } } textformatting = { format = { name = red format = \"color:{1,0,0}\" } }");
        Assert.NotSame(workspace.Text, updated.Text);

        var doc = GuiParser.Parse("textbox = { raw_text = \"#red Probe#!\" fontsize = 15 autoresize = yes }");
        var box = updated.CreateLayoutEngine().Layout(updated.CreateResolver().Resolve(doc.Root.Children[0]));
        var run = Assert.IsType<JominiTextVisual>(box.Visual).Lines[0].Runs[0];
        Assert.Equal(new JominiColor(1, 0, 0, 1), run.Color);
        Assert.True(box.Width > 0);
    }

    [Fact]
    public void LayoutHasSpritesWithAnInstall()
    {
        var workspace = GuiWorkspace.Create(Options());
        var doc = GuiParser.Parse("icon = { size = { 20 20 } texture = \"gfx/interface/a.dds\" }");

        var box = workspace.CreateLayoutEngine().Layout(workspace.CreateResolver().Resolve(doc.Root.Children[0]));

        Assert.NotNull(box.Sprite);
    }

    [Fact]
    public void MissingInstallGivesSchematicBoxesWithoutTextureNotes()
    {
        // Spezifikation "Fehlende Installation: schematische Kästen wie heute": keine Sprites, keine Hinweise "nicht gefunden" an jedem Kasten.
        var dds = Path.Combine(_temp, @"mod\in_game\gfx\interface\b.dds");
        Directory.CreateDirectory(Path.GetDirectoryName(dds)!);
        File.Copy(Path.Combine(_temp, @"install\game\in_game\gfx\interface\a.dds"), dds);
        var options = Options() with { InstallDirectory = Path.Combine(_temp, "no-install") };
        var workspace = GuiWorkspace.Create(options);
        var doc = GuiParser.Parse("widget = { size = { 40 40 } icon = { size = { 20 20 } texture = \"gfx/interface/b.dds\" } icon = { size = { 20 20 } texture = \"gfx/interface/none.dds\" } }");

        var box = workspace.CreateLayoutEngine().Layout(workspace.CreateResolver().Resolve(doc.Root.Children[0]));

        Assert.All(box.Descendants().Prepend(box), b => Assert.Null(b.Sprite));
        Assert.All(box.Descendants().Prepend(box), b => Assert.Empty(b.Notes));
    }

    [Fact]
    public void TexticonWithoutSizeTakesTheTextureSizeThroughTheWorkspace()
    {
        // Wie Victoria 3: iconsize nur mit texture und fontsize. Die Texturgröße (a.dds 64×32) kommt über Build/WithIndex in den Textsatz.
        var workspace = GuiWorkspace.Create(Options());
        var updated = workspace.WithModFile(
            Path.Combine(_temp, @"mod\in_game\gui\m.gui"),
            "types M { type m = v { } } texticon = { icon = a iconsize = { texture = \"gfx/interface/a.dds\" fontsize = 15 } }");

        var doc = GuiParser.Parse("textbox = { raw_text = \"@a!\" fontsize = 15 autoresize = yes }");
        var box = updated.CreateLayoutEngine().Layout(updated.CreateResolver().Resolve(doc.Root.Children[0]));

        var visual = Assert.IsType<JominiTextVisual>(box.Visual);
        var icon = Assert.Single(Assert.Single(visual.Lines).Runs);
        Assert.Equal((JominiTextRunKind.Icon, 64.0, 32.0), (icon.Kind, icon.Width, icon.IconHeight));
        Assert.Empty(visual.MissingIcons);
        Assert.Contains(GuiTexts.LayoutTextApproximated("texticon"), box.Notes);
    }

    [Fact]
    public void SavedLocalisationOfModAndDependencyIsReadAgain()
    {
        var yml = Path.Combine(_temp, @"mod\localization\english\m_l_english.yml");
        WriteYml(yml, "PROBE_KEY:0 \"Alt\"");
        var workspace = GuiWorkspace.Create(Options());
        Assert.Equal("Alt", PreviewText(workspace, "PROBE_KEY"));

        // Gespeicherte .yml-Datei der Mod: der Textdienst bekommt eine neue Lokalisierung, die nächste Vorschau zeigt den neuen Text.
        var text = WriteYml(yml, "PROBE_KEY:0 \"Neu\"");
        var updated = workspace.WithModFile(yml, text);
        Assert.NotSame(workspace, updated);
        Assert.NotSame(workspace.Text, updated.Text);
        Assert.Same(workspace.Index, updated.Index);
        Assert.Equal("Neu", PreviewText(updated, "PROBE_KEY"));

        // Ebenso in einer Abhängigkeit; Vanilla und Pfade außerhalb ändern nichts.
        var dependency = Path.Combine(_temp, @"dep\localization\english\d_l_english.yml");
        Assert.NotSame(updated, updated.WithModFile(dependency, WriteYml(dependency, "DEP_KEY:0 \"D\"")));
        var vanilla = Path.Combine(_temp, @"install\game\localization\english\v_l_english.yml");
        Assert.Same(updated, updated.WithModFile(vanilla, WriteYml(vanilla, "V_KEY:0 \"V\"")));
        Assert.Same(updated, updated.WithModFile(Path.Combine(_temp, @"elsewhere\x_l_english.yml"), "l_english:\n"));
    }

    /// <summary>Schreibt eine englische yml-Datei mit BOM und liefert ihren Text.</summary>
    private static string WriteYml(string path, params string[] lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var text = "l_english:\n" + string.Concat(lines.Select(line => " " + line + "\n"));
        File.WriteAllText(path, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return text;
    }

    private static string PreviewText(GuiWorkspace workspace, string key)
    {
        var doc = GuiParser.Parse($"textbox = {{ text = \"{key}\" fontsize = 15 autoresize = yes }}");
        var box = workspace.CreateLayoutEngine().Layout(workspace.CreateResolver().Resolve(doc.Root.Children[0]));
        return string.Concat(Assert.IsType<JominiTextVisual>(box.Visual).Lines.SelectMany(line => line.Runs).Select(run => run.Text));
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
