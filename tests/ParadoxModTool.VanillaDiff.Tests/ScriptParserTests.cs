using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class ScriptParserTests
{
    private const string Sample =
        "# leading comment for key1\n" +
        "key1 = value1\n" +
        "nested = {\n" +
        "    inner_a = 10\n" +
        "    inner_b = \"a string\" # trailing on inner_b\n" +
        "}\n" +
        "list_block = { alpha beta gamma }\n" +
        "threshold >= 5\n";

    [Fact]
    public void Parse_LiestFlacheUndVerschachtelteAnweisungenKorrekt()
    {
        var root = ScriptDocument.Parse(Sample);

        Assert.Equal(4, root.Entries.Count);

        var key1 = Assert.IsType<ScriptStatement>(root.Entries[0]);
        Assert.Equal("key1", key1.Key);
        Assert.Equal(ScriptOperator.Equals, key1.Operator);
        Assert.Single(key1.LeadingComments);
        Assert.Equal("leading comment for key1", key1.LeadingComments[0]);
        var key1Value = Assert.IsType<ScriptScalar>(key1.Value);
        Assert.Equal("value1", key1Value.Text);
        Assert.False(key1Value.IsQuoted);

        var nested = Assert.IsType<ScriptStatement>(root.Entries[1]);
        Assert.Equal("nested", nested.Key);
        var nestedBlock = Assert.IsType<ScriptBlock>(nested.Value);
        Assert.Equal(2, nestedBlock.Entries.Count);

        var innerA = Assert.IsType<ScriptStatement>(nestedBlock.Entries[0]);
        Assert.Equal("inner_a", innerA.Key);
        Assert.Equal("10", Assert.IsType<ScriptScalar>(innerA.Value).Text);

        var innerB = Assert.IsType<ScriptStatement>(nestedBlock.Entries[1]);
        Assert.Equal("inner_b", innerB.Key);
        var innerBValue = Assert.IsType<ScriptScalar>(innerB.Value);
        Assert.Equal("a string", innerBValue.Text);
        Assert.True(innerBValue.IsQuoted);
        Assert.Equal("trailing on inner_b", innerB.TrailingComment);
    }

    [Fact]
    public void Parse_ListenBlockOhneSchluessel_WirdAlsListItemsGeparst()
    {
        var root = ScriptDocument.Parse(Sample);

        var listBlockStmt = Assert.IsType<ScriptStatement>(root.Entries[2]);
        var listBlock = Assert.IsType<ScriptBlock>(listBlockStmt.Value);
        Assert.Equal(3, listBlock.Entries.Count);

        var texts = listBlock.Entries
            .Select(e => Assert.IsType<ScriptScalar>(Assert.IsType<ScriptListItem>(e).Value).Text)
            .ToList();

        Assert.Equal(new[] { "alpha", "beta", "gamma" }, texts);
    }

    [Fact]
    public void Parse_VergleichsOperator_WirdErkannt()
    {
        var root = ScriptDocument.Parse(Sample);

        var threshold = Assert.IsType<ScriptStatement>(root.Entries[3]);
        Assert.Equal("threshold", threshold.Key);
        Assert.Equal(ScriptOperator.GreaterOrEqual, threshold.Operator);
        Assert.Equal("5", Assert.IsType<ScriptScalar>(threshold.Value).Text);
    }

    [Fact]
    public void Parse_NichtGeschlosseneKlammer_WirftScriptParseException()
    {
        Assert.Throws<ScriptParseException>(() => ScriptDocument.Parse("nested = {\n a = 1\n"));
    }

    [Fact]
    public void Parse_UnerwarteteSchliessendeKlammer_WirftScriptParseException()
    {
        Assert.Throws<ScriptParseException>(() => ScriptDocument.Parse("a = 1\n}\n"));
    }

    [Fact]
    public void WriteUndErneutParsen_IstStrukturellGleichwertig()
    {
        var root = ScriptDocument.Parse(Sample);
        var written = ScriptWriter.Write(root);
        var reparsed = ScriptDocument.Parse(written);

        Assert.True(ScriptStructuralComparer.ValueEquals(root, reparsed));
    }

    [Fact]
    public void Parse_UnterschiedlicheFormatierungGleicherInhalt_IstStrukturellGleich()
    {
        var compact = ScriptDocument.Parse("idea={category=adm cost=100}\n");
        var spaced = ScriptDocument.Parse("idea = {\n    category = adm\n    cost = 100\n}\n");

        Assert.True(ScriptStructuralComparer.ValueEquals(compact, spaced));
    }

    [Fact]
    public void Parse_SetztSourceSpanProAnweisung()
    {
        const string source = "key1 = value1\nnested = {\n    inner_a = 10\n}\n";
        var root = ScriptDocument.Parse(source);

        var key1 = Assert.IsType<ScriptStatement>(root.Entries[0]);
        Assert.NotNull(key1.SourceText);
        Assert.Equal(source, key1.SourceText);
        Assert.Equal("key1 = value1", source.Substring(key1.SourceStart!.Value, key1.SourceEnd!.Value - key1.SourceStart!.Value));

        var nested = Assert.IsType<ScriptStatement>(root.Entries[1]);
        var expectedNestedSlice = "nested = {\n    inner_a = 10\n}";
        Assert.Equal(expectedNestedSlice, source.Substring(nested.SourceStart!.Value, nested.SourceEnd!.Value - nested.SourceStart!.Value));
    }

    [Fact]
    public void Merge_SyntheticStatement_HatKeinenSourceSpan()
    {
        var old = ScriptDocument.Parse("idea = {\n a = 1\n b = 2\n}\n");
        var newVanilla = ScriptDocument.Parse("idea = {\n a = 10\n b = 2\n}\n");
        var mod = ScriptDocument.Parse("idea = {\n a = 1\n b = 20\n}\n");

        var result = MergeEngine.Merge(old, newVanilla, mod);

        var ideaStmt = result.MergedRoot.Entries.OfType<ScriptStatement>().First(s => s.Key == "idea");
        // "idea" is rebuilt as a new ScriptStatement wrapping the recursively-merged nested
        // block (see MergeEngine.HandleBothChanged) - it has no single coherent source slice.
        Assert.Null(ideaStmt.SourceText);
    }
}
