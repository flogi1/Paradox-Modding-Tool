using ParadoxModTool.GuiDesign.Checks;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.GuiDesign.Text;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Befunde der Jomini-Vorschau (<see cref="JominiFindings"/>) und die Arten, die die Jomini-Engine ihren Fehlerhinweisen mitgibt.</summary>
public sealed class JominiFindingsTests : IClassFixture<JominiEngineTextFixture>, IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-jominifindings-" + Guid.NewGuid().ToString("N"));
    private readonly JominiEngineTextFixture _text;
    private readonly GuiTextureLocator _textures;

    public JominiFindingsTests(JominiEngineTextFixture text)
    {
        _text = text;
        var gfx = Path.Combine(_temp, "install", "game", "gfx");
        TestDds.WriteBgra(Path.Combine(gfx, "strip.dds"), 120, 40);
        _textures = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(Path.Combine(_temp, "install"), Array.Empty<string>(), null, Array.Empty<string>()));
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private LayoutBox Layout(string gui, string language = "l_english")
    {
        var (resolver, doc) = GuiResolverTests.Setup(gui);
        var element = resolver.Resolve(doc.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock));
        return new GuiLayoutEngine(GuiResolverTests.Rules, _textures.SizeOf, _text.Service, _textures).Layout(element, new LayoutOptions { Language = language });
    }

    private static GuiSchematicScene Scene(LayoutBox root) => new(root, _ => true);

    private static IReadOnlyList<GuiFinding> Collect(LayoutBox root, bool overflow = false) =>
        JominiFindings.Collect(Scene(root), overflow, CancellationToken.None);

    private static ResolvedElement Resolved(string key)
    {
        var (resolver, doc) = GuiResolverTests.Setup(key + " = { }");
        return resolver.Resolve(doc.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock));
    }

    private static LayoutBox Plain(string key = "widget") => new()
    {
        Element = Resolved(key),
    };

    [Fact]
    public void Collect_TaggedNoteIsError()
    {
        var box = Plain();
        box.Notes.Add("kaputt");
        box.NoteKinds.Add(new LayoutNoteKind(GuiFindingKind.TextureMissing, "kaputt"));

        var finding = Assert.Single(Collect(box));

        Assert.Equal(GuiFindingKind.TextureMissing, finding.Kind);
        Assert.Equal(GuiFindingSeverity.Error, finding.Severity);
        Assert.Equal("kaputt", finding.Message);
        Assert.NotNull(finding.Node);
    }

    [Fact]
    public void Collect_UntaggedNoteIsHint()
    {
        var box = Plain();
        box.Notes.Add("x");

        var finding = Assert.Single(Collect(box));

        Assert.Equal(GuiFindingKind.PreviewNote, finding.Kind);
        Assert.Equal(GuiFindingSeverity.Hint, finding.Severity);
        Assert.Equal("x", finding.Message);
    }

    [Fact]
    public void Collect_OverflowOnlyWhenEnabled()
    {
        var box = Plain();
        box.OutsideParent = true;

        Assert.Empty(Collect(box));
        var finding = Assert.Single(Collect(box, overflow: true));
        Assert.Equal(GuiFindingKind.Overflow, finding.Kind);
        Assert.Equal(GuiFindingSeverity.Hint, finding.Severity);
        Assert.Equal(GuiTexts.FindingOutsideParent, finding.Message);
    }

    [Fact]
    public void Collect_RepeatedListSamplesOnce()
    {
        var root = Plain();
        var element = Resolved("icon");
        for (var i = 0; i < 3; i++)
        {
            var sample = new LayoutBox { Element = element, RepeatIndex = i };
            sample.Notes.Add("kaputt");
            sample.NoteKinds.Add(new LayoutNoteKind(GuiFindingKind.FrameOutOfRange, "kaputt"));
            root.Children.Add(sample);
        }

        Assert.Single(Collect(root));
    }

    [Fact]
    public void Collect_ErrorsBeforeHints()
    {
        var root = Plain();
        root.Notes.Add("hint");
        var child = new LayoutBox { Element = Resolved("icon") };
        child.Notes.Add("err");
        child.NoteKinds.Add(new LayoutNoteKind(GuiFindingKind.GameError, "err"));
        root.Children.Add(child);

        Assert.Equal(new[] { GuiFindingSeverity.Error, GuiFindingSeverity.Hint }, Collect(root).Select(f => f.Severity));
    }

    [Fact]
    public void Producer_missing_texture_is_tagged()
    {
        var box = Layout("icon = { texture = \"gfx/none.dds\" }");

        Assert.Contains(new LayoutNoteKind(GuiFindingKind.TextureMissing, GuiTexts.LayoutTextureMissing("gfx/none.dds")), box.NoteKinds);
        Assert.Contains(Collect(box), f => f.Kind == GuiFindingKind.TextureMissing && f.Severity == GuiFindingSeverity.Error);
    }

    [Fact]
    public void Producer_unknown_font_is_tagged()
    {
        var box = Layout("textbox = { raw_text = \"Probe\" font = \"NoSuchFont\" autoresize = yes }");

        Assert.Contains(new LayoutNoteKind(GuiFindingKind.FontMissing, GuiTexts.LayoutTextFontUnknown("NoSuchFont")), box.NoteKinds);
    }

    [Fact]
    public void Producer_missing_loc_key_is_tagged()
    {
        var box = Layout("textbox = { text = \"NO_SUCH_KEY\" font = \"Arial\" fontsize = 15 autoresize = yes }");

        Assert.Contains(new LayoutNoteKind(GuiFindingKind.LocalisationMissing, GuiTexts.LayoutTextKeyMissing("NO_SUCH_KEY")), box.NoteKinds);
    }

    [Fact]
    public void Producer_frame_out_of_range_is_tagged()
    {
        var box = Layout("icon = { texture = \"gfx/strip.dds\" framesize = { 40 40 } frame = 9 }");

        Assert.Contains(new LayoutNoteKind(GuiFindingKind.FrameOutOfRange, GuiTexts.LayoutFrameOutOfRange(9, 3)), box.NoteKinds);
    }

    [Fact]
    public void Producer_game_error_is_tagged_and_approximations_are_not()
    {
        var box = Layout("hbox = { widget = { size = { 10 10 } parentanchor = center } }");

        Assert.Contains(box.Children[0].NoteKinds, k => k.Kind == GuiFindingKind.GameError);
        Assert.All(Collect(box).Where(f => f.Kind == GuiFindingKind.PreviewNote), f => Assert.DoesNotContain(f.Message, box.Children[0].NoteKinds.Select(k => k.Text)));
    }
}
