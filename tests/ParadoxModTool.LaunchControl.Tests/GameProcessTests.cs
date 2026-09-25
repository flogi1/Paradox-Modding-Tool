using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public class GameProcessTests
{
    [Fact]
    public void RunningSinceFindsProcessesByExeName()
    {
        Assert.NotEmpty(GameProcess.RunningSince(Environment.ProcessPath));
        Assert.Empty(GameProcess.RunningSince(@"C:\nirgends\surely-not-running-4711.exe"));
        Assert.Empty(GameProcess.RunningSince(null));
    }

    [Fact]
    public void DebugSessionIsAProcessStartedAfterALaunchWithDebugMode()
    {
        var launched = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var tracker = new DebugLaunchTracker();
        tracker.Launched(@"C:\Game\binaries\eu5.exe", "-debug_mode --ignore-disable-mods-on-crash", launched);

        Assert.True(tracker.IsDebugSession(@"D:\Other\eu5.exe", new[] { launched.AddSeconds(3) }));
        Assert.False(tracker.IsDebugSession(@"C:\Game\binaries\eu5.exe", new[] { launched.AddMinutes(-10) }));
        Assert.False(tracker.IsDebugSession(@"C:\Game\eu4.exe", new[] { launched.AddSeconds(3) }));

        tracker.Forget();
        Assert.False(tracker.IsDebugSession(@"C:\Game\binaries\eu5.exe", new[] { launched.AddSeconds(3) }));

        tracker.Launched(@"C:\Game\binaries\eu5.exe", string.Empty, launched);
        Assert.False(tracker.IsDebugSession(@"C:\Game\binaries\eu5.exe", new[] { launched.AddSeconds(3) }));
    }

    [Fact]
    public void DebugArgumentsComeFromTheFirstOptionWithDebugMode()
    {
        Assert.True(DebugLaunchTracker.HasDebugFlag("-debug_mode"));
        Assert.False(DebugLaunchTracker.HasDebugFlag("-debug_modeX"));
        Assert.Equal("-debug_mode", DebugLaunchTracker.DebugArguments(new[] { string.Empty, "-debug_mode", "-debug_mode --ignore-disable-mods-on-crash" }));
        Assert.Null(DebugLaunchTracker.DebugArguments(new[] { string.Empty }));
    }

    [Fact]
    public void NewFilesNeedARestartUntilTheGameStartsAfterThem()
    {
        var gameStart = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var tracker = new NewFileTracker();
        tracker.Created(@"C:\Mod\gui\old.gui", gameStart.AddMinutes(-5));
        tracker.Created(@"C:\Mod\gui\m_overrides.gui", gameStart.AddMinutes(3));
        tracker.Created(@"C:\Mod\gui\undone.gui", gameStart.AddMinutes(4));
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\Mod\gui\old.gui", @"C:\Mod\gui\M_OVERRIDES.gui" };

        Assert.Equal(new[] { @"C:\Mod\gui\m_overrides.gui" }, tracker.NeedRestart(new[] { gameStart }, existing.Contains));
        Assert.Empty(tracker.NeedRestart(new[] { gameStart.AddMinutes(10) }, existing.Contains));
        Assert.Equal(new[] { @"C:\Mod\gui\old.gui", @"C:\Mod\gui\m_overrides.gui" }, tracker.NeedRestart(new[] { DateTime.MinValue }, existing.Contains));
        Assert.Empty(tracker.NeedRestart(Array.Empty<DateTime>(), existing.Contains));

        tracker.Created(@"c:\mod\gui\M_Overrides.gui", gameStart.AddMinutes(6));
        Assert.Single(tracker.NeedRestart(new[] { gameStart }, existing.Contains));
    }
}
