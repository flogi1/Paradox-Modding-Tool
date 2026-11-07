using ParadoxModTool.ReferenceTree;
using Xunit;

namespace ParadoxModTool.ReferenceTree.Tests;

public class ReferenceQueryTests
{
    [Theory]
    [InlineData("\ttrigger_event = iv2.5", 18, null, "iv2.5")]
    [InlineData("\ttrigger_event = { id = flavor_fra.1 }", 26, null, "flavor_fra.1")]
    [InlineData("\tlimit = { var:my_var > 1 }", 14, null, "my_var")]
    [InlineData("\thas_flag = flag:my_flag", 18, null, "my_flag")]
    [InlineData("\tscope:actor.var:x = 1", 18, null, "x")]
    [InlineData("\tmy_fx = yes", 3, null, "my_fx")]
    [InlineData("anything", 0, "iv2.5", "iv2.5")]
    public void Picks(string line, int column, string? selection, string expected) =>
        Assert.Equal(expected, ReferenceQuery.FromEditor(line, column, selection));

    [Theory]
    [InlineData("\tset_variable = $NAME$", 18, null)]
    [InlineData("\t = ", 2, null)]
    [InlineData("", 0, null)]
    [InlineData("x", 0, "*")]
    public void NoName(string line, int column, string? selection) =>
        Assert.Null(ReferenceQuery.FromEditor(line, column, selection));
}
