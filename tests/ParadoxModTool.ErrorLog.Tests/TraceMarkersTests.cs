using System.Text;
using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class TraceMarkersTests
{
    private const string GamePath = "common/on_action/a.txt";
    private const string File1 = "own/in_game/common/on_action/a.txt";
    private const string Text = "a = {\r\n\teffect = {\r\n\t\tscope:y = {\r\n\t\t\tadd_gold = 1\r\n\t\t}\r\n\t\tadd_gold = 2 # Kommentar\r\n\t}\r\n}\r\n";

    private static TraceRules Rules => Catalogs.Eu5.Trace;

    private static IReadOnlyList<TraceGuard> Guards => Catalogs.Eu5.Find("RUN-001")!.TraceGuards;

    private static ModSource Own(TempTree tree) => new("Own", tree.PathOf("own"), IsOwn: true, LoadOrder: 0);

    private static void Write(ModRenamePlan plan)
    {
        foreach (var file in plan.Files)
        {
            File.WriteAllText(file.Path, file.NewText, new UTF8Encoding(file.HasBom));
        }
    }

    [Fact]
    public void InsertThenScanFindsTheTrace()
    {
        using var tree = new TempTree((File1, Text));
        var mod = Own(tree);

        var insert = TraceMarkers.PlanInsert(mod, tree.PathOf(File1), GamePath, 3, "Undefined event target 'y'", Guards, Rules, "3f2a");
        Write(insert.Edit!);
        var trace = Assert.Single(TraceMarkers.Scan(mod, Rules));

        Assert.Equal(TraceStatus.Ok, insert.Plan.Status);
        var line = Assert.Single(insert.Edit!.Files[0].Lines);
        Assert.Equal((3, "\t\tscope:y = {"), (line.Number, line.Before));
        Assert.Equal(("3f2a", 3, 3, 5, true, false), (trace.Id, trace.Line, trace.FirstLine, trace.LastLine, trace.Guarded, trace.Broken));
        Assert.Equal(tree.PathOf(File1), trace.Path);
    }

    [Fact]
    public void RemoveRestoresTheFileByteForByte()
    {
        using var tree = new TempTree((File1, string.Empty));
        var path = tree.PathOf(File1);
        var original = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Text)).ToArray();
        File.WriteAllBytes(path, original);
        var mod = Own(tree);

        Write(TraceMarkers.PlanInsert(mod, path, GamePath, 3, "Undefined event target 'y'", Guards, Rules, "0001").Edit!);
        Write(TraceMarkers.PlanInsert(mod, path, GamePath, 6, "x", Guards, Rules, "0002").Edit!);
        Assert.Equal(2, TraceMarkers.Scan(mod, Rules).Count);
        foreach (var plan in TraceMarkers.PlanRemove(TraceMarkers.Scan(mod, Rules)))
        {
            Write(plan);
        }

        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Empty(TraceMarkers.Scan(mod, Rules));
    }

    [Fact]
    public void ABrokenMarkerIsListedAndRemovable()
    {
        using var tree = new TempTree((File1, Text));
        var mod = Own(tree);
        var path = tree.PathOf(File1);
        Write(TraceMarkers.PlanInsert(mod, path, GamePath, 6, "x", Guards, Rules, "00ab").Edit!);
        File.WriteAllText(path, File.ReadAllText(path).Replace("debug_log = \"PMT_TRACE_00ab\" debug_log_scopes = yes ", string.Empty));

        var trace = Assert.Single(TraceMarkers.Scan(mod, Rules));
        Assert.True(trace.Broken);
        Write(Assert.Single(TraceMarkers.PlanRemove(new[] { trace })));

        Assert.Equal(Text, File.ReadAllText(path));
    }

    [Fact]
    public void TextThatOnlyLooksLikeAMarkerIsIgnored()
    {
        using var tree = new TempTree((File1, "a = {\n\teffect = {\n\t\tdebug_log = \"PMT_TRACE_abcd\" # siehe PMT-TRACE Doku\n\t\tadd_gold = 1 # PMT-TRACE in Arbeit\n\t}\n}\n"));

        Assert.Empty(TraceMarkers.Scan(Own(tree), Rules));
    }

    [Fact]
    public void AChangedFileIsNotCurrent()
    {
        using var tree = new TempTree((File1, Text));
        var path = tree.PathOf(File1);
        var insert = TraceMarkers.PlanInsert(Own(tree), path, GamePath, 6, "x", Guards, Rules, "0003");
        Assert.True(TraceMarkers.IsCurrent(insert));

        File.AppendAllText(path, "b = { }\r\n");

        Assert.False(TraceMarkers.IsCurrent(insert));
    }

    [Fact]
    public void NoEditForALineWithoutEffectContext()
    {
        using var tree = new TempTree(("own/in_game/common/scripted_triggers/t.txt", "t = {\n\tscope:y = { is_z = yes }\n}\n"));

        var insert = TraceMarkers.PlanInsert(Own(tree), tree.PathOf("own/in_game/common/scripted_triggers/t.txt"), "common/scripted_triggers/t.txt", 2, "x", Guards, Rules, "0004");

        Assert.Equal(TraceStatus.NoEffectContext, insert.Plan.Status);
        Assert.Null(insert.Edit);
    }

    [Fact]
    public void NewIdAvoidsUsedIds()
    {
        var used = Enumerable.Range(0, 0xffff).Select(i => i.ToString("x4")).ToList();

        Assert.Equal("ffff", TraceMarkers.NewId(used, new Random(1)));
        Assert.Matches("^[0-9a-f]{4}$", TraceMarkers.NewId(Array.Empty<string>(), new Random(2)));
    }
}
