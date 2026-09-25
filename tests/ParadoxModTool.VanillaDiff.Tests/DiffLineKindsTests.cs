using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class DiffLineKindsTests
{
    private static Func<int, int> LineOf(string text) =>
        offset => 1 + text.Take(Math.Clamp(offset, 0, text.Length)).Count(c => c == '\n');

    private static (Dictionary<int, DiffKind> Left, Dictionary<int, DiffKind> Right) Kinds(string left, string right)
    {
        var diffs = ScriptDiffer.Diff(ScriptDocument.Parse(left), ScriptDocument.Parse(right));
        return (
            DiffLineKinds.Compute(diffs, useLeftSide: true, LineOf(left)),
            DiffLineKinds.Compute(diffs, useLeftSide: false, LineOf(right)));
    }

    [Fact]
    public void GeaenderterBlock_FaerbtNurDieGeaendertenKinder_NichtDieKopfzeile()
    {
        var (left, right) = Kinds(
            "effect = {\n a = 1\n b = 2\n}\n",
            "effect = {\n a = 1\n b = 3\n}\n");

        Assert.False(left.ContainsKey(1));
        Assert.False(right.ContainsKey(1));
        Assert.Equal(DiffKind.Modified, left[3]);
        Assert.Equal(DiffKind.Modified, right[3]);
    }

    [Fact]
    public void VerschachtelterGeaenderterBlock_LaesstAlleKopfzeilenUnberuehrt()
    {
        var (left, right) = Kinds(
            "effect = {\n scope:actor = {\n  add_gold = 1\n }\n}\n",
            "effect = {\n scope:actor = {\n  add_gold = 2\n }\n}\n");

        Assert.False(right.ContainsKey(1));
        Assert.False(right.ContainsKey(2));
        Assert.Equal(DiffKind.Modified, right[3]);
        Assert.Single(left);
    }

    [Fact]
    public void BlockMitGeaendertemOperator_FaerbtDieKopfzeile()
    {
        var (left, right) = Kinds("a = {\n x = 1\n}\n", "a >= {\n x = 1\n y = 2\n}\n");

        Assert.Equal(DiffKind.Modified, left[1]);
        Assert.Equal(DiffKind.Modified, right[1]);
        Assert.Equal(DiffKind.Added, right[3]);
    }

    [Fact]
    public void HinzugefuegterUndEntfernterBlock_WerdenUeberDieGanzeSpanneGefaerbt()
    {
        var (left, right) = Kinds("a = 1\nb = {\n x = 1\n}\n", "a = 1\n");

        Assert.Equal(DiffKind.Removed, left[2]);
        Assert.Equal(DiffKind.Removed, left[3]);
        Assert.Equal(DiffKind.Removed, left[4]);
        Assert.Empty(right);
    }

    [Fact]
    public void GeaenderterEinzelwert_FaerbtSeineZeile()
    {
        var (left, right) = Kinds("a = 1\n", "a = 2\n");

        Assert.Equal(DiffKind.Modified, left[1]);
        Assert.Equal(DiffKind.Modified, right[1]);
    }
}
