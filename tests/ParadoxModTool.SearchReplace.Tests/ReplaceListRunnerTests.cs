using System.Text;
using ParadoxModTool.SearchReplace.ReplaceLists;
using Xunit;

namespace ParadoxModTool.SearchReplace.Tests;

/// <summary>Ersetzungsliste über einen Mod-Ordner: Reihenfolge, Zählung je Regel, BOM und Zeilenenden, ungültiges UTF-8, veraltete Vorschau.</summary>
public sealed class ReplaceListRunnerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pmt-replacelist-" + Guid.NewGuid().ToString("N"));

    public ReplaceListRunnerTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string Write(string relative, string text, bool bom = false)
    {
        var path = Path.Combine(_folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new UTF8Encoding(bom));
        return path;
    }

    private static ReplaceRule Rule(string search, string replace, bool enabled = true) => new() { Search = search, Replace = replace, Enabled = enabled };

    private ReplaceListRun Run(params ReplaceRule[] rules) => ReplaceListRunner.Run(rules, ReplaceListRunner.ReadFolder(_folder));

    [Fact]
    public void Rules_run_in_order_and_a_second_run_changes_nothing()
    {
        var path = Write("a.txt", "x = a\n");

        var run = Run(Rule("a", "b"), Rule("b", "c"));

        var change = Assert.Single(run.Changes);
        Assert.Equal("x = c\n", change.NewText);
        Assert.Equal(new[] { 1, 1 }, run.RuleCounts);
        File.WriteAllText(path, change.NewText);
        Assert.Empty(Run(Rule("a", "b"), Rule("b", "c")).Changes);
    }

    [Fact]
    public void Counts_follow_the_rules_above()
    {
        Write("a.txt", "castle_01 castle_01\n");
        Write("b.txt", "castle_01\n");

        var run = Run(Rule("castle_01", "castle_02"), Rule("castle_02", "#castle_02"), Rule("castle_03", "y"));

        Assert.Equal(new[] { 3, 3, 0 }, run.RuleCounts);
        Assert.Equal(2, run.Changes.Count);
    }

    [Fact]
    public void Bom_and_crlf_stay()
    {
        Write("a.txt", "a\r\nb\r\n", bom: true);

        var change = Assert.Single(Run(Rule("a", "z")).Changes);

        Assert.True(change.HasBom);
        Assert.Equal("z\r\nb\r\n", change.NewText);
        Assert.Equal("a\r\nb\r\n", change.OldText);
    }

    [Fact]
    public void Invalid_utf8_is_skipped_and_listed()
    {
        var path = Path.Combine(_folder, "bad.txt");
        File.WriteAllBytes(path, new byte[] { 0x61, 0xFF, 0x0A });

        var read = ReplaceListRunner.ReadFolder(_folder);
        var run = ReplaceListRunner.Run(new[] { Rule("a", "z") }, read);

        Assert.Empty(read.Files);
        Assert.Equal(new[] { path }, read.Skipped);
        Assert.Equal(new[] { path }, run.Skipped);
        Assert.Empty(run.Changes);
    }

    [Fact]
    public void Only_default_extensions_below_the_folder()
    {
        Write("sub/a.txt", "w\n");
        Write("b.gui", "w\n");
        Write("c.dds", "w\n");

        var run = Run(Rule("w", "v"));

        Assert.Equal(
            new[] { Path.Combine(_folder, "b.gui"), Path.Combine(_folder, "sub", "a.txt") },
            run.Changes.Select(c => c.Path));
    }

    [Fact]
    public void Disabled_and_invalid_rules_do_not_run()
    {
        Write("a.txt", "a b\n");

        var run = Run(Rule("a b", "x"), Rule("a", "x", enabled: false));

        Assert.Empty(run.Changes);
        Assert.Equal(new[] { 0, 0 }, run.RuleCounts);
    }

    [Fact]
    public void Missing_folder_gives_an_empty_read()
    {
        var read = ReplaceListRunner.ReadFolder(Path.Combine(_folder, "missing"));

        Assert.False(read.FolderExists);
        Assert.Empty(read.Files);
        Assert.Empty(read.Skipped);
    }

    [Fact]
    public void StillCurrent_detects_an_edit_after_the_run()
    {
        var edited = Write("a.txt", "a\n", bom: true);
        Write("b.txt", "a\n");

        var run = Run(Rule("a", "z"));
        File.WriteAllText(edited, "a a\n", new UTF8Encoding(true));

        Assert.False(ReplaceListRunner.StillCurrent(run.Changes.Single(c => c.Path == edited)));
        Assert.True(ReplaceListRunner.StillCurrent(run.Changes.Single(c => c.Path != edited)));
    }

    [Fact]
    public void Text_pattern_and_safe_comment_rules_run_over_the_folder_and_are_counted_per_rule()
    {
        var trigger = "building_requirement_castle_city_church = { LEVEL = 01 }";
        Write("common/buildings/b.txt", $"a = {{\n\t{trigger}\n\tbuildings = {{ x y }}\n\tspecial_building = hg_01\n}}\r\n".Replace("\r\n", "\n"));

        var run = Run(
            new ReplaceRule { Mode = RuleMode.Text, Search = trigger, Replace = "building_requirement_main_building = { LEVEL = 2 }" },
            new ReplaceRule { Search = "y", Replace = "#y", SafeComment = true },
            new ReplaceRule { Mode = RuleMode.Pattern, Search = "special_building = *", Replace = "#special_building = $1" });

        Assert.Equal(new[] { 1, 1, 1 }, run.RuleCounts);
        Assert.Equal(
            "a = {\n\tbuilding_requirement_main_building = { LEVEL = 2 }\n\tbuildings = { x\n\t#y\n\t}\n\t#special_building = hg_01\n}\n",
            run.Changes.Single().NewText);
    }

    [Fact]
    public void Invalid_text_rules_are_skipped_like_invalid_word_rules()
    {
        Write("a.txt", "a = b\n");

        var run = Run(new ReplaceRule { Mode = RuleMode.Pattern, Search = "a = *", Replace = "$2" }, new ReplaceRule { Mode = RuleMode.Text, Search = "" });

        Assert.Empty(run.Changes);
        Assert.Equal(new[] { 0, 0 }, run.RuleCounts);
    }
}
