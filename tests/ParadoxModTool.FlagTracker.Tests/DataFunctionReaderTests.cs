using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public class DataFunctionReaderTests
{
    [Fact]
    public void Gui_VarCall()
    {
        var scan = DataFunctionReader.Read("[Country.MakeScope.Var('iv2_var_idea_cost_adm').GetValue]");

        var site = Assert.Single(scan.Sites);
        Assert.Equal("iv2_var_idea_cost_adm", site.Name);
        Assert.Equal(FlagKind.Variable, site.Kind);
        Assert.Equal(FlagRole.Read, site.Role);
        Assert.Equal(1, site.Line);
    }

    [Fact]
    public void Yml_GetVariable_SkipsComment()
    {
        var scan = DataFunctionReader.Read("l_english:\n key:0 \"[ROOT.Char.GetVariable('x').GetValue]\"\n # [GetVariable('y')]\n");

        var site = Assert.Single(scan.Sites);
        Assert.Equal("x", site.Name);
        Assert.Equal(FlagRole.Read, site.Role);
        Assert.Equal(2, site.Line);
    }

    [Theory]
    [InlineData(" key:0 \"a \"b\" #P [GetVariable('x').GetValue]#!\"")]
    [InlineData(" key:0 \"a \"b #P [GetVariable('x').GetValue]#!\"")]
    [InlineData(" key: \"say \"hi #P [GetVariable('x').GetValue]#!\" # [GetVariable('y')]")]
    public void Yml_InnerQuotes_HashInsideValueIsNoComment(string line)
    {
        var scan = DataFunctionReader.Read("l_english:\n" + line + "\n", localization: true);
        var site = Assert.Single(scan.Sites);
        Assert.Equal("x", site.Name);
        Assert.Equal(2, site.Line);
    }

    [Theory]
    [InlineData("# key:0 \"[GetVariable('y')]\"")]
    [InlineData(" #key:0 \"[GetVariable('y')]\"")]
    [InlineData(" key:0 \"text\" # [GetVariable('y')]")]
    [InlineData(" key:0 # [GetVariable('y')]")]
    public void Yml_CommentsStillSkipped(string line)
    {
        Assert.Empty(DataFunctionReader.Read("l_english:\n" + line + "\n", localization: true).Sites);
    }

    [Fact]
    public void GlobalFunction()
    {
        var scan = DataFunctionReader.Read("[GetGlobalVariable(\"g\").GetValue][GetLocalVariable('l')][VariableList('v')][HasVariable('h')]");

        Assert.Collection(scan.Sites,
            s => Assert.Equal(("g", FlagKind.GlobalVariable), (s.Name, s.Kind)),
            s => Assert.Equal(("l", FlagKind.LocalVariable), (s.Name, s.Kind)),
            s => Assert.Equal(("v", FlagKind.VariableList), (s.Name, s.Kind)),
            s => Assert.Equal(("h", FlagKind.Variable), (s.Name, s.Kind)));
    }
}
