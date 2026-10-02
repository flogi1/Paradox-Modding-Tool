using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class ErrorLogLocationsTests
{
    [Fact]
    public void LogFolderIsNextToTheModFolder()
    {
        var folder = ErrorLogLocations.LogFolderFor("C:/Users/x/Documents/Paradox Interactive/Europa Universalis V/mod/");

        Assert.Equal(
            Path.GetFullPath("C:/Users/x/Documents/Paradox Interactive/Europa Universalis V/logs"),
            Path.GetFullPath(folder!));
    }

    [Fact]
    public void LogFolderExpandsEnvironmentVariablesAndNeedsATemplate()
    {
        var folder = ErrorLogLocations.LogFolderFor("%USERPROFILE%/Documents/Paradox Interactive/Europa Universalis V/mod");

        Assert.StartsWith(Environment.GetEnvironmentVariable("USERPROFILE")!, folder, StringComparison.OrdinalIgnoreCase);
        Assert.Null(ErrorLogLocations.LogFolderFor(null));
        Assert.Null(ErrorLogLocations.LogFolderFor("  "));
    }

    [Fact]
    public void FilesListsOnlyExistingLogFilesInIndexOrder()
    {
        using var tree = new TempTree(("logs/error.3.log", "c"), ("logs/error.log", "a"), ("logs/error.1.log", "b"), ("logs/game.log", "x"));

        var files = ErrorLogLocations.Files(tree.PathOf("logs"));

        Assert.Equal(new[] { 0, 1, 3 }, files.Select(r => r.Index));
        Assert.Equal(new[] { "error.log", "error.1.log", "error.3.log" }, files.Select(r => r.FileName));
        Assert.Empty(ErrorLogLocations.Files(tree.PathOf("missing")));
        Assert.Empty(ErrorLogLocations.Runs(null));
    }

    [Fact]
    public void RunsGroupRealFilesAndReadRunReadsAllPartsInOrder()
    {
        using var tree = new TempTree(
            ("logs/error.log", "[15:19:50][a.cpp:1]: c\n"),
            ("logs/error.1.log", "[15:15:28][a.cpp:1]: a\n[15:19:50][a.cpp:1]: b\n"),
            ("logs/error.2.log", "[15:08:22][a.cpp:1]: alt\n"));
        var stamp = new DateTime(2026, 9, 26, 15, 20, 0, DateTimeKind.Local);
        File.SetLastWriteTime(tree.PathOf("logs/error.log"), stamp);
        File.SetLastWriteTime(tree.PathOf("logs/error.1.log"), stamp.AddSeconds(-10));
        File.SetLastWriteTime(tree.PathOf("logs/error.2.log"), stamp.AddMinutes(-6));

        var runs = ErrorLogLocations.Runs(tree.PathOf("logs"));
        var read = LogReader.ReadRun(runs[0]);

        Assert.Equal(2, runs.Count);
        Assert.Equal(new[] { "a", "b", "c" }, read.Lines.Select(l => l.Message));
        Assert.Equal(new[] { 0, 0, 1 }, read.Lines.Select(l => l.Part));
        Assert.Equal(new[] { 1, 2, 1 }, read.Lines.Select(l => l.LineNumber));
    }

    [Fact]
    public void RunsUseTheDebugLogStartForTheCurrentRun()
    {
        using var tree = new TempTree(
            ("logs/debug.log", "[15:00:00][a.cpp:1]: start\n[15:31:00][a.cpp:1]: jetzt\n"),
            ("logs/error.log", "[15:30:00][a.cpp:1]: c\n"),
            ("logs/error.1.log", "[15:10:00][a.cpp:1]: a\n[15:20:00][a.cpp:1]: b\n"));
        var stamp = new DateTime(2026, 9, 26, 15, 31, 0, DateTimeKind.Local);
        File.SetLastWriteTime(tree.PathOf("logs/debug.log"), stamp);
        File.SetLastWriteTime(tree.PathOf("logs/error.log"), stamp.AddMinutes(-1));
        File.SetLastWriteTime(tree.PathOf("logs/error.1.log"), stamp.AddMinutes(-11));

        var run = Assert.Single(ErrorLogLocations.Runs(tree.PathOf("logs")));

        Assert.Equal(new[] { 1, 0 }, run.Files.Select(f => f.Index));
    }

    [Fact]
    public void FileTimesTakeTheDateFromTheLastWriteAcrossMidnight()
    {
        using var tree = new TempTree(("logs/error.log", "[23:59:58][a.cpp:1]: a\n[00:00:03][a.cpp:1]: b\n"));
        var path = tree.PathOf("logs/error.log");
        File.SetLastWriteTime(path, new DateTime(2026, 9, 27, 0, 0, 5, DateTimeKind.Local));

        var times = LogFileTimes.For(ErrorLogLocations.Files(tree.PathOf("logs"))[0]);

        Assert.Equal(new DateTime(2026, 9, 26, 23, 59, 58), times.First);
        Assert.Equal(new DateTime(2026, 9, 27, 0, 0, 3), times.Last);
    }

    [Fact]
    public void SuppressionsLiveInTheGameDirectory()
    {
        Assert.Equal(Path.Combine("G", "errorlog", "suppressions.json"), ErrorLogLocations.SuppressionsPath("G"));
    }
}
