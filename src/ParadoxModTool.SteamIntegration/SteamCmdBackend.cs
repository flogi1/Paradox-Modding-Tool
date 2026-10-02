using System.ComponentModel;
using System.Text;

namespace ParadoxModTool.SteamIntegration;

/// <summary>Lädt über SteamCMD hoch: schreibt die VDF-Datei neben die Upload-Kopie, startet SteamCMD und wertet die Ausgabe aus.</summary>
public sealed class SteamCmdBackend : IWorkshopBackend
{
    private readonly ISteamCmdProcess _process;
    private readonly string _steamCmdPath;
    private readonly string _userName;
    private readonly TimeSpan _timeout;

    public SteamCmdBackend(ISteamCmdProcess process, string steamCmdPath, string userName, TimeSpan timeout)
    {
        _process = process;
        _steamCmdPath = steamCmdPath;
        _userName = userName;
        _timeout = timeout;
    }

    public async Task<WorkshopBackendResult> UploadAsync(WorkshopUpload upload, IProgress<string>? progress, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(_userName) || _userName.Any(c => char.IsWhiteSpace(c) || c == '"'))
        {
            throw new ArgumentException(SteamTexts.UserNameInvalid, nameof(upload));
        }

        var first = upload.WorkshopId is null;
        var vdfPath = Path.Combine(Path.GetDirectoryName(upload.ContentFolder)!, "build.vdf");
        var item = new WorkshopItem(
            upload.AppId,
            first ? "0" : upload.WorkshopId!,
            upload.ContentFolder,
            upload.ChangeNote,
            upload.Visibility,
            upload.Title,
            upload.Description,
            upload.PreviewFile);
        File.WriteAllText(vdfPath, WorkshopItemVdf.Build(item), new UTF8Encoding(false));

        progress?.Report(SteamTexts.CmdStarting);
        var start = new SteamCmdStart(
            _steamCmdPath,
            new[] { "+login", _userName, "+workshop_build_item", vdfPath, "+quit" },
            _timeout);

        SteamCmdRun run;
        try
        {
            run = await _process.RunAsync(start, progress, cancellation);
        }
        catch (OperationCanceledException)
        {
            return Result(WorkshopPublishOutcome.Cancelled, ReadNewId(vdfPath, first), Array.Empty<string>(), SteamTexts.UploadCancelled);
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return Result(WorkshopPublishOutcome.Failed, null, Array.Empty<string>(), SteamTexts.CmdStartFailed(ex.Message));
        }

        var id = first ? ReadNewId(vdfPath, first) : upload.WorkshopId;
        var outcome = SteamCmdOutput.Interpret(run);
        return outcome.Kind switch
        {
            SteamCmdOutcomeKind.Success when first && id is null =>
                Result(WorkshopPublishOutcome.Failed, null, run.Lines, SteamTexts.CmdNoWorkshopId),
            SteamCmdOutcomeKind.Success =>
                Result(WorkshopPublishOutcome.Success, id, run.Lines, SteamTexts.Uploaded),
            SteamCmdOutcomeKind.LoginRequired => Result(WorkshopPublishOutcome.LoginRequired, id, run.Lines, outcome.Message),
            SteamCmdOutcomeKind.GuardRequired => Result(WorkshopPublishOutcome.GuardRequired, id, run.Lines, outcome.Message),
            SteamCmdOutcomeKind.Timeout => Result(WorkshopPublishOutcome.Timeout, id, run.Lines, outcome.Message),
            _ => Result(WorkshopPublishOutcome.Failed, id, run.Lines, outcome.Message),
        };
    }

    private static WorkshopBackendResult Result(WorkshopPublishOutcome outcome, string? id, IReadOnlyList<string> output, string message) =>
        new(outcome, id, output, message);

    /// <summary>Die von SteamCMD in die VDF-Datei geschriebene neue ID; <see langword="null"/>, wenn keine (oder <c>0</c>) drinsteht.</summary>
    private static string? ReadNewId(string vdfPath, bool first)
    {
        if (!first || !File.Exists(vdfPath))
        {
            return null;
        }

        try
        {
            var id = WorkshopItemVdf.ReadPublishedFileId(File.ReadAllText(vdfPath));
            return id is null || id.All(c => c == '0') ? null : id;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
