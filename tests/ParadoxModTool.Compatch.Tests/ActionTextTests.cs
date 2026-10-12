using ParadoxModTool.VanillaDiff;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class ActionTextTests
{
    private static IReadOnlyList<ScriptEntry> E(string text) => ScriptDocument.Parse(text).Entries;

    private static MergeAction Action(string a, string b, string vanilla) => new()
    {
        Id = "s|farm::x",
        Address = EntryAddress.ForKey("s", "farm"),
        Path = new[] { "x" },
        DisplayPath = "farm › x",
        Stage = MergeStage.Conflict,
        DefaultDecision = DecisionKind.TakeB,
        A = E(a),
        B = E(b),
        Vanilla = E(vanilla),
    };

    [Fact]
    public void Result_entries_follow_the_decision()
    {
        var action = Action("cost = 2", "cost = 3", "cost = 1");

        Assert.Equal("cost = 2", ActionText.Render(ActionText.ResultEntries(action, new MergeDecision(DecisionKind.TakeA))).Trim());
        Assert.Equal("cost = 3", ActionText.Render(ActionText.ResultEntries(action, new MergeDecision(DecisionKind.TakeB))).Trim());
        Assert.Equal("cost = 1", ActionText.Render(ActionText.ResultEntries(action, new MergeDecision(DecisionKind.TakeVanilla))).Trim());
        Assert.Equal("cost = 7", ActionText.Render(ActionText.ResultEntries(action, new MergeDecision(DecisionKind.Manual, "cost = 7"))).Trim());
        Assert.Empty(ActionText.ResultEntries(action, new MergeDecision(DecisionKind.Manual, "a = 1\n}\n")));
        Assert.Equal(string.Empty, ActionText.Render(Array.Empty<ScriptEntry>()));
    }

    [Fact]
    public void Rows_align_a_result_b_and_optional_vanilla()
    {
        var action = Action("a = 1\nb = 2\nc = 3\nd = 4", "a = 1", "a = 1");

        var rows = ActionText.Rows(action, new MergeDecision(DecisionKind.TakeA), withVanilla: false);
        var withVanilla = ActionText.Rows(action, new MergeDecision(DecisionKind.TakeA), withVanilla: true);

        Assert.Equal(4, rows.Count);
        Assert.Equal(RowOrigin.Same, rows[0].Origin);
        Assert.All(rows.Skip(1), r => Assert.Equal(RowOrigin.FromA, r.Origin));
        Assert.All(rows, r => Assert.Null(r.Vanilla));
        Assert.Equal("a = 1", withVanilla[0].Vanilla);
    }

    [Fact]
    public void Mini_rows_cut_after_max_and_count_the_rest()
    {
        var action = Action("a = 1\nb = 2\nc = 3\nd = 4", "a = 1", "a = 1");
        var decision = new MergeDecision(DecisionKind.TakeA);

        var (cut, hidden) = ActionText.MiniRows(action, decision, 2);
        var (all, none) = ActionText.MiniRows(action, decision, 10);

        Assert.Equal(2, cut.Count);
        Assert.Equal(2, hidden);
        Assert.Equal(4, all.Count);
        Assert.Equal(0, none);
    }

    [Fact]
    public void Strip_comment_lines_drops_lines_starting_with_hash_only()
    {
        Assert.Equal("a = 1\nd = 3 # alt\n", ActionText.StripCommentLines("a = 1\n# b = 2\n  \t#c = {\nd = 3 # alt\n"));
        Assert.Equal(string.Empty, ActionText.StripCommentLines("# nur Kommentar"));
    }

    [Fact]
    public void Rows_hide_comment_lines_on_request()
    {
        var action = Action("a = 1\n# old = {\n#   x = 1\n# }\nb = 2", "a = 1", "a = 1");
        var decision = new MergeDecision(DecisionKind.TakeA);

        var shown = ActionText.Rows(action, decision, withVanilla: false);
        var hidden = ActionText.Rows(action, decision, withVanilla: false, hideComments: true);
        var (mini, _) = ActionText.MiniRows(action, decision, 10, hideComments: true);

        Assert.Contains(shown, r => (r.A ?? string.Empty).TrimStart().StartsWith('#'));
        Assert.DoesNotContain(hidden, r => new[] { r.A, r.Result, r.B }.Any(c => c?.TrimStart().StartsWith('#') == true));
        Assert.Equal(hidden.Count, mini.Count);
    }

    [Fact]
    public void Side_names()
    {
        Assert.Equal("A", ActionText.SideName(DecisionKind.TakeA));
        Assert.Equal("B", ActionText.SideName(DecisionKind.TakeB));
        Assert.Equal(CompatchTexts.ColumnVanilla, ActionText.SideName(DecisionKind.TakeVanilla));
    }
}
