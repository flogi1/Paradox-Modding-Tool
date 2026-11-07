using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class PerformanceAnswerReaderTests
{
    [Fact]
    public void ReadReturnsAdviceAndRecipe()
    {
        var answer = PerformanceAnswerReader.Read("Reduce the pulse.\n\n```json\n<<<FILE recipe.json>>>\n{ \"a\": 1 }\n<<<END>>>\n```\n\nThen measure again.\n");

        Assert.Equal("{ \"a\": 1 }", answer.RecipeJson);
        Assert.Empty(answer.Problems);
        Assert.Equal("Reduce the pulse.\n\n\nThen measure again.", answer.Advice);
        Assert.DoesNotContain("```", answer.Advice);
        Assert.DoesNotContain("<<<", answer.Advice);
    }

    [Fact]
    public void ReadKeepsClosingFenceOfCodeProposalBeforeRecipe()
    {
        var answer = PerformanceAnswerReader.Read("Change it:\n```\nx = 1\n```\n<<<FILE recipe.json>>>\n{ \"a\": 1 }\n<<<END>>>\n");

        Assert.Equal("Change it:\n```\nx = 1\n```", answer.Advice);
        Assert.Equal("{ \"a\": 1 }", answer.RecipeJson);
    }

    [Fact]
    public void ReadWithoutBlockIsAdviceOnly()
    {
        var answer = PerformanceAnswerReader.Read("  Just advice.  ");

        Assert.Equal("Just advice.", answer.Advice);
        Assert.Null(answer.RecipeJson);
        Assert.Empty(answer.Problems);
    }

    [Fact]
    public void ReadReportsBlockWithoutEnd()
    {
        var answer = PerformanceAnswerReader.Read("Advice first.\n<<<FILE recipe.json>>>\n{ \"a\": 1 }\n");

        Assert.Equal("Advice first.", answer.Advice);
        Assert.Null(answer.RecipeJson);
        Assert.Single(answer.Problems);
    }

    [Fact]
    public void ReadRejectsOtherFileNames()
    {
        var answer = PerformanceAnswerReader.Read("Advice.\n<<<FILE other.txt>>>\nx\n<<<END>>>\n");

        Assert.Null(answer.RecipeJson);
        Assert.Equal("Advice.", answer.Advice);
        var problem = Assert.Single(answer.Problems);
        Assert.Contains("other.txt", problem);
    }

    [Theory]
    [InlineData("<<<FILE recipe.json>>>\n{}\n<<<END>>>")]
    [InlineData("```json\n<<<FILE recipe.json>>>\n{}\n<<<END>>>\n```")]
    [InlineData("```\n<<<FILE recipe.json>>>\n{}\n<<<END>>>\n```\n")]
    public void ReadRecipeWithoutAdviceHasEmptyAdvice(string text)
    {
        var answer = PerformanceAnswerReader.Read(text);

        Assert.Equal(string.Empty, answer.Advice);
        Assert.Equal("{}", answer.RecipeJson);
        Assert.Empty(answer.Problems);
    }
}
