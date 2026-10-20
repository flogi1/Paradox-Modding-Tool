using System.Text;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Quellen eines klassischen Spiels (HOI IV, EU IV): Wurzeln samt DLC-Ordnern, nur <c>interface/</c>, Windows-1252, Fingerabdruck mit .gfx.</summary>
public sealed class ClassicSourceSetTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-classic-" + Guid.NewGuid().ToString("N"));

    private static readonly GuiLayout Layout = new(
        Array.Empty<string>(), FirstWins: false, ModsBeforeVanilla: false, Dialect: GuiDialect.Classic,
        VanillaRoots: new[] { "", "integrated_dlc/*", "dlc/*" });

    private static readonly GuiRules Rules = new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Layout);

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private string Install => Path.Combine(_temp, "install");

    private static string Window(string name) => "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"" + name + "\"\n\t}\n}\n";

    private string Write(string relative, string text = "x = 1")
    {
        var path = Path.Combine(_temp, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void ClassicRoots_expand_dlc_folders_in_order()
    {
        Write(@"install\interface\a.gui");
        Write(@"install\integrated_dlc\b\interface\b.gui");
        Write(@"install\integrated_dlc\a\interface\a.gui");
        Write(@"install\dlc\x\interface\x.gui");
        var dep = Path.Combine(_temp, "dep");
        var mod = Path.Combine(_temp, "mod");

        var roots = GuiSourceSet.ClassicRootsFor(Install, Layout.VanillaRoots!, new[] { dep }, mod);

        Assert.Equal(
            new[]
            {
                (Install, GuiLayer.Vanilla, "vanilla"),
                (Path.Combine(Install, "integrated_dlc", "a"), GuiLayer.Vanilla, "a"),
                (Path.Combine(Install, "integrated_dlc", "b"), GuiLayer.Vanilla, "b"),
                (Path.Combine(Install, "dlc", "x"), GuiLayer.Vanilla, "x"),
                (dep, GuiLayer.Dependency, "dep"),
                (mod, GuiLayer.Mod, "mod"),
            },
            roots.Select(r => (r.Directory, r.Layer, r.Label)));
    }

    [Fact]
    public void ClassicRoots_skip_missing_folders()
    {
        Directory.CreateDirectory(Install);

        var roots = GuiSourceSet.ClassicRootsFor(Install, Layout.VanillaRoots!, Array.Empty<string>(), null);

        Assert.Equal(new[] { Install }, roots.Select(r => r.Directory));
    }

    [Fact]
    public void Collect_classic_takes_only_interface_files()
    {
        Write(@"install\interface\a.gui");
        Write(@"install\interface\sub\b.gui");
        Write(@"install\common\c.gui");
        Write(@"install\interface\d.gfx");
        var roots = GuiSourceSet.ClassicRootsFor(Install, new[] { string.Empty }, Array.Empty<string>(), null);

        var gui = GuiSourceSet.Collect(roots, ".gui", "interface");
        var gfx = GuiSourceSet.Collect(roots, ".gfx", "interface");

        Assert.Equal(new[] { "interface/a.gui", "interface/sub/b.gui" }, gui.Select(f => f.RelativePath));
        Assert.Equal(new[] { "interface/d.gfx" }, gfx.Select(f => f.RelativePath));
    }

    [Fact]
    public void Classic_vanilla_path_is_anywhere_under_install()
    {
        var modFile = Path.Combine(_temp, "mod", "interface", "x.gui");

        Assert.True(GuiContexts.IsVanillaPath(Install, Path.Combine(Install, "interface", "x.gui"), Layout));
        Assert.True(GuiContexts.IsVanillaPath(Install, Path.Combine(Install, "integrated_dlc", "a", "interface", "x.gui"), Layout));
        Assert.False(GuiContexts.IsVanillaPath(Install, modFile, Layout));
        Assert.False(GuiContexts.IsVanillaPath(Install, Install + "-mods" + Path.DirectorySeparatorChar + "x.gui", Layout));
        // Jomini bleibt bei game/jomini/clausewitz: interface/ direkt in der Installation ist dort kein Vanilla.
        Assert.False(GuiContexts.IsVanillaPath(Install, Path.Combine(Install, "interface", "x.gui"), GuiLayout.Default));
    }

    [Fact]
    public void Workspace_indexes_classic_interface_with_dlc_and_windows1252()
    {
        Write(@"install\interface\a.gui", Window("main_win"));
        Write(@"install\integrated_dlc\d\interface\d.gui", Window("dlc_win"));
        Write(@"install\common\c.gui", Window("ignored_win"));
        var modGui = Path.Combine(_temp, "mod", "interface", "m.gui");
        Directory.CreateDirectory(Path.GetDirectoryName(modGui)!);
        // Rohes 0xE9 (é in Windows-1252) in einem Eigenschaftswert, nicht in einem Kommentar: nur über GuiTextReader kommt "café" im Index an.
        File.WriteAllBytes(
            modGui,
            Encoding.ASCII.GetBytes("guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"mod_win\"\n\t\ttext = \"caf")
                .Concat(new byte[] { 0xE9 })
                .Concat(Encoding.ASCII.GetBytes("\"\n\t}\n}\n"))
                .ToArray());

        var workspace = GuiWorkspace.Create(new GuiWorkspaceOptions(Install, Path.Combine(_temp, "mod"), Array.Empty<string>(), Rules, null));

        Assert.NotNull(workspace.Index.Find("main_win", GuiDefinitionKind.NamedWidget));
        Assert.Equal("d", workspace.Index.Find("dlc_win", GuiDefinitionKind.NamedWidget)!.File.Label);
        var mod = workspace.Index.Find("mod_win", GuiDefinitionKind.NamedWidget)!;
        Assert.Equal(GuiLayer.Mod, mod.File.Layer);
        Assert.Equal("café", mod.Node.Children.Single(c => c.Name == "text").Value);
        Assert.Null(workspace.Index.Find("ignored_win", GuiDefinitionKind.NamedWidget));
    }

    [Fact]
    public void Vanilla_gfx_change_changes_the_catalog_fingerprint()
    {
        Write(@"install\interface\a.gui");
        var gfx = Write(@"install\interface\a.gfx", "spriteTypes = { }");
        var options = new GuiWorkspaceOptions(Install, null, Array.Empty<string>(), Rules, null);
        var before = GuiWorkspace.Create(options);

        File.WriteAllText(gfx, "spriteTypes = { # geändert }");
        var after = GuiWorkspace.Create(options);

        Assert.NotEqual(before.Catalog.Fingerprint, after.Catalog.Fingerprint);
        Assert.Equal(new[] { "interface/a.gfx" }, after.ClassicGfxFiles.Select(f => f.RelativePath));
    }

    [Fact]
    public void ClassicGfxFiles_survive_a_mod_file_save()
    {
        Write(@"install\interface\a.gui");
        Write(@"install\interface\a.gfx", "spriteTypes = { }");
        var modGui = Write(@"mod\interface\m.gui", "guiTypes = { }");
        var workspace = GuiWorkspace.Create(new GuiWorkspaceOptions(Install, Path.Combine(_temp, "mod"), Array.Empty<string>(), Rules, null));

        var saved = workspace.WithModFile(modGui, "guiTypes = { }");

        Assert.Single(saved.ClassicGfxFiles);
        Assert.Same(workspace.ClassicGfxFiles, saved.ClassicGfxFiles);
        // Eine .gui-Datei außerhalb von interface/ kommt nicht in den klassischen Index.
        Assert.Same(saved, saved.WithModFile(Write(@"mod\other\n.gui", "guiTypes = { }"), "guiTypes = { }"));
    }

    [Fact]
    public async Task ClassicExamples_are_built_in_the_background_and_survive_a_mod_file_save()
    {
        Write(@"install\interface\a.gui", Window("main_win"));
        var modGui = Write(@"mod\interface\m.gui", Window("mod_win"));
        var workspace = GuiWorkspace.Create(new GuiWorkspaceOptions(Install, Path.Combine(_temp, "mod"), Array.Empty<string>(), Rules, null));

        var examples = await workspace.ClassicExamples!;

        Assert.Equal("main_win", Assert.Single(examples.ForElement("containerWindowType")).ElementName);
        var saved = workspace.WithModFile(modGui, Window("mod_win"));
        Assert.Same(workspace.ClassicExamples, saved.ClassicExamples);
    }

    [Fact]
    public void Jomini_workspace_has_no_classic_examples()
    {
        Write(@"install\game\in_game\gui\base.gui", "types V { type v = icon { } }");

        var workspace = GuiWorkspace.Create(new GuiWorkspaceOptions(Install, null, Array.Empty<string>(), GuiResolverTests.Rules, null));

        Assert.Null(workspace.ClassicExamples);
    }

    [Fact]
    public void Jomini_workspace_has_no_classic_gfx_files()
    {
        Write(@"install\game\in_game\gui\base.gui", "types V { type v = icon { } }");

        var workspace = GuiWorkspace.Create(new GuiWorkspaceOptions(Install, null, Array.Empty<string>(), GuiResolverTests.Rules, null));

        Assert.Empty(workspace.ClassicGfxFiles);
    }
}
