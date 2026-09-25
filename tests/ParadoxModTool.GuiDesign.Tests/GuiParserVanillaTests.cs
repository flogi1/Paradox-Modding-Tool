using System.Diagnostics;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiParserVanillaTests
{
    private readonly ITestOutputHelper _output;

    public GuiParserVanillaTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void EveryVanillaFileParsesWithNestedSpansAndOnlyBraceDiagnostics()
    {
        var watch = Stopwatch.StartNew();
        var files = VanillaGui.Files();
        foreach (var path in files)
        {
            var doc = GuiParser.Parse(File.ReadAllText(path));
            Assert.All(doc.Diagnostics, d => Assert.True(
                d.Kind is GuiDiagnosticKind.UnexpectedCloseBrace or GuiDiagnosticKind.MissingCloseBrace,
                $"{path}: {d.Message} (Zeile {d.Line})"));
            SpanChecks.AssertNested(doc, path);
            SpanChecks.AssertGapsAreWhitespace(doc, path);
        }

        _output.WriteLine($"{files.Count} Dateien in {watch.ElapsedMilliseconds} ms geprüft");
    }

    [Fact]
    public void TruncatedVanillaFilesNeverThrow()
    {
        var files = VanillaGui.Files();
        var random = new Random(42);
        foreach (var path in files.Where((_, i) => i % 25 == 0))
        {
            var text = File.ReadAllText(path);
            for (var k = 0; k < 20 && text.Length > 0; k++)
            {
                var cut = text[..random.Next(text.Length)];
                var doc = GuiParser.Parse(cut);
                SpanChecks.AssertNested(doc, $"{path} bis {cut.Length}");
            }
        }
    }
}
