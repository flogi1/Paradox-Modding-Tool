using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class ScriptWriterTests
{
    [Fact]
    public void Write_OhneVerbatim_NormalisiertWieBisher()
    {
        var root = ScriptDocument.Parse("key1    =   value1\n");
        var written = ScriptWriter.Write(root);
        Assert.Equal("key1 = value1\n", written);
    }

    [Fact]
    public void Write_MitVerbatim_GibtOriginalRohtextZurueck()
    {
        const string source = "key1    =   value1   # oddly spaced\n";
        var root = ScriptDocument.Parse(source);
        var written = ScriptWriter.Write(root, preferSourceVerbatim: true);
        // Der Rohtext des Statements selbst ("key1    =   value1") bleibt exakt erhalten;
        // der Trailing-Kommentar wird weiterhin generisch (mit genau einem Leerzeichen vor "#")
        // angehängt, da er nicht Teil des Statement-Spans ist (siehe ScriptParser.ParseEntry).
        Assert.Equal("key1    =   value1 # oddly spaced\n", written);
    }

    [Fact]
    public void Write_MitVerbatim_SynthetischerEintragWirdWeiterhinNormalisiert()
    {
        var old = ScriptDocument.Parse("idea = {\n a    =   1\n b = 2\n}\n");
        var newVanilla = ScriptDocument.Parse("idea = {\n a    =   10\n b = 2\n}\n");
        var mod = ScriptDocument.Parse("idea = {\n a    =   1\n b = 20\n}\n");
        var merged = MergeEngine.Merge(old, newVanilla, mod).MergedRoot;

        var written = ScriptWriter.Write(merged, preferSourceVerbatim: true);

        // "idea" selbst ist synthetisch (kein SourceText) -> normalisiert geschrieben,
        // aber "a" kommt unverändert aus newVanilla (eigener Span) -> dessen raues
        // "a    =   10" bleibt erhalten, weil newVanilla dort nicht angetastet wurde.
        Assert.Contains("a    =   10", written);
    }
}
