using Xunit;

namespace ParadoxModTool.Tools.Tests;

public class WhitespaceCleanerTests
{
    [Fact]
    public void Clean_EntferntLeerzeichenUndTabsAmZeilenende()
    {
        var input = "foo = bar   \nbaz = qux\t\t\n";

        var result = WhitespaceCleaner.Clean(input);

        Assert.Equal("foo = bar\nbaz = qux\n", result);
    }

    [Fact]
    public void Clean_LaesstEinzelneLeerzeileUnangetastet()
    {
        // Nutzer-Feedback / Konzept: "Absätze von einer Zeile ... sollten nicht entfernt werden."
        var input = "block_a = { }\n\nblock_b = { }\n";

        var result = WhitespaceCleaner.Clean(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void Clean_ReduziertMehrereAufeinanderfolgendeLeerzeilenAufEine()
    {
        var input = "block_a = { }\n\n\n\nblock_b = { }\n";

        var result = WhitespaceCleaner.Clean(input);

        Assert.Equal("block_a = { }\n\nblock_b = { }\n", result);
    }

    [Fact]
    public void Clean_LaesstMehrfacheLeerzeichenInnerhalbEinerZeileUnangetastet()
    {
        // Bewusst NICHT bereinigt - dient oft der Ausrichtung von "=" untereinander.
        var input = "foo        = bar\nbazzzz     = qux\n";

        var result = WhitespaceCleaner.Clean(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void Clean_LaesstFuehrendeEinrueckungUnangetastet()
    {
        var input = "outer = {\n\tinner = yes   \n}\n";

        var result = WhitespaceCleaner.Clean(input);

        Assert.Equal("outer = {\n\tinner = yes\n}\n", result);
    }

    [Fact]
    public void Clean_OhneAbschliessendenZeilenumbruch_FuegtKeinenHinzu()
    {
        var input = "foo = bar   ";

        var result = WhitespaceCleaner.Clean(input);

        Assert.Equal("foo = bar", result);
    }

    [Fact]
    public void Clean_MitCrLf_BehaeltCrLfBei()
    {
        var input = "foo = bar   \r\n\r\n\r\n\r\nbaz = qux\r\n";

        var result = WhitespaceCleaner.Clean(input);

        Assert.Equal("foo = bar\r\n\r\nbaz = qux\r\n", result);
    }

    [Fact]
    public void Clean_LeererText_GibtLeerenTextZurueck()
    {
        Assert.Equal(string.Empty, WhitespaceCleaner.Clean(string.Empty));
    }
}
