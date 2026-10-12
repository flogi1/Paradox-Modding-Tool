using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

/// <summary>Präfixe, die im Ordner nicht wirken oder anders wirken als gedacht (Victoria 3: Ordnerregeln mit eigenen Präfixen).</summary>
public class PrefixCheckTests : CompatTestBase
{
    private static readonly string[] ReplaceOnly = { "REPLACE", "TRY_REPLACE", "REPLACE_OR_CREATE" };

    private static OverrideRules Vic3Rules() => new(new[]
    {
        new OverrideRule("common/*", OverrideBehavior.LastWins, Operators: Array.Empty<string>()),
        new OverrideRule("common/buildings", OverrideBehavior.PerKey),
        new OverrideRule("common/scripted_effects", OverrideBehavior.PerKey, Operators: ReplaceOnly),
        new OverrideRule("common/history", OverrideBehavior.AppendOnly, false, Operators: Array.Empty<string>()),
        new OverrideRule("events", OverrideBehavior.FirstWins, Operators: Array.Empty<string>()),
    });

    [Theory]
    [InlineData("INJECT:foo", "common/coat_of_arms/a.txt", PrefixIssueKind.NoEffect)]
    [InlineData("REPLACE:foo", "events/a.txt", PrefixIssueKind.NoEffect)]
    [InlineData("INJECT:foo", "common/scripted_effects/a.txt", PrefixIssueKind.ActsAsReplace)]
    [InlineData("TRY_INJECT:foo", "common/scripted_effects/a.txt", PrefixIssueKind.ActsAsReplace)]
    public void Prefix_the_folder_does_not_support_is_found(string key, string path, PrefixIssueKind expected)
    {
        Assert.Equal(expected, PrefixCheck.Classify(key, Vic3Rules().Match(path)));
    }

    [Theory]
    [InlineData("INJECT:foo", "common/buildings/a.txt")]
    [InlineData("REPLACE:foo", "common/scripted_effects/a.txt")]
    [InlineData("foo", "common/coat_of_arms/a.txt")]
    public void Supported_prefix_or_plain_key_is_fine(string key, string path)
    {
        Assert.Null(PrefixCheck.Classify(key, Vic3Rules().Match(path)));
    }

    [Fact]
    public void Folder_without_own_operator_rule_is_not_checked()
    {
        var euV = new OverrideRules(new[] { new OverrideRule("*/common", OverrideBehavior.PerKey), new OverrideRule("*/events", OverrideBehavior.FirstWins) });

        Assert.Null(PrefixCheck.Classify("INJECT:foo", euV.Match("in_game/events/a.txt")));
    }

    [Fact]
    public void Issues_in_text_mark_the_prefix_and_name_the_folder()
    {
        const string Text = "namespace = x\n\nINJECT:x.1 = {\n\ttype = country_event\n}\n";

        var issue = Assert.Single(PrefixCheck.Find(Text, "events/a.txt", Vic3Rules()));

        Assert.Equal(PrefixIssueKind.NoEffect, issue.Kind);
        Assert.Equal(Text.IndexOf("INJECT:", StringComparison.Ordinal), issue.Start);
        Assert.Equal("INJECT:".Length, issue.Length);
        Assert.Equal(3, issue.Line);
        Assert.Contains("events", issue.Message);
        Assert.Contains("00_", issue.Message);
    }

    [Fact]
    public void Last_wins_folder_tip_names_a_later_file()
    {
        var issue = Assert.Single(PrefixCheck.Find("REPLACE:GBR = { color1 = red }\n", "common/coat_of_arms/coat_of_arms/a.txt", Vic3Rules()));

        Assert.Contains("zz_", issue.Message);
    }

    [Fact]
    public void Acts_as_replace_message_asks_for_the_full_entry()
    {
        var issue = Assert.Single(PrefixCheck.Find("INJECT:debug_fail = { a = 1 }\n", "common/scripted_effects/a.txt", Vic3Rules()));

        Assert.Equal(PrefixIssueKind.ActsAsReplace, issue.Kind);
        Assert.Contains("REPLACE:", issue.Message);
    }

    [Fact]
    public void File_is_found_relative_to_its_mod_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "pmt-prefix-mod");
        var file = Path.Combine(root, "events", "a.txt");

        Assert.Single(PrefixCheck.FindForFile(file, "REPLACE:x.1 = { }\n", Vic3Rules(), new[] { Path.Combine(Path.GetTempPath(), "other"), root }));
        Assert.Empty(PrefixCheck.FindForFile(Path.Combine(Path.GetTempPath(), "elsewhere", "events", "a.txt"), "REPLACE:x.1 = { }\n", Vic3Rules(), new[] { root }));
    }

    [Fact]
    public void Longest_matching_root_gives_the_relative_path()
    {
        var repo = Path.Combine(Path.GetTempPath(), "pmt-prefix-repo");
        var mod = Path.Combine(repo, "mod");

        Assert.Single(PrefixCheck.FindForFile(Path.Combine(mod, "events", "a.txt"), "REPLACE:x.1 = { }\n", Vic3Rules(), new[] { repo, mod }));
    }

    [Theory]
    [InlineData("TRY_INJECT:debug_fail", "TRY_REPLACE:")]
    [InlineData("INJECT_OR_CREATE:debug_fail", "REPLACE_OR_CREATE:")]
    public void Acts_as_replace_names_the_matching_replace_form(string key, string form)
    {
        var issue = Assert.Single(PrefixCheck.Find(key + " = { a = 1 }\n", "common/scripted_effects/a.txt", Vic3Rules()));

        Assert.Contains(form, issue.Message);
    }

    [Fact]
    public void File_in_a_mod_collection_folder_skips_the_mod_folder_name()
    {
        var collection = Path.Combine(Path.GetTempPath(), "pmt-prefix-collection");
        var file = Path.Combine(collection, "My Mod", "events", "a.txt");

        var issue = Assert.Single(PrefixCheck.FindForFile(file, "REPLACE:x.1 = { }\n", Vic3Rules(), Array.Empty<string>(), new[] { collection }));
        Assert.Contains("00_", issue.Message);
    }

    [Fact]
    public void Mod_check_reports_each_prefix_of_a_mod()
    {
        var vanilla = Vanilla();
        var mine = Mod("Mine", 2);
        Put(mine, "events/a.txt", "namespace = x\nREPLACE:x.1 = { type = country_event }\n");
        Put(mine, "common/scripted_effects/a.txt", "INJECT:debug_fail = { a = 1 }\n");
        Put(mine, "common/buildings/a.txt", "INJECT:building_x = { a = 1 }\n");

        var findings = CompatAnalyzer.Analyze(new CompatRequest { Participants = new[] { vanilla, mine }, Rules = Vic3Rules() }).Findings;

        var noEffect = Assert.Single(findings, f => f.Kind == CompatKind.PrefixNoEffect);
        Assert.Equal("events/a.txt", noEffect.RelativePath);
        Assert.Equal(CompatSeverity.Conflict, noEffect.Severity);
        Assert.Equal("Mine", noEffect.Winner!.Name);
        Assert.Contains("Mine", noEffect.Message);
        var actsAsReplace = Assert.Single(findings, f => f.Kind == CompatKind.PrefixActsAsReplace);
        Assert.Equal(CompatSeverity.Hint, actsAsReplace.Severity);
        Assert.Equal("INJECT:debug_fail", actsAsReplace.Key);
        Assert.Equal(1, actsAsReplace.Contributions.Single().Line);
        Assert.Equal(2, noEffect.Contributions.Single().Line);
    }
}
