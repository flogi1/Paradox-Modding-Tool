using System.Diagnostics;
using System.Globalization;

namespace ParadoxModTool.SteamIntegration;

/// <summary>
/// Lädt über den laufenden Steam-Client hoch (Steamworks <c>ISteamUGC</c>): legt beim ersten Veröffentlichen die Seite an, setzt die Felder, reicht die Änderung ein und wartet auf das Ergebnis.
/// Das Programm sieht keine Zugangsdaten; die Anmeldung gehört dem Steam-Client.
/// </summary>
public sealed class SteamClientBackend : IWorkshopBackend
{
    private readonly Func<ISteamworksApi> _factory;
    private readonly TimeSpan _poll;
    private readonly TimeSpan _timeout;

    public SteamClientBackend(Func<ISteamworksApi> factory, TimeSpan poll, TimeSpan timeout)
    {
        _factory = factory;
        _poll = poll;
        _timeout = timeout;
    }

    public async Task<WorkshopBackendResult> UploadAsync(WorkshopUpload upload, IProgress<string>? progress, CancellationToken cancellation)
    {
        if (!uint.TryParse(upload.AppId, NumberStyles.None, CultureInfo.InvariantCulture, out var appId))
        {
            throw new ArgumentException(SteamTexts.AppIdNotNumber, nameof(upload));
        }

        using var api = _factory();
        try
        {
            api.Init();
        }
        catch (SteamworksUnavailableException ex)
        {
            return Result(WorkshopPublishOutcome.Unavailable, null, ex.Message);
        }

        var id = upload.WorkshopId;
        var needsAgreement = false;
        try
        {
            if (id is null)
            {
                progress?.Report(SteamTexts.CreatingItem);
                var createCall = api.CreateItem(appId);
                if (createCall == 0)
                {
                    return Result(WorkshopPublishOutcome.Failed, null, SteamTexts.CreateRejected);
                }

                await WaitAsync(api, createCall, 0, progress, cancellation);
                var created = api.ReadCreateItemResult(createCall);
                if (created is null)
                {
                    return Result(WorkshopPublishOutcome.Failed, null, SteamTexts.CreateUnreadable);
                }

                if (created.Result != 1)
                {
                    return Result(WorkshopPublishOutcome.Failed, null, SteamResultText.Describe(created.Result));
                }

                id = created.PublishedFileId.ToString(CultureInfo.InvariantCulture);
                needsAgreement = created.NeedsLegalAgreement;
            }

            if (!ulong.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var fileId))
            {
                return Result(WorkshopPublishOutcome.Failed, id, SteamTexts.WorkshopIdNotNumber);
            }

            var handle = api.StartItemUpdate(appId, fileId);
            if (handle == 0)
            {
                return Result(WorkshopPublishOutcome.Failed, id, SteamTexts.UpdateNotStarted);
            }

            if (Rejected(upload.Title, api.SetItemTitle, handle))
            {
                return Result(WorkshopPublishOutcome.Failed, id, SteamTexts.TitleRejected);
            }

            if (Rejected(upload.Description, api.SetItemDescription, handle))
            {
                return Result(WorkshopPublishOutcome.Failed, id, SteamTexts.DescriptionRejected);
            }

            if (Rejected(upload.PreviewFile, api.SetItemPreview, handle))
            {
                return Result(WorkshopPublishOutcome.Failed, id, SteamTexts.PreviewRejected);
            }

            if (upload.Visibility is { } visibility && !api.SetItemVisibility(handle, (int)visibility))
            {
                return Result(WorkshopPublishOutcome.Failed, id, SteamTexts.VisibilityRejected);
            }

            if (!api.SetItemContent(handle, upload.ContentFolder))
            {
                return Result(WorkshopPublishOutcome.Failed, id, SteamTexts.ContentRejected);
            }

            progress?.Report(SteamTexts.Submitting);
            var submitCall = api.SubmitItemUpdate(handle, upload.ChangeNote);
            if (submitCall == 0)
            {
                return Result(WorkshopPublishOutcome.Failed, id, SteamTexts.SubmitRejected);
            }

            await WaitAsync(api, submitCall, handle, progress, cancellation);
            var submitted = api.ReadSubmitItemResult(submitCall);
            if (submitted is null)
            {
                return Result(WorkshopPublishOutcome.Failed, id, SteamTexts.SubmitUnreadable);
            }

            var agreement = submitted.NeedsLegalAgreement || needsAgreement;
            return submitted.Result != 1
                ? Result(WorkshopPublishOutcome.Failed, id, SteamResultText.Describe(submitted.Result), agreement)
                : Result(WorkshopPublishOutcome.Success, id, SteamTexts.Uploaded, agreement);
        }
        catch (OperationCanceledException)
        {
            return Result(WorkshopPublishOutcome.Cancelled, id, SteamTexts.ClientCancelled);
        }
        catch (TimeoutException)
        {
            return Result(WorkshopPublishOutcome.Timeout, id, SteamTexts.ClientTimeout);
        }
        finally
        {
            api.Shutdown();
        }
    }

    /// <summary>Wartet, bis das Aufrufergebnis da ist, und meldet dabei den Fortschritt des Updates (wenn <paramref name="handle"/> gesetzt ist).</summary>
    private async Task WaitAsync(ISteamworksApi api, ulong call, ulong handle, IProgress<string>? progress, CancellationToken cancellation)
    {
        var clock = Stopwatch.StartNew();
        string? last = null;
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            api.RunCallbacks();
            if (api.IsCallCompleted(call))
            {
                return;
            }

            if (clock.Elapsed > _timeout)
            {
                throw new TimeoutException();
            }

            if (handle != 0 && Describe(api.GetItemUpdateProgress(handle)) is { } text && text != last)
            {
                last = text;
                progress?.Report(text);
            }

            await Task.Delay(_poll, cancellation);
        }
    }

    private static string? Describe(ItemUpdateProgress p) => p.Status switch
    {
        ItemUpdateStatus.PreparingConfig => SteamTexts.PreparingConfig,
        ItemUpdateStatus.PreparingContent => SteamTexts.PreparingContent,
        ItemUpdateStatus.UploadingContent => p.Total == 0 ? SteamTexts.UploadingContent : SteamTexts.UploadingContentPercent(p.Processed * 100 / p.Total),
        ItemUpdateStatus.UploadingPreviewFile => SteamTexts.UploadingPreview,
        ItemUpdateStatus.CommittingChanges => SteamTexts.Committing,
        _ => null,
    };

    /// <summary>Wendet <paramref name="set"/> an, wenn ein Wert da ist; <see langword="true"/>, wenn Steam ihn ablehnt.</summary>
    private static bool Rejected(string? value, Func<ulong, string, bool> set, ulong handle) =>
        value is not null && !set(handle, value);

    private static WorkshopBackendResult Result(WorkshopPublishOutcome outcome, string? id, string message, bool needsAgreement = false) =>
        new(outcome, id, Array.Empty<string>(), message, needsAgreement);
}
