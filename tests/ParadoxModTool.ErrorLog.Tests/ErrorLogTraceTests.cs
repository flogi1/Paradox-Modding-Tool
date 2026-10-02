using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class ErrorLogTraceTests
{
    private const string File1 = "own/in_game/common/on_action/a.txt";

    private static TempTree Tree() => new((File1, "a = {\n\teffect = {\n\t\tscope:y = {\n\t\t\tadd_gold = 1\n\t\t}\n\t}\n}\n"));

    private static (ErrorLogSession Session, ModSource Own) NewSession(TempTree tree)
    {
        var session = new ErrorLogSession(Catalogs.Eu5, new SuppressionSet(), null, "de");
        var own = new ModSource("Own", tree.PathOf("own"), IsOwn: true, LoadOrder: 0);
        session.SetSources(new[] { own }, Array.Empty<string>());
        session.LoadRun("error.log", LogReader.Parse(new[]
        {
            "[10:00:00][jomini_script_system.cpp:256]: Script system error!",
            "  Error: Undefined event target 'y'",
            "  Script location: common/on_action/a.txt:3",
            "[10:00:00][jomini_script_system.cpp:256]: Script system error!",
            "  Error: Undefined event target 'y'",
            "  Script location: common/on_action/a.txt:3",
            "[10:00:00][jomini_script_system.cpp:256]: Script system error!",
            "  Error: something else",
            "  Script location: common/on_action/a.txt:7",
        }), DateTime.UtcNow.AddDays(1));
        return (session, own);
    }

    private static TraceHit Hit(string tag) =>
        new("3f2a", "10:00:00", "common/on_action/a.txt", 3, new TraceScope("Country", tag, tag), null, Array.Empty<(string, TraceScope)>());

    [Fact]
    public void TraceForFindsTheTraceOfAnErrorInItsRange()
    {
        using var tree = Tree();
        var (session, own) = NewSession(tree);
        var trace = new ActiveTrace(own, tree.PathOf(File1), "3f2a", 3, 3, 5, Guarded: true, Broken: false);

        session.SetTraces(new[] { trace });

        Assert.Same(trace, session.TraceFor(session.Entries[0]));
        Assert.Null(session.TraceFor(session.Entries[2]));
    }

    [Fact]
    public void TraceResultForUsesTheHitsOfThatTrace()
    {
        using var tree = Tree();
        var (session, own) = NewSession(tree);
        var trace = new ActiveTrace(own, tree.PathOf(File1), "3f2a", 3, 3, 5, Guarded: true, Broken: false);
        session.SetTraces(new[] { trace });

        session.SetTraceHits(new[] { Hit("ENG"), Hit("FRA") });

        var result = session.TraceResultFor(session.Entries[0])!;
        Assert.Equal(TraceMatchKind.Exact, result.Kind);
        Assert.Equal(new[] { "ENG", "FRA" }, result.Scopes.Select(s => s.Scope.Id).OrderBy(t => t));
        Assert.Equal(2, session.HitCountOf(trace));
        Assert.Contains("3f2a", session.TraceHitIds);
        Assert.Null(session.TraceResultFor(session.Entries[2]));
    }

    [Fact]
    public void AddingHitsRaisesChanged()
    {
        using var tree = Tree();
        var (session, own) = NewSession(tree);
        session.SetTraces(new[] { new ActiveTrace(own, tree.PathOf(File1), "3f2a", 3, 3, 5, Guarded: false, Broken: false) });
        var raised = 0;
        var version = session.TraceVersion;
        session.Changed += (_, _) => raised++;

        session.AddTraceHits(new[] { Hit("ENG") });
        session.AddTraceHits(Array.Empty<TraceHit>());

        Assert.Equal(1, raised);
        Assert.Equal(version + 1, session.TraceVersion);
        Assert.Equal(1, session.HitCountOf(session.Traces[0]));
    }
}
