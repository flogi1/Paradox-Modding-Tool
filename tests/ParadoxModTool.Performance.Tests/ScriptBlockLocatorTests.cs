using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class ScriptBlockLocatorTests
{
    private const string Two =
        "# Kopf\r\n" +
        "first = {\r\n" +
        "\ta = 1\r\n" +
        "}\r\n" +
        "\r\n" +
        "iv2_x = {\r\n" +
        "\tif = {\r\n" +
        "\t\tlimit = {\r\n" +
        "\t\t\tb = 2\r\n" +
        "\t\t}\r\n" +
        "\t}\r\n" +
        "}\r\n";

    [Fact]
    public void LocateFindsTopLevelKeyAndRange()
    {
        var info = ScriptBlockLocator.Locate(Two, 9);

        Assert.NotNull(info);
        Assert.Equal("iv2_x", info!.TopLevelKey);
        Assert.Equal(6, info.StartLine);
        Assert.Equal(12, info.EndLine);
        var expected = Two.Substring(Two.IndexOf("iv2_x", StringComparison.Ordinal));
        Assert.Equal(expected.TrimEnd('\r', '\n'), info.BlockText);
    }

    [Fact]
    public void LocateFindsTheFirstBlock()
    {
        var info = ScriptBlockLocator.Locate(Two, 3);

        Assert.Equal("first", info!.TopLevelKey);
        Assert.Equal(2, info.StartLine);
        Assert.Equal(4, info.EndLine);
        Assert.Equal(new[] { "first" }, info.KeyPath);
    }

    [Fact]
    public void LocateReturnsKeyPathToInnermostBlock()
    {
        var info = ScriptBlockLocator.Locate(Two, 9);

        Assert.Equal(new[] { "iv2_x", "if", "limit" }, info!.KeyPath);
        Assert.Equal(new[] { "iv2_x", "if" }, ScriptBlockLocator.Locate(Two, 7)!.KeyPath);
    }

    [Fact]
    public void LocateIsNullOutsideBlocks()
    {
        Assert.Null(ScriptBlockLocator.Locate(Two, 5));
        Assert.Null(ScriptBlockLocator.Locate(Two, 1));
        Assert.Null(ScriptBlockLocator.Locate(Two, 99));
        Assert.Null(ScriptBlockLocator.Locate(Two, 0));
        Assert.Null(ScriptBlockLocator.Locate(string.Empty, 1));
    }

    [Fact]
    public void LocateFindsABlockOpenedAfterIndentation()
    {
        var info = ScriptBlockLocator.Locate("  indented = {\n    x = 1\n  }\n", 1);

        Assert.Equal("indented", info!.TopLevelKey);
        Assert.Equal(1, info.StartLine);
        Assert.Equal(3, info.EndLine);
    }

    [Fact]
    public void LocateToleratesUnclosedBlock()
    {
        const string text = "ok = {\n\ta = 1\n}\nbroken = {\n\tif = {\n\t\tb = 2\n";

        var inBroken = ScriptBlockLocator.Locate(text, 6);
        var inOk = ScriptBlockLocator.Locate(text, 2);

        Assert.Equal("ok", inOk!.TopLevelKey);
        if (inBroken is not null)
        {
            Assert.Equal("broken", inBroken.TopLevelKey);
            Assert.True(inBroken.EndLine >= inBroken.StartLine);
        }

        Assert.Null(Record.Exception(() => ScriptBlockLocator.Locate("}}} a = { { {", 1)));
    }

    [Fact]
    public void LineIndexRoundTrips()
    {
        var index = new ScriptLineIndex("ab\r\ncde\r\n\r\nf");

        Assert.Equal(4, index.LineCount);
        for (var line = 1; line <= index.LineCount; line++)
        {
            Assert.Equal(line, index.LineOf(index.StartOf(line)));
        }

        Assert.Equal(0, index.StartOf(1));
        Assert.Equal(2, index.EndOf(1));
        Assert.Equal(4, index.StartOf(2));
        Assert.Equal(7, index.EndOf(2));
        Assert.Equal(index.StartOf(3), index.EndOf(3));
        Assert.Equal(12, index.EndOf(4));
        Assert.Equal(2, index.LineOf(6));
    }
}
