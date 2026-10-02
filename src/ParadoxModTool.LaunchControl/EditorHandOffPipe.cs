using System.IO.Pipes;
using System.Text;

namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Weitergabe an ein laufendes PMT (Spec "Weitergabe an ein laufendes PMT"): Named Pipe je Spiel-ID, nur für den eigenen Benutzer, eine
/// <see cref="EditorRequest"/> je Verbindung (Plan 4 Ruling 3).
/// </summary>
public static class EditorHandOffPipe
{
    public static string PipeName(string gameId) => "ParadoxModTool-" + gameId;

    /// <summary>
    /// Schickt die Anfrage an das PMT dieses Spiels. Versucht es bis <paramref name="timeout"/>, weil ein gerade startendes PMT die Sperre schon
    /// hält, aber noch nicht lauscht. False, wenn in der Zeit keins angenommen hat.
    /// </summary>
    public static async Task<bool> SendAsync(string gameId, EditorRequest request, TimeSpan timeout, CancellationToken cancellation = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        var payload = Encoding.UTF8.GetBytes(EditorHandOffMessage.Encode(request));
        while (true)
        {
            var remaining = deadline - DateTime.UtcNow;
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName(gameId), PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await client.ConnectAsync((int)Math.Clamp(remaining.TotalMilliseconds, 50, 1000), cancellation).ConfigureAwait(false);
                await client.WriteAsync(payload, cancellation).ConfigureAwait(false);
                await client.FlushAsync(cancellation).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
            {
                // Noch kein Dienst (oder gerade belegt): gleich noch einmal.
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(200, cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Nimmt Anfragen an, bis <paramref name="cancellation"/> endet. Eine abgebrochene Verbindung beendet den Dienst nicht; eine, die nach
    /// <paramref name="readTimeout"/> (Standard 5 s) nicht fertig ist, wird verworfen, damit ein hängender Client spätere Anfragen nicht blockiert. <paramref name="received"/>
    /// läuft auf dem Thread des Dienstes; die Oberfläche muss selbst auf ihren Thread wechseln.
    /// </summary>
    public static async Task ServeAsync(string gameId, Action<EditorRequest> received, CancellationToken cancellation, TimeSpan? readTimeout = null)
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName(gameId), PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellation).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8);
                using var read = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                read.CancelAfter(readTimeout ?? TimeSpan.FromSeconds(5));
                var text = await reader.ReadToEndAsync(read.Token).ConfigureAwait(false);
                if (EditorHandOffMessage.Decode(text) is { } request)
                {
                    received(request);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                // Zeitlimit beim Lesen: Verbindung verwerfen, weiter lauschen.
            }
            catch (UnauthorizedAccessException)
            {
                // Der Name gehört einem anderen Benutzer: kein Dienst in diesem Prozess.
                return;
            }
            catch (IOException)
            {
                await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}

/// <summary>
/// Protokoll der Weitergabe (Spec: "PMT protokolliert die empfangenen Argumente im eigenen Log", Plan 4 Ruling 11): eine Zeile mit Zeitstempel je
/// Eintrag; über <see cref="MaxBytes"/> wird die Datei zu <c>.1</c>. Schreibfehler werden verschluckt, das Protokoll ist nur eine Hilfe.
/// </summary>
public static class HandOffLog
{
    public const long MaxBytes = 256 * 1024;

    public static void Append(string path, string text, DateTime now)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
            {
                File.Move(path, path + ".1", overwrite: true);
            }

            File.AppendAllText(path, $"{now:yyyy-MM-dd HH:mm:ss} {text}{Environment.NewLine}", new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Protokoll ist Hilfe, kein Muss.
        }
    }
}
