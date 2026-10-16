using System.Text;
using ParadoxModTool.Patches.Ai;
using Xunit;

namespace ParadoxModTool.Patches.Tests;

/// <summary>Gemeinsame Helfer für KI-Aufträge: Vorbild-Dateien lesen, Dateiblöcke und Codeblöcke ausgeben, Dateien kopieren.</summary>
public sealed class AiTaskFilesTests
{
    [Fact]
    public void Large_file_is_truncated()
    {
        using var mod = new TempMod();
        mod.WriteMod("big.txt", new string('a', 300 * 1024), bom: false);

        var file = Assert.Single(AiTaskFiles.ReadFiles(mod.ModRoot, new[] { "big.txt" }));

        Assert.True(file.Truncated);
        Assert.Equal(300 * 1024, file.Bytes);
        Assert.True(file.Text!.Length <= AiTaskFiles.MaxFileBytes);
        var text = new StringBuilder();
        AiTaskFiles.AppendFiles(text, "example", new[] { file });
        Assert.Contains("[truncated: first 200 KB of 300 KB]", text.ToString());
    }

    [Fact]
    public void Binary_file_is_not_included()
    {
        using var mod = new TempMod();
        mod.WriteMod("coa.dds", "DDS \0\0\0x", bom: false);

        var file = Assert.Single(AiTaskFiles.ReadFiles(mod.ModRoot, new[] { "coa.dds" }));

        Assert.Null(file.Text);
        var text = new StringBuilder();
        AiTaskFiles.AppendFiles(text, "example", new[] { file });
        Assert.Equal("----- example: coa.dds (binary, not included) -----\n", text.ToString());
    }

    [Fact]
    public void Unsafe_and_missing_paths_are_skipped_and_bom_is_removed()
    {
        using var mod = new TempMod();
        mod.WriteMod("a.txt", "x = 1", bom: true);

        var files = AiTaskFiles.ReadFiles(mod.ModRoot, new[] { "../a.txt", "missing.txt", "a.txt" });

        var file = Assert.Single(files);
        Assert.Equal("x = 1", file.Text);
    }

    [Fact]
    public void AppendFiles_uses_label()
    {
        var text = new StringBuilder();

        AiTaskFiles.AppendFiles(text, "mod file", new[] { new AiTaskFile("a.txt", "one\r\ntwo\r\n\r\n", false, 12) });

        Assert.Equal("----- mod file: a.txt -----\none\ntwo\n----- end -----\n", text.ToString());
    }

    [Fact]
    public void Total_size_sums_bytes()
    {
        using var mod = new TempMod();
        mod.WriteMod("a.txt", "12345", bom: false);
        mod.WriteMod("b.txt", "123", bom: false);

        Assert.Equal(8, AiTaskFiles.TotalBytes(AiTaskFiles.ReadFiles(mod.ModRoot, new[] { "a.txt", "b.txt" })));
    }

    [Fact]
    public void Snippet_bytes_count_utf8_and_note()
    {
        Assert.Equal(4 + 3, AiTaskFiles.SnippetBytes(new[] { new AiTaskSnippet("a", true, "abcd"), new AiTaskSnippet("b", false, "ä ") }));
        Assert.Equal(4 + 2, AiTaskFiles.SnippetBytes(new[] { new AiTaskSnippet("a", true, "abcd") { Note = "xy" } }));
    }

    [Theory]
    [InlineData("ab\ncd\nef\n", 4, 1, "cd")]
    [InlineData("ab\ncd\nef\n", 1, 3, "ab\ncd")]
    [InlineData("ab\r\ncd\r\nef", 2, 5, "ab\r\ncd")]
    [InlineData("ab\ncd\nef", 0, 3, "ab")]
    [InlineData("ab\ncd\nef", 7, 2, "ef")]
    [InlineData("ab\ncd", 1, 0, "")]
    public void Selection_grows_to_whole_lines(string text, int start, int length, string expected)
    {
        Assert.Equal(expected, AiTaskFiles.WholeLines(text, start, length));
    }

    [Fact]
    public void AppendSnippets_lists_blocks_and_puts_the_write_hint_after_the_excerpt_sentence()
    {
        var snippets = new[]
        {
            new AiTaskSnippet("in_game/common/x.txt", false, "a = 1\r\n") { Note = "Repeats.\r\nKeep.\r\n" },
            new AiTaskSnippet("", true, "u = 1") { FromGame = true },
        };
        var text = new StringBuilder();

        AiTaskFiles.AppendSnippets(text, snippets, ", so use RULE.", "GAME HINT.");

        var result = text.ToString();
        Assert.Contains("\n## Code blocks from the modder\n\n", result);
        Assert.Contains("the rest of the file belongs to other content and must stay, so use RULE. If a path is unknown, ask the modder for it.\n", result);
        Assert.Contains("A line `Note: ...` right under a block header", result);
        Assert.Contains("\"game file\" marks a block from the base game", result);
        Assert.Contains("(path relative to the game folder, mods use the same paths). GAME HINT.\n", result);
        Assert.Contains("----- code block: in_game/common/x.txt (excerpt) -----\nNote: Repeats.\nNote: Keep.\na = 1\n----- end -----\n", result);
        Assert.Contains("----- code block: game file (path unknown, ask the modder) (whole file) -----\nu = 1\n----- end -----\n", result);
    }

    [Fact]
    public void AppendSnippets_omits_note_and_game_explanations_when_unused()
    {
        var text = new StringBuilder();

        AiTaskFiles.AppendSnippets(text, new[] { new AiTaskSnippet("a.txt", true, "x") }, ".", "GAME HINT.");

        Assert.DoesNotContain("Note: ...", text.ToString());
        Assert.DoesNotContain("\"game file\" marks", text.ToString());
    }

    [Fact]
    public void CopyFiles_keeps_relative_paths_and_skips_unsafe_and_missing_ones()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/a.txt", "x", bom: false);
        var target = Path.Combine(mod.ModRoot, "..", "copy");

        AiTaskFiles.CopyFiles(mod.ModRoot, new[] { "common/a.txt", "../outside.txt", "missing.txt" }, target);

        Assert.True(File.Exists(Path.Combine(target, "common", "a.txt")));
        Assert.Single(Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Feedback_lists_messages_after_the_intro()
    {
        var text = AiTaskFiles.Feedback("Problems:", new[] { "a: b", "c" });

        Assert.Equal("Problems:\n\n- a: b\n- c\n", text);
    }
}
