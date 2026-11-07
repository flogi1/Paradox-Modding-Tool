using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Checks;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Prüfungen der klassischen GUI (Plan 3, Task 3): verlierende Definitionen, fehlende Lokalisierung, replace_path auf Unterordner.</summary>
public sealed class ClassicDefinitionChecksTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-classicdef-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Install => Path.Combine(_root, "install");

    private string Mod => Path.Combine(_root, "mod");

    private static string Window(string name, string body = "") =>
        "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"" + name + "\"\n\t\tsize = { width = 20 height = 10 }\n" + body + "\t}\n}\n";

    private static string Sprite(string name) =>
        "spriteTypes = {\n\tspriteType = { name = \"" + name + "\" texturefile = \"gfx/interface/x.dds\" }\n}\n";

    private string Put(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>Legt die Dateien an, baut den Arbeitsbereich und prüft die Datei <paramref name="open"/> (Pfad relativ zur Mod bzw. zur Installation).</summary>
    private IReadOnlyList<GuiFinding> Check(string game, IClassicCheck check, string open, bool openIsMod = true, string? descriptor = null)
    {
        if (descriptor is not null)
        {
            Put(Mod, "descriptor.mod", descriptor);
        }

        Directory.CreateDirectory(Install);
        Directory.CreateDirectory(Mod);
        var rules = ClassicLayoutKit.Rules(game);
        var workspace = GuiWorkspace.Create(new GuiWorkspaceOptions(Install, Mod, Array.Empty<string>(), rules, null));
        var path = Path.Combine(openIsMod ? Mod : Install, open.Replace('/', Path.DirectorySeparatorChar));
        var document = GuiParser.Parse(File.ReadAllText(path));
        var context = new ClassicCheckContext(workspace, document, path, null, openIsMod, false);
        return ClassicCheckRunner.Run(context, new[] { check }, CancellationToken.None);
    }

    [Fact]
    public void LosesToOther_Hoi4_LaterVanillaFileWins()
    {
        Put(Install, "interface/zz_vanilla.gui", Window("topbar"));
        Put(Mod, "interface/00_x.gui", Window("topbar"));

        var finding = Assert.Single(Check("hoi4", new LosesToOtherCheck(), "interface/00_x.gui"));

        Assert.Equal(GuiFindingKind.LosesToOther, finding.Kind);
        Assert.Equal(GuiFindingSeverity.Warning, finding.Severity);
        Assert.Contains("zz_vanilla.gui", finding.Message);
        Assert.Equal(GuiTexts.FindingWindowLoses("topbar", "interface/zz_vanilla.gui", "vanilla", false), finding.Message);
        Assert.NotNull(finding.Node);
        Assert.Equal(2, finding.Line);
    }

    [Fact]
    public void LosesToOther_Eu4_FirstWins()
    {
        Put(Install, "interface/a_vanilla.gui", Window("topbar"));
        Put(Mod, "interface/z_x.gui", Window("topbar"));

        var finding = Assert.Single(Check("eu4", new LosesToOtherCheck(), "interface/z_x.gui"));

        Assert.Contains("a_vanilla.gui", finding.Message);
        Assert.Equal(GuiTexts.FindingWindowLoses("topbar", "interface/a_vanilla.gui", "vanilla", true), finding.Message);
    }

    [Fact]
    public void LosesToOther_SpriteLastWins()
    {
        Put(Install, "interface/zz_vanilla.gfx", Sprite("GFX_a"));
        Put(Mod, "interface/00_x.gfx", Sprite("GFX_a"));

        var finding = Assert.Single(Check("hoi4", new LosesToOtherCheck(), "interface/00_x.gfx"));

        Assert.Equal(GuiFindingKind.LosesToOther, finding.Kind);
        Assert.Contains("zz_vanilla.gfx", finding.Message);
        Assert.Contains("GFX_a", finding.Message);
    }

    [Fact]
    public void LosesToOther_WinnerIsMod_NoFinding()
    {
        Put(Install, "interface/00_vanilla.gui", Window("topbar"));
        Put(Mod, "interface/zz_x.gui", Window("topbar"));
        Put(Install, "interface/00_vanilla.gfx", Sprite("GFX_a"));
        Put(Mod, "interface/zz_x.gfx", Sprite("GFX_a"));

        Assert.Empty(Check("hoi4", new LosesToOtherCheck(), "interface/zz_x.gui"));
        Assert.Empty(Check("hoi4", new LosesToOtherCheck(), "interface/zz_x.gfx"));
    }

    [Fact]
    public void Check_ModFilesOnly()
    {
        Put(Install, "interface/zz_vanilla.gui", Window("topbar", "\t\ttext = \"MISSING_KEY\"\n"));
        Put(Install, "interface/00_vanilla.gui", Window("topbar"));

        Assert.Empty(Check("hoi4", new LosesToOtherCheck(), "interface/00_vanilla.gui", openIsMod: false));
        Assert.Empty(Check("hoi4", new LocalisationCheck(), "interface/zz_vanilla.gui", openIsMod: false));
    }

    private static string Loc(string key, string value = "x") => "l_english:\n " + key + ":0 \"" + value + "\"\n";

    [Fact]
    public void Localisation_MissingKey()
    {
        Put(Mod, "localisation/a_l_english.yml", Loc("KNOWN"));
        var gui = Window(
            "w",
            "\t\ttext = \"MISSING_KEY\"\n\t\tbuttonText = \"KNOWN\"\n\t\tTooltip = \"MISSING_TIP\"\n\t\tdelayedTooltipText = \"MISSING_DELAYED\"\n");
        Put(Mod, "interface/a.gui", gui);

        var findings = Check("hoi4", new LocalisationCheck(), "interface/a.gui");

        Assert.Equal(3, findings.Count);
        Assert.All(findings, f =>
        {
            Assert.Equal(GuiFindingKind.LocalisationMissing, f.Kind);
            Assert.Equal(GuiFindingSeverity.Warning, f.Severity);
            Assert.NotNull(f.Node);
        });
        Assert.Contains(findings, f => f.Message.Contains("MISSING_KEY"));
        Assert.Contains(findings, f => f.Message.Contains("MISSING_TIP"));
        Assert.Contains(findings, f => f.Message.Contains("MISSING_DELAYED"));
        Assert.DoesNotContain(findings, f => f.Message.Contains("KNOWN"));
    }

    [Fact]
    public void Localisation_PlainTextIgnored()
    {
        Put(Mod, "interface/a.gui", Window("w", "\t\ttext = \"Hello world\"\n\t\ttooltip = \"\"\n\t\tpdx_tooltip = \"\"\n"));

        Assert.Empty(Check("hoi4", new LocalisationCheck(), "interface/a.gui"));
    }

    [Fact]
    public void Localisation_BindingIgnored()
    {
        Put(Mod, "interface/a.gui", Window("w", "\t\ttext = \"[?var]\"\n\t\ttooltip = \"[GetName]\"\n\t\ttooltipText = \"@FOO\"\n"));

        Assert.Empty(Check("hoi4", new LocalisationCheck(), "interface/a.gui"));
    }

    [Fact]
    public void ReplacePath_SubfolderError()
    {
        Put(Mod, "interface/sub/a.gui", Window("w"));
        Put(Mod, "interface/other/b.gui", Window("w2"));
        Put(Mod, "interface/c.gui", Window("w3"));
        const string descriptor = "name=\"x\"\nreplace_path=\"interface/sub\"\n";

        var finding = Assert.Single(Check("eu4", new ReplacePathCheck(), "interface/sub/a.gui", descriptor: descriptor));
        Assert.Equal(GuiFindingKind.ReplacePathSubfolder, finding.Kind);
        Assert.Equal(GuiFindingSeverity.Error, finding.Severity);
        Assert.Contains("interface/sub", finding.Message);

        Assert.Empty(Check("eu4", new ReplacePathCheck(), "interface/other/b.gui"));
        Assert.Empty(Check("eu4", new ReplacePathCheck(), "interface/c.gui"));
        Assert.Empty(Check("hoi4", new ReplacePathCheck(), "interface/sub/a.gui"));
    }

    [Fact]
    public void Localisation_NonKeysIgnored()
    {
        Put(Mod, "interface/a.gui", Window("w", "\t\ttext = \"XXX\"\n\t\ttext = \"100%\"\n\t\ttext = \"1936\"\n\t\ttext = \"A_$B$\"\n\t\ttext = \"§GA_B§!\"\n\t\ttext = \"£A_B\"\n"));

        Assert.Empty(Check("hoi4", new LocalisationCheck(), "interface/a.gui"));
    }

    [Fact]
    public void Localisation_CaseInsensitiveFallback()
    {
        Put(Mod, "localisation/a_l_english.yml", Loc("Known_Key"));
        Put(Mod, "interface/a.gui", Window("w", "\t\ttext = \"KNOWN_KEY\"\n"));

        Assert.Empty(Check("hoi4", new LocalisationCheck(), "interface/a.gui"));
    }

    [Fact]
    public void Localisation_VanillaGuiKeyIsKnown()
    {
        Put(Install, "interface/v.gui", Window("v", "\t\ttext = \"CLOSE_TITLE\"\n"));
        Put(Mod, "interface/a.gui", Window("w", "\t\ttext = \"CLOSE_TITLE\"\n\t\ttext = \"OTHER_KEY\"\n"));

        var finding = Assert.Single(Check("hoi4", new LocalisationCheck(), "interface/a.gui"));
        Assert.Contains("OTHER_KEY", finding.Message);
    }
}

/// <summary>Anti-Lärm: Die Vanilla-Dateien selbst als Mod geprüft ergeben keinen Befund zur Lokalisierung (HOI IV und EU IV; fehlt das Spiel, kehrt der Test zurück).</summary>
[Collection(nameof(RealClassicGuiTests))]
public sealed class ClassicLocalisationVanillaTests
{
    private readonly RealClassicWorkspaces _workspaces;

    public ClassicLocalisationVanillaTests(RealClassicWorkspaces workspaces) => _workspaces = workspaces;

    [Theory]
    [InlineData("hoi4")]
    [InlineData("eu4")]
    public void Vanilla_as_mod_has_no_localisation_findings(string game)
    {
        var built = game == "hoi4" ? _workspaces.Hoi4 : _workspaces.Eu4;
        if (built is null)
        {
            return;
        }

        var workspace = built.Workspace;
        var findings = new List<GuiFinding>();
        var scanned = 0;
        foreach (var file in workspace.Index.Files.Where(f => f.RelativePath.StartsWith("interface/", StringComparison.OrdinalIgnoreCase)))
        {
            var context = new ClassicCheckContext(workspace, workspace.Index.DocumentOf(file), file.FullPath, null, true, false);
            findings.AddRange(new LocalisationCheck().Run(context, CancellationToken.None));
            scanned++;
        }

        Assert.True(scanned > 100, "zu wenige Dateien: " + scanned);
        Assert.True(findings.Count == 0, string.Join("\n", findings.Take(20).Select(f => f.Message)));
    }
}
