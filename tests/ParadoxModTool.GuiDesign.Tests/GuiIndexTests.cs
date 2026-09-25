using System.Diagnostics;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiIndexTests
{
    private readonly ITestOutputHelper _output;

    public GuiIndexTests(ITestOutputHelper output) => _output = output;

    internal static GuiSourceFile F(string relativePath, GuiLayer layer = GuiLayer.Vanilla) =>
        new($@"C:\test\{layer}\{relativePath.Replace('/', '\\')}", relativePath, layer, layer.ToString(), 0, 0);

    internal static GuiIndex Build(params (GuiSourceFile File, string Text)[] files) =>
        GuiIndex.Build(files.Select(f => f.File).ToList(), file => files.First(f => f.File == file).Text);

    [Fact]
    public void FindsTypesTemplatesAndNamedWidgets()
    {
        var index = Build((F("in_game/gui/a.gui"),
            "types Buttons {\n\ttype my_button = button {\n\t\tblock \"text\" { }\n\t\tblock \"icon\" { }\n\t}\n}\n" +
            "template my_template { size = { 1 2 } }\n" +
            "window = {\n\tname = \"main_window\"\n}\n"));

        var type = index.Find("my_button", GuiDefinitionKind.Type)!;
        Assert.Equal("button", type.BaseName);
        Assert.Equal("Buttons", type.TypesContainer);
        Assert.Equal(new[] { "text", "icon" }, type.Blocks);
        Assert.Equal(2, type.Line);

        var template = index.Find("my_template", GuiDefinitionKind.Template)!;
        Assert.True(template.IsTopLevel);

        var window = index.Find("main_window", GuiDefinitionKind.NamedWidget)!;
        Assert.Equal("window", window.WidgetKey);
        Assert.True(window.IsTopLevel);
        Assert.Null(index.Find("my_button", GuiDefinitionKind.Template));
    }

    [Fact]
    public void ModWinsOverDependencyWinsOverVanilla()
    {
        var vanilla = (F("in_game/gui/a.gui"), "types V { type x = icon { } }");
        var dependency = (F("in_game/gui/b.gui", GuiLayer.Dependency), "types D { type x = button { } }");
        var mod = (F("in_game/gui/c.gui", GuiLayer.Mod), "types M { type x = widget { } }");

        Assert.Equal("widget", Build(vanilla, dependency, mod).Find("x", GuiDefinitionKind.Type)!.BaseName);
        Assert.Equal("button", Build(vanilla, dependency).Find("x", GuiDefinitionKind.Type)!.BaseName);
        Assert.Equal("widget", Build(mod, dependency, vanilla).Find("x", GuiDefinitionKind.Type)!.BaseName);
    }

    [Fact]
    public void LaterFileWinsWithinOneLayer()
    {
        var index = Build(
            (F("in_game/gui/a.gui"), "types A { type x = icon { } }"),
            (F("in_game/gui/b.gui"), "types B { type x = button { } }"));

        Assert.Equal("button", index.Find("x", GuiDefinitionKind.Type)!.BaseName);
        Assert.Equal(2, index.FindAll("x", GuiDefinitionKind.Type).Count);
    }

    [Fact]
    public void ModDuplicatesListsOnlyNamesDefinedTwiceInTheMod()
    {
        var index = Build(
            (F("in_game/gui/v1.gui"), "types A { type y = icon { } }"),
            (F("in_game/gui/v2.gui"), "types B { type y = icon { } }"),
            (F("in_game/gui/m1.gui", GuiLayer.Mod), "types C { type x = icon { } }\nwidget = { name = \"n\" }"),
            (F("in_game/gui/m2.gui", GuiLayer.Mod), "types D { type x = button { } }\nwidget = { name = \"n\" }"));

        var group = Assert.Single(index.ModDuplicates);
        Assert.All(group, d => Assert.Equal("x", d.Name));
        Assert.Equal(2, group.Count);
    }

    [Fact]
    public void UsagesOfFindsWidgetKeysAndUsingReferences()
    {
        var index = Build((F("in_game/gui/a.gui"),
            "types A { type my_button = button { using = my_template } }\n" +
            "window = {\n\tmy_button = { }\n\thbox = { my_button = { } }\n}\n"));

        var usages = index.UsagesOf("my_button");
        Assert.Equal(2, usages.Count);
        Assert.All(usages, u => Assert.Equal(GuiUsageKind.Widget, u.Kind));
        Assert.Equal(GuiUsageKind.Using, Assert.Single(index.UsagesOf("my_template")).Kind);
        Assert.Empty(index.UsagesOf("does_not_exist"));
    }

    [Fact]
    public void FileOfAndDocumentOfMapNodesBackToTheirFile()
    {
        var file = F("in_game/gui/a.gui");
        var index = Build((file, "types A { type x = icon { size = { 1 2 } } }"));
        var node = index.Find("x", GuiDefinitionKind.Type)!.Node.Children[0];

        Assert.Equal(file, index.FileOf(node));
        Assert.Same(index.DocumentOf(file), index.DocumentOf(node));
        Assert.Null(index.FileOf(GuiParser.Parse("a = 1").Root));
    }

    [Fact]
    public void WithFileReplacesAnExistingFileAndAddsANewOne()
    {
        var vanilla = F("in_game/gui/a.gui");
        var index = Build((vanilla, "types A { type x = icon { } }"));

        var mod = F("in_game/gui/m.gui", GuiLayer.Mod);
        var added = index.WithFile(mod, "types M { type x = button { } }");
        Assert.Equal("button", added.Find("x", GuiDefinitionKind.Type)!.BaseName);

        var replaced = added.WithFile(mod, "types M { type z = widget { } }");
        Assert.Equal("icon", replaced.Find("x", GuiDefinitionKind.Type)!.BaseName);
        Assert.NotNull(replaced.Find("z", GuiDefinitionKind.Type));
        Assert.Equal(2, replaced.Files.Count);
    }

    [Fact]
    public void DiagnosticsCarryTheirFile()
    {
        var file = F("in_game/gui/broken.gui");
        var index = Build((file, "a = { }\n}\n"));

        var diagnostic = Assert.Single(index.Diagnostics);
        Assert.Equal(file, diagnostic.File);
        Assert.Equal(GuiDiagnosticKind.UnexpectedCloseBrace, diagnostic.Diagnostic.Kind);
    }

    [Fact]
    public void CollectSkipsSnapshotsAndDotFoldersInMods()
    {
        var temp = Path.Combine(Path.GetTempPath(), "pmt-gui-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var relative in new[]
            {
                @"install\game\in_game\gui\b.gui",
                @"install\jomini\main_menu\gui\j.gui",
                @"mod\in_game\gui\a.gui",
                @"mod\snapshots\in_game\gui\a.gui",
                @"mod\.git\x.gui",
            })
            {
                var path = Path.Combine(temp, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "a = 1");
            }

            var roots = GuiSourceSet.RootsFor(Path.Combine(temp, "install"), Array.Empty<string>(), Path.Combine(temp, "mod"));
            var files = GuiSourceSet.Collect(roots);

            Assert.Equal(
                new[] { ("main_menu/gui/j.gui", GuiLayer.Vanilla), ("in_game/gui/b.gui", GuiLayer.Vanilla), ("in_game/gui/a.gui", GuiLayer.Mod) },
                files.Select(f => (f.RelativePath, f.Layer)));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void FingerprintChangesWhenAFileChanges()
    {
        var a = new GuiSourceFile(@"C:\a.gui", "a.gui", GuiLayer.Vanilla, "game", 10, 100);
        var b = a with { Size = 11 };

        Assert.Equal(GuiSourceSet.ComputeFingerprint(new[] { a }), GuiSourceSet.ComputeFingerprint(new[] { a }));
        Assert.NotEqual(GuiSourceSet.ComputeFingerprint(new[] { a }), GuiSourceSet.ComputeFingerprint(new[] { b }));
    }

    [Fact]
    public void BuildsIndexOverVanilla()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var watch = Stopwatch.StartNew();
        var files = GuiSourceSet.Collect(GuiSourceSet.RootsFor(VanillaGui.InstallDirectory, Array.Empty<string>(), null));
        var index = GuiIndex.Build(files);
        _output.WriteLine($"{files.Count} Dateien, {index.Definitions.Count()} Definitionen in {watch.ElapsedMilliseconds} ms");

        Assert.True(index.Definitions.Count(d => d.Kind == GuiDefinitionKind.Type) > 1000);
        Assert.True(index.Definitions.Count(d => d.Kind == GuiDefinitionKind.Template) > 1000);
        Assert.Equal("textbox", index.Find("text_single", GuiDefinitionKind.Type)!.BaseName);
        Assert.Empty(index.ModDuplicates);
        Assert.All(index.Diagnostics, d => Assert.True(d.Diagnostic.Kind is GuiDiagnosticKind.UnexpectedCloseBrace or GuiDiagnosticKind.MissingCloseBrace));

        watch.Restart();
        var modFile = new GuiSourceFile(@"C:\mod\in_game\gui\x.gui", "in_game/gui/x.gui", GuiLayer.Mod, "mod", 0, 0);
        var withMod = index.WithFile(modFile, "types M { type x = widget { } }");
        _output.WriteLine($"WithFile (eine Mod-Datei speichern) in {watch.ElapsedMilliseconds} ms");
        Assert.NotNull(withMod.Find("x", GuiDefinitionKind.Type));
    }
}
