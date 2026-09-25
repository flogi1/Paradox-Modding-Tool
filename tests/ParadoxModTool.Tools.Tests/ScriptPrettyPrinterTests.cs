using ParadoxModTool.VanillaDiff;
using Xunit;

namespace ParadoxModTool.Tools.Tests;

public class ScriptPrettyPrinterTests
{
    [Fact]
    public void TryFormat_GueltigesSkript_VereinheitlichtEinrueckung()
    {
        var input = "outer = { inner = yes other = \"value\" }";

        var result = ScriptPrettyPrinter.TryFormat(input);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("outer = {\n    inner = yes\n    other = \"value\"\n}\n", result.FormattedText);
    }

    [Fact]
    public void TryFormat_NichtGeschlosseneKlammer_LiefertFehlerOhneFormattedText()
    {
        var input = "outer = { inner = yes";

        var result = ScriptPrettyPrinter.TryFormat(input);

        Assert.False(result.Success);
        Assert.Null(result.FormattedText);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    /// <summary>
    /// Regressionstest für den in Runde 7 entdeckten und behobenen Datenverlust-Bug in
    /// <see cref="ScriptParser"/> (siehe <see cref="ScriptCommentEntry"/>-Klassenkommentar):
    /// ein Kommentar ganz am Ende eines Blocks, ohne nachfolgenden Eintrag, darf beim
    /// Formatieren NICHT verloren gehen.
    /// </summary>
    [Fact]
    public void TryFormat_HaengenderKommentarAmBlockende_BleibtErhalten()
    {
        var input = "outer = {\n    inner = yes\n    # haengender Kommentar\n}\n";

        var result = ScriptPrettyPrinter.TryFormat(input);

        Assert.True(result.Success);
        Assert.Equal("outer = {\n    inner = yes\n    # haengender Kommentar\n}\n", result.FormattedText);
    }

    [Fact]
    public void TryFormat_LeeresSkript_LiefertLeeresErgebnis()
    {
        var result = ScriptPrettyPrinter.TryFormat(string.Empty);

        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.FormattedText);
    }
}
