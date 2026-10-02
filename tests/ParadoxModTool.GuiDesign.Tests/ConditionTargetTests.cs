using ParadoxModTool.GuiDesign.Conditions;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public sealed class ConditionTargetTests
{
    private const string Text = "widget = {\n\tvisible = \"[Not(A.B)]\"\n\ttext = \"[A.B]\"\n\tenabled = yes\n}";

    [Fact]
    public void Find_VisibleWithBrackets()
    {
        var document = GuiParser.Parse(Text);

        var target = ConditionTarget.Find(document, Text.IndexOf("Not", StringComparison.Ordinal));

        Assert.NotNull(target);
        Assert.Equal("visible", target!.Property);
        Assert.Equal("\"[Not(A.B)]\"", target.ValueText);
        Assert.Equal("\"[Not(A.B)]\"", Text[target.ValueSpan.Start..target.ValueSpan.End]);
        Assert.NotNull(ConditionTarget.Find(document, Text.IndexOf("visible", StringComparison.Ordinal)));
        Assert.NotNull(ConditionTarget.Find(document, Text.IndexOf("]\"", StringComparison.Ordinal) + 2));
    }

    [Fact]
    public void Find_OtherPropertyOrNoBrackets_IsNull()
    {
        var document = GuiParser.Parse(Text);

        Assert.Null(ConditionTarget.Find(document, Text.IndexOf("text", StringComparison.Ordinal) + 9));
        Assert.Null(ConditionTarget.Find(document, Text.IndexOf("yes", StringComparison.Ordinal)));
        Assert.Null(ConditionTarget.Find(document, 0));
    }

    [Fact]
    public void ReplaceRange_OnlyForConditionResults()
    {
        var target = ConditionTarget.Find(GuiParser.Parse(Text), Text.IndexOf("Not", StringComparison.Ordinal))!;

        Assert.Equal(target.ValueSpan, ConditionTarget.ReplaceRange(target, fromConditionTab: true));
        Assert.Null(ConditionTarget.ReplaceRange(target, fromConditionTab: false));
        Assert.Null(ConditionTarget.ReplaceRange(null, fromConditionTab: true));
    }

    [Theory]
    [InlineData("visible", true)]
    [InlineData("enabled", true)]
    [InlineData("down", true)]
    [InlineData("checked", true)]
    [InlineData("text", false)]
    [InlineData("Visible", false)]
    public void IsConditionProperty(string name, bool expected) => Assert.Equal(expected, ConditionTarget.IsConditionProperty(name));
}
