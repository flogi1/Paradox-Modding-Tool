using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class LocalizationKeyReaderTests
{
    [Fact]
    public void Reads_keys_with_and_without_version_and_skips_header_and_comments()
    {
        const string text = "﻿l_english:\n # Kommentar\n foo.bar:0 \"Text\"\n baz: \"Anderer\"\n\n  qux:2 \"Mit Zitat\"\n";
        var keys = LocalizationKeyReader.Read(text);
        Assert.Equal(new[] { "foo.bar", "baz", "qux" }, keys.Select(k => k.Key));
        Assert.Equal(new[] { 3, 4, 6 }, keys.Select(k => k.Line));
        Assert.Equal("foo.bar:0 \"Text\"", keys[0].Text);
    }

    [Fact]
    public void Empty_text_gives_no_keys()
    {
        Assert.Empty(LocalizationKeyReader.Read(string.Empty));
    }
}
