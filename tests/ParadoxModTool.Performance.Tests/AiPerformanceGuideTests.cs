using System.Text;
using ParadoxModTool.Patches.Ai;
using Xunit;

namespace ParadoxModTool.Performance.Tests;

/// <summary>KI-Anleitung für die Performance-Analyse (<c>ai-guide/performance</c>): Kopf und Antwortformat von SKILL.md, Regeln und Schwellen in reference.md, Beispiel.</summary>
public sealed class AiPerformanceGuideTests
{
    private static string GuideRoot => RepoFiles.PathOf("ai-guide/performance");

    private static string Read(string relative) => File.ReadAllText(Path.Combine(GuideRoot, relative.Replace('/', Path.DirectorySeparatorChar))).Replace("\r\n", "\n");

    [Fact]
    public void SkillHasFrontMatterAndAnswerFormat()
    {
        var skill = Read("SKILL.md");

        Assert.StartsWith("---\nname: pmt-performance\ndescription: ", skill, StringComparison.Ordinal);
        Assert.Contains("## Befund", skill, StringComparison.Ordinal);
        Assert.Contains("## Vorschläge", skill, StringComparison.Ordinal);
        Assert.Contains("## Nachmessen", skill, StringComparison.Ordinal);
        Assert.Contains("<<<FILE recipe.json>>>", skill, StringComparison.Ordinal);
        Assert.Contains("<<<END>>>", skill, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceNamesTheRulesAndThresholds()
    {
        var reference = Read("reference.md");

        foreach (var needle in new[] { "error-spam", "unguarded-var", "frequent-tick", "vanilla-copy", "many-calls", "ai_tick_frequency", "0.05", "100000", "0.5", "30 days", "25 %" })
        {
            Assert.Contains(needle, reference, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ReferenceGlossaryUsesTheRealTaskHeadings()
    {
        var reference = Read("reference.md");

        foreach (var heading in new[]
        {
            PerformanceTexts.AiHeadingRun, PerformanceTexts.AiHeadingOrigins, PerformanceTexts.AiHeadingHotspots,
            PerformanceTexts.AiHeadingHints, PerformanceTexts.AiHeadingFiles, PerformanceTexts.AiHeadingDeliverables,
        })
        {
            Assert.Contains("`## " + heading + "`", reference, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ExamplesFolderHasTaskAndAnswer()
    {
        var task = Read("examples/iv2-researcher/task.md");
        var answer = Read("examples/iv2-researcher/answer.md");

        Assert.Contains("## " + PerformanceTexts.AiHeadingHotspots, task, StringComparison.Ordinal);
        Assert.Contains("----- hotspot block: ", task, StringComparison.Ordinal);
        Assert.Contains("## Befund", answer, StringComparison.Ordinal);
        Assert.Contains("## Vorschläge", answer, StringComparison.Ordinal);
        Assert.Contains("## Nachmessen", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("<<<FILE", answer, StringComparison.Ordinal);
    }

    [Fact]
    public void ExampleAnswerIsReadByTheAnswerReaderAsPlainAdvice()
    {
        var answer = PerformanceAnswerReader.Read(Read("examples/iv2-researcher/answer.md"));

        Assert.Null(answer.RecipeJson);
        Assert.Empty(answer.Problems);
        Assert.Contains("## Befund", answer.Advice, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTextWithRealGuideStaysUnder100000Bytes()
    {
        var draft = new AiTaskDraft("## Messlauf\nLauf: vorher\n", new[] { "common/a.txt" },
            new[] { new AiTaskDraftSnippet("common/a.txt", 3, 9, "eff = {\n\tadd_gold = 1\n}", "Hotspot Nr. 1") });
        var request = new PerformanceTaskRequest(draft, "Make it faster", "eu5", null, null, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<AiTaskSnippet>(), false);

        var text = PerformanceTaskBuilder.BuildText(request, GuideRoot, null);

        Assert.Contains("name: pmt-performance", text, StringComparison.Ordinal);
        Assert.Contains("# Example", text, StringComparison.Ordinal);
        var bytes = Encoding.UTF8.GetByteCount(text);
        Assert.True(bytes < 100_000, "BuildText ist " + bytes + " Bytes groß");
    }

    [Fact]
    public void BuildTextWorstCaseDraftStaysUnder100000Bytes()
    {
        // 30 Mod-Zeilen über 12 Dateien, je Datei ein Block mit weit über 120 Zeilen (rund 50 Byte, eingerückt), dazu Hinweise
        var files = Enumerable.Range(1, 12).Select(i => $"common/scripted_effects/mod_effects_{i:00}.txt").ToList();
        var rows = new List<string>();
        for (var i = 0; i < 30; i++)
        {
            rows.Add($"effect @ {files[i % 12]}:{2 + i};100000;1.5;{300 - i}");
        }

        var lines = new StringBuilder();
        lines.Append("big_effect = {\n");
        for (var i = 0; i < 200; i++)
        {
            lines.Append("\t\tadd_gold = { value = scope:target.gold_amount_").Append(i % 10).Append(" }\n");
        }

        lines.Append("}\n");
        var block = lines.ToString();
        using var tree = new TempTree(files.Select(f => ("in_game/" + f, block)).ToArray());
        var classifier = new OriginClassifier(tree.Root, Array.Empty<string>(), Array.Empty<string>());
        var csv = "File Location;Call Count;Self Time;Total Time\n" + string.Join("\n", rows) + "\n";
        var summary = new ProfilingSummary(ProfilingDumpReader.Read(new StringReader(csv)), classifier);
        var hints = files.Take(5).Select(f => new PerformanceHint("many-calls", HintSeverity.Warning, f, 10, "Sehr viele Aufrufe", new string('x', 200), 1)).ToList();
        var run = new PerformanceRun("20261020-100000-ab12", "vorher", "eu5", "m1", @"C:\mods\Flogi", "Playset 1", "save1", 12, 12, "aaa", string.Empty,
            new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc), string.Empty);

        var draft = PerformanceAiTask.Build(run, summary, new RunDuration(12, "k", true), hints, "Notiz", classifier, "aaa", new AiTaskLimits(),
            path => File.ReadAllText(tree.PathOf("in_game/" + path)));
        var request = new PerformanceTaskRequest(draft, "Make it faster", "eu5", null, null, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<AiTaskSnippet>(), false);

        var text = PerformanceTaskBuilder.BuildText(request, GuideRoot, null);

        Assert.Equal(12, draft.Snippets.Count);
        var bytes = Encoding.UTF8.GetByteCount(text);
        Assert.True(bytes < 100_000, "BuildText ist " + bytes + " Bytes groß");
    }
}
