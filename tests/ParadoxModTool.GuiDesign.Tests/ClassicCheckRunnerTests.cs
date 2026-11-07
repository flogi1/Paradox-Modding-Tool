using ParadoxModTool.GuiDesign.Checks;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Prüfungen der klassischen GUI (Plan 3, Task 2): Läufer und die Befunde aus den Layout-Hinweisen.</summary>
public sealed class ClassicCheckRunnerTests : IDisposable
{
    private readonly ClassicLayoutKit _kit = new();
    private readonly string _install = Path.Combine(Path.GetTempPath(), "pmt-classiccheck-" + Guid.NewGuid().ToString("N"));
    private GuiWorkspace? _workspace;

    public void Dispose()
    {
        _kit.Dispose();
        if (Directory.Exists(_install))
        {
            Directory.Delete(_install, recursive: true);
        }
    }

    private static string Window(string body) =>
        "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"win\"\n\t\tsize = { width = 200 height = 100 }\n" + body + "\n\t}\n}\n";

    private const string Path_ = @"C:\mod\interface\edit.gui";

    /// <summary>Legt das erste Fenster wie <see cref="ClassicLayoutKit.Layout"/>, behält aber das Dokument für die Knoten der Befunde.</summary>
    private ClassicCheckContext Context(string gui, bool isModFile = true, bool overflow = false, LayoutOptions? options = null)
    {
        var rules = ClassicLayoutKit.Rules("hoi4");
        var document = GuiParser.Parse(gui);
        var index = GuiIndex.FromDocuments(Array.Empty<GuiSourceFile>(), Array.Empty<GuiDocument>(), rules.Layout);
        var resolver = new GuiResolver(index, rules, document, Path_);
        var guiTypes = document.Root.Children.First(c => string.Equals(c.Name, "guiTypes", StringComparison.OrdinalIgnoreCase));
        var window = guiTypes.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock);
        var box = new ClassicLayoutEngine(_kit.Assets()).Layout(resolver.Resolve(window), options);
        var scene = new GuiSchematicScene(box, node => ReferenceEquals(node.Root, document.Root));
        return new ClassicCheckContext(Workspace(rules), document, Path_, scene, isModFile, overflow);
    }

    private GuiWorkspace Workspace(GuiRules rules)
    {
        if (_workspace is null)
        {
            Directory.CreateDirectory(Path.Combine(_install, "interface"));
            _workspace = GuiWorkspace.Create(new GuiWorkspaceOptions(_install, null, Array.Empty<string>(), rules, null));
        }

        return _workspace;
    }

    private static IReadOnlyList<GuiFinding> Run(ClassicCheckContext context) =>
        ClassicCheckRunner.Run(context, new IClassicCheck[] { new LayoutFindingsCheck() }, CancellationToken.None);

    private static GuiNode Named(ClassicCheckContext context, string name) =>
        context.Document.Root.Descendants().First(n => n.Kind == GuiNodeKind.KeyedBlock
            && n.Children.Any(c => string.Equals(c.Name, "name", StringComparison.OrdinalIgnoreCase) && c.Value?.Trim('"') == name));

    private const string Broken =
        "\t\ticonType = { name = \"nosprite\" spriteType = \"GFX_not_defined\" }\n"
        + "\t\ticonType = { name = \"badanchor\" spriteType = \"GFX_icon\" orientation = \"SIDEWAYS\" }\n";

    [Fact]
    public void Run_NotModFile_Empty()
    {
        var context = Context(Window(Broken), isModFile: false);

        Assert.Empty(Run(context));
    }

    [Fact]
    public void LayoutFindings_MapsNotes()
    {
        var context = Context(Window(Broken));

        var findings = Run(context);

        Assert.Equal(2, findings.Count);
        var error = findings[0];
        Assert.Equal(GuiFindingKind.SpriteMissing, error.Kind);
        Assert.Equal(GuiFindingSeverity.Error, error.Severity);
        Assert.Same(Named(context, "nosprite"), error.Node);
        Assert.Equal(Path_, error.FilePath);
        Assert.Equal(Named(context, "nosprite").Line, error.Line);
        Assert.Equal(GuiTexts.ClassicSpriteMissing("GFX_not_defined"), error.Message);

        var warning = findings[1];
        Assert.Equal(GuiFindingKind.AnchorUnknown, warning.Kind);
        Assert.Equal(GuiFindingSeverity.Warning, warning.Severity);
        Assert.Same(Named(context, "badanchor"), warning.Node);
    }

    [Fact]
    public void LayoutFindings_ApproximationsAreNotFindings()
    {
        // Rollbalken (nur angenähert), Container ohne Größe (aus den Kindern geschätzt), UI-Skalierung ≠ 1: Hinweise, aber keine Befunde.
        var context = Context(
            Window("\t\tscrollbarType = { name = \"bar\" }\n\t\tcontainerWindowType = { name = \"inner\" iconType = { name = \"i\" spriteType = \"GFX_icon\" } }\n"),
            options: new LayoutOptions { UiScale = 1.25 });

        Assert.NotEmpty(context.Scene!.Boxes.SelectMany(b => b.Box.Notes));
        Assert.Empty(Run(context));
    }

    [Fact]
    public void LayoutFindings_OverflowOnlyWhenEnabled()
    {
        const string body = "\t\ticonType = { name = \"out\" spriteType = \"GFX_icon\" position = { x = 190 y = 0 } }\n";

        Assert.Empty(Run(Context(Window(body), overflow: false)));

        var context = Context(Window(body), overflow: true);
        var finding = Assert.Single(Run(context));
        Assert.Equal(GuiFindingKind.Overflow, finding.Kind);
        Assert.Equal(GuiFindingSeverity.Hint, finding.Severity);
        Assert.Same(Named(context, "out"), finding.Node);
    }

    /// <summary>Wirft beim Aufzählen (nach einem ersten Befund), wie eine Prüfung mit yield.</summary>
    private sealed class ThrowingCheck : IClassicCheck
    {
        public IEnumerable<GuiFinding> Run(ClassicCheckContext context, CancellationToken cancellation)
        {
            yield return new GuiFinding(GuiFindingKind.FontMissing, GuiFindingSeverity.Error, "half", null, context.DocumentPath, 1);
            throw new InvalidOperationException("boom");
        }
    }

    [Fact]
    public void Run_ThrowingCheck_IsIsolated()
    {
        var context = Context(Window(Broken));

        var findings = ClassicCheckRunner.Run(context, new IClassicCheck[] { new ThrowingCheck(), new LayoutFindingsCheck() }, CancellationToken.None);

        Assert.Equal(3, findings.Count);
        Assert.Equal(GuiFindingKind.SpriteMissing, findings[0].Kind);
        Assert.Equal(GuiFindingKind.AnchorUnknown, findings[1].Kind);
        var failed = findings[2];
        Assert.Equal(GuiFindingKind.CheckFailed, failed.Kind);
        Assert.Equal(GuiFindingSeverity.Hint, failed.Severity);
        Assert.Null(failed.Node);
        Assert.Equal(GuiTexts.FindingCheckFailed(nameof(ThrowingCheck), "boom"), failed.Message);
        Assert.DoesNotContain(findings, f => f.Message == "half");
    }

    [Fact]
    public void Run_Cancelled_Throws()
    {
        var context = Context(Window(Broken));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => ClassicCheckRunner.Run(context, new IClassicCheck[] { new LayoutFindingsCheck() }, cancelled.Token));
    }

    [Fact]
    public void Run_SortsBySeverityThenLine()
    {
        var context = Context(Window(
            "\t\ticonType = { name = \"a\" spriteType = \"GFX_icon\" orientation = \"SIDEWAYS\" }\n"
            + "\t\ticonType = { name = \"b\" spriteType = \"GFX_not_defined\" }\n"
            + "\t\ticonType = { name = \"c\" spriteType = \"GFX_also_missing\" }\n"));

        var findings = Run(context);

        Assert.Equal(new[] { "b", "c", "a" }, findings.Select(f => f.Node!.Children.First(c => c.Name == "name").Value!.Trim('"')));
    }
}
