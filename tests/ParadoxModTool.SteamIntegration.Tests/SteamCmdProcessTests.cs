using Xunit;

namespace ParadoxModTool.SteamIntegration.Tests;

/// <summary>Der echte Prozess-Adapter, geprüft mit <c>cmd.exe</c> statt SteamCMD (nur Windows).</summary>
public class SteamCmdProcessTests
{
    private static string Cmd => Environment.GetEnvironmentVariable("ComSpec") ?? @"C:\Windows\System32\cmd.exe";

    [Fact]
    public async Task LiestDieAusgabeZeilenweise_UndDenExitCode()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var reported = new List<string>();
        var run = await new SteamCmdProcess().RunAsync(
            new SteamCmdStart(Cmd, new[] { "/c", "echo hallo & exit 3" }, TimeSpan.FromSeconds(30)),
            new Progress<string>(reported.Add),
            default);

        Assert.Equal(3, run.ExitCode);
        Assert.False(run.TimedOut);
        Assert.Contains(run.Lines, l => l.Contains("hallo"));
    }

    [Fact]
    public async Task DieEingabeIstGeschlossen_EineNachfrageBlockiertNicht()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var run = await new SteamCmdProcess().RunAsync(
            new SteamCmdStart(Cmd, new[] { "/c", "set /p x=Passwort: & echo fertig" }, TimeSpan.FromSeconds(30)),
            null,
            default);

        Assert.False(run.TimedOut);
        Assert.Contains(run.Lines, l => l.Contains("fertig"));
    }

    [Fact]
    public async Task Zeitgrenze_BeendetDenProzess()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var started = DateTime.UtcNow;
        var run = await new SteamCmdProcess().RunAsync(
            new SteamCmdStart(Cmd, new[] { "/c", "ping -n 30 127.0.0.1 > nul" }, TimeSpan.FromMilliseconds(500)),
            null,
            default);

        Assert.True(run.TimedOut);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task Abbruch_WirftOperationCanceled_UndBeendetDenProzess()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SteamCmdProcess().RunAsync(
            new SteamCmdStart(Cmd, new[] { "/c", "ping -n 30 127.0.0.1 > nul" }, TimeSpan.FromSeconds(60)),
            null,
            cts.Token));
    }
}
