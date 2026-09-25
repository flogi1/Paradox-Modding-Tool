using Xunit;

namespace ParadoxModTool.SteamIntegration.Tests;

public class SteamUploadProtocolTests
{
    private static SteamUploadRequest Request() => new(
        @"C:\Steam\steam_api64.dll",
        "3450310",
        new WorkshopUpload("3450310", @"C:\temp\content", null, "Notiz\nzweite Zeile", WorkshopVisibility.Private, "Titel", null, @"C:\temp\content\thumb.png"),
        250,
        1800);

    [Fact]
    public void Anfrage_UeberJson_KommtUnveraendertZurueck()
    {
        var back = SteamUploadProtocol.DeserializeRequest(SteamUploadProtocol.SerializeRequest(Request()));

        Assert.Equal(@"C:\Steam\steam_api64.dll", back.DllPath);
        Assert.Equal("3450310", back.AppId);
        Assert.Equal(250, back.PollMilliseconds);
        Assert.Equal(1800, back.TimeoutSeconds);
        Assert.Equal(Request().Upload, back.Upload);
    }

    [Fact]
    public void ErgebnisZeile_KommtUnveraendertZurueck()
    {
        var result = new WorkshopBackendResult(WorkshopPublishOutcome.Cancelled, "123", new[] { "a", "b" }, "Text mit Umlaut ä", true);

        var line = SteamUploadProtocol.ResultLine(result);

        Assert.DoesNotContain('\n', line);
        Assert.True(SteamUploadProtocol.TryParseLine(line, out var progress, out var parsed));
        Assert.Null(progress);
        Assert.Equal(WorkshopPublishOutcome.Cancelled, parsed!.Outcome);
        Assert.Equal("123", parsed.WorkshopId);
        Assert.Equal(new[] { "a", "b" }, parsed.Output);
        Assert.Equal("Text mit Umlaut ä", parsed.Message);
        Assert.True(parsed.NeedsLegalAgreement);
    }

    [Fact]
    public void FortschrittsZeile_HatKeinenZeilenumbruch_UndWirdGelesen()
    {
        var line = SteamUploadProtocol.ProgressLine("eins\r\nzwei");

        Assert.DoesNotContain('\n', line);
        Assert.True(SteamUploadProtocol.TryParseLine(line, out var progress, out var result));
        Assert.Equal("eins zwei", progress);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("irgendein Text")]
    [InlineData("RESULT\t{kaputt")]
    public void UnbekannteOderKaputteZeile_WirdIgnoriert(string line)
    {
        Assert.False(SteamUploadProtocol.TryParseLine(line, out var progress, out var result));
        Assert.Null(progress);
        Assert.Null(result);
    }
}

public class SteamUploadHelperTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-helper-" + Guid.NewGuid().ToString("N"));
    private readonly FakeSteamworksApi _api = new();

    public SteamUploadHelperTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string WriteRequest(string? workshopId)
    {
        var request = new SteamUploadRequest(
            "dll",
            "3450310",
            new WorkshopUpload("3450310", @"C:\temp\content", workshopId, "Notiz", null, null, null, null),
            0,
            5);
        var path = Path.Combine(_dir, "request.json");
        File.WriteAllText(path, SteamUploadProtocol.SerializeRequest(request));
        return path;
    }

    private static (List<string> Progress, WorkshopBackendResult? Result) Parse(string output)
    {
        var progress = new List<string>();
        WorkshopBackendResult? result = null;
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (SteamUploadProtocol.TryParseLine(line.TrimEnd('\r'), out var p, out var r))
            {
                if (p is not null)
                {
                    progress.Add(p);
                }

                result = r ?? result;
            }
        }

        return (progress, result);
    }

    [Fact]
    public async Task Erfolg_SchreibtFortschrittUndErgebnisZeile()
    {
        var output = new StringWriter();

        var exit = await SteamUploadHelper.RunAsync(WriteRequest("123"), _ => _api, output, TextReader.Null, default);

        Assert.Equal(0, exit);
        var (progress, result) = Parse(output.ToString());
        Assert.Equal(WorkshopPublishOutcome.Success, result!.Outcome);
        Assert.Equal("123", result.WorkshopId);
        Assert.Contains("Shutdown", _api.Calls);
    }

    [Fact]
    public async Task CancelZeile_BrichtDenUploadAb_MitErgebnisCancelled()
    {
        _api.PendingPolls = int.MaxValue;
        var output = new StringWriter();

        var exit = await SteamUploadHelper.RunAsync(WriteRequest("123"), _ => _api, output, new StringReader("CANCEL\n"), default);

        Assert.Equal(0, exit);
        var (_, result) = Parse(output.ToString());
        Assert.Equal(WorkshopPublishOutcome.Cancelled, result!.Outcome);
        Assert.Equal("Shutdown", _api.Calls[^1]);
    }

    [Fact]
    public async Task KaputteAnfrage_LiefertFailed_UndExitCode2()
    {
        var path = Path.Combine(_dir, "bad.json");
        File.WriteAllText(path, "{ kaputt");
        var output = new StringWriter();

        var exit = await SteamUploadHelper.RunAsync(path, _ => _api, output, TextReader.Null, default);

        Assert.Equal(2, exit);
        var (_, result) = Parse(output.ToString());
        Assert.Equal(WorkshopPublishOutcome.Failed, result!.Outcome);
    }

    [Fact]
    public async Task FehlendeAnfrageDatei_LiefertFailed_UndExitCode2()
    {
        var output = new StringWriter();

        var exit = await SteamUploadHelper.RunAsync(Path.Combine(_dir, "fehlt.json"), _ => _api, output, TextReader.Null, default);

        Assert.Equal(2, exit);
        Assert.Equal(WorkshopPublishOutcome.Failed, Parse(output.ToString()).Result!.Outcome);
    }

    [Fact]
    public async Task InitScheitert_LiefertUnavailable()
    {
        _api.InitError = new SteamworksUnavailableException("Steam läuft nicht.");
        var output = new StringWriter();

        await SteamUploadHelper.RunAsync(WriteRequest("123"), _ => _api, output, TextReader.Null, default);

        var result = Parse(output.ToString()).Result!;
        Assert.Equal(WorkshopPublishOutcome.Unavailable, result.Outcome);
        Assert.Equal("Steam läuft nicht.", result.Message);
    }
}

public class SteamClientProcessBackendTests
{
    private sealed class FakeLauncher : ISteamHelperLauncher
    {
        public Func<string, Action<string>, CancellationToken, Task<int>> Behavior { get; set; } = (_, _, _) => Task.FromResult(0);

        public string? RequestFile { get; private set; }

        public bool RequestExistedDuringRun { get; private set; }

        public async Task<int> RunAsync(string requestFile, Action<string> onLine, CancellationToken cancellation)
        {
            RequestFile = requestFile;
            RequestExistedDuringRun = File.Exists(requestFile);
            return await Behavior(requestFile, onLine, cancellation);
        }
    }

    private static WorkshopUpload Upload(string? id) =>
        new("3450310", @"C:\temp\content", id, "Notiz", WorkshopVisibility.Public, "T", "D", null);

    private static SteamClientProcessBackend Backend(FakeLauncher launcher) =>
        new(launcher, @"C:\Steam\steam_api64.dll", TimeSpan.FromMilliseconds(250), TimeSpan.FromMinutes(30));

    [Fact]
    public async Task LeitetFortschrittWeiter_UndLiefertDasErgebnisDesHelfers()
    {
        var launcher = new FakeLauncher
        {
            Behavior = (_, onLine, _) =>
            {
                onLine("Startmeldung, die kein Protokoll ist");
                onLine(SteamUploadProtocol.ProgressLine("Inhalt wird hochgeladen … 50 %"));
                onLine(SteamUploadProtocol.ResultLine(new WorkshopBackendResult(WorkshopPublishOutcome.Success, "123", Array.Empty<string>(), "ok", true)));
                return Task.FromResult(0);
            },
        };
        var progress = new ListProgress();

        var result = await Backend(launcher).UploadAsync(Upload("123"), progress, default);

        Assert.Equal(WorkshopPublishOutcome.Success, result.Outcome);
        Assert.Equal("123", result.WorkshopId);
        Assert.True(result.NeedsLegalAgreement);
        Assert.Equal(new[] { "Inhalt wird hochgeladen … 50 %" }, progress.Lines);
    }

    [Fact]
    public async Task Anfragedatei_EnthaeltDieAngaben_UndWirdDanachGeloescht()
    {
        string? content = null;
        var launcher = new FakeLauncher
        {
            Behavior = (file, onLine, _) =>
            {
                content = File.ReadAllText(file);
                onLine(SteamUploadProtocol.ResultLine(new WorkshopBackendResult(WorkshopPublishOutcome.Success, "123", Array.Empty<string>(), "ok")));
                return Task.FromResult(0);
            },
        };

        await Backend(launcher).UploadAsync(Upload("123"), null, default);

        Assert.True(launcher.RequestExistedDuringRun);
        var request = SteamUploadProtocol.DeserializeRequest(content!);
        Assert.Equal(@"C:\Steam\steam_api64.dll", request.DllPath);
        Assert.Equal(250, request.PollMilliseconds);
        Assert.Equal(1800, request.TimeoutSeconds);
        Assert.Equal(Upload("123"), request.Upload);
        Assert.False(File.Exists(launcher.RequestFile));
    }

    [Fact]
    public async Task HelferOhneErgebnis_IstFailed_MitExitCode()
    {
        var launcher = new FakeLauncher { Behavior = (_, _, _) => Task.FromResult(-5) };

        var result = await Backend(launcher).UploadAsync(Upload("123"), null, default);

        Assert.Equal(WorkshopPublishOutcome.Failed, result.Outcome);
        Assert.Contains("-5", result.Message);
    }

    [Fact]
    public async Task HelferLaesstSichNichtStarten_IstFailed_UndRaeumtAuf()
    {
        var launcher = new FakeLauncher { Behavior = (_, _, _) => throw new System.ComponentModel.Win32Exception("kaputt") };

        var result = await Backend(launcher).UploadAsync(Upload("123"), null, default);

        Assert.Equal(WorkshopPublishOutcome.Failed, result.Outcome);
        Assert.Contains("kaputt", result.Message);
        Assert.False(File.Exists(launcher.RequestFile));
    }

    [Fact]
    public async Task Abbruch_GibtDenTokenWeiter_UndNutztDasErgebnisDesHelfers()
    {
        using var cts = new CancellationTokenSource();
        var launcher = new FakeLauncher
        {
            Behavior = async (_, onLine, token) =>
            {
                cts.Cancel();
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (OperationCanceledException)
                {
                    // Der Helfer meldet sein Ergebnis trotz Abbruch.
                }

                onLine(SteamUploadProtocol.ResultLine(new WorkshopBackendResult(WorkshopPublishOutcome.Cancelled, "123", Array.Empty<string>(), "abgebrochen")));
                return 0;
            },
        };

        var result = await Backend(launcher).UploadAsync(Upload(null), null, cts.Token);

        Assert.Equal(WorkshopPublishOutcome.Cancelled, result.Outcome);
        Assert.Equal("123", result.WorkshopId);
    }

    [Fact]
    public async Task LauncherWirftOperationCanceled_IstCancelled()
    {
        var launcher = new FakeLauncher { Behavior = (_, _, _) => throw new OperationCanceledException() };

        var result = await Backend(launcher).UploadAsync(Upload("123"), null, default);

        Assert.Equal(WorkshopPublishOutcome.Cancelled, result.Outcome);
        Assert.Equal("123", result.WorkshopId);
    }

    [Fact]
    public async Task UngueltigeAppId_WirftArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Backend(new FakeLauncher()).UploadAsync(Upload("123") with { AppId = "abc" }, null, default));
    }
}
