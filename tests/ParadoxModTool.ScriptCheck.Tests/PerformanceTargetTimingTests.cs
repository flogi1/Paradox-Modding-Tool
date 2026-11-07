using System.Diagnostics;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.ScriptCheck.Tests;

/// <summary>
/// Zeitmessung der Leistungsprüfung eines fremden Ziels (Vanilla) über den echten Weg (<see cref="PerformanceTarget.Request"/> und
/// <see cref="ScriptCheckSession"/>, Speicher im Temp-Ordner): erster Aufbau mit leerem Speicher, dann Refresh mit warmem Speicher. Läuft nur
/// mit <c>PMT_FLAGS_&lt;SPIEL&gt;_GAME</c>; gibt die Sekunden aus, prüft aber keine Zeiten.
/// </summary>
public sealed class PerformanceTargetTimingTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ScriptCheckTestRoots _roots = new();

    public PerformanceTargetTimingTests(ITestOutputHelper output) => _output = output;

    public void Dispose() => _roots.Dispose();

    [Fact]
    public Task Timing_Ck3() => Measure("ck3", "CK3");

    [Fact]
    public Task Timing_Vic3() => Measure("vic3", "VIC3");

    [Fact]
    public Task Timing_Eu4() => Measure("eu4", "EU4");

    [Fact]
    public Task Timing_Eu5() => Measure("eu5", "EU5");

    [Fact]
    public Task Timing_Hoi4() => Measure("hoi4", "HOI4");

    [Fact]
    public Task Timing_Stellaris() => Measure("stellaris", "STELLARIS");

    private async Task Measure(string gameId, string game)
    {
        var vanilla = Environment.GetEnvironmentVariable("PMT_FLAGS_" + game + "_GAME");
        if (string.IsNullOrEmpty(vanilla) || !Directory.Exists(vanilla))
        {
            return;
        }

        // Wie die App: Inhaltsordner = Spielordner; Installationsordner = sein Elternordner bei Jomini-Spielen (Ordner "game"), sonst er selbst.
        var content = Path.GetFullPath(vanilla).TrimEnd(Path.DirectorySeparatorChar);
        var install = string.Equals(Path.GetFileName(content), "game", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(content)! : content;
        var vanillaRoots = ErrorLogSources.VanillaRoots(install, content);
        var profile = RepoFiles.Profile(gameId);
        var rules = ScriptCheckTestRoots.Rules(gameId);
        var target = new PerformanceTarget(PerformanceTargetKind.Vanilla, "Vanilla", content);
        var storeDirectory = Path.Combine(_roots.Temp, "store", gameId);
        ScriptCheckRequest Create() => PerformanceTarget.Request(target, profile, Array.Empty<ModSource>(), null, vanillaRoots, rules, new ScriptCheckStore(storeDirectory));

        var session = new ScriptCheckSession();
        var watch = Stopwatch.StartNew();
        await session.RefreshAsync(Create, null, CancellationToken.None);
        var first = watch.Elapsed.TotalSeconds;
        Assert.True(session.State == ScriptCheckState.Ready, session.StatusText);
        var firstFindings = session.Result!.Findings.Count;

        watch.Restart();
        await session.RefreshAsync(Create, null, CancellationToken.None);
        var second = watch.Elapsed.TotalSeconds;
        Assert.True(session.State == ScriptCheckState.Ready, session.StatusText);

        _output.WriteLine(
            $"{gameId}: Wurzeln {string.Join(" | ", vanillaRoots)}; Dateien {session.Index!.FileCount}, ungelesen {session.Index.UnreadFiles.Count}; "
                + $"erster Aufbau {first:F1} s ({firstFindings} Funde), Refresh {second:F1} s ({session.Result!.Findings.Count} Funde)");
    }
}
