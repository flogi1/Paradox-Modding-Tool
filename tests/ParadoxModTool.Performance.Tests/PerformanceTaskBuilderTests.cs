using ParadoxModTool.Patches.Ai;
using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class PerformanceTaskBuilderTests
{
    private static readonly AiTaskDraft Draft = new("## Messlauf\nLauf: vorher\n", new[] { "common/a.txt" },
        new[] { new AiTaskDraftSnippet("common/a.txt", 3, 9, "eff = {\n\tadd_gold = 1\n}", "Hotspot Nr. 1") });

    private static TempTree Guides() => new(
        ("guide/SKILL.md", "# Skill\nBe good.\n"),
        ("guide/reference.md", "# Reference\nRules.\n"),
        ("guide/examples/x/a.md", "example text\n"),
        ("recipe/reference.md", "# Recipe reference\nrecipe rules\n"),
        ("mod/common/a.txt", "eff = {\n}\n"),
        ("game/common/g.txt", "vanilla = {\n}\n"));

    private static PerformanceTaskRequest Request(TempTree tree, bool withRecipe = false, AiTaskSnippet[]? user = null) =>
        new(Draft, "Make it faster", "eu5", tree.PathOf("mod"), tree.PathOf("game"), new[] { "common/a.txt" }, new[] { "common/g.txt" },
            user ?? Array.Empty<AiTaskSnippet>(), withRecipe);

    [Fact]
    public void BuildTextContainsGuideTaskAndSnippets()
    {
        using var tree = Guides();
        var user = new[] { new AiTaskSnippet("common/u.txt", false, "mine = yes") };

        var text = PerformanceTaskBuilder.BuildText(Request(tree, user: user), tree.PathOf("guide"), null);

        Assert.StartsWith("# Skill", text);
        Assert.Contains("# Reference", text);
        Assert.Contains("# Example", text);
        Assert.Contains("example text", text);
        Assert.Contains("# Task", text);
        Assert.Contains("Lauf: vorher", text);
        Assert.Contains("## What the modder wants\n\nMake it faster", text);
        Assert.Contains("----- mod file: common/a.txt -----", text);
        Assert.Contains("----- game file: common/g.txt -----", text);
        Assert.Contains("----- code block: common/u.txt (excerpt) -----", text);
        Assert.Contains("----- hotspot block: common/a.txt:3-9 -----\nNote: Hotspot Nr. 1\neff = {", text);
        Assert.DoesNotContain("\r", text);
        Assert.True(text.IndexOf("# Task", StringComparison.Ordinal) < text.IndexOf("----- mod file", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildTextAppendsRecipeReferenceOnlyWhenRequested()
    {
        using var tree = Guides();
        var recipe = tree.PathOf("recipe");

        var without = PerformanceTaskBuilder.BuildText(Request(tree), tree.PathOf("guide"), recipe);
        var withoutFolder = PerformanceTaskBuilder.BuildText(Request(tree, withRecipe: true), tree.PathOf("guide"), null);
        var missingFolder = PerformanceTaskBuilder.BuildText(Request(tree, withRecipe: true), tree.PathOf("guide"), tree.PathOf("nope"));
        var with = PerformanceTaskBuilder.BuildText(Request(tree, withRecipe: true), tree.PathOf("guide"), recipe);

        Assert.DoesNotContain("Patch recipe format", without);
        Assert.DoesNotContain("Patch recipe format", withoutFolder);
        Assert.DoesNotContain("Patch recipe format", missingFolder);
        Assert.Contains("# Patch recipe format (optional)", with);
        Assert.Contains("recipe rules", with);
    }

    [Fact]
    public void BuildFolderWritesGuideTaskAndCopies()
    {
        using var tree = Guides();
        var target = tree.PathOf("out");

        PerformanceTaskBuilder.BuildFolder(Request(tree, withRecipe: true), tree.PathOf("guide"), tree.PathOf("recipe"), target);

        Assert.True(File.Exists(Path.Combine(target, "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(target, "examples", "x", "a.md")));
        var task = File.ReadAllText(Path.Combine(target, "task.md"));
        Assert.Contains("# Task", task);
        Assert.Contains("Lauf: vorher", task);
        Assert.Contains("Make it faster", task);
        Assert.Contains("`mod/`", task);
        Assert.Contains("`answer.md`", task);
        Assert.DoesNotContain("`answer.md`", PerformanceTaskBuilder.BuildText(Request(tree, withRecipe: true), tree.PathOf("guide"), tree.PathOf("recipe")));
        Assert.True(File.Exists(Path.Combine(target, "mod", "common", "a.txt")));
        Assert.True(File.Exists(Path.Combine(target, "game", "common", "g.txt")));
        Assert.Contains("----- hotspot block: common/a.txt:3-9 -----", File.ReadAllText(Path.Combine(target, "snippets.md")));
        Assert.Contains("recipe rules", File.ReadAllText(Path.Combine(target, "recipe-format.md")));
    }

    [Fact]
    public void BuildFolderOmitsSnippetsAndRecipeFileWhenNotNeeded()
    {
        using var tree = Guides();
        var target = tree.PathOf("out");
        var request = Request(tree) with { Draft = Draft with { Snippets = Array.Empty<AiTaskDraftSnippet>() } };

        PerformanceTaskBuilder.BuildFolder(request, tree.PathOf("guide"), tree.PathOf("recipe"), target);

        Assert.False(File.Exists(Path.Combine(target, "snippets.md")));
        Assert.False(File.Exists(Path.Combine(target, "recipe-format.md")));
    }

    [Fact]
    public void FeedbackListsMessagesAfterIntro()
    {
        var text = PerformanceTaskBuilder.Feedback(new[] { "Eins", "Zwei" });

        Assert.StartsWith(PerformanceTexts.AiFeedbackIntro, text);
        Assert.Contains("- Eins\n- Zwei\n", text);
    }
}
