using System.Globalization;
using System.Text;
using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class PerformanceAiTaskTests
{
    private static readonly RunDuration Twelve = new(12, "k", true);

    private static PerformanceRun Run(string? hash = "aaa") =>
        new("20261020-100000-ab12", "vorher", "eu5", "m1", @"C:\mods\Flogi", "Playset 1", "save1", 12, 12, hash, string.Empty,
            new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc), string.Empty);

    private static string Block(string name, int lines)
    {
        var text = new StringBuilder(name + " = {\n");
        for (var i = 0; i < lines - 2; i++)
        {
            text.Append("\tadd_gold = ").Append(i + 1).Append('\n');
        }

        return text.Append("}\n").ToString();
    }

    private static (ProfilingSummary Summary, OriginClassifier Classifier) Summarize(TempTree tree, params string[] rows)
    {
        var classifier = new OriginClassifier(tree.Root, Array.Empty<string>(), Array.Empty<string>());
        var csv = "File Location;Call Count;Self Time;Total Time\n" + string.Join("\n", rows) + "\n";
        return (new ProfilingSummary(ProfilingDumpReader.Read(new StringReader(csv)), classifier), classifier);
    }

    private static Func<string, string?> Reader(TempTree tree) => path =>
    {
        var full = tree.PathOf("in_game/" + path);
        return File.Exists(full) ? File.ReadAllText(full) : null;
    };

    private static AiTaskDraft Build(TempTree tree, ProfilingSummary summary, OriginClassifier classifier, IReadOnlyList<PerformanceHint>? hints = null,
        string? hintsNote = null, string? currentHash = "aaa", AiTaskLimits? limits = null, Func<string, string?>? read = null) =>
        PerformanceAiTask.Build(Run(), summary, Twelve, hints ?? Array.Empty<PerformanceHint>(), hintsNote, classifier, currentHash,
            limits ?? new AiTaskLimits(), read ?? Reader(tree));

    [Fact]
    public void DraftListsTopRowsHintsAndFiles()
    {
        using var tree = new TempTree(
            ("in_game/common/scripted_effects/a.txt", Block("eff_a", 6)),
            ("in_game/common/scripted_effects/b.txt", Block("eff_b", 6)));
        var (summary, classifier) = Summarize(tree,
            "effect @ common/scripted_effects/a.txt:2;50;1.5;4",
            "trigger @ common/scripted_effects/b.txt:3;7;0.5;9",
            "effect @ common/scripted_effects/vanilla_only.txt:1;1;9;99");
        var hint = new PerformanceHint("error-spam", HintSeverity.Warning, "common/scripted_effects/b.txt", 3, "Fehlerflut", "Viele Fehler", 5);

        var draft = Build(tree, summary, classifier, new[] { hint }, "Notiz zu den Hinweisen");
        var md = draft.Markdown;

        Assert.Contains("## " + PerformanceTexts.AiHeadingRun, md);
        Assert.Contains(PerformanceTexts.AiRunName("vorher"), md);
        Assert.Contains(PerformanceTexts.AiRunMonths(12, 12), md);
        Assert.Contains(PerformanceTexts.NoiseNote(new NoiseSettings()), md);
        Assert.Contains(PerformanceTexts.AiTimeUnitNote, md);
        Assert.Contains(PerformanceTexts.AiHashSame, md);
        Assert.Contains("## " + PerformanceTexts.AiHeadingOrigins, md);
        Assert.Contains(PerformanceTexts.ReportModSelf(2.0.ToString("N3", CultureInfo.CurrentCulture)), md);
        Assert.Contains("## " + PerformanceTexts.AiHeadingHotspots, md);
        Assert.Contains(PerformanceTexts.AiHotspotHeader, md);
        Assert.Contains("common/scripted_effects/b.txt:3", md);
        Assert.DoesNotContain("vanilla_only", md);
        Assert.True(md.IndexOf("b.txt:3", StringComparison.Ordinal) < md.IndexOf("a.txt:2", StringComparison.Ordinal), "rank by total");
        Assert.Contains("## " + PerformanceTexts.AiHeadingHints, md);
        Assert.Contains(PerformanceTexts.ReportHintLine(HintSeverity.Warning, hint.Path, hint.Line, hint.Title), md);
        Assert.Contains("  Viele Fehler", md);
        Assert.Contains("Notiz zu den Hinweisen", md);
        Assert.Contains("## " + PerformanceTexts.AiHeadingFiles, md);
        Assert.Contains("## " + PerformanceTexts.AiHeadingDeliverables, md);
        Assert.Contains(PerformanceTexts.AiDeliverableFindings, md);
        Assert.Equal(new[] { "common/scripted_effects/b.txt", "common/scripted_effects/a.txt" }, draft.ModFiles);
    }

    [Fact]
    public void DraftSnippetsAreBoundedAndDeduped()
    {
        var text = Block("blk_one", 6) + "\n" + Block("blk_two", 6) + "\n" + Block("blk_three", 6) + "\n" + Block("blk_four", 6);
        using var tree = new TempTree(("in_game/common/scripted_effects/a.txt", text));
        // 14 rows in the first three blocks (lines 1-6, 8-13, 15-20) plus a fourth block (22-27).
        var lines = new[] { 2, 3, 4, 5, 9, 10, 11, 12, 16, 17, 18, 19, 23, 24 };
        var rows = lines.Select((line, i) => $"effect @ common/scripted_effects/a.txt:{line};10;1;{100 - i}").ToArray();
        var (summary, classifier) = Summarize(tree, rows);

        var draft = Build(tree, summary, classifier, limits: new AiTaskLimits(30, 3, 120));

        Assert.Equal(3, draft.Snippets.Count);
        Assert.Equal(new[] { 1, 8, 15 }, draft.Snippets.Select(s => s.StartLine).ToArray());
        Assert.All(draft.Snippets, s => Assert.StartsWith("blk_", s.Code));
        Assert.Equal(PerformanceTexts.AiSnippetNoteHotspot(1, 100), draft.Snippets[0].Note);
    }

    [Fact]
    public void DraftCutsLongBlockAndNotesIt()
    {
        using var tree = new TempTree(("in_game/common/scripted_effects/big.txt", Block("big", 200)));
        var (summary, classifier) = Summarize(tree, "effect @ common/scripted_effects/big.txt:5;1;1;3");

        var snippet = Assert.Single(Build(tree, summary, classifier).Snippets);

        Assert.Equal(1, snippet.StartLine);
        Assert.Equal(120, snippet.EndLine);
        Assert.Equal(120, snippet.Code.Split('\n').Length);
        Assert.EndsWith(PerformanceTexts.AiSnippetCut(120, 200), snippet.Note);
    }

    [Fact]
    public void DraftCutsBlockWithLongLinesAtByteCapOnLineBoundary()
    {
        var text = new StringBuilder("wide = {\n");
        for (var i = 0; i < 60; i++)
        {
            text.Append('\t').Append(new string('x', 198)).Append('\n');
        }

        text.Append("}\n");
        using var tree = new TempTree(("in_game/common/scripted_effects/wide.txt", text.ToString()));
        var (summary, classifier) = Summarize(tree, "effect @ common/scripted_effects/wide.txt:5;1;1;3");

        var snippet = Assert.Single(Build(tree, summary, classifier).Snippets);

        Assert.True(Encoding.UTF8.GetByteCount(snippet.Code) <= PerformanceAiTask.SnippetBytes);
        Assert.All(snippet.Code.Split('\n').Skip(1), l => Assert.Equal(199, l.Length));
        var shown = snippet.Code.Split('\n').Length;
        Assert.True(shown < 62, "cut");
        Assert.Equal(shown, snippet.EndLine);
        Assert.EndsWith(PerformanceTexts.AiSnippetCut(shown, 62), snippet.Note);
    }

    [Fact]
    public void DraftAddsHintBlocksAfterHotspotsAndSkipsMissingFiles()
    {
        using var tree = new TempTree(
            ("in_game/common/scripted_effects/a.txt", Block("eff_a", 6) + "\n" + Block("eff_b", 6)));
        var (summary, classifier) = Summarize(tree, "effect @ common/scripted_effects/a.txt:2;1;1;3");
        var hints = new[]
        {
            new PerformanceHint("r", HintSeverity.Info, "common/scripted_effects/a.txt", 9, "Zweiter Block", "d", 1),
            new PerformanceHint("r", HintSeverity.Info, "common/scripted_effects/a.txt", 3, "Selber Block", "d", 1),
            new PerformanceHint("r", HintSeverity.Info, "common/scripted_effects/fehlt.txt", 3, "Fehlt", "d", 1),
        };

        var draft = Build(tree, summary, classifier, hints);

        Assert.Equal(2, draft.Snippets.Count);
        Assert.Equal(PerformanceTexts.AiSnippetNoteHint("Zweiter Block"), draft.Snippets[1].Note);
        Assert.Equal(8, draft.Snippets[1].StartLine);
    }

    [Fact]
    public void DraftWithoutModRowsHasNoSnippets()
    {
        using var tree = new TempTree();
        var (summary, classifier) = Summarize(tree, "effect @ common/scripted_effects/vanilla.txt:2;1;1;3");
        var reads = 0;

        var draft = Build(tree, summary, classifier, read: _ =>
        {
            reads++;
            return Block("x", 6);
        });

        Assert.Contains(PerformanceTexts.AiNoModRows, draft.Markdown);
        Assert.DoesNotContain(PerformanceTexts.AiHotspotHeader, draft.Markdown);
        Assert.Empty(draft.Snippets);
        Assert.Empty(draft.ModFiles);
        Assert.Equal(0, reads);
    }

    [Theory]
    [InlineData("aaa", "bbb", true)]
    [InlineData("aaa", "aaa", false)]
    public void DraftMarksChangedModHash(string atRun, string now, bool changed)
    {
        using var tree = new TempTree();
        var (summary, classifier) = Summarize(tree);

        var md = PerformanceAiTask.Build(Run(atRun), summary, Twelve, Array.Empty<PerformanceHint>(), null, classifier, now, new AiTaskLimits(), _ => null).Markdown;

        Assert.Equal(changed, md.Contains(PerformanceTexts.AiHashChanged(atRun, now)));
        Assert.Equal(changed, md.Contains(PerformanceTexts.AiHashNow));
        Assert.Equal(!changed, md.Contains(PerformanceTexts.AiHashSame));
    }

    [Fact]
    public void DraftSaysFileStateUnknownWithoutHash()
    {
        using var tree = new TempTree();
        var (summary, classifier) = Summarize(tree);

        var md = Build(tree, summary, classifier, currentHash: null).Markdown;

        Assert.Contains(PerformanceTexts.AiHashUnknown, md);
        Assert.DoesNotContain(PerformanceTexts.AiHashNow, md);
    }
}
