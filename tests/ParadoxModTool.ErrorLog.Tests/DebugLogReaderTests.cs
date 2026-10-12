using System.Text;
using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class DebugLogReaderTests
{
    private const string Source = "jomini_effect_impl.cpp";

    /// <summary>Ausschnitt aus dem Probelauf (debug.log, 2026-09-26), Marker auf Hex-Ids umgestellt.</summary>
    private const string Probe = """
        [15:17:58][jomini_effect_impl.cpp:479]: common/on_action/pmt_scope_probe_on_actions.txt:19: PMT_TRACE_0001
        [15:17:58][jomini_effect_impl.cpp:2666]: Country 'England' (ENG)
        [15:17:58][jomini_effect_impl.cpp:479]: common/on_action/pmt_scope_probe_on_actions.txt:24: PMT_TRACE_0002
        [15:17:58][jomini_effect_impl.cpp:2666]: Country 'England' (ENG)
        Root: Country 'England' (ENG)

        Saved event targets:
        [15:17:58][pdx_data_factory.cpp:1426]: Could not find data system function 'GetName' in 'ROOT.GetName'.
        [15:17:58][jomini_effect_impl.cpp:479]: common/on_action/pmt_scope_probe_on_actions.txt:32: PMT_TRACE_0004
        [15:17:58][jomini_effect_impl.cpp:2666]: Character 'King Edward III Plantagenet' (97)
        Root: Country 'England' (ENG)

        Saved event targets:
        [15:17:58][jomini_effect_impl.cpp:479]: common/on_action/pmt_scope_probe_on_actions.txt:39: PMT_TRACE_0005
        [15:17:58][jomini_effect_impl.cpp:2666]: Location 'London' (1550)
        Root: Country 'England' (ENG)

        Saved event targets:
        pmt_probe_capital: Location 'London' (1550)
        [15:17:58][jomini_effect_impl.cpp:479]: common/on_action/pmt_scope_probe_on_actions.txt:44: PMT_TRACE_5_AFTER_ERROR
        [15:17:59][jomini_effect_impl.cpp:479]: common/on_action/other.txt:3: mein eigener Text
        [15:17:59][jomini_effect_impl.cpp:2666]: Country 'France' (FRA)
        """;

    private static IReadOnlyList<TraceHit> Hits(string text) => DebugLogReader.Hits(LogReader.Parse(text.Split('\n')), Source);

    /// <summary>Victoria 3 (Probelauf 2026-10-10, deutsche Oberfläche): Scope als "Art: Name (Id)" mit übersetzter Art, Marker hier eingesetzt.</summary>
    [Fact]
    public void Vic3_scope_line_gives_a_hit_with_kind_name_and_id()
    {
        const string Vic3 = """
            [22:11:03][jomini_effect_impl.cpp:454]: events/pmt_probe_events.txt:9: PMT_TRACE_0001
            [22:11:03][jomini_effect_impl.cpp:2501]: Land: Belgien (25)
            Root: Land: Belgien (25)

            Saved event targets:
            pmt_probe_saved: Staat: Brabant (113)
            """;

        var hit = Assert.Single(Hits(Vic3));

        Assert.Equal("events/pmt_probe_events.txt", hit.File);
        Assert.Equal(9, hit.Line);
        Assert.Equal(new TraceScope("Land", "Belgien", "25"), hit.Scope);
        Assert.Equal(new TraceScope("Land", "Belgien", "25"), hit.Root);
        Assert.Equal(("pmt_probe_saved", new TraceScope("Staat", "Brabant", "113")), Assert.Single(hit.Saved));
    }

    [Fact]
    public void ProbeExcerptGivesCountryRulerAndCapitalHits()
    {
        var hits = Hits(Probe);

        Assert.Equal(new[] { "0001", "0002", "0004", "0005" }, hits.Select(h => h.Id));
        Assert.Equal(new TraceScope("Country", "England", "ENG"), hits[0].Scope);
        Assert.Null(hits[0].Root);
        Assert.Equal(("common/on_action/pmt_scope_probe_on_actions.txt", 19, "15:17:58"), (hits[0].File, hits[0].Line, hits[0].Time));
        Assert.Equal(new TraceScope("Character", "King Edward III Plantagenet", "97"), hits[2].Scope);
        Assert.Equal(new TraceScope("Country", "England", "ENG"), hits[2].Root);
        var saved = Assert.Single(hits[3].Saved);
        Assert.Equal(("pmt_probe_capital", new TraceScope("Location", "London", "1550")), (saved.Name, saved.Scope));
    }

    [Fact]
    public void ForeignDebugLogLinesAreIgnored()
    {
        Assert.DoesNotContain(Hits(Probe), h => h.File == "common/on_action/other.txt");
    }

    [Fact]
    public void ATraceWithoutScopeLineIsSkipped()
    {
        var hits = Hits("[10:00:00][jomini_effect_impl.cpp:479]: a.txt:1: PMT_TRACE_00aa\n[10:00:00][other.cpp:1]: x\n");

        Assert.Empty(hits);
    }

    /// <summary>Ausschnitt aus dem Probelauf von Crusader Kings III (debug.log, 2026-09-30), Text auf Spur-Marker umgestellt.</summary>
    private const string Ck3Probe = """
        [11:23:28][D][jomini_effect_impl.cpp:450]: file: common/on_action/pmt_probe_on_actions.txt line: 8 (pmt_probe_start): PMT_TRACE_0001
        [11:23:28][D][effectimpl.cpp:642]: Vratislav Přemyslid of d_bohemia (Internal ID: 32769 - Historical ID 522) weak (Character - 32769)!
        [11:23:28][D][jomini_effect_impl.cpp:450]: file: events/pmt_probe_events.txt line: 9 (pmt_probe.1:immediate): PMT_TRACE_0002
        [11:23:28][D][effectimpl.cpp:642]: Vratislav Přemyslid of d_bohemia (Internal ID: 32769 - Historical ID 522) weak (Character - 32769)!
        Root: Vratislav Přemyslid of d_bohemia (Internal ID: 32769 - Historical ID 522) weak (Character - 32769)!

        Saved event targets:
        new_nomad_title: The Hasanid Nomadic Camp (Internal ID: 18173 - Internal Key: x_c_nomad_714) weak (Landed_title - 18173)!
        pool_province: Edo (Internal ID: 8073) weak (Province - 8073)!
        """;

    [Fact]
    public void Ck3_trace_lines_with_a_separate_scope_source()
    {
        var hits = DebugLogReader.Hits(LogReader.Parse(Ck3Probe.Split('\n')), Source, "effectimpl.cpp");

        Assert.Equal(2, hits.Count);
        Assert.Equal("0001", hits[0].Id);
        Assert.Equal("common/on_action/pmt_probe_on_actions.txt", hits[0].File);
        Assert.Equal(8, hits[0].Line);
        Assert.Equal(new TraceScope("Character", "Vratislav Přemyslid of d_bohemia", "32769"), hits[0].Scope);
        Assert.Equal("events/pmt_probe_events.txt", hits[1].File);
        Assert.Equal(9, hits[1].Line);
        Assert.Equal(new TraceScope("Character", "Vratislav Přemyslid of d_bohemia", "32769"), hits[1].Root);
        Assert.Equal(
            new[] { ("new_nomad_title", new TraceScope("Landed_title", "The Hasanid Nomadic Camp", "18173")), ("pool_province", new TraceScope("Province", "Edo", "8073")) },
            hits[1].Saved);
    }

    [Fact]
    public void Ck3_scope_of_a_lowborn_character_without_historical_id()
    {
        // Probelauf 1.20.0.2 (2026-09-30): Figuren ohne Historical ID schreiben "(Internal ID 47526)" ohne Doppelpunkt.
        Assert.Equal(
            new TraceScope("Character", "Yeldem Lowborn of", "47526"),
            TraceScope.Parse("Yeldem Lowborn of  (Internal ID 47526) weak (Character - 47526)!"));
    }

    [Fact]
    public void Ck3_scope_line_from_another_source_is_ignored_without_scope_source()
    {
        Assert.Empty(DebugLogReader.Hits(LogReader.Parse(Ck3Probe.Split('\n')), Source));
    }

    [Fact]
    public void ReadHitsKeepsOnlyTraceLinesOfABigFile()
    {
        using var tree = new TempTree(("logs/debug.log", string.Empty));
        var path = tree.PathOf("logs/debug.log");
        var builder = new StringBuilder();
        for (var i = 0; i < 100_000; i++)
        {
            builder.Append("[10:00:00][pdx_persistent_reader.cpp:289]: Füllzeile ").Append(i).Append('\n');
            if (i is 500 or 99_000)
            {
                builder.Append("[10:00:01][jomini_effect_impl.cpp:479]: common/a.txt:5: PMT_TRACE_0abc\n");
                builder.Append("[10:00:01][jomini_effect_impl.cpp:2666]: Country 'France' (FRA)\nRoot: Country 'France' (FRA)\n");
            }
        }

        File.WriteAllText(path, builder.ToString());

        var (hits, end) = DebugLogReader.ReadHits(path, Source);

        Assert.Equal(2, hits.Count);
        Assert.All(hits, h => Assert.Equal(new TraceScope("Country", "France", "FRA"), h.Root));
        Assert.Equal(new FileInfo(path).Length, end.Offset);
        Assert.Empty(LogReader.ReadFrom(path, end).Lines);
    }
}
