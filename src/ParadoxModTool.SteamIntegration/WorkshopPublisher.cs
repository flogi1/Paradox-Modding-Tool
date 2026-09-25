using ParadoxModTool.Mods;

namespace ParadoxModTool.SteamIntegration;

/// <summary>Alle Angaben für einen Upload. <see cref="PreviewFile"/> ist relativ zum Mod-Ordner mit <c>/</c>. Ohne <see cref="WorkshopId"/> (leer oder <c>0</c>) wird eine neue Workshop-Seite angelegt.</summary>
/// <param name="UpdateListing">Bei einem Update auch Titel, Beschreibung und Vorschaubild senden. Beim ersten Veröffentlichen immer.</param>
/// <param name="Visibility"><see langword="null"/> sendet keine Sichtbarkeit (bei einem Update bleibt sie unverändert).</param>
/// <param name="TempRoot">Ordner für die Kopie; <see langword="null"/> = <c>%TEMP%\ParadoxModTool\staging</c>.</param>
public sealed record WorkshopPublishRequest(
    string AppId,
    string ModFolder,
    string? WorkshopId,
    string ChangeNote,
    WorkshopVisibility? Visibility,
    bool UpdateListing,
    string? Title,
    string? Description,
    string? PreviewFile,
    ModStagingRules Rules,
    string? TempRoot = null);

public enum WorkshopPublishOutcome
{
    Success,
    LoginRequired,
    GuardRequired,
    Timeout,
    Failed,
    Cancelled,

    /// <summary>Der gewählte Weg ist nicht benutzbar (z. B. Steam läuft nicht).</summary>
    Unavailable,
}

/// <param name="WorkshopId">Die Workshop-ID nach dem Lauf. Beim ersten Veröffentlichen auch dann gesetzt, wenn der Lauf danach scheiterte (die Seite existiert dann schon).</param>
/// <param name="NeedsLegalAgreement">Steam verlangt, dass der Nutzer der Workshop-Vereinbarung zustimmt.</param>
public sealed record WorkshopPublishResult(
    WorkshopPublishOutcome Outcome,
    string? WorkshopId,
    IReadOnlyList<string> Output,
    string Message,
    StagingResult? Staging,
    bool NeedsLegalAgreement = false);

/// <summary>
/// Stellt die Upload-Kopie zusammen und übergibt sie einem <see cref="IWorkshopBackend"/>. Die Kopie liegt im Temp-Ordner und wird immer gelöscht.
/// </summary>
public sealed class WorkshopPublisher
{
    private readonly IWorkshopBackend _backend;

    public WorkshopPublisher(IWorkshopBackend backend)
    {
        _backend = backend;
    }

    public async Task<WorkshopPublishResult> PublishAsync(WorkshopPublishRequest request, IProgress<string>? progress, CancellationToken cancellation)
    {
        Validate(request);
        var first = IsFirst(request.WorkshopId);
        var root = request.TempRoot ?? Path.Combine(Path.GetTempPath(), "ParadoxModTool", "staging");
        var work = Path.Combine(root, Guid.NewGuid().ToString("N")[..8]);
        var content = Path.Combine(work, "content");
        StagingResult? staging = null;

        try
        {
            progress?.Report("Dateien werden zusammengestellt …");
            Directory.CreateDirectory(work);
            try
            {
                staging = ModStaging.Stage(request.ModFolder, content, request.Rules);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new WorkshopPublishResult(WorkshopPublishOutcome.Failed, null, Array.Empty<string>(), "Die Dateien konnten nicht zusammengestellt werden: " + ex.Message, null);
            }

            var includeListing = first || request.UpdateListing;
            string? preview = null;
            if (includeListing && !string.IsNullOrWhiteSpace(request.PreviewFile))
            {
                var candidate = Path.Combine(content, request.PreviewFile.Replace('/', Path.DirectorySeparatorChar));
                preview = File.Exists(candidate) ? candidate : null;
            }

            var upload = new WorkshopUpload(
                request.AppId,
                content,
                first ? null : request.WorkshopId!.Trim(),
                request.ChangeNote,
                request.Visibility,
                includeListing ? request.Title : null,
                includeListing ? request.Description : null,
                preview);

            var result = await _backend.UploadAsync(upload, progress, cancellation);
            return new WorkshopPublishResult(result.Outcome, result.WorkshopId, result.Output, result.Message, staging, result.NeedsLegalAgreement);
        }
        finally
        {
            DeleteQuietly(work);
        }
    }

    private static bool IsFirst(string? workshopId) =>
        string.IsNullOrWhiteSpace(workshopId) || workshopId.Trim().All(c => c == '0');

    private static void Validate(WorkshopPublishRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AppId) || !request.AppId.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Die AppID muss eine Zahl sein.", nameof(request));
        }

        if (!string.IsNullOrWhiteSpace(request.WorkshopId) && !request.WorkshopId.Trim().All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Die Workshop-ID muss eine Zahl sein.", nameof(request));
        }
    }

    private static void DeleteQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Aufräumen ist nur ein Versuch; der Ordner liegt im Temp-Ordner.
        }
    }
}
