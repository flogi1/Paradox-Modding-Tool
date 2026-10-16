using System.Text;
using ParadoxModTool.Generators.Ai;
using ParadoxModTool.Patches.Ai;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Auftrag für eine KI zusammenstellen (Spec 2026-10-05-generator-packages-ai-guide, Abschnitt 2 "Auftrag").</summary>
public sealed class AiTaskBuilderTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private AiTaskRequest Request(IReadOnlyList<string> files, string? cli = null, string? package = null) =>
        new("Ten ideas per tag, four levels each.", "eu5", AiGuideTests.ExampleMod, files, package, cli);

    private static readonly AiTaskSnippet[] Snippets =
    {
        new("in_game/common/scripted_effects/iv2_effects.txt", false, "iv2_add_group_18 = {\r\n\tset_variable = x\r\n}\r\n"),
        new("main_menu/localization/english/iv2_l_english.yml", true, " iv2_title_18: \"Admin\""),
        new("", false, "unknown = yes"),
    };

    [Fact]
    public void Task_text_contains_code_blocks_with_path_and_scope()
    {
        var request = Request(Array.Empty<string>()) with { Snippets = Snippets };

        var text = AiTaskBuilder.BuildText(request, AiGuideTests.GuideRoot);

        Assert.Contains("## Code blocks from the modder", text);
        Assert.Contains("----- code block: in_game/common/scripted_effects/iv2_effects.txt (excerpt) -----\niv2_add_group_18 = {\n\tset_variable = x\n}\n----- end -----", text);
        Assert.Contains("----- code block: main_menu/localization/english/iv2_l_english.yml (whole file) -----\n iv2_title_18: \"Admin\"\n----- end -----", text);
        Assert.Contains("----- code block: (path unknown, ask the modder) (excerpt) -----", text);
        Assert.Contains("from the example files and code blocks below", text);
    }

    [Fact]
    public void Task_without_code_blocks_has_no_code_block_section()
    {
        var text = AiTaskBuilder.BuildText(Request(Array.Empty<string>()), AiGuideTests.GuideRoot);

        Assert.DoesNotContain("## Code blocks from the modder", text);
    }

    [Fact]
    public void Folder_task_contains_code_blocks()
    {
        var target = _temp.PathOf("task");

        AiTaskBuilder.BuildFolder(Request(Array.Empty<string>()) with { Snippets = Snippets }, AiGuideTests.GuideRoot, target);

        var task = File.ReadAllText(Path.Combine(target, "task.md"));
        Assert.Contains("----- code block: in_game/common/scripted_effects/iv2_effects.txt (excerpt) -----", task);
    }

    [Fact]
    public void Code_block_note_follows_the_header_line_by_line()
    {
        var snippet = new AiTaskSnippet("in_game/common/x.txt", false, "a = 1\r\n") { Note = "Repeats per idea.\r\nKeep potential.\r\n" };

        var text = AiTaskBuilder.BuildText(Request(Array.Empty<string>()) with { Snippets = new[] { snippet } }, AiGuideTests.GuideRoot);

        Assert.Contains("----- code block: in_game/common/x.txt (excerpt) -----\nNote: Repeats per idea.\nNote: Keep potential.\na = 1\n----- end -----", text);
        Assert.Contains("A line `Note: ...` right under a block header", text);
    }

    [Fact]
    public void Game_file_block_is_marked_and_explained()
    {
        var snippet = new AiTaskSnippet("in_game/common/advances/a.txt", true, "a = 1") { FromGame = true };

        var text = AiTaskBuilder.BuildText(Request(Array.Empty<string>()) with { Snippets = new[] { snippet } }, AiGuideTests.GuideRoot);

        Assert.Contains("----- code block: game file in_game/common/advances/a.txt (whole file) -----", text);
        Assert.Contains("\"game file\" marks a block from the base game", text);
    }

    [Fact]
    public void Mod_only_blocks_do_not_explain_game_files()
    {
        var text = AiTaskBuilder.BuildText(Request(Array.Empty<string>()) with { Snippets = Snippets }, AiGuideTests.GuideRoot);

        Assert.DoesNotContain("\"game file\" marks", text);
    }

    [Fact]
    public void Large_file_is_truncated()
    {
        _temp.Write("mod/big.txt", new string('a', 300 * 1024));

        var file = Assert.Single(AiTaskFiles.ReadFiles(_temp.PathOf("mod"), new[] { "big.txt" }));

        Assert.True(file.Truncated);
        Assert.Equal(300 * 1024, file.Bytes);
        Assert.True(file.Text!.Length <= AiTaskFiles.MaxFileBytes);
        var text = AiTaskBuilder.BuildText(new AiTaskRequest("d", "eu5", _temp.PathOf("mod"), new[] { "big.txt" }, null, null), AiGuideTests.GuideRoot);
        Assert.Contains("[truncated: first 200 KB of 300 KB]", text);
    }

    [Fact]
    public void Binary_file_is_not_included()
    {
        _temp.Write("mod/coa.dds", "DDS \0\0\0x");

        var file = Assert.Single(AiTaskFiles.ReadFiles(_temp.PathOf("mod"), new[] { "coa.dds" }));

        Assert.Null(file.Text);
        var text = AiTaskBuilder.BuildText(new AiTaskRequest("d", "eu5", _temp.PathOf("mod"), new[] { "coa.dds" }, null, null), AiGuideTests.GuideRoot);
        Assert.Contains("----- example: coa.dds (binary, not included) -----", text);
    }

    [Fact]
    public void Task_text_contains_guide_description_examples_and_cli()
    {
        var text = AiTaskBuilder.BuildText(Request(new[] { "in_game/common/static_modifiers/sw_HAM.txt" }, cli: @"C:\PMT\pmt-generator.exe"), AiGuideTests.GuideRoot);

        Assert.Contains("name: pmt-generator-packages", text);
        Assert.Contains("<<<FILE examples/stadtwappen/package.json>>>", text);
        Assert.Contains("Ten ideas per tag, four levels each.", text);
        Assert.Contains("----- example: in_game/common/static_modifiers/sw_HAM.txt -----", text);
        Assert.Contains("sw_modifier_HAM_1 = {", text);
        Assert.Contains(@"""C:\PMT\pmt-generator.exe"" roundtrip", text);
        Assert.Contains("Game: eu5", text);
    }

    [Fact]
    public void Task_text_without_cli_asks_modder_to_check()
    {
        var text = AiTaskBuilder.BuildText(Request(Array.Empty<string>()), AiGuideTests.GuideRoot);

        Assert.DoesNotContain("pmt-generator.exe\" roundtrip", text);
        Assert.Contains("Prüfen", text);
    }

    [Fact]
    public void Task_text_includes_open_package()
    {
        var text = AiTaskBuilder.BuildText(Request(Array.Empty<string>(), package: AiGuideTests.ExamplePackage), AiGuideTests.GuideRoot);

        Assert.Contains("----- package: package.json -----", text);
        Assert.Contains("----- package: templates/loc.sbn -----", text);
    }

    [Fact]
    public void Folder_contains_guide_task_examples_package()
    {
        var target = _temp.PathOf("task");

        AiTaskBuilder.BuildFolder(Request(new[] { "in_game/common/static_modifiers/sw_HAM.txt" }, package: AiGuideTests.ExamplePackage), AiGuideTests.GuideRoot, target);

        Assert.True(File.Exists(Path.Combine(target, "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(target, "reference.md")));
        Assert.True(File.Exists(Path.Combine(target, "examples", "stadtwappen", "package.json")));
        Assert.Contains("Ten ideas per tag", File.ReadAllText(Path.Combine(target, "task.md")));
        Assert.True(File.Exists(Path.Combine(target, "example", "in_game", "common", "static_modifiers", "sw_HAM.txt")));
        Assert.True(File.Exists(Path.Combine(target, "package", "templates", "loc.sbn")));
    }

    [Fact]
    public void Feedback_lists_messages()
    {
        var text = AiTaskBuilder.Feedback(new[] { "read.keys: The pattern needs the group (?<key>...).", "package.json: \"id\" is missing." });

        Assert.Contains("- read.keys: The pattern needs the group (?<key>...).", text);
        Assert.Contains("PMT", text);
    }
}
