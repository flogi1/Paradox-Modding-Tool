using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public sealed class FlagQueryTests
{
    [Theory]
    [InlineData("scope:actor.var:iv2_x", "iv2_x", "iv2_x")]
    [InlineData("flag:X_y", "X_y", "X_y")]
    [InlineData("unlocked_policy_$type$", "type", "unlocked_policy_*")]
    [InlineData("[Country.MakeScope.Var('iv2_var_idea_cost_adm').GetValue]", "idea_cost", "iv2_var_idea_cost_adm")]
    [InlineData("$A$_$B$", "_", "*_*")]
    [InlineData("add_to_list = global_var:my_list", "my_list", "my_list")]
    public void FromEditor_WordAtColumn(string line, string inside, string expected)
    {
        var column = line.IndexOf(inside, StringComparison.Ordinal) + 1;

        Assert.Equal(expected, FlagQuery.FromEditor(line, column, null));
    }

    [Fact]
    public void FromEditor_SelectionWinsOverWord()
    {
        Assert.Equal("my_flag", FlagQuery.FromEditor("other_word here", 2, " my_flag "));
    }

    [Theory]
    [InlineData("Var('x')", "x")]
    [InlineData("GetVariable(\"abc\")", "abc")]
    [InlineData("'quoted'", "quoted")]
    public void FromEditor_SelectionWrapperIsStripped(string selection, string expected)
    {
        Assert.Equal(expected, FlagQuery.FromEditor("", 0, selection));
    }

    [Fact]
    public void FromEditor_ColumnOnWhitespace_IsNull()
    {
        Assert.Null(FlagQuery.FromEditor("a  b", 2, null));
        Assert.Null(FlagQuery.FromEditor("", 0, null));
        Assert.Null(FlagQuery.FromEditor("x = y", 2, "   "));
    }

    [Fact]
    public void FromEditor_MultiLineSelectionIsIgnored()
    {
        Assert.Equal("word", FlagQuery.FromEditor("word", 1, "a\nb"));
    }

    [Theory]
    [InlineData("name = my_flag }", 14, "my_flag")]
    [InlineData("name = my_flag", 14, "my_flag")]
    [InlineData("name = my_flag  x", 15, null)]
    public void FromEditor_CaretRightAfterWord(string line, int column, string? expected)
    {
        Assert.Equal(expected, FlagQuery.FromEditor(line, column, null));
    }

    [Theory]
    [InlineData("$PARAM$")]
    [InlineData("$A$$B$")]
    public void FromEditor_OnlyPlaceholders_IsNull(string line)
    {
        Assert.Null(FlagQuery.FromEditor(line, 2, null));
    }

    [Theory]
    [InlineData("add_flag = my_flag")]
    [InlineData("a b")]
    [InlineData("Var('a b')")]
    public void FromEditor_SelectionWithWhitespaceOrEquals_FallsBackToWord(string selection)
    {
        Assert.Equal("word", FlagQuery.FromEditor("word", 1, selection));
    }
}
