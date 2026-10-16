using ParadoxModTool.Patches.Ai;
using Xunit;

namespace ParadoxModTool.Patches.Tests;

/// <summary>Dateiblöcke einer KI-Antwort lesen: <c>&lt;&lt;&lt;FILE pfad&gt;&gt;&gt;</c> bis <c>&lt;&lt;&lt;END&gt;&gt;&gt;</c>.</summary>
public sealed class AiFileBlockParserTests
{
    private static bool OnlyRecipeOrTxt(string path) => path is "recipe.json" || path.StartsWith("t/", StringComparison.Ordinal);

    [Fact]
    public void Parses_fenced_blocks_with_prose()
    {
        var answer = "Here you go.\n\n<<<FILE recipe.json>>>\n```json\n{ \"a\": 1 }\n```\n<<<END>>>\n\nAnd more:\n" +
            "```\n<<<FILE t/a.txt>>>\nx = 1\n<<<END>>>\n```\nGood luck!";

        var result = AiFileBlockParser.Parse(answer, OnlyRecipeOrTxt, "recipe.json");

        Assert.Empty(result.Problems);
        Assert.Equal(new[] { "recipe.json", "t/a.txt" }, result.Files.Keys.OrderBy(k => k));
        Assert.Equal("{ \"a\": 1 }", result.Files["recipe.json"]);
        Assert.Equal("x = 1", result.Files["t/a.txt"]);
    }

    [Fact]
    public void Crlf_is_normalized_and_backslashes_in_paths_become_slashes()
    {
        var result = AiFileBlockParser.Parse("<<<FILE recipe.json>>>\r\n{}\r\n<<<END>>>\r\n<<<FILE t\\a.txt>>>\r\na\r\nb\r\n<<<END>>>\r\n", OnlyRecipeOrTxt, "recipe.json");

        Assert.Empty(result.Problems);
        Assert.Equal("a\nb", result.Files["t/a.txt"]);
    }

    [Fact]
    public void Disallowed_path_is_problem_and_not_kept()
    {
        var result = AiFileBlockParser.Parse("<<<FILE recipe.json>>>\n{}\n<<<END>>>\n<<<FILE notes.txt>>>\nx\n<<<END>>>", OnlyRecipeOrTxt, "recipe.json");

        Assert.Equal(new AiBlockProblem(AiBlockProblemKind.NotAllowed, "notes.txt"), Assert.Single(result.Problems));
        Assert.DoesNotContain("notes.txt", result.Files.Keys);
    }

    [Fact]
    public void Duplicate_is_problem_and_first_wins()
    {
        var result = AiFileBlockParser.Parse("<<<FILE recipe.json>>>\n{ \"a\": 1 }\n<<<END>>>\n<<<FILE recipe.json>>>\n{}\n<<<END>>>", OnlyRecipeOrTxt, "recipe.json");

        Assert.Equal(new AiBlockProblem(AiBlockProblemKind.Duplicate, "recipe.json"), Assert.Single(result.Problems));
        Assert.Equal("{ \"a\": 1 }", result.Files["recipe.json"]);
    }

    [Fact]
    public void Missing_end_is_problem()
    {
        var result = AiFileBlockParser.Parse("<<<FILE recipe.json>>>\n{}\n<<<FILE t/a.txt>>>\nx", OnlyRecipeOrTxt, "recipe.json");

        Assert.Contains(new AiBlockProblem(AiBlockProblemKind.BlockWithoutEnd, "recipe.json"), result.Problems);
        Assert.Contains(new AiBlockProblem(AiBlockProblemKind.BlockWithoutEnd, "t/a.txt"), result.Problems);
        Assert.Empty(result.Files);
    }

    [Fact]
    public void Missing_required_file_and_no_blocks_are_problems()
    {
        Assert.Equal(new AiBlockProblem(AiBlockProblemKind.MissingRequired, "recipe.json"),
            Assert.Single(AiFileBlockParser.Parse("<<<FILE t/a.txt>>>\nx\n<<<END>>>", OnlyRecipeOrTxt, "recipe.json").Problems));
        Assert.Equal(new AiBlockProblem(AiBlockProblemKind.NoFiles, string.Empty),
            Assert.Single(AiFileBlockParser.Parse("Sorry, I cannot help.", OnlyRecipeOrTxt, "recipe.json").Problems));
    }
}
