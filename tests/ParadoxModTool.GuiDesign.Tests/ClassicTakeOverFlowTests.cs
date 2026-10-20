using System.Text;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// "In Mod übernehmen" für EU IV von Anfang bis Ende, wie die App es macht: Windows-1252-Vanilla-Datei lesen, Entwurf beginnen, ein Fenster ändern,
/// planen, mit <see cref="ClassicOverridePlanner.WriteEncoding"/> schreiben und den Index neu aufbauen.
/// </summary>
public sealed class ClassicTakeOverFlowTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-classic-flow-" + Guid.NewGuid().ToString("N"));

    private static readonly GameProfile Profile = GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "eu4.json"));

    public ClassicTakeOverFlowTests()
    {
        // é als rohes 0xE9 (Windows-1252) in einem Kommentar im Fenster, wie in EU IV end_of_combat.gui.
        var text = "guiTypes = {\n\twindowType = {\n\t\tname = \"end_of_combat\"\n\t\t# Kampfende é\n\t\tposition = { x = 0 y = 0 }\n\t}\n"
            + "\twindowType = {\n\t\tname = \"other_win\"\n\t\tposition = { x = 5 y = 5 }\n\t}\n}\n";
        var path = Path.Combine(Install, "interface", "end_of_combat.gui");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        File.WriteAllBytes(path, Encoding.GetEncoding(1252).GetBytes(text));
        Directory.CreateDirectory(ModRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private string Install => Path.Combine(_temp, "install");

    private string ModRoot => Path.Combine(_temp, "mod");

    private string OverridePath => Path.Combine(ModRoot, "interface", "00_mod_overrides.gui");

    private GuiWorkspace Build() =>
        GuiWorkspace.Create(new GuiWorkspaceOptions(Install, ModRoot, Array.Empty<string>(), GuiRules.FromProfile(Profile), null));

    /// <summary>Beginnt den Entwurf wie <c>StartGuiDraft</c>, ändert ihn und schreibt den Plan wie <c>ApplyGuiPlan</c>.</summary>
    private (string Start, GuiOverridePlan Plan) TakeOver(Func<string, string> edit)
    {
        var workspace = Build();
        var source = workspace.Index.Files.Single(f => f.Layer == GuiLayer.Vanilla && f.RelativePath == "interface/end_of_combat.gui");
        var (vanillaText, encodingName) = GuiTextReader.Read(source.FullPath);
        var start = GuiDraft.Start(source, vanillaText, workspace.Index).Text;
        var targets = GuiModTargets.For(ModRoot, null, null, workspace.Index.Layout.FirstWins, classic: true);
        var plan = GuiOverridePlanner.Plan(new GuiOverrideRequest(
            source,
            vanillaText,
            edit(start),
            workspace.Index,
            targets,
            file => File.Exists(file) ? GuiTextReader.Read(file).Text : null,
            start)
        {
            SourceEncoding = encodingName,
        });

        Assert.True(plan.CanApply);
        var encodings = plan.Files.Select(file => ClassicOverridePlanner.WriteEncoding(file, Profile)).ToList();
        for (var i = 0; i < plan.Files.Count; i++)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(plan.Files[i].FullPath)!);
            File.WriteAllText(plan.Files[i].FullPath, plan.Files[i].NewText, encodings[i]);
        }

        return (start, plan);
    }

    private static string MoveEndOfCombat(string text) => text.Replace("x = 0 y = 0", "x = 10 y = 0");

    [Fact]
    public void Draft_reads_windows1252_source()
    {
        var (start, _) = TakeOver(MoveEndOfCombat);

        Assert.Contains("Kampfende é", start);
    }

    [Fact]
    public void Written_override_is_minimal()
    {
        TakeOver(MoveEndOfCombat);

        var written = Directory.GetFiles(ModRoot, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(ModRoot, f).Replace('\\', '/'));
        Assert.Equal(new[] { "interface/00_mod_overrides.gui" }, written);
        var (text, encodingName) = GuiTextReader.Read(OverridePath);
        Assert.Equal(new[] { "end_of_combat" }, ClassicWindows.Of(GuiParser.Parse(text)).Select(ClassicWindows.NameOf));
        Assert.Contains("x = 10 y = 0", text);

        // Nicht-ASCII aus einer Windows-1252-Quelle: Die neue Datei bleibt Windows-1252, das é kommt unverändert zurück.
        Assert.Contains("Kampfende é", text);
        Assert.Equal(GuiTextReader.Windows1252, encodingName);
    }

    [Fact]
    public void Rebuilt_workspace_finds_mod_window_as_winner()
    {
        TakeOver(MoveEndOfCombat);

        var winner = ClassicWindows.Winner(Build().Index, "end_of_combat")!;

        Assert.Equal(GuiLayer.Mod, winner.File.Layer);
        Assert.Equal("interface/00_mod_overrides.gui", winner.File.RelativePath);
    }

    [Fact]
    public void New_ascii_file_is_utf8_without_bom()
    {
        TakeOver(text => text.Replace("x = 5 y = 5", "x = 6 y = 5"));

        // EU IV-Profil: neue .gui-Dateien als UTF-8 ohne BOM, wie Vanilla und die gemessenen Probe-Mods.
        var bytes = File.ReadAllBytes(OverridePath);
        Assert.False(ModFileEncoding.HasUtf8Bom(bytes));
        Assert.Equal(GuiTextReader.Utf8, GuiTextReader.Read(OverridePath).EncodingName);
    }
}
