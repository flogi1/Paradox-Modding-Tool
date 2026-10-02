using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class LineMarksTests
{
    [Fact]
    public void First_and_last_follow_the_drag_in_either_direction()
    {
        var marks = new LineMarks(7, 3);

        Assert.Equal(3, marks.First);
        Assert.Equal(7, marks.Last);
        Assert.True(marks.Contains(5));
        Assert.True(marks.Contains(3));
        Assert.False(marks.Contains(8));
    }

    [Fact]
    public void A_click_marks_one_line()
    {
        var marks = new LineMarks(4, 4);

        Assert.Equal(4, marks.First);
        Assert.Equal(4, marks.Last);
        Assert.Equal(1, marks.Count);
    }
}
