using Xunit;

namespace ParadoxModTool.SteamIntegration.Tests;

public class SteamClientBackendTests
{
    private readonly FakeSteamworksApi _api = new();

    private SteamClientBackend Backend(TimeSpan? timeout = null) =>
        new(() => _api, TimeSpan.Zero, timeout ?? TimeSpan.FromSeconds(5));

    private static WorkshopUpload Upload(string? id, bool listing = false, WorkshopVisibility? visibility = null) => new(
        AppId: "3450310",
        ContentFolder: @"C:\temp\content",
        WorkshopId: id,
        ChangeNote: "Notiz",
        Visibility: visibility,
        Title: listing ? "Titel" : null,
        Description: listing ? "Beschreibung" : null,
        PreviewFile: listing ? @"C:\temp\content\.metadata\thumbnail.png" : null);

    [Fact]
    public async Task ErstesVeroeffentlichen_LegtAnUndSendetAlleFelder_InDieserReihenfolge()
    {
        var result = await Backend().UploadAsync(Upload(null, listing: true, visibility: WorkshopVisibility.Private), null, default);

        Assert.Equal(WorkshopPublishOutcome.Success, result.Outcome);
        Assert.Equal("3456789012", result.WorkshopId);
        Assert.Equal(
            new[]
            {
                "Init",
                "CreateItem 3450310",
                "Start 3450310 3456789012",
                "Title Titel",
                "Description Beschreibung",
                @"Preview C:\temp\content\.metadata\thumbnail.png",
                "Visibility 2",
                @"Content C:\temp\content",
                "Submit 77 Notiz",
                "Shutdown",
            },
            _api.Calls);
        Assert.True(_api.Disposed);
    }

    [Fact]
    public async Task Update_OhneListingUndSichtbarkeit_SendetNurInhaltUndNotiz()
    {
        var result = await Backend().UploadAsync(Upload("123"), null, default);

        Assert.Equal(WorkshopPublishOutcome.Success, result.Outcome);
        Assert.Equal("123", result.WorkshopId);
        Assert.Equal(
            new[] { "Init", "Start 3450310 123", @"Content C:\temp\content", "Submit 77 Notiz", "Shutdown" },
            _api.Calls);
    }

    [Fact]
    public async Task Update_MitListing_SendetTitelBeschreibungUndBild()
    {
        await Backend().UploadAsync(Upload("123", listing: true), null, default);

        Assert.Contains("Title Titel", _api.Calls);
        Assert.Contains("Description Beschreibung", _api.Calls);
        Assert.Contains(@"Preview C:\temp\content\.metadata\thumbnail.png", _api.Calls);
        Assert.DoesNotContain(_api.Calls, c => c.StartsWith("Visibility", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InitScheitert_IstUnavailable_ohneWeitereAufrufe()
    {
        _api.InitError = new SteamworksUnavailableException("Steam läuft nicht.");

        var result = await Backend().UploadAsync(Upload("123"), null, default);

        Assert.Equal(WorkshopPublishOutcome.Unavailable, result.Outcome);
        Assert.Equal("Steam läuft nicht.", result.Message);
        Assert.Equal(new[] { "Init" }, _api.Calls);
        Assert.True(_api.Disposed);
    }

    [Fact]
    public async Task AnlegenAbgelehnt_IstFailed_ohneId_undOhneUpdate()
    {
        _api.CreateResult = new CreateItemResult(15, 0, false);

        var result = await Backend().UploadAsync(Upload(null), null, default);

        Assert.Equal(WorkshopPublishOutcome.Failed, result.Outcome);
        Assert.Null(result.WorkshopId);
        Assert.Contains("verweigert", result.Message);
        Assert.DoesNotContain(_api.Calls, c => c.StartsWith("Start", StringComparison.Ordinal));
        Assert.Equal("Shutdown", _api.Calls[^1]);
    }

    [Fact]
    public async Task AnlegenErgebnisNichtLesbar_IstFailed()
    {
        _api.CreateResult = null;

        var result = await Backend().UploadAsync(Upload(null), null, default);

        Assert.Equal(WorkshopPublishOutcome.Failed, result.Outcome);
        Assert.Null(result.WorkshopId);
    }

    [Fact]
    public async Task ErstesVeroeffentlichen_ScheitertNachDemAnlegen_BehaeltDieId()
    {
        _api.SubmitResult = new SubmitItemResult(25, 0, false);

        var result = await Backend().UploadAsync(Upload(null), null, default);

        Assert.Equal(WorkshopPublishOutcome.Failed, result.Outcome);
        Assert.Equal("3456789012", result.WorkshopId);
        Assert.Contains("Limit", result.Message);
    }

    [Fact]
    public async Task Vereinbarung_BeimAnlegen_BleibtErfolg_MitFlag()
    {
        _api.CreateResult = new CreateItemResult(1, 3456789012UL, true);

        var result = await Backend().UploadAsync(Upload(null), null, default);

        Assert.Equal(WorkshopPublishOutcome.Success, result.Outcome);
        Assert.True(result.NeedsLegalAgreement);
    }

    [Fact]
    public async Task Vereinbarung_BeimEinreichen_BleibtErfolg_MitFlag()
    {
        _api.SubmitResult = new SubmitItemResult(1, 0, true);

        var result = await Backend().UploadAsync(Upload("123"), null, default);

        Assert.Equal(WorkshopPublishOutcome.Success, result.Outcome);
        Assert.True(result.NeedsLegalAgreement);
    }

    [Fact]
    public async Task StartHandleNull_IstFailed()
    {
        _api.Handle = 0;

        var result = await Backend().UploadAsync(Upload("123"), null, default);

        Assert.Equal(WorkshopPublishOutcome.Failed, result.Outcome);
        Assert.Equal("Shutdown", _api.Calls[^1]);
    }

    [Theory]
    [InlineData("Title", "Titel")]
    [InlineData("Description", "Beschreibung")]
    [InlineData("Preview", "Vorschaubild")]
    [InlineData("Visibility", "Sichtbarkeit")]
    [InlineData("Content", "Inhalt")]
    public async Task SetAbgelehnt_IstFailed_MitNamenDesFelds_UndOhneSubmit(string failing, string expected)
    {
        _api.FailSet = failing;

        var result = await Backend().UploadAsync(Upload("123", listing: true, visibility: WorkshopVisibility.Public), null, default);

        Assert.Equal(WorkshopPublishOutcome.Failed, result.Outcome);
        Assert.Contains(expected, result.Message);
        Assert.DoesNotContain(_api.Calls, c => c.StartsWith("Submit", StringComparison.Ordinal));
        Assert.Equal("Shutdown", _api.Calls[^1]);
    }

    [Fact]
    public async Task Abbruch_WaehrendDesWartens_LiefertCancelled_MitId_UndShutdown()
    {
        using var cts = new CancellationTokenSource();
        _api.OnRunCallbacks = () =>
        {
            if (_api.Calls.Any(c => c.StartsWith("Submit", StringComparison.Ordinal)))
            {
                cts.Cancel();
            }
        };
        // Das Anlegen soll sofort fertig sein, das Einreichen nie: nach dem Anlegen wartet der Fake dauerhaft.
        var afterCreate = new AfterCreatePending(_api);
        var result = await new SteamClientBackend(() => afterCreate, TimeSpan.Zero, TimeSpan.FromSeconds(5)).UploadAsync(Upload(null), null, cts.Token);

        Assert.Equal(WorkshopPublishOutcome.Cancelled, result.Outcome);
        Assert.Equal("3456789012", result.WorkshopId);
        Assert.Contains("abschließen", result.Message);
        Assert.Equal("Shutdown", _api.Calls[^1]);
    }

    [Fact]
    public async Task Zeitueberschreitung_IstTimeout_UndShutdown()
    {
        _api.PendingPolls = int.MaxValue;

        var result = await Backend(TimeSpan.FromMilliseconds(30)).UploadAsync(Upload("123"), null, default);

        Assert.Equal(WorkshopPublishOutcome.Timeout, result.Outcome);
        Assert.Equal("123", result.WorkshopId);
        Assert.Equal("Shutdown", _api.Calls[^1]);
    }

    [Fact]
    public async Task Fortschritt_WirdAlsTextGemeldet_OhneWiederholung()
    {
        _api.PendingPolls = 5;
        _api.ProgressQueue.Enqueue(new ItemUpdateProgress(ItemUpdateStatus.PreparingContent, 0, 0));
        _api.ProgressQueue.Enqueue(new ItemUpdateProgress(ItemUpdateStatus.UploadingContent, 50, 100));
        _api.ProgressQueue.Enqueue(new ItemUpdateProgress(ItemUpdateStatus.UploadingContent, 50, 100));
        _api.ProgressQueue.Enqueue(new ItemUpdateProgress(ItemUpdateStatus.CommittingChanges, 100, 100));
        var progress = new ListProgress();

        await Backend().UploadAsync(Upload("123"), progress, default);

        Assert.Contains(progress.Lines, l => l.Contains("50 %"));
        Assert.Single(progress.Lines, l => l.Contains("50 %"));
        Assert.Contains(progress.Lines, l => l.Contains("festgeschrieben"));
    }

    [Fact]
    public async Task UngueltigeAppId_WirftArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Backend().UploadAsync(Upload("123") with { AppId = "abc" }, null, default));
    }

    [Fact]
    public async Task UngueltigeWorkshopId_IstFailed()
    {
        var result = await Backend().UploadAsync(Upload("12ab"), null, default);

        Assert.Equal(WorkshopPublishOutcome.Failed, result.Outcome);
    }

    /// <summary>Wie der Fake, aber nach dem Anlegen meldet <see cref="ISteamworksApi.IsCallCompleted"/> nie ein Ergebnis für das Einreichen.</summary>
    private sealed class AfterCreatePending : ISteamworksApi
    {
        private readonly FakeSteamworksApi _inner;

        public AfterCreatePending(FakeSteamworksApi inner) => _inner = inner;

        public void Init() => _inner.Init();

        public void Shutdown() => _inner.Shutdown();

        public void RunCallbacks() => _inner.RunCallbacks();

        public ulong CreateItem(uint appId) => _inner.CreateItem(appId);

        public ulong StartItemUpdate(uint appId, ulong publishedFileId) => _inner.StartItemUpdate(appId, publishedFileId);

        public bool SetItemTitle(ulong handle, string title) => _inner.SetItemTitle(handle, title);

        public bool SetItemDescription(ulong handle, string description) => _inner.SetItemDescription(handle, description);

        public bool SetItemPreview(ulong handle, string previewFile) => _inner.SetItemPreview(handle, previewFile);

        public bool SetItemContent(ulong handle, string contentFolder) => _inner.SetItemContent(handle, contentFolder);

        public bool SetItemVisibility(ulong handle, int visibility) => _inner.SetItemVisibility(handle, visibility);

        public ulong SubmitItemUpdate(ulong handle, string changeNote) => _inner.SubmitItemUpdate(handle, changeNote);

        public bool IsCallCompleted(ulong call) => call == 100;

        public CreateItemResult? ReadCreateItemResult(ulong call) => _inner.ReadCreateItemResult(call);

        public SubmitItemResult? ReadSubmitItemResult(ulong call) => _inner.ReadSubmitItemResult(call);

        public ItemUpdateProgress GetItemUpdateProgress(ulong handle) => _inner.GetItemUpdateProgress(handle);

        public void Dispose() => _inner.Dispose();
    }
}
