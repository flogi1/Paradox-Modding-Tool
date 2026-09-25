using System.ComponentModel;
using Xunit;

namespace ParadoxModTool.GitIntegration.Tests;

public class ProcessRunnerTests
{
    private static ProcessSpec Cmd(params string[] args) => new("cmd.exe", new[] { "/c" }.Concat(args).ToList());

    [Fact]
    public async Task LiefertAusgabeUndExitCode()
    {
        var result = await new ProcessRunner().RunAsync(Cmd("echo hallo & exit 3"), null, default);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains(result.Output, l => l.Contains("hallo"));
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task ErgaenztUmgebungsvariablen()
    {
        var spec = Cmd("echo %PMT_TEST_VAR%") with { Environment = new Dictionary<string, string> { ["PMT_TEST_VAR"] = "wert42" } };

        var result = await new ProcessRunner().RunAsync(spec, null, default);

        Assert.Contains(result.Output, l => l.Contains("wert42"));
    }

    [Fact]
    public async Task MeldetZeilenAnDenFortschritt()
    {
        var lines = new List<string>();

        await new ProcessRunner().RunAsync(Cmd("echo eins"), new SyncProgress(lines), default);

        Assert.Contains(lines, l => l.Contains("eins"));
    }

    [Fact]
    public async Task NichtStartbaresProgramm_WirftWin32Exception()
    {
        await Assert.ThrowsAsync<Win32Exception>(() => new ProcessRunner().RunAsync(new ProcessSpec("definitiv-nicht-vorhanden-pmt.exe", Array.Empty<string>()), null, default));
    }

    [Fact]
    public async Task Zeitueberschreitung_BeendetDenProzess()
    {
        var spec = new ProcessSpec("ping", new[] { "-n", "30", "127.0.0.1" }, null, TimeSpan.FromMilliseconds(400));

        var result = await new ProcessRunner().RunAsync(spec, null, default);

        Assert.True(result.TimedOut);
    }

    [Fact]
    public async Task Abbruch_WirftOperationCanceled()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ProcessRunner().RunAsync(new ProcessSpec("ping", new[] { "-n", "30", "127.0.0.1" }), null, cts.Token));
    }

    private sealed class SyncProgress : IProgress<string>
    {
        private readonly List<string> _lines;

        public SyncProgress(List<string> lines) => _lines = lines;

        public void Report(string value)
        {
            lock (_lines)
            {
                _lines.Add(value);
            }
        }
    }
}
