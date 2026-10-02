using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class CompatFindingSortTests
{
    private static CompatFinding Finding(string path, CompatSeverity severity = CompatSeverity.Conflict, string key = "") => new()
    {
        Severity = severity,
        Kind = CompatKind.KeyConflict,
        RelativePath = path,
        Key = key,
        Message = string.Empty,
    };

    private static IEnumerable<string> Paths(IEnumerable<CompatFinding> findings) => findings.Select(f => f.RelativePath);

    [Fact]
    public void Resolved_findings_come_first_and_keep_their_order()
    {
        var a = Finding("a");
        var b = Finding("b");
        var c = Finding("c");
        var d = Finding("d");
        var resolved = new HashSet<CompatFinding> { b, d };

        var sorted = CompatFindingSort.Sort(new[] { a, b, c, d }, CompatSortColumn.Resolved, descending: false, resolved.Contains);

        Assert.Equal(new[] { "b", "d", "a", "c" }, Paths(sorted));
    }

    [Fact]
    public void Descending_puts_resolved_findings_last()
    {
        var a = Finding("a");
        var b = Finding("b");
        var resolved = new HashSet<CompatFinding> { a };

        var sorted = CompatFindingSort.Sort(new[] { a, b }, CompatSortColumn.Resolved, descending: true, resolved.Contains);

        Assert.Equal(new[] { "b", "a" }, Paths(sorted));
    }

    [Fact]
    public void Severity_sorts_conflicts_first()
    {
        var info = Finding("info", CompatSeverity.Info);
        var conflict = Finding("conflict", CompatSeverity.Conflict);
        var hint = Finding("hint", CompatSeverity.Hint);

        var sorted = CompatFindingSort.Sort(new[] { info, conflict, hint }, CompatSortColumn.Severity, descending: false, _ => false);

        Assert.Equal(new[] { "conflict", "hint", "info" }, Paths(sorted));
    }

    [Fact]
    public void Paths_sort_naturally_and_ignore_case()
    {
        var sorted = CompatFindingSort.Sort(new[] { Finding("x10.txt"), Finding("X2.txt"), Finding("x1.txt") }, CompatSortColumn.Path, descending: false, _ => false);

        Assert.Equal(new[] { "x1.txt", "X2.txt", "x10.txt" }, Paths(sorted));
    }
}
