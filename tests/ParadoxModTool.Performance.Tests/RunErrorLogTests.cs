using Xunit;
using ParadoxModTool.ErrorLog;

namespace ParadoxModTool.Performance.Tests;

public class RunErrorLogTests
{
    [Fact]
    public void CopyConcatenatesCurrentRunFilesOldestFirst()
    {
        using var tree = new TempTree(
            ("logs/debug.log", "[15:00:00][a.cpp:1]: start\n[15:31:00][a.cpp:1]: jetzt\n"),
            ("logs/error.log", "[15:30:00][a.cpp:1]: c\n"),
            ("logs/error.1.log", "[15:10:00][a.cpp:1]: a\n[15:20:00][a.cpp:1]: b\n"));
        var stamp = new DateTime(2026, 9, 26, 15, 31, 0, DateTimeKind.Local);
        File.SetLastWriteTime(tree.PathOf("logs/debug.log"), stamp);
        File.SetLastWriteTime(tree.PathOf("logs/error.log"), stamp.AddMinutes(-1));
        File.SetLastWriteTime(tree.PathOf("logs/error.1.log"), stamp.AddMinutes(-11));
        var runFolder = tree.PathOf("runs/perf_1");

        var copied = RunErrorLog.Copy(tree.PathOf("logs"), runFolder);

        Assert.True(copied);
        var path = RunErrorLog.PathFor(runFolder);
        Assert.NotNull(path);
        var messages = LogReader.ReadFile(path!).Select(l => l.Message).ToArray();
        Assert.Equal(new[] { "a", "b", "c" }, messages);
        Assert.Single(Directory.GetFiles(runFolder));
    }

    [Fact]
    public void CopyReturnsFalseWithoutLogs()
    {
        using var tree = new TempTree();
        var runFolder = tree.PathOf("runs/perf_1");

        Assert.False(RunErrorLog.Copy(tree.PathOf("logs"), runFolder));
        Assert.Null(RunErrorLog.PathFor(runFolder));
    }

    [Fact]
    public void CopyOverwritesAnEarlierCopy()
    {
        using var tree = new TempTree(
            ("logs/error.log", "[15:30:00][a.cpp:1]: neu\n"),
            ("runs/perf_1/error.log", "alt\n"));

        Assert.True(RunErrorLog.Copy(tree.PathOf("logs"), tree.PathOf("runs/perf_1")));

        Assert.Equal(new[] { "neu" }, LogReader.ReadFile(RunErrorLog.PathFor(tree.PathOf("runs/perf_1"))!).Select(l => l.Message));
    }

    [Fact]
    public void CopyJoinsFilesWithANewlineAndDropsTheBomOfLaterFiles()
    {
        // Die ältere Datei endet ohne Zeilenumbruch, die neuere beginnt mit einem UTF-8-BOM: Die Kopie muss trotzdem drei saubere Zeilen haben.
        using var tree = new TempTree(("logs/debug.log", "[15:00:00][a.cpp:1]: start\n[15:31:00][a.cpp:1]: jetzt\n"));
        File.WriteAllBytes(tree.PathOf("logs/error.1.log"), System.Text.Encoding.UTF8.GetBytes("[15:10:00][a.cpp:1]: a\n[15:20:00][a.cpp:1]: b"));
        File.WriteAllBytes(tree.PathOf("logs/error.log"), new byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.UTF8.GetBytes("[15:30:00][a.cpp:1]: c\n")).ToArray());
        var stamp = new DateTime(2026, 9, 26, 15, 31, 0, DateTimeKind.Local);
        File.SetLastWriteTime(tree.PathOf("logs/debug.log"), stamp);
        File.SetLastWriteTime(tree.PathOf("logs/error.log"), stamp.AddMinutes(-1));
        File.SetLastWriteTime(tree.PathOf("logs/error.1.log"), stamp.AddMinutes(-11));
        var runFolder = tree.PathOf("runs/perf_1");

        Assert.True(RunErrorLog.Copy(tree.PathOf("logs"), runFolder));

        var path = RunErrorLog.PathFor(runFolder)!;
        Assert.Equal(
            new[] { "[15:10:00][a.cpp:1]: a", "[15:20:00][a.cpp:1]: b", "[15:30:00][a.cpp:1]: c" },
            File.ReadAllText(path).TrimEnd('\n').Split('\n'));
        Assert.Equal(new[] { "a", "b", "c" }, LogReader.ReadFile(path).Select(l => l.Message));
        Assert.DoesNotContain((byte)0xEF, File.ReadAllBytes(path));
    }
}
