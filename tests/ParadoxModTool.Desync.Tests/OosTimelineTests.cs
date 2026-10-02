using System.Text;
using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class OosTimelineTests
{
    private const string Env = "[jominiapplication.cpp:571]: ";

    private static string Join(params string[] lines) => string.Join("\n", lines) + "\n";

    private static string Server(string role, int machine, params (string Time, string Date)[] ticks)
    {
        var sb = new StringBuilder();
        foreach (var (time, date) in ticks)
            sb.Append('[').Append(time).Append(']').Append(Env).Append(role).Append('_').Append(machine)
                .Append(" Tick: ").Append(date).Append('\n');
        var last = ticks[^1].Time;
        sb.Append('[').Append(last).Append(']').Append(Env).Append("OOS Detected! OOS ID: 11c0de175691b2bbbb7330e63084ac66\n");
        return sb.ToString();
    }

    private static string GameLine(string time, string text) => $"[{time}][country.cpp:7468]: {text}";

    private static IReadOnlyList<TimelineEntry> Build(int gameDays, params OosDump[] dumps) =>
        OosTimeline.Build(dumps, Catalogs.Eu5, gameDays);

    [Fact]
    public void Entries_of_both_machines_are_merged_on_game_time_not_wall_time()
    {
        using var tree = new TempTree();
        // Die Host-Uhr geht 1 s vor: dieselbe Spielstunde liegt beim Host eine Sekunde später als beim Client.
        using var host = OosDump.Open(Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0,
            (DumpFileKind.DedicatedServer, Server("Host", 1, ("10:00:01", "1425.2.4.2"), ("10:00:02", "1425.2.4.4"), ("10:00:03", "1425.2.4.10"))),
            (DumpFileKind.Game, Join(GameLine("10:00:02", "Marriage arranged"), GameLine("10:00:03", "Later event")))));
        using var client = OosDump.Open(Dumps.Folder(tree, "client", 2, Dumps.ClientMetadata11c0,
            (DumpFileKind.DedicatedServer, Server("Client", 2, ("10:00:00", "1425.2.4.2"), ("10:00:01", "1425.2.4.4"), ("10:00:02", "1425.2.4.10"))),
            (DumpFileKind.Game, Join(GameLine("10:00:01", "Marriage arranged"), GameLine("10:00:02", "Later event")))));

        var entries = Build(1, client, host);

        Assert.Equal(new[] { 1, 2, 1, 2 }, entries.Select(e => e.MachineId));
        Assert.Equal(new[] { "1425.2.4.4", "1425.2.4.4", "1425.2.4.10", "1425.2.4.10" }, entries.Select(e => e.GameDate));
        Assert.Equal(new[] { "Marriage arranged", "Marriage arranged", "Later event", "Later event" },
            entries.Select(e => e.Line.Message));
        Assert.Equal(new[] { "Host", "Client", "Host", "Client" }, entries.Select(e => e.Role));
        Assert.All(entries, e => Assert.Equal(DumpFileKind.Game, e.Source));
    }

    [Fact]
    public void Lines_before_the_window_are_left_out()
    {
        using var tree = new TempTree();
        using var host = OosDump.Open(Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0,
            (DumpFileKind.DedicatedServer, Server("Host", 1,
                ("10:00:00", "1425.2.1"), ("10:00:10", "1425.2.2"), ("10:00:20", "1425.2.3"), ("10:00:30", "1425.2.3.2"))),
            (DumpFileKind.Game, Join(
                GameLine("10:00:05", "day one"),
                GameLine("10:00:15", "day two"),
                GameLine("10:00:25", "day three")))));

        var entries = Build(1, host);

        Assert.Equal(new[] { "day two", "day three" }, entries.Select(e => e.Line.Message));
        Assert.Equal(new[] { "1425.2.2", "1425.2.3" }, entries.Select(e => e.GameDate));
    }

    [Fact]
    public void Invalid_commands_are_leads_with_their_command_data()
    {
        using var tree = new TempTree();
        using var host = OosDump.Open(Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0,
            (DumpFileKind.DedicatedServer, Server("Host", 1, ("22:41:00", "1425.1.30"), ("22:47:13", "1425.2.4.8"))),
            (DumpFileKind.Multiplayer, Join(
                "[22:41:20][commanddispatcher.cpp:77]: Invalid command. ID: 95976, Type: upgrade_rgo, Recipient: BROADCAST",
                "[22:41:20][commanddispatcher.cpp:73]: Command data: ID: 95976, Type: upgrade_rgo, Recipient: BROADCAST, Message: 'upgrade_rgo={",
                "\tdefault_state={",
                "\t\tidentity=95976",
                "\t}",
                "\tobject={",
                "\t\tcountry=1183",
                "\t\tlocation=2750",
                "\t}",
                "}",
                "'",
                "[22:47:05][pdxinput_context.cpp:2869]: Could not push the provided stack context. ID: 0"))));

        var entries = Build(3, host);

        var invalid = Assert.Single(entries, e => e.Lead == LeadKind.InvalidCommand);
        Assert.Equal(DumpFileKind.Multiplayer, invalid.Source);
        Assert.Contains("upgrade_rgo", invalid.Line.Message);
        Assert.Contains("ID: 95976", invalid.Line.Message);
        var data = Assert.Single(entries, e => e.Line.Message.StartsWith("Command data:", StringComparison.Ordinal));
        Assert.Contains("country=1183", data.Line.Message);
        Assert.Equal(LeadKind.None, data.Lead);
        Assert.Equal(LeadKind.None, entries.Single(e => e.Line.Message.StartsWith("Could not push", StringComparison.Ordinal)).Lead);
    }

    [Fact]
    public void A_script_error_near_the_oos_is_a_lead()
    {
        using var tree = new TempTree();
        using var host = OosDump.Open(Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0,
            (DumpFileKind.DedicatedServer, Server("Host", 1, ("19:46:00", "1425.2.3"), ("19:46:10", "1425.2.4.8"))),
            (DumpFileKind.Error, Join(
                "[19:46:06][pdx_gui_layout.cpp:232]: gui/shared/main_menu_cooltip_types.gui:1320 - Unsupported widget layout dependencies.",
                "[19:46:08][jomini_script_system.cpp:248]: Script system error!",
                "  Error: Event target link 'scope' returned an unset scope",
                "  Script location: common/character_interactions/marry_noble.txt:92",
                "",
                "[19:46:09][audio2_wwise.cpp:721]: [Wwise] Event 'RULER_FAMILY_CHAR_COMES_OF_AGE' not found"))));

        var entries = Build(1, host);

        var script = Assert.Single(entries, e => e.Lead == LeadKind.ScriptError);
        Assert.Equal(DumpFileKind.Error, script.Source);
        Assert.Contains("marry_noble.txt:92", script.Line.Message);
        Assert.Equal("RUN-001", script.Error!.Code);
        Assert.Equal("common/character_interactions/marry_noble.txt", script.Error.FilePath);
        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public void An_error_without_a_file_reference_or_script_source_is_no_lead()
    {
        using var tree = new TempTree();
        using var host = OosDump.Open(Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0,
            (DumpFileKind.DedicatedServer, Server("Host", 1, ("19:46:00", "1425.2.3"), ("19:46:10", "1425.2.4.8"))),
            (DumpFileKind.Error, Join(
                "[19:46:08][audio2_wwise.cpp:721]: [Wwise] Event 'RULER_FAMILY_CHAR_COMES_OF_AGE' not found",
                "[19:46:09][jomini_script_system.cpp:248]: Script system error!",
                "  Error: Undefined event target 'target'",
                "  Script location: common/character_interactions/marry_noble.txt:92"))));

        var entries = Build(1, host);

        Assert.Equal(new[] { LeadKind.None, LeadKind.ScriptError }, entries.Select(e => e.Lead));
    }

    [Fact]
    public void A_message_seen_on_only_one_machine_is_marked()
    {
        using var tree = new TempTree();
        using var host = OosDump.Open(Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0,
            (DumpFileKind.DedicatedServer, Server("Host", 1, ("10:00:00", "1425.2.4"), ("10:00:05", "1425.2.4.8"))),
            (DumpFileKind.Game, Join(GameLine("10:00:01", "seen on both"), GameLine("10:00:02", "seen on both"),
                GameLine("10:00:03", "host only")))));
        using var client = OosDump.Open(Dumps.Folder(tree, "client", 2, Dumps.ClientMetadata11c0,
            (DumpFileKind.DedicatedServer, Server("Client", 2, ("10:00:00", "1425.2.4"), ("10:00:05", "1425.2.4.8"))),
            (DumpFileKind.Game, Join(GameLine("10:00:01", "seen on both"), GameLine("10:00:04", "client only")))));

        var entries = Build(1, host, client);

        var flags = entries.ToDictionary(e => (e.MachineId, e.Line.Message, e.Line.LineNumber), e => e.OnlyThisMachine);
        Assert.False(flags[(1, "seen on both", 1)]);
        Assert.False(flags[(2, "seen on both", 1)]);
        // Das zweite Vorkommen beim Host hat beim Client kein Gegenstück (Multimengen-Vergleich).
        Assert.True(flags[(1, "seen on both", 2)]);
        Assert.True(flags[(1, "host only", 3)]);
        Assert.True(flags[(2, "client only", 2)]);
    }

    [Fact]
    public void A_single_dump_marks_nothing_as_only_on_this_machine()
    {
        using var tree = new TempTree();
        using var host = OosDump.Open(Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0,
            (DumpFileKind.Game, Join(GameLine("10:00:01", "alone")))));

        var entries = Build(1, host);

        Assert.False(Assert.Single(entries).OnlyThisMachine);
    }

    [Fact]
    public void A_dump_without_tick_lines_still_lists_its_tail_without_game_dates()
    {
        using var tree = new TempTree();
        var lines = Enumerable.Range(1, 250).Select(i => GameLine("10:00:00", "line " + i)).ToArray();
        using var host = OosDump.Open(Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0,
            (DumpFileKind.Game, Join(lines))));

        var entries = Build(1, host);

        Assert.Equal(200, entries.Count);
        Assert.All(entries, e => Assert.Null(e.GameDate));
        Assert.Null(entries[0].Role);
        Assert.Equal("line 51", entries[0].Line.Message);
        Assert.Equal("line 250", entries[^1].Line.Message);
    }

    [Fact]
    public void Game_dates_are_ordered_numerically()
    {
        using var tree = new TempTree();
        using var host = OosDump.Open(Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0,
            (DumpFileKind.DedicatedServer, Server("Host", 1, ("10:00:00", "1425.2.4"), ("10:00:01", "1425.2.4.8"), ("10:00:02", "1425.2.4.10"))),
            (DumpFileKind.Game, Join(GameLine("10:00:01", "eight"), GameLine("10:00:02", "ten")))));
        using var client = OosDump.Open(Dumps.Folder(tree, "client", 2, Dumps.ClientMetadata11c0,
            (DumpFileKind.DedicatedServer, Server("Client", 2, ("10:00:00", "1425.2.4"), ("10:00:01", "1425.2.4.8"), ("10:00:02", "1425.2.4.10"))),
            (DumpFileKind.Game, Join(GameLine("10:00:01", "eight")))));

        var entries = Build(1, client, host);

        Assert.Equal(new[] { "1425.2.4.8", "1425.2.4.8", "1425.2.4.10" }, entries.Select(e => e.GameDate));
    }

    [Fact]
    public void The_window_ends_at_the_oos_of_the_pair_when_the_log_holds_an_earlier_oos()
    {
        using var tree = new TempTree();
        var server = Join(
            $"[10:00:00]{Env}Host_1 Tick: 1431.4.11",
            $"[10:00:10]{Env}Host_1 Tick: 1431.4.12",
            $"[10:00:10]{Env}OOS Detected! OOS ID: 2e5541e097da87cefc47d15d13967125",
            $"[10:30:00]{Env}Host_1 Tick: 1439.7.17",
            $"[10:30:10]{Env}Host_1 Tick: 1439.7.18",
            $"[10:30:10]{Env}OOS Detected! OOS ID: 11c0de175691b2bbbb7330e63084ac66");
        using var host = OosDump.Open(Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0,
            (DumpFileKind.DedicatedServer, server),
            (DumpFileKind.Game, Join(
                GameLine("10:00:05", "first oos"),
                GameLine("10:00:15", "between"),
                GameLine("10:30:05", "own oos day"),
                GameLine("10:30:15", "last day")))));

        var entries = Build(0, host);

        // Fenster = nur der letzte Spieltag vor dem eigenen OOS, nicht der Tag vor dem früheren.
        Assert.Equal(new[] { "last day" }, entries.Select(e => e.Line.Message));
        Assert.Equal(new[] { "1439.7.18" }, entries.Select(e => e.GameDate));
    }
}
