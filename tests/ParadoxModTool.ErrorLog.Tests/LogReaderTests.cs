using System.Text;
using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class LogReaderTests
{
    [Fact]
    public void ParseSplitsEnvelopeIntoTimeSourceAndMessage()
    {
        var lines = LogReader.Parse(new[]
        {
            "[19:49:44][jomini_effect.cpp:556]: Unknown effect cmf_show_alert at common/scripted_effects/01_iv2_alert_effects.txt:47",
            "[16:59:05][jomini_effect.cpp:1207]:  Possible match: 'iv2_var_national_idea_cost_adm'",
        });

        Assert.Equal(2, lines.Count);
        Assert.Equal("19:49:44", lines[0].Time);
        Assert.Equal("jomini_effect.cpp", lines[0].Source);
        Assert.Equal(556, lines[0].SourceLine);
        Assert.StartsWith("Unknown effect cmf_show_alert", lines[0].Message);
        Assert.Equal(1, lines[0].LineNumber);
        Assert.Equal("Possible match: 'iv2_var_national_idea_cost_adm'", lines[1].Message);
        Assert.Equal(2, lines[1].LineNumber);
    }

    [Fact]
    public void ParseReadsTheLevelFieldOfCk3Lines()
    {
        // Crusader Kings III schreibt zwischen Uhrzeit und Quelle ein Level ([E] Fehler, [D] debug.log).
        var lines = LogReader.Parse(new[]
        {
            "[11:21:13][E][dlc_descriptor.cpp:70]: Invalid supported_version in file: mod/Crusader Wars.mod line: 7",
            "[11:23:28][D][jomini_effect_impl.cpp:450]: file: events/pmt_probe_events.txt line: 9 (pmt_probe.1:immediate): PMT probe event 1",
        });

        Assert.Equal(2, lines.Count);
        Assert.Equal("11:21:13", lines[0].Time);
        Assert.Equal("dlc_descriptor.cpp", lines[0].Source);
        Assert.Equal(70, lines[0].SourceLine);
        Assert.Equal("Invalid supported_version in file: mod/Crusader Wars.mod line: 7", lines[0].Message);
        Assert.Equal("jomini_effect_impl.cpp", lines[1].Source);
        Assert.StartsWith("file: events/pmt_probe_events.txt line: 9", lines[1].Message);
    }

    [Fact]
    public void ParseAppendsIndentedLinesWithoutTimestampToThePreviousLine()
    {
        var lines = LogReader.Parse(new[]
        {
            "[12:45:54][jomini_script_system.cpp:256]: Script system error!",
            "  Error: Event target link 'government_type' returned an unset scope",
            "  Script location: common/building_types/01_IV2_idea_buildings.txt:839",
            "",
            "[12:45:55][other.cpp:1]: next",
        });

        Assert.Equal(2, lines.Count);
        Assert.Equal(
            "Script system error!\nError: Event target link 'government_type' returned an unset scope\nScript location: common/building_types/01_IV2_idea_buildings.txt:839",
            lines[0].Message);
        Assert.Equal(5, lines[1].LineNumber);
    }

    [Fact]
    public void ParseKeepsUnknownLinesAsMessageWithoutTime()
    {
        // Ohne vorige Meldung bleibt die Zeile eine eigene; beim Anhängen einer Leseportion gehört sie zur letzten Meldung.
        var line = Assert.Single(LogReader.Parse(new[] { "irgendein Text ohne Hülle" }));

        Assert.Equal(string.Empty, line.Time);
        Assert.Equal("irgendein Text ohne Hülle", line.Message);
        Assert.True(line.IsContinuation);
        var appended = new List<LogLine>();
        LogReader.Append(appended, new[] { line });
        Assert.False(Assert.Single(appended).IsContinuation);
    }

    [Fact]
    public void ParseAppendsUnindentedLinesWithoutTimestampToThePreviousLine()
    {
        // Aufrufkette nach "Script location:" und mehrzeilige Lesefehler schreibt das Spiel ohne Hülle und ohne Einrückung.
        var lines = LogReader.Parse(new[]
        {
            "[15:12:41][jomini_script_system.cpp:252]: Script system error!",
            "  Error: Event target link 'var' returned an unset scope",
            "  Script location: common/scripted_triggers/war_triggers.txt:33",
            "common/peace_treaties/scaligeri_peace_treaties.txt:109",
            "common/scripted_triggers/war_triggers.txt:126",
            "",
            "[15:09:56][pdx_persistent_reader.cpp:289]: Error: \"Failed to read key reference: a_1: a_1, near line: 3",
            "Failed to read key reference: b_2: b_2, near line: 3\" in file: \"\" near line: 3",
        });

        Assert.Equal(2, lines.Count);
        Assert.Equal(
            "Script system error!\nError: Event target link 'var' returned an unset scope\nScript location: common/scripted_triggers/war_triggers.txt:33"
            + "\ncommon/peace_treaties/scaligeri_peace_treaties.txt:109\ncommon/scripted_triggers/war_triggers.txt:126",
            lines[0].Message);
        Assert.EndsWith("\nFailed to read key reference: b_2: b_2, near line: 3\" in file: \"\" near line: 3", lines[1].Message);
        Assert.Equal(7, lines[1].LineNumber);
    }

    [Fact]
    public void ReadFromReturnsOnlyNewLinesAndAdvancesThePosition()
    {
        var path = TempLog("[10:00:00][a.cpp:1]: eins\n[10:00:01][a.cpp:1]: zwei\n");
        try
        {
            var first = LogReader.ReadFrom(path, LogPosition.Start);
            Assert.Equal(new[] { "eins", "zwei" }, first.Lines.Select(l => l.Message));
            Assert.False(first.Restarted);

            File.AppendAllText(path, "[10:00:02][a.cpp:1]: drei\n");
            var second = LogReader.ReadFrom(path, first.Next);

            var line = Assert.Single(second.Lines);
            Assert.Equal("drei", line.Message);
            Assert.Equal(3, line.LineNumber);
            Assert.False(second.Restarted);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadFromStopsBeforeAnIncompleteLastLine()
    {
        var path = TempLog("[10:00:00][a.cpp:1]: eins\n[10:00:01][a.cpp:1]: halb");
        try
        {
            var first = LogReader.ReadFrom(path, LogPosition.Start);
            Assert.Equal(new[] { "eins" }, first.Lines.Select(l => l.Message));

            File.AppendAllText(path, "fertig\n");
            var second = LogReader.ReadFrom(path, first.Next);
            Assert.Equal(new[] { "halbfertig" }, second.Lines.Select(l => l.Message));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadFromDetectsRotationByShrinkingOrChangedHead()
    {
        var path = TempLog("[10:00:00][a.cpp:1]: alter Lauf mit langer Zeile\n");
        try
        {
            var first = LogReader.ReadFrom(path, LogPosition.Start);

            File.WriteAllText(path, "[11:00:00][a.cpp:1]: neu\n");
            var shrunk = LogReader.ReadFrom(path, first.Next);
            Assert.True(shrunk.Restarted);
            Assert.Equal(new[] { "neu" }, shrunk.Lines.Select(l => l.Message));
            Assert.Equal(1, shrunk.Lines[0].LineNumber);

            File.WriteAllText(path, "[12:00:00][b.cpp:2]: anderer Anfang, aber diesmal deutlich länger als vorher\n");
            var changed = LogReader.ReadFrom(path, shrunk.Next);
            Assert.True(changed.Restarted);
            Assert.Single(changed.Lines);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadFromWorksWhileAnotherWriterHoldsTheFile()
    {
        var path = TempLog("[10:00:00][a.cpp:1]: eins\n");
        try
        {
            using var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
            var chunk = LogReader.ReadFrom(path, LogPosition.Start);
            Assert.Single(chunk.Lines);
            Assert.Single(LogReader.ReadFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadFromSkipsTheByteOrderMark()
    {
        var path = Path.Combine(Path.GetTempPath(), "pmt-log-" + Guid.NewGuid().ToString("N") + ".log");
        File.WriteAllText(path, "[10:00:00][a.cpp:1]: ä\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        try
        {
            var line = Assert.Single(LogReader.ReadFrom(path, LogPosition.Start).Lines);
            Assert.Equal("10:00:00", line.Time);
            Assert.Equal("ä", line.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AppendMergesContinuationLinesAcrossChunks()
    {
        var existing = LogReader.Parse(new[] { "[12:45:54][jomini_script_system.cpp:256]: Script system error!" });
        var chunk = LogReader.Parse(new[] { "  Error: x", "  Script location: common/a.txt:3", "[12:45:55][b.cpp:1]: next" }, firstLineNumber: 2);

        Assert.True(chunk[0].IsContinuation);
        LogReader.Append(existing, chunk);

        Assert.Equal(2, existing.Count);
        Assert.Equal("Script system error!\nError: x\nScript location: common/a.txt:3", existing[0].Message);
        Assert.Equal("next", existing[1].Message);
    }

    [Fact]
    public void PartIsStampedAndOrdersAcrossParts()
    {
        var older = LogReader.Parse(new[] { "[10:00:00][a.cpp:1]: a", "[10:00:01][a.cpp:1]: b" }, part: 0);
        var newer = LogReader.Parse(new[] { "[10:00:02][a.cpp:1]: c" }, part: 1);

        Assert.All(older, l => Assert.Equal(0, l.Part));
        Assert.Equal(1, newer[0].Part);
        Assert.True(newer[0].Order > older[1].Order);
        Assert.True(older[1].Order > older[0].Order);
    }

    [Fact]
    public void FirstAndLastTimeSkipLinesWithoutEnvelope()
    {
        using var tree = new TempTree(("logs/error.log",
            "  Fortsetzung ohne Hülle\n[10:00:05][a.cpp:1]: eins\n[10:00:09][a.cpp:1]: zwei\n  Error: x\n  Script location: a.txt:3\n"));

        var (first, last) = LogReader.FirstAndLastTime(tree.PathOf("logs/error.log"));

        Assert.Equal(new TimeSpan(10, 0, 5), first);
        Assert.Equal(new TimeSpan(10, 0, 9), last);
    }

    [Fact]
    public void LastTimeIsFoundBehindALongBlockWithoutEnvelope()
    {
        var block = string.Concat(Enumerable.Repeat("  Script location: common/a.txt:3\n", 4000));
        using var tree = new TempTree(("logs/error.log", "[09:00:00][a.cpp:1]: a\n[09:30:00][a.cpp:1]: b\n" + block));

        var (_, last) = LogReader.FirstAndLastTime(tree.PathOf("logs/error.log"));

        Assert.Equal(new TimeSpan(9, 30, 0), last);
    }

    [Fact]
    public void FileWithoutEnvelopeHasNoTimes()
    {
        using var tree = new TempTree(("logs/error.log", string.Empty), ("logs/b.log", "nur Text\n"));

        Assert.Equal(((TimeSpan?)null, (TimeSpan?)null), LogReader.FirstAndLastTime(tree.PathOf("logs/error.log")));
        Assert.Equal(((TimeSpan?)null, (TimeSpan?)null), LogReader.FirstAndLastTime(tree.PathOf("logs/b.log")));
    }

    [Fact]
    public void ReadFromStampsThePart()
    {
        using var tree = new TempTree(("logs/error.log", "[10:00:00][a.cpp:1]: a\n"));

        var chunk = LogReader.ReadFrom(tree.PathOf("logs/error.log"), LogPosition.Start, part: 2);

        Assert.Equal(2, Assert.Single(chunk.Lines).Part);
        Assert.False(chunk.Continued);
    }

    [Fact]
    public void AppendMergesUnindentedContinuationLinesAcrossChunks()
    {
        var existing = LogReader.Parse(new[] { "[12:45:54][jomini_script_system.cpp:256]: Script system error!", "  Script location: common/a.txt:3" });
        var chunk = LogReader.Parse(new[] { "common/b.txt:9", "[12:45:55][b.cpp:1]: next" }, firstLineNumber: 3);

        LogReader.Append(existing, chunk);

        Assert.Equal(2, existing.Count);
        Assert.Equal("Script system error!\nScript location: common/a.txt:3\ncommon/b.txt:9", existing[0].Message);
    }

    private static string TempLog(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "pmt-log-" + Guid.NewGuid().ToString("N") + ".log");
        File.WriteAllText(path, content);
        return path;
    }
}
