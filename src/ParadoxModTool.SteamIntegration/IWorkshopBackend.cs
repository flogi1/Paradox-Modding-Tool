namespace ParadoxModTool.SteamIntegration;

/// <summary>Alles, was ein Backend für einen Upload braucht. Die Upload-Kopie liegt fertig in <see cref="ContentFolder"/>.</summary>
/// <param name="WorkshopId"><see langword="null"/> = neue Workshop-Seite anlegen.</param>
/// <param name="Title">Nur gesetzt, wenn Titel, Beschreibung und Vorschaubild gesendet werden sollen.</param>
/// <param name="PreviewFile">Absoluter Pfad des Vorschaubilds oder <see langword="null"/>.</param>
public sealed record WorkshopUpload(
    string AppId,
    string ContentFolder,
    string? WorkshopId,
    string ChangeNote,
    WorkshopVisibility? Visibility,
    string? Title,
    string? Description,
    string? PreviewFile);

/// <param name="WorkshopId">Die Workshop-ID nach dem Lauf; beim ersten Veröffentlichen auch dann gesetzt, wenn der Lauf danach scheiterte.</param>
/// <param name="NeedsLegalAgreement">Steam verlangt, dass der Nutzer der Workshop-Vereinbarung zustimmt.</param>
public sealed record WorkshopBackendResult(
    WorkshopPublishOutcome Outcome,
    string? WorkshopId,
    IReadOnlyList<string> Output,
    string Message,
    bool NeedsLegalAgreement = false);

/// <summary>Ein Weg, eine fertig zusammengestellte Upload-Kopie auf Steam zu bringen (SteamCMD oder Steam-Client).</summary>
public interface IWorkshopBackend
{
    Task<WorkshopBackendResult> UploadAsync(WorkshopUpload upload, IProgress<string>? progress, CancellationToken cancellation);
}
