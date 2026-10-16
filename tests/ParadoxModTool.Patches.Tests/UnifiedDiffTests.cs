using ParadoxModTool.Patches.Ai;
using System.Text.RegularExpressions;
using Xunit;

namespace ParadoxModTool.Patches.Tests;

/// <summary>Zeilen-Diff im Unified-Format für <c>pmt-patch plan --diff</c>.</summary>
public sealed class UnifiedDiffTests
{
    [Fact]
    public void Identical_texts_give_no_lines()
    {
        Assert.Empty(UnifiedDiff.Build("a.txt", "x\ny\n", "x\ny\n"));
    }

    [Fact]
    public void Changed_line_gives_one_hunk_with_context()
    {
        var lines = UnifiedDiff.Build("common/a.txt", "l1\nl2\nl3\nl4\nl5\nl6\nl7\n", "l1\nl2\nl3\nX\nl5\nl6\nl7\n");
        Assert.Equal(
            new[] { "--- a/common/a.txt", "+++ b/common/a.txt", "@@ -2,5 +2,5 @@", " l2", " l3", "-l4", "+X", " l5", " l6" },
            lines);
    }

    [Fact]
    public void New_file_is_all_plus()
    {
        var lines = UnifiedDiff.Build("n.txt", null, "a\nb\n");
        Assert.Equal(new[] { "--- /dev/null", "+++ b/n.txt", "@@ -0,0 +1,2 @@", "+a", "+b" }, lines);
    }

    [Fact]
    public void Crlf_and_lf_compare_equal_in_hunks()
    {
        Assert.Equal(
            new[] { "--- a/a.txt", "+++ b/a.txt", "@@ -1,2 +1,2 @@", " x", "-y", "+z" },
            UnifiedDiff.Build("a.txt", "x\r\ny\r\n", "x\nz\n"));
    }

    [Theory]
    [InlineData("x\r\ny\r\n", "x\ny\n", "# only line endings differ: CRLF -> LF")]
    [InlineData("x\ny\n", "x\r\ny\r\n", "# only line endings differ: LF -> CRLF")]
    [InlineData("x\r\ny\n", "x\ny\n", "# only line endings differ: mixed -> LF")]
    [InlineData("x\ny", "x\ny\n", "# only the final line break differs: added")]
    [InlineData("x\ny\n", "x\ny", "# only the final line break differs: removed")]
    [InlineData("x", "x\r\n", "# only the final line break differs: added")]
    [InlineData("x\r\ny\n", "x\ny\r\n", "# only line endings differ")]
    public void Layout_only_change_names_what_differs(string oldText, string newText, string note)
    {
        Assert.Equal(new[] { "--- a/a.txt", "+++ b/a.txt", note }, UnifiedDiff.Build("a.txt", oldText, newText));
    }

    [Fact]
    public void Line_endings_and_final_break_are_both_named()
    {
        Assert.Equal(
            new[] { "--- a/a.txt", "+++ b/a.txt", "# only line endings differ: CRLF -> LF", "# only the final line break differs: removed" },
            UnifiedDiff.Build("a.txt", "x\r\ny\r\n", "x\ny"));
    }

    [Fact]
    public void Distant_changes_give_two_hunks_and_insert_delete_counts()
    {
        var old = string.Join("\n", Enumerable.Range(1, 20).Select(i => "l" + i)) + "\n";
        var updated = old.Replace("l3\n", "l3\nnew\n").Replace("l18\n", string.Empty);
        var lines = UnifiedDiff.Build("a.txt", old, updated);
        Assert.Equal(new[]
        {
            "--- a/a.txt", "+++ b/a.txt",
            "@@ -2,4 +2,5 @@", " l2", " l3", "+new", " l4", " l5",
            "@@ -16,5 +17,4 @@", " l16", " l17", "-l18", " l19", " l20",
        }, lines);
    }

    [Fact]
    public void Large_unrelated_texts_do_not_blow_up()
    {
        var old = string.Join("\n", Enumerable.Range(0, 6000).Select(i => "a" + i));
        var updated = string.Join("\n", Enumerable.Range(0, 6000).Select(i => "b" + i));
        var lines = UnifiedDiff.Build("big.txt", old, updated);
        Assert.Equal(6000, lines.Count(l => l.StartsWith('-') && !l.StartsWith("---")));
        Assert.Equal(6000, lines.Count(l => l.StartsWith('+') && !l.StartsWith("+++")));
    }

    [Fact]
    public void Applying_the_hunks_to_the_old_text_gives_the_new_text()
    {
        for (var seed = 1; seed <= 60; seed++)
        {
            var random = new Random(seed);
            var oldLines = Enumerable.Range(0, random.Next(0, 40)).Select(_ => "v" + random.Next(0, 6)).ToList();
            var newLines = new List<string>(oldLines);
            for (var edit = random.Next(0, 9); edit > 0; edit--)
            {
                var at = random.Next(0, newLines.Count + 1);
                switch (random.Next(0, 3))
                {
                    case 0 when at < newLines.Count:
                        newLines.RemoveAt(at);
                        break;
                    case 1:
                        newLines.Insert(at, "n" + random.Next(0, 100));
                        break;
                    case 2 when at < newLines.Count:
                        newLines[at] = "r" + random.Next(0, 100);
                        break;
                }
            }

            var oldText = oldLines.Count == 0 ? string.Empty : string.Join("\n", oldLines) + "\n";
            var newText = newLines.Count == 0 ? string.Empty : string.Join("\n", newLines) + "\n";
            foreach (var context in new[] { 0, 1, 2, 3 })
            {
                var diff = UnifiedDiff.Build("f.txt", oldText, newText, context);
                Assert.Equal(oldText == newText, diff.Count == 0);
                Assert.Equal(newLines, ApplyHunks(oldLines, diff), StringComparer.Ordinal);
            }
        }
    }

    /// <summary>Liest die Hunks wie <c>patch</c>: prüft Kopf, Zähler und Startzeilen und gibt die neuen Zeilen zurück.</summary>
    private static List<string> ApplyHunks(IReadOnlyList<string> oldLines, IReadOnlyList<string> diff)
    {
        var result = new List<string>();
        if (diff.Count == 0)
        {
            return oldLines.ToList();
        }

        Assert.Equal("--- a/f.txt", diff[0]);
        Assert.Equal("+++ b/f.txt", diff[1]);
        var position = 0;
        var i = 2;
        while (i < diff.Count)
        {
            var header = Regex.Match(diff[i], @"^@@ -(\d+),(\d+) \+(\d+),(\d+) @@$");
            Assert.True(header.Success, diff[i]);
            int oldStart = int.Parse(header.Groups[1].Value), oldCount = int.Parse(header.Groups[2].Value);
            int newStart = int.Parse(header.Groups[3].Value), newCount = int.Parse(header.Groups[4].Value);
            // Bei leerer Seite zeigt die Startzeile auf die Zeile davor.
            var copyUntil = oldCount == 0 ? oldStart : oldStart - 1;
            Assert.True(copyUntil >= position, "Hunks überlappen");
            while (position < copyUntil)
            {
                result.Add(oldLines[position++]);
            }

            Assert.Equal(newCount == 0 ? result.Count : result.Count + 1, newStart);
            i++;
            int seenOld = 0, seenNew = 0;
            while (i < diff.Count && !diff[i].StartsWith("@@ -", StringComparison.Ordinal))
            {
                var text = diff[i][1..];
                switch (diff[i][0])
                {
                    case ' ':
                        Assert.Equal(oldLines[position++], text);
                        result.Add(text);
                        seenOld++;
                        seenNew++;
                        break;
                    case '-':
                        Assert.Equal(oldLines[position++], text);
                        seenOld++;
                        break;
                    case '+':
                        result.Add(text);
                        seenNew++;
                        break;
                    default:
                        Assert.Fail("unbekanntes Zeilenzeichen: " + diff[i]);
                        break;
                }

                i++;
            }

            Assert.Equal(oldCount, seenOld);
            Assert.Equal(newCount, seenNew);
        }

        while (position < oldLines.Count)
        {
            result.Add(oldLines[position++]);
        }

        return result;
    }
}
