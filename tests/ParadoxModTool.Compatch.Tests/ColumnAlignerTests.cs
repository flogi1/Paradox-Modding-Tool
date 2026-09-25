using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class ColumnAlignerTests
{
    [Fact]
    public void Identical_texts_are_all_same()
    {
        var rows = ColumnAligner.Align("a\nb\n", "a\nb\n", "a\nb\n", null);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(RowOrigin.Same, r.Origin));
        Assert.Null(rows[0].Vanilla);
    }

    [Fact]
    public void Lines_are_classified_by_origin_and_gaps_align()
    {
        var a = "farm = {\n\tcost = 2\n\ttime = 1\n}";
        var b = "farm = {\n\tcost = 1\n\ttime = 2\n\textra = yes\n}";
        var result = "farm = {\n\tcost = 2\n\ttime = 2\n\textra = yes\n}";

        var rows = ColumnAligner.Align(a, result, b, "farm = {\n\tcost = 1\n\ttime = 1\n}");

        Assert.Equal(RowOrigin.Same, rows[0].Origin);
        Assert.Contains(rows, r => r.Result == "\tcost = 2" && r.Origin == RowOrigin.FromA && r.B is null);
        Assert.Contains(rows, r => r.Result == "\ttime = 2" && r.Origin == RowOrigin.FromB);
        Assert.Contains(rows, r => r.Result == "\textra = yes" && r.Origin == RowOrigin.FromB && r.A is null);
        Assert.Contains(rows, r => r.Result is null && r.Origin == RowOrigin.Gap && r.B == "\tcost = 1");
        Assert.Equal("}", rows[^1].Result);
    }

    [Fact]
    public void Manual_lines_are_other_and_indentation_is_ignored_for_matching()
    {
        var rows = ColumnAligner.Align("x = 1", "  x = 1\nmine = 5", "x = 1", null);

        Assert.Equal(RowOrigin.Same, rows[0].Origin);
        Assert.Equal("  x = 1", rows[0].Result);
        Assert.Equal(RowOrigin.Other, rows[1].Origin);
    }

    [Fact]
    public void Empty_inputs_give_no_rows()
    {
        Assert.Empty(ColumnAligner.Align(string.Empty, string.Empty, string.Empty, string.Empty));
        Assert.Single(ColumnAligner.Align("a", string.Empty, string.Empty, null));
    }
}
