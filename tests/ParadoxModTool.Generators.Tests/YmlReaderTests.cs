using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Lokalisierung lesen: Schlüssel → Wert, Kopfzeile und Kommentare übersprungen, Paradox-Maskierung zurückgenommen.</summary>
public sealed class YmlReaderTests
{
    [Fact]
    public void Values_reads_keys_with_and_without_version_and_skips_header_and_comments()
    {
        var values = YmlReader.Values("﻿l_english:\r\n # Kommentar\r\n a_1: \"Eins\"\r\n b_2:0 \"Zwei\" # Notiz\r\n\r\n c: \"\"\r\n");

        Assert.Equal(new Dictionary<string, string> { ["a_1"] = "Eins", ["b_2"] = "Zwei", ["c"] = string.Empty }, values);
    }

    [Fact]
    public void Unescape_takes_back_quotes_line_breaks_and_backslashes() =>
        Assert.Equal("a \"b\"\nc\\d", YmlReader.Unescape("a \\\"b\\\"\\nc\\\\d"));

    [Fact]
    public void Values_keep_escaped_quotes_inside() =>
        Assert.Equal("a \\\"b\\\"", YmlReader.Values(" k: \"a \\\"b\\\"\"\n")["k"]);
}
