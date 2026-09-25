using System.Text.Json;

namespace ParadoxModTool.SteamIntegration;

/// <summary>
/// Der Hilfsprozess für den Upload über den Steam-Client. Steam merkt sich das Spiel als "läuft", solange der Prozess lebt, der Steamworks benutzt hat; auch nach <c>SteamAPI_Shutdown</c>
/// bleibt es dabei. Darum läuft der Upload in einem eigenen kurzlebigen Prozess, der danach endet.
/// </summary>
public static class SteamUploadHelper
{
    /// <summary>Führt den Upload aus einer Anfragedatei aus und schreibt <c>PROGRESS</c>/<c>RESULT</c>-Zeilen nach <paramref name="output"/>. Eine Zeile <c>CANCEL</c> in <paramref name="input"/> bricht ab.</summary>
    /// <returns><c>0</c>, wenn ein Ergebnis geschrieben wurde (auch ein Fehlschlag des Uploads); <c>2</c>, wenn schon die Anfrage unbrauchbar war.</returns>
    public static async Task<int> RunAsync(
        string requestFile,
        Func<SteamUploadRequest, ISteamworksApi> apiFactory,
        TextWriter output,
        TextReader input,
        CancellationToken cancellation)
    {
        var gate = new object();
        void Write(string line)
        {
            lock (gate)
            {
                output.WriteLine(line);
                output.Flush();
            }
        }

        SteamUploadRequest request;
        try
        {
            request = SteamUploadProtocol.DeserializeRequest(File.ReadAllText(requestFile));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Write(SteamUploadProtocol.ResultLine(new WorkshopBackendResult(WorkshopPublishOutcome.Failed, null, Array.Empty<string>(), "Die Anfrage an den Steam-Hilfsprozess war nicht lesbar: " + ex.Message)));
            return 2;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        _ = WatchForCancelAsync(input, cts);

        var backend = new SteamClientBackend(() => apiFactory(request), TimeSpan.FromMilliseconds(request.PollMilliseconds), TimeSpan.FromSeconds(request.TimeoutSeconds));
        WorkshopBackendResult result;
        try
        {
            result = await backend.UploadAsync(request.Upload, new LineProgress(text => Write(SteamUploadProtocol.ProgressLine(text))), cts.Token);
        }
        catch (ArgumentException ex)
        {
            result = new WorkshopBackendResult(WorkshopPublishOutcome.Failed, null, Array.Empty<string>(), ex.Message);
        }

        Write(SteamUploadProtocol.ResultLine(result));
        return 0;
    }

    private static async Task WatchForCancelAsync(TextReader input, CancellationTokenSource cts)
    {
        try
        {
            while (await input.ReadLineAsync() is { } line)
            {
                if (line.Trim() == SteamUploadProtocol.CancelLine)
                {
                    cts.Cancel();
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // Die Eingabe ist weg (der Elternprozess endete); der Upload läuft zu Ende.
        }
    }

    private sealed class LineProgress : IProgress<string>
    {
        private readonly Action<string> _write;

        public LineProgress(Action<string> write)
        {
            _write = write;
        }

        public void Report(string value) => _write(value);
    }
}
