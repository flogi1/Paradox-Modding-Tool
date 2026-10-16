using ParadoxModTool.Patches.Ai;
using Xunit;

namespace ParadoxModTool.Patches.Tests;

/// <summary>Antwort einer KI lesen (Spec 2026-10-12-patch-recipes-ai-guide, Abschnitt 2 "Ergebnis"): ein Dateiblock recipe.json, tolerant gelesen.</summary>
public sealed class RecipeAnswerReaderTests : IDisposable
{
    private readonly string _work = Path.Combine(Path.GetTempPath(), "pmt-recipe-answer-" + Guid.NewGuid().ToString("N"));

    public RecipeAnswerReaderTests()
    {
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_work, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private const string ChatAnswer = """
        Here is your recipe. I read every building file first.

        <<<FILE recipe.json>>>
        ```json
        {
          // the id stays
          "id": "ai_recipe",
          "name": "AI recipe",
          "columns": [
            { "name": "castle", "type": "Text", },
          ],
          "rows": [
            { "key": "k1", "values": { "castle": "x" }, },
          ],
          "rules": [
            { "id": "t1", "name": "T", "Kind": "Template", "target": "common/x.txt", "text": "{key} = { }", },
          ],
        }
        ```
        <<<END>>>

        Tell me if the preview looks wrong.
        """;

    [Fact]
    public void Fenced_json_with_prose_comments_and_trailing_commas_imports()
    {
        var answer = RecipeAnswerReader.FromText(ChatAnswer);

        Assert.Empty(answer.Problems);
        Assert.NotNull(answer.Recipe);
        Assert.Equal("ai_recipe", answer.Recipe!.Id);
        Assert.Equal("castle", Assert.Single(answer.Recipe.Columns).Name);
        Assert.Equal("x", answer.Recipe.Rows[0].Values["castle"]);
        var rule = Assert.IsType<TemplateRule>(Assert.Single(answer.Recipe.Rules));
        Assert.Equal("common/x.txt", rule.Target);
    }

    [Fact]
    public void Null_lists_become_empty_and_the_check_finds_nothing_to_complain_about()
    {
        var answer = RecipeAnswerReader.FromJson("""{ "id": "r", "name": "R", "columns": null, "rows": null, "rules": null }""");

        Assert.Empty(answer.Problems);
        Assert.NotNull(answer.Recipe);
        Assert.Empty(answer.Recipe!.Columns);
        Assert.Empty(answer.Recipe.Rows);
        Assert.Empty(answer.Recipe.Rules);
        // Ein Rezept ohne Spalten, Zeilen und Regeln ist strukturell in Ordnung: die Prüfung meldet dazu nichts (Leere zeigt die Vorschau).
        Assert.Empty(RecipeChecker.Check(answer.Recipe));
    }

    [Fact]
    public void Missing_columns_and_null_rows_are_reported_by_the_check_without_crash()
    {
        var answer = RecipeAnswerReader.FromJson("""
            { "id": "r", "name": "R", "rows": [ { "key": "k1", "values": { "castle": "x" } }, { "key": "k2", "values": null } ],
              "rules": [ { "kind": "template", "id": "t", "target": "a.txt" } ] }
            """);

        Assert.Empty(answer.Problems);
        Assert.NotNull(answer.Recipe);
        Assert.Empty(answer.Recipe!.Columns);
        Assert.Empty(answer.Recipe.Rows[1].Values);

        var issues = RecipeChecker.Check(answer.Recipe);

        Assert.Collection(issues,
            i => Assert.Equal(new RecipeIssue(CheckSeverity.Error, "row k1", PatchTexts.CheckRowUnknownColumn("castle")), i),
            i => Assert.Equal(new RecipeIssue(CheckSeverity.Error, "rule t", PatchTexts.CheckFieldEmpty("text")), i));
    }

    [Fact]
    public void PascalCase_enums_load()
    {
        var answer = RecipeAnswerReader.FromJson("""
            { "id": "r", "name": "R",
              "rules": [ { "id": "c1", "kind": "lines", "files": "common/*.txt", "match": "x = yes", "matchKind": "Text", "action": "Comment" } ] }
            """);

        Assert.Empty(answer.Problems);
        var rule = Assert.IsType<LineRule>(Assert.Single(answer.Recipe!.Rules));
        Assert.Equal(LineAction.Comment, rule.Action);
    }

    [Fact]
    public void Unknown_properties_are_reported_separately()
    {
        var answer = RecipeAnswerReader.FromJson("""{ "id": "r", "name": "R", "colour": "red" }""");

        Assert.Empty(answer.Problems);
        Assert.Contains(answer.UnknownProperties, p => p.Contains("colour"));
    }

    [Fact]
    public void Extra_file_is_rejected()
    {
        var text = "<<<FILE recipe.json>>>\n{ \"id\": \"r\" }\n<<<END>>>\n<<<FILE notes.md>>>\nhi\n<<<END>>>\n";

        var answer = RecipeAnswerReader.FromText(text);

        Assert.Null(answer.Recipe);
        var problem = Assert.Single(answer.Problems);
        Assert.Contains("notes.md", problem);
        Assert.Contains("recipe.json", problem);
    }

    [Fact]
    public void Missing_recipe_json_is_reported()
    {
        var answer = RecipeAnswerReader.FromText("Here you go, no blocks at all.");
        Assert.Null(answer.Recipe);
        Assert.Single(answer.Problems);

        var other = RecipeAnswerReader.FromText("<<<FILE notes.md>>>\nhi\n<<<END>>>\n");
        Assert.Null(other.Recipe);
        Assert.Contains(other.Problems, p => p.Contains("recipe.json"));
    }

    [Fact]
    public void Block_without_end_and_duplicate_are_reported()
    {
        var noEnd = RecipeAnswerReader.FromText("<<<FILE recipe.json>>>\n{ \"id\": \"r\" }\n");
        Assert.Null(noEnd.Recipe);
        Assert.Contains(noEnd.Problems, p => p.Contains("<<<END>>>"));

        var twice = RecipeAnswerReader.FromText("<<<FILE recipe.json>>>\n{ \"id\": \"r\" }\n<<<END>>>\n<<<FILE recipe.json>>>\n{ \"id\": \"s\" }\n<<<END>>>\n");
        Assert.Null(twice.Recipe);
        Assert.Contains(twice.Problems, p => p.Contains("recipe.json"));
    }

    [Fact]
    public void Broken_json_reports_message()
    {
        var answer = RecipeAnswerReader.FromText("<<<FILE recipe.json>>>\n{ \"id\": \n<<<END>>>\n");

        Assert.Null(answer.Recipe);
        var problem = Assert.Single(answer.Problems);
        Assert.StartsWith("recipe.json: ", problem);
        Assert.True(problem.Length > "recipe.json: ".Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("{ \"id\": [ 5 ] }")]
    [InlineData("{ \"rules\": [ { \"id\": \"x\", \"kind\": \"unknown\" } ] }")]
    public void Unusable_json_is_a_problem_not_an_exception(string json)
    {
        var answer = RecipeAnswerReader.FromJson(json);

        Assert.Null(answer.Recipe);
        Assert.Single(answer.Problems);
        Assert.StartsWith("recipe.json: ", answer.Problems[0]);
    }

    [Fact]
    public void Folder_with_recipe_json_imports()
    {
        File.WriteAllText(Path.Combine(_work, "recipe.json"), "{ \"id\": \"from_folder\", \"name\": \"F\", }");

        var answer = RecipeAnswerReader.FromFolder(_work);

        Assert.Empty(answer.Problems);
        Assert.Equal("from_folder", answer.Recipe!.Id);
    }

    [Fact]
    public void Folder_without_recipe_json_is_reported()
    {
        File.WriteAllText(Path.Combine(_work, "other.json"), "{}");

        var answer = RecipeAnswerReader.FromFolder(_work);

        Assert.Null(answer.Recipe);
        var problem = Assert.Single(answer.Problems);
        Assert.Contains("recipe.json", problem);
    }

    [Fact]
    public void Missing_folder_is_reported()
    {
        var answer = RecipeAnswerReader.FromFolder(Path.Combine(_work, "nope"));

        Assert.Null(answer.Recipe);
        Assert.Single(answer.Problems);
    }
}
