using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

/// <summary>Markierte Zeilen einer Seite in die andere übernehmen (Compare und Update Tool, Taste U).</summary>
public class LineTakeTests
{
    private static IReadOnlyList<DiffRow> Rows(string left, string right, params AlignmentGap[] gaps) =>
        DiffRows.Build(Count(left), Count(right), gaps);

    private static int Count(string text) => text.TrimEnd('\n').Split('\n').Length;

    [Fact]
    public void Replaces_marked_lines()
    {
        const string left = "a\nb\nc\n";
        const string right = "a\nX\nc\n";

        Assert.Equal("a\nb\nc\n", LineTake.Apply(Rows(left, right), left, right, DiffSide.Left, 2, 2));
    }

    [Fact]
    public void Only_the_marked_lines_change()
    {
        const string left = "a\nb\nc\n";
        const string right = "X\nY\nZ\n";

        Assert.Equal("X\nb\nZ\n", LineTake.Apply(Rows(left, right), left, right, DiffSide.Left, 2, 2));
    }

    [Fact]
    public void Range_crossing_a_target_gap_inserts_at_the_row()
    {
        const string left = "a\nb\nc\nd\n";
        const string right = "a\nd\n";
        var rows = Rows(left, right, new AlignmentGap(DiffSide.Right, 2, 2));

        Assert.Equal("a\nb\nc\nd\n", LineTake.Apply(rows, left, right, DiffSide.Left, 2, 3));
    }

    [Fact]
    public void Source_gap_removes_target_line()
    {
        const string left = "a\nb\nc\nd\n";
        const string right = "a\nd\n";
        var rows = Rows(left, right, new AlignmentGap(DiffSide.Right, 2, 2));

        Assert.Equal("a\nd\n", LineTake.Apply(rows, right, left, DiffSide.Right, 1, 2));
    }

    [Fact]
    public void Marked_lines_in_reverse_order()
    {
        const string left = "a\nb\nc\n";
        const string right = "x\ny\nz\n";

        Assert.Equal(
            LineTake.Apply(Rows(left, right), left, right, DiffSide.Left, 1, 3),
            LineTake.Apply(Rows(left, right), left, right, DiffSide.Left, 3, 1));
    }

    [Fact]
    public void Rows_past_the_target_end_append()
    {
        const string left = "a\nb\nc\nd\n";
        const string right = "a\nb\n";

        Assert.Equal("a\nb\nc\nd\n", LineTake.Apply(Rows(left, right), left, right, DiffSide.Left, 3, 4));
    }

    [Fact]
    public void First_and_last_line_of_the_file()
    {
        const string left = "A\nb\nC\n";
        const string right = "a\nb\nc\n";

        Assert.Equal("A\nb\nc\n", LineTake.Apply(Rows(left, right), left, right, DiffSide.Left, 1, 1));
        Assert.Equal("a\nb\nC\n", LineTake.Apply(Rows(left, right), left, right, DiffSide.Left, 3, 3));
    }

    [Fact]
    public void Keeps_crlf_and_trailing_newline_of_the_target()
    {
        const string left = "a\nB\nc\n";
        const string right = "a\r\nb\r\nc\r\n";

        Assert.Equal("a\r\nB\r\nc\r\n", LineTake.Apply(Rows(left, right), left, right, DiffSide.Left, 2, 2));
    }

    [Fact]
    public void Keeps_missing_trailing_newline_of_the_target()
    {
        const string left = "a\nB\nc\n";
        const string right = "a\nb\nc";

        Assert.Equal("a\nB\nc", LineTake.Apply(Rows(left, right), left, right, DiffSide.Left, 2, 2));
    }
}
