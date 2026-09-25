using System.ComponentModel;
using ParadoxModTool.Mods;
using Xunit;

namespace ParadoxModTool.SteamIntegration.Tests;

public class WorkshopPublisherTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-pub-" + Guid.NewGuid().ToString("N"));
    private readonly FakeSteamCmdProcess _fake = new();

    public WorkshopPublisherTests()
    {
        Put("mod/.metadata/metadata.json", "{}");
        Put("mod/.metadata/thumbnail.png", "png");
        Put("mod/common/a.txt", "a");
        Put("mod/.pmt-versions/dev/y.txt", "y");
        Put("mod/mod.zip", "z");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string Mod => Path.Combine(_dir, "mod");

    private string Temp => Path.Combine(_dir, "temp");

    private void Put(string relative, string content)
    {
        var path = Path.Combine(_dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private WorkshopPublishRequest Request(string? workshopId, bool updateListing = false, WorkshopVisibility? visibility = null) => new(
        AppId: "3450310",
        ModFolder: Mod,
        WorkshopId: workshopId,
        ChangeNote: "Notiz",
        Visibility: visibility,
        UpdateListing: updateListing,
        Title: "Titel",
        Description: "Beschreibung",
        PreviewFile: ".metadata/thumbnail.png",
        Rules: ModStagingRules.Default,
        TempRoot: Temp);

    private Task<WorkshopPublishResult> Publish(WorkshopPublishRequest request, CancellationToken token = default, string user = "flogi") =>
        new WorkshopPublisher(new SteamCmdBackend(_fake, @"C:\tools\steamcmd.exe", user, TimeSpan.FromMinutes(30))).PublishAsync(request, null, token);

    private static Task<SteamCmdRun> Ok() => Task.FromResult(new SteamCmdRun(0, new[] { "Success." }, false));

    private void AssertTempEmpty() => Assert.False(Directory.Exists(Temp) && Directory.EnumerateFileSystemEntries(Temp).Any());

    [Fact]
    public async Task ErstesVeroeffentlichen_LiefertDieNeueId_SendetAlleFelder_UndKopiertOhneVersionsaeste()
    {
        string vdf = string.Empty;
        List<string> staged = new();
        _fake.Behavior = (start, _) =>
        {
            var path = FakeSteamCmdProcess.VdfPath(start);
            vdf = File.ReadAllText(path);
            var content = Path.Combine(Path.GetDirectoryName(path)!, "content");
            staged = Directory.EnumerateFiles(content, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(content, f).Replace('\\', '/'))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            File.WriteAllText(path, vdf.Replace("\"publishedfileid\"\t\t\"0\"", "\"publishedfileid\"\t\t\"3456789012\""));
            return Ok();
        };

        var result = await Publish(Request(null, visibility: WorkshopVisibility.Private));

        Assert.Equal(WorkshopPublishOutcome.Success, result.Outcome);
        Assert.Equal("3456789012", result.WorkshopId);
        Assert.Contains("\"publishedfileid\"\t\t\"0\"", vdf);
        Assert.Contains("\"appid\"\t\t\"3450310\"", vdf);
        Assert.Contains("\"title\"\t\t\"Titel\"", vdf);
        Assert.Contains("\"description\"\t\t\"Beschreibung\"", vdf);
        Assert.Contains("\"visibility\"\t\t\"2\"", vdf);
        Assert.Contains("\"changenote\"\t\t\"Notiz\"", vdf);
        Assert.Contains("thumbnail.png", vdf);
        Assert.Equal(new[] { ".metadata/metadata.json", ".metadata/thumbnail.png", "common/a.txt" }, staged);
        Assert.Equal(3, result.Staging!.FileCount);
        AssertTempEmpty();
    }

    [Fact]
    public async Task Start_ArgumenteUndZeitgrenze()
    {
        await Publish(Request("123"));

        var start = Assert.Single(_fake.Starts);
        Assert.Equal(@"C:\tools\steamcmd.exe", start.ExecutablePath);
        Assert.Equal(new[] { "+login", "flogi", "+workshop_build_item" }, start.Arguments.Take(3));
        Assert.EndsWith("build.vdf", start.Arguments[3]);
        Assert.StartsWith(Temp, start.Arguments[3], StringComparison.OrdinalIgnoreCase);
        Assert.Equal("+quit", start.Arguments[4]);
        Assert.Equal(TimeSpan.FromMinutes(30), start.Timeout);
    }

    [Fact]
    public async Task Update_OhneListing_LaesstTitelBeschreibungBildUndSichtbarkeitAus()
    {
        string vdf = string.Empty;
        _fake.Behavior = (start, _) =>
        {
            vdf = File.ReadAllText(FakeSteamCmdProcess.VdfPath(start));
            return Ok();
        };

        var result = await Publish(Request("123"));

        Assert.Equal(WorkshopPublishOutcome.Success, result.Outcome);
        Assert.Equal("123", result.WorkshopId);
        Assert.Contains("\"publishedfileid\"\t\t\"123\"", vdf);
        Assert.DoesNotContain("\"title\"", vdf);
        Assert.DoesNotContain("\"description\"", vdf);
        Assert.DoesNotContain("\"previewfile\"", vdf);
        Assert.DoesNotContain("\"visibility\"", vdf);
    }

    [Fact]
    public async Task Update_MitListingUndSichtbarkeit_SendetDieFelder()
    {
        string vdf = string.Empty;
        _fake.Behavior = (start, _) =>
        {
            vdf = File.ReadAllText(FakeSteamCmdProcess.VdfPath(start));
            return Ok();
        };

        await Publish(Request("123", updateListing: true, visibility: WorkshopVisibility.Public));

        Assert.Contains("\"title\"\t\t\"Titel\"", vdf);
        Assert.Contains("\"description\"\t\t\"Beschreibung\"", vdf);
        Assert.Contains("\"previewfile\"", vdf);
        Assert.Contains("\"visibility\"\t\t\"0\"", vdf);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData(null)]
    public async Task IdLeerOderNull_IstErstesVeroeffentlichen(string? id)
    {
        string vdf = string.Empty;
        _fake.Behavior = (start, _) =>
        {
            var path = FakeSteamCmdProcess.VdfPath(start);
            vdf = File.ReadAllText(path);
            File.WriteAllText(path, vdf.Replace("\"publishedfileid\"\t\t\"0\"", "\"publishedfileid\"\t\t\"55\""));
            return Ok();
        };

        var result = await Publish(Request(id));

        Assert.Contains("\"publishedfileid\"\t\t\"0\"", vdf);
        Assert.Contains("\"title\"", vdf);
        Assert.Equal("55", result.WorkshopId);
    }

    [Fact]
    public async Task AnmeldungAbgelaufen_UndSteamGuard()
    {
        _fake.Behavior = (_, _) => Task.FromResult(new SteamCmdRun(5, new[] { "Logging in user 'flogi' to Steam Public...", "password:" }, false));
        var login = await Publish(Request("123"));
        Assert.Equal(WorkshopPublishOutcome.LoginRequired, login.Outcome);
        Assert.Contains("Steam-Anmeldung einrichten", login.Message);
        AssertTempEmpty();

        _fake.Behavior = (_, _) => Task.FromResult(new SteamCmdRun(5, new[] { "Steam Guard code:" }, false));
        Assert.Equal(WorkshopPublishOutcome.GuardRequired, (await Publish(Request("123"))).Outcome);
    }

    [Fact]
    public async Task ErstesVeroeffentlichen_ScheitertNachDemAnlegen_BehaeltDieId()
    {
        _fake.Behavior = (start, _) =>
        {
            var path = FakeSteamCmdProcess.VdfPath(start);
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"publishedfileid\"\t\t\"0\"", "\"publishedfileid\"\t\t\"999\""));
            return Task.FromResult(new SteamCmdRun(1, new[] { "ERROR! Failed to upload content." }, false));
        };

        var result = await Publish(Request(null));

        Assert.Equal(WorkshopPublishOutcome.Failed, result.Outcome);
        Assert.Equal("999", result.WorkshopId);
        AssertTempEmpty();
    }

    [Fact]
    public async Task ErstesVeroeffentlichen_ErfolgOhneId_IstFailed()
    {
        var result = await Publish(Request(null));

        Assert.Equal(WorkshopPublishOutcome.Failed, result.Outcome);
        Assert.Null(result.WorkshopId);
        Assert.Contains("Workshop-ID", result.Message);
    }

    [Fact]
    public async Task Abbruch_LiefertCancelled_UndRaeumtAuf()
    {
        using var cts = new CancellationTokenSource();
        _fake.Behavior = (_, token) =>
        {
            cts.Cancel();
            throw new OperationCanceledException(token);
        };

        var result = await Publish(Request("123"), cts.Token);

        Assert.Equal(WorkshopPublishOutcome.Cancelled, result.Outcome);
        AssertTempEmpty();
    }

    [Fact]
    public async Task Zeitueberschreitung()
    {
        _fake.Behavior = (_, _) => Task.FromResult(new SteamCmdRun(-1, Array.Empty<string>(), TimedOut: true));

        var result = await Publish(Request("123"));

        Assert.Equal(WorkshopPublishOutcome.Timeout, result.Outcome);
        AssertTempEmpty();
    }

    [Fact]
    public async Task ProzessLaesstSichNichtStarten_IstFailed()
    {
        _fake.Behavior = (_, _) => throw new Win32Exception("Das System kann die angegebene Datei nicht finden.");

        var result = await Publish(Request("123"));

        Assert.Equal(WorkshopPublishOutcome.Failed, result.Outcome);
        Assert.Contains("nicht finden", result.Message);
        AssertTempEmpty();
    }

    [Fact]
    public async Task ModOrdnerFehlt_IstFailed_ProzessStartetNie()
    {
        var request = Request("123") with { ModFolder = Path.Combine(_dir, "gibtsnicht") };

        var result = await Publish(request);

        Assert.Equal(WorkshopPublishOutcome.Failed, result.Outcome);
        Assert.Empty(_fake.Starts);
        AssertTempEmpty();
    }

    [Fact]
    public async Task UngueltigeAnfrage_WirftArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Publish(Request("123") with { AppId = "abc" }));
        await Assert.ThrowsAsync<ArgumentException>(() => Publish(Request("123"), user: " "));
        await Assert.ThrowsAsync<ArgumentException>(() => Publish(Request("12ab")));
        await Assert.ThrowsAsync<ArgumentException>(() => Publish(Request("123"), user: "a b"));
    }
}
