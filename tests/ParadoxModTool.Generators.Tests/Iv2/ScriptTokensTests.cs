using Xunit;

namespace ParadoxModTool.Generators.Tests.Iv2;

/// <summary>Vergleichshilfen für den IV2-Abgleich: Token-Folge ohne Leerraum und Kommentare, Lokalisierung als Schlüssel → Wert.</summary>
public sealed class ScriptTokensTests
{
    [Fact]
    public void Tokens_ignore_whitespace_and_comments_and_keep_quoted_strings() =>
        Assert.Equal(new[] { "a", "=", "{", "b", "=", "\"x y # z\"", "}" }, ScriptTokens.Of("a = { b = \"x y # z\" } # c\n"));

    [Fact]
    public void Tokens_split_around_braces_and_equals_without_spaces() =>
        Assert.Equal(new[] { "K", "=", "{", "log", "=", "yes", "}" }, ScriptTokens.Of("K={\nlog=yes\n}"));

    [Fact]
    public void Yml_map_maps_pmt_escaping_to_python_style()
    {
        Assert.Equal("a 'b' c", YmlMap.Of("l_english:\n k: \"a \\\"b\\\"\\nc\"\n", pythonStyle: false)["k"]);
        Assert.Equal("a 'b' c", YmlMap.Of("l_english:\n k: \"a 'b' c\"\n # Kommentar\n", pythonStyle: true)["k"]);
    }

    [Fact]
    public void Bundle_reads_files_with_paths()
    {
        var path = Path.Combine(Path.GetTempPath(), "pmt-bundle-" + Guid.NewGuid().ToString("N") + ".bundle");
        File.WriteAllText(path, "##FILE a/b c.txt\nx = 1\r\n##FILE d.yml\nl_english:\n k: \"v\"\n");
        try
        {
            var files = Bundle.Read(path);
            Assert.Equal(new[] { "a/b c.txt", "d.yml" }, files.Keys);
            Assert.Equal("x = 1\n", files["a/b c.txt"]);
            Assert.Equal("l_english:\n k: \"v\"\n", files["d.yml"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Yml_map_keeps_paradox_references() =>
        Assert.Equal("$x$ $iv2_level_2$", YmlMap.Of(" k_1: \"$x$ $iv2_level_2$\"\n", pythonStyle: true)["k_1"]);
}
