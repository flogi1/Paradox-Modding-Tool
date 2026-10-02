using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

/// <summary>Zeilen, die im Compare und Update Tool nebeneinanderstehen: jede Seite mit ihren Lücken, Zeile für Zeile gepaart.</summary>
public class DiffRowsTests
{
    [Fact]
    public void Without_gaps_lines_pair_one_to_one()
    {
        var rows = DiffRows.Build(3, 3, Array.Empty<AlignmentGap>());

        Assert.Equal(new[] { new DiffRow(1, 1), new DiffRow(2, 2), new DiffRow(3, 3) }, rows);
    }

    [Fact]
    public void A_gap_on_the_right_pushes_its_lines_down()
    {
        var rows = DiffRows.Build(4, 2, new[] { new AlignmentGap(DiffSide.Right, 2, 2) });

        Assert.Equal(new[] { new DiffRow(1, 1), new DiffRow(2, null), new DiffRow(3, null), new DiffRow(4, 2) }, rows);
    }

    [Fact]
    public void A_gap_before_line_one_starts_with_empty_rows()
    {
        var rows = DiffRows.Build(1, 3, new[] { new AlignmentGap(DiffSide.Left, 1, 2) });

        Assert.Equal(new[] { new DiffRow(null, 1), new DiffRow(null, 2), new DiffRow(1, 3) }, rows);
    }

    [Fact]
    public void The_shorter_side_ends_with_empty_rows()
    {
        var rows = DiffRows.Build(3, 5, Array.Empty<AlignmentGap>());

        Assert.Equal(new DiffRow(null, 4), rows[3]);
        Assert.Equal(new DiffRow(null, 5), rows[4]);
        Assert.Equal(5, rows.Count);
    }
}
