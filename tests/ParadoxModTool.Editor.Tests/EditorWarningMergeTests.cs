using ParadoxModTool.Scopes;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Scope-Warnungen und Präfix-Warnungen landen gemeinsam in der Wellenlinie, nach Textstelle geordnet.</summary>
public sealed class EditorWarningMergeTests
{
    [Fact]
    public void Scope_and_prefix_warnings_are_merged_in_text_order()
    {
        var scope = new[] { new ScopeIssue(40, 5, "add_treasury", "state", new[] { "country" }) };
        var prefix = new[] { new EditorWarning(10, 7, "INJECT: wirkt nicht", true), new EditorWarning(60, 7, "wie REPLACE", false) };

        var merged = ScopeWarningController.Merge(scope, prefix);

        Assert.Equal(new[] { 10, 40, 60 }, merged.Select(w => w.Start));
        Assert.True(merged[1].Severe);
        Assert.Equal(scope[0].Message, merged[1].Message);
        Assert.False(merged[2].Severe);
    }

    [Fact]
    public void Prefix_check_is_not_asked_for_files_that_are_no_script()
    {
        var asked = 0;
        Func<string, IReadOnlyList<EditorWarning>>? Checker(string? path)
        {
            asked++;
            return _ => Array.Empty<EditorWarning>();
        }

        Assert.Null(ScopeWarningController.PrefixCheckFor(@"C:\mod\localization\english\x_l_english.yml", Checker));
        Assert.NotNull(ScopeWarningController.PrefixCheckFor(@"C:\mod\events\x.txt", Checker));
        Assert.Equal(1, asked);
    }

    [Fact]
    public void Failing_prefix_check_gives_no_check_instead_of_an_error()
    {
        Assert.Null(ScopeWarningController.PrefixCheckFor(@"C:\mod\events\x.txt", _ => throw new System.IO.IOException("rules.json locked")));
    }
}
