using ParadoxModTool.ErrorLog;
using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class TickMapTests
{
    private const string Env = "[jominiapplication.cpp:571]: ";

    private static List<LogLine> Parse(params string[] lines) => LogReader.Parse(lines);

    [Fact]
    public void Host_ticks_map_wall_time_to_game_date_and_find_the_oos_tick()
    {
        var map = TickMap.Parse(Parse(
            "[22:47:12]" + Env + "Host_1 Tick: 1425.2.3.22",
            "[22:47:13]" + Env + "Host_1 Tick: 1425.2.4",
            "[22:47:13]" + Env + "Host_1 Tick: 1425.2.4.2",
            "[22:47:13]" + Env + "Host_1 Tick: 1425.2.4.8",
            "[22:47:13]" + Env + "Pause Toggled! Paused: true, PlayerID: 0",
            "[22:47:13]" + Env + "OOS Detected! OOS ID: 11c0de175691b2bbbb7330e63084ac66",
            "[22:47:13]" + Env + "Writing OOS Dumps..."));

        Assert.Equal("Host", map.Role);
        Assert.Equal(1, map.MachineId);
        Assert.Equal(4, map.Ticks.Count);
        Assert.Equal("1425.2.4.8", map.OosTick!.Date);
        Assert.Equal("1425.2.4", map.OosTick.Day);
        Assert.Equal("1425.2.4", map.Ticks[1].Date);
        Assert.Equal("1425.2.4", map.Ticks[1].Day);
        Assert.Equal(22 * 3600 + 47 * 60 + 13, map.OosTick.WallSeconds);
    }

    [Fact]
    public void Client_ticks_are_read_with_their_own_role()
    {
        var map = TickMap.Parse(Parse(
            "[22:47:12]" + Env + "Client_2 Tick: 1425.2.3.22",
            "[22:47:13]" + Env + "Client_2 Tick: 1425.2.4",
            "[22:47:13]" + Env + "OOS Detected! OOS ID: 11c0de175691b2bbbb7330e63084ac66"));

        Assert.Equal("Client", map.Role);
        Assert.Equal(2, map.MachineId);
        Assert.Equal("1425.2.4", map.OosTick!.Date);
    }

    [Fact]
    public void A_machine_number_too_large_for_an_int_leaves_the_machine_unknown_but_keeps_the_ticks()
    {
        var map = TickMap.Parse(Parse(
            "[22:47:12]" + Env + "Host_99999999999999999999 Tick: 1425.2.3.22",
            "[22:47:13]" + Env + "OOS Detected! OOS ID: 11c0de175691b2bbbb7330e63084ac66"));

        Assert.Equal("Host", map.Role);
        Assert.Null(map.MachineId);
        Assert.Equal("1425.2.3.22", map.OosTick!.Date);
    }

    [Fact]
    public void A_log_without_tick_lines_has_no_role_and_no_oos_tick()
    {
        var map = TickMap.Parse(Parse(
            "[22:47:12]" + Env + "Hosting Game...",
            "[22:47:13]" + Env + "OOS Detected! OOS ID: 11c0de175691b2bbbb7330e63084ac66"));

        Assert.Null(map.Role);
        Assert.Null(map.MachineId);
        Assert.Empty(map.Ticks);
        Assert.Null(map.OosTick);
        Assert.Null(map.WindowStart(3));
        Assert.Null(map.DateAt(100));
    }

    [Fact]
    public void A_log_without_an_oos_line_has_no_oos_tick()
    {
        var map = TickMap.Parse(Parse("[22:47:12]" + Env + "Host_1 Tick: 1425.2.3.22"));

        Assert.Null(map.OosTick);
        Assert.Null(map.WindowStart(1));
    }

    [Fact]
    public void Wall_clock_rolls_over_at_midnight()
    {
        var lines = Parse(
            "[23:59:58]" + Env + "Host_1 Tick: 1425.2.4.22",
            "[00:00:02]" + Env + "Host_1 Tick: 1425.2.5");
        var map = TickMap.Parse(lines);

        var absolute = WallClock.Absolute(lines);
        Assert.Equal(86_398, absolute[0]);
        Assert.Equal(86_402, absolute[1]);
        Assert.Equal("1425.2.4.22", map.DateAt(86_400 + 1));
        Assert.Equal("1425.2.5", map.DateAt(86_402));
    }

    [Fact]
    public void Wall_clock_adds_a_day_only_for_jumps_back_of_more_than_twelve_hours()
    {
        var lines = Parse(
            "[10:00:00]" + Env + "a",
            "[09:59:59]" + Env + "small step back",
            "[21:59:59]" + Env + "b",
            "[09:00:00]" + Env + "c: more than 12 h before the previous one");

        var absolute = WallClock.Absolute(lines);

        Assert.Equal(new long[] { 36_000, 35_999, 79_199, 86_400 + 32_400 }, absolute);
    }

    [Fact]
    public void Wall_clock_never_subtracts_the_first_line()
    {
        var absolute = WallClock.Absolute(Parse("[21:07:11]" + Env + "first", "[21:07:12]" + Env + "second"));

        Assert.Equal(new long[] { 21 * 3600 + 7 * 60 + 11, 21 * 3600 + 7 * 60 + 12 }, absolute);
    }

    [Fact]
    public void Window_start_counts_distinct_game_days_backwards_from_the_oos()
    {
        var map = TickMap.Parse(Parse(
            "[10:00:00]" + Env + "Host_1 Tick: 1425.2.1.20",
            "[10:00:01]" + Env + "Host_1 Tick: 1425.2.1.22",
            "[10:00:02]" + Env + "Host_1 Tick: 1425.2.2",
            "[10:00:03]" + Env + "Host_1 Tick: 1425.2.2.2",
            "[10:00:04]" + Env + "Host_1 Tick: 1425.2.3",
            "[10:00:05]" + Env + "Host_1 Tick: 1425.2.4",
            "[10:00:06]" + Env + "Host_1 Tick: 1425.2.4.2",
            "[10:00:06]" + Env + "OOS Detected! OOS ID: 1"));
        const long ten = 10 * 3600;

        Assert.Equal(ten + 5, map.WindowStart(0));
        Assert.Equal(ten + 4, map.WindowStart(1));
        Assert.Equal(ten + 2, map.WindowStart(2));
        Assert.Equal(ten + 0, map.WindowStart(3));
        Assert.Equal(ten + 0, map.WindowStart(30));
    }

    [Fact]
    public void Logs_starting_earlier_than_the_server_log_share_its_wall_clock_origin()
    {
        var server = Parse(
            "[21:09:24]" + Env + "Host_1 Tick: 1413.7.12.2",
            "[22:47:09]" + Env + "Host_1 Tick: 1425.2.1.14",
            "[22:47:09]" + Env + "Host_1 Tick: 1425.2.1.16",
            "[22:47:10]" + Env + "Host_1 Tick: 1425.2.1.18",
            "[22:47:13]" + Env + "OOS Detected! OOS ID: 1");
        var error = Parse(
            "[21:07:00][pdx_gui_factory.cpp:282]: first line starts before the server log",
            "[22:47:09][audio2_wwise.cpp:721]: line at the tick second");
        var map = TickMap.Parse(server);

        var errorWall = WallClock.Absolute(error);

        Assert.Null(map.DateAt(errorWall[0]));
        Assert.Equal("1425.2.1.16", map.DateAt(errorWall[1]));
    }

    private static List<LogLine> TwoOosLog() => Parse(
        "[10:00:01]" + Env + "Host_1 Tick: 1431.4.12.2",
        "[10:00:02]" + Env + "Host_1 Tick: 1431.4.12.4",
        "[10:00:02]" + Env + "OOS Detected! OOS ID: 2e5541e097da87cefc47d15d13967125",
        "[10:30:01]" + Env + "Host_1 Tick: 1439.7.18.4",
        "[10:30:02]" + Env + "Host_1 Tick: 1439.7.18.6",
        "[10:30:02]" + Env + "OOS Detected! OOS ID: 9591f7274fdf952e5715cdd232582018");

    [Fact]
    public void The_oos_tick_is_the_one_before_the_oos_line_with_the_requested_id()
    {
        var second = TickMap.Parse(TwoOosLog(), "9591f7274fdf952e");
        var first = TickMap.Parse(TwoOosLog(), "2e5541e097da87ce");

        Assert.Equal("1439.7.18.6", second.OosTick!.Date);
        Assert.Equal("1431.4.12.4", first.OosTick!.Date);
        Assert.Equal(["2e5541e097da87cefc47d15d13967125", "9591f7274fdf952e5715cdd232582018"], second.OosIds);
    }

    [Fact]
    public void Without_an_id_or_with_an_unknown_id_the_oos_tick_is_the_one_before_the_last_oos_line()
    {
        Assert.Equal("1439.7.18.6", TickMap.Parse(TwoOosLog()).OosTick!.Date);
        Assert.Equal("1439.7.18.6", TickMap.Parse(TwoOosLog(), "ffffffffffffffff").OosTick!.Date);
    }
}
