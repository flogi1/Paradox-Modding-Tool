using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace ParadoxModTool.SteamIntegration;

/// <summary>Startet den Hilfsprozess (siehe <see cref="SteamUploadHelper"/>) und liefert seine Standardausgabe Zeile für Zeile. Die Abstraktion erlaubt Tests ohne echten Prozess.</summary>
public interface ISteamHelperLauncher
{
    /// <summary>
    /// Startet den Helfer mit der Anfragedatei und wartet, bis er endet; jede Ausgabezeile geht an <paramref name="onLine"/>. Bei Abbruch bittet der Launcher den Helfer um Abbruch
    /// (Zeile <c>CANCEL</c>) und wartet weiter auf sein Ende; erst nach einer Karenzzeit wird der Prozess beendet. Rückgabe ist der Exitcode.
    /// </summary>
    Task<int> RunAsync(string requestFile, Action<string> onLine, CancellationToken cancellation);
}

/// <summary>
/// Lädt über den Steam-Client hoch, aber in einem eigenen kurzlebigen Prozess: Steam zeigt das Spiel sonst als "läuft" an, bis das Programm selbst endet. Die Ablauflogik steckt in
/// <see cref="SteamClientBackend"/> (im Helfer), dieser Teil startet den Helfer und reicht Fortschritt und Ergebnis durch.
/// </summary>
public sealed class SteamClientProcessBackend : IWorkshopBackend
{
    private readonly ISteamHelperLauncher _launcher;
    private readonly string _dllPath;
    private readonly TimeSpan _poll;
    private readonly TimeSpan _timeout;

    public SteamClientProcessBackend(ISteamHelperLauncher launcher, string dllPath, TimeSpan poll, TimeSpan timeout)
    {
        _launcher = launcher;
        _dllPath = dllPath;
        _poll = poll;
        _timeout = timeout;
    }

    public async Task<WorkshopBackendResult> UploadAsync(WorkshopUpload upload, IProgress<string>? progress, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(upload.AppId) || !upload.AppId.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Die AppID muss eine Zahl sein.", nameof(upload));
        }

        var request = new SteamUploadRequest(_dllPath, upload.AppId, upload, (int)_poll.TotalMilliseconds, (int)_timeout.TotalSeconds);
        var folder = Path.Combine(Path.GetTempPath(), "ParadoxModTool", "helper");
        var file = Path.Combine(folder, "upload-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        WorkshopBackendResult? result = null;
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(file, SteamUploadProtocol.SerializeRequest(request), new UTF8Encoding(false));

            int exitCode;
            try
            {
                exitCode = await _launcher.RunAsync(
                    file,
                    line =>
                    {
                        if (SteamUploadProtocol.TryParseLine(line, out var text, out var parsed))
                        {
                            if (text is not null)
                            {
                                progress?.Report(text);
                            }

                            result = parsed ?? result;
                        }
                    },
                    cancellation);
            }
            catch (OperationCanceledException)
            {
                return result ?? new WorkshopBackendResult(WorkshopPublishOutcome.Cancelled, upload.WorkshopId, Array.Empty<string>(), "Der Upload wurde abgebrochen.");
            }
            catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                return new WorkshopBackendResult(WorkshopPublishOutcome.Failed, null, Array.Empty<string>(), "Der Steam-Hilfsprozess konnte nicht gestartet werden: " + ex.Message);
            }

            return result ?? new WorkshopBackendResult(
                WorkshopPublishOutcome.Failed,
                upload.WorkshopId,
                Array.Empty<string>(),
                "Der Steam-Hilfsprozess endete ohne Ergebnis (Exitcode " + exitCode.ToString(CultureInfo.InvariantCulture) + ").");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new WorkshopBackendResult(WorkshopPublishOutcome.Failed, null, Array.Empty<string>(), "Die Anfrage an den Steam-Hilfsprozess konnte nicht geschrieben werden: " + ex.Message);
        }
        finally
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Aufräumen ist nur ein Versuch; die Datei liegt im Temp-Ordner.
            }
        }
    }
}
