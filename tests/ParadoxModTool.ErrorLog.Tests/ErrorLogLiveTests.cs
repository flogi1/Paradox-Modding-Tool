using Xunit;
using static ParadoxModTool.ErrorLog.Tests.ErrorLogSessionTests;

namespace ParadoxModTool.ErrorLog.Tests;

public class ErrorLogLiveTests
{
    private static readonly DateTime FutureRun = DateTime.UtcNow.AddDays(1);

    private const string UnknownEffect = "[19:49:44][jomini_effect.cpp:556]: Unknown effect cmf_suppress at common/scripted_effects/purger.txt:2";
    private const string Engine = "[17:00:05][game_gui_types.cpp:1540]: couldn't convert parameter value to something we recognise";

    [Fact]
    public void AppendedLinesAreLiveNewAndCounted()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", Lines(UnknownEffect), FutureRun);
        session.StartLive();

        session.AppendLines(LogReader.Parse(new[] { Engine }, firstLineNumber: 2));

        Assert.True(session.LiveActive);
        Assert.Equal(2, session.Entries.Count);
        Assert.False(session.IsLiveNew(session.Entries[0]));
        Assert.True(session.IsLiveNew(session.Entries[1]));
        Assert.Equal(1, session.LiveNewCount(ErrorFilter.All));
    }

    [Fact]
    public void MarkLiveSeenResetsTheCounterButNotTheMarkers()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", Lines(UnknownEffect), FutureRun);
        session.StartLive();
        session.AppendLines(LogReader.Parse(new[] { Engine }, firstLineNumber: 2));

        session.MarkLiveSeen();
        Assert.Equal(0, session.LiveNewCount(ErrorFilter.All));
        Assert.True(session.IsLiveNew(session.Entries[1]));

        session.AppendLines(LogReader.Parse(new[] { UnknownEffect }, firstLineNumber: 3));
        Assert.Equal(1, session.LiveNewCount(ErrorFilter.All));
    }

    [Fact]
    public void LinesOfANewFilePartAreLiveNewAfterTheOldPart()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", LogReader.Parse(new[] { UnknownEffect, Engine }), FutureRun);
        session.StartLive();

        session.AppendLines(LogReader.Parse(new[] { UnknownEffect }, firstLineNumber: 1, part: 1));

        Assert.True(session.IsLiveNew(session.Entries[^1]));
        Assert.False(session.IsLiveNew(session.Entries[0]));
        Assert.Equal(1, session.LiveNewCount(ErrorFilter.All));
    }

    [Fact]
    public void LiveNewCountFollowsFilterAndSuppression()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", Lines(Engine), FutureRun);
        session.StartLive();
        session.AppendLines(LogReader.Parse(new[] { UnknownEffect, Engine }, firstLineNumber: 2));

        Assert.Equal(2, session.LiveNewCount(ErrorFilter.All));
        Assert.Equal(1, session.LiveNewCount(ErrorFilter.All with { Warnings = false }));

        session.Suppress(new Suppression(SuppressionScope.Code, "EFF-001"));

        Assert.Equal(1, session.LiveNewCount(ErrorFilter.All));
    }

    [Fact]
    public void AppendLinesMergesContinuationIntoTheLastEntry()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", Lines("[12:45:54][jomini_script_system.cpp:256]: Script system error!"), FutureRun);
        session.StartLive();

        session.AppendLines(LogReader.Parse(new[] { "  Error: x", "  Script location: common/a.txt:3" }, firstLineNumber: 2));

        var entry = Assert.Single(session.Entries);
        Assert.Contains("Script location: common/a.txt:3", entry.Message);
    }

    [Fact]
    public void StartLiveAfterRotationMarksEverythingAndStopLiveClearsIt()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", Lines(UnknownEffect, Engine), FutureRun);

        session.StartLive(everythingIsNew: true);
        Assert.All(session.Entries, e => Assert.True(session.IsLiveNew(e)));
        Assert.Equal(2, session.LiveNewCount(ErrorFilter.All));

        session.StopLive();
        Assert.False(session.LiveActive);
        Assert.False(session.IsLiveNew(session.Entries[0]));
        Assert.Equal(0, session.LiveNewCount(ErrorFilter.All));
    }
}
