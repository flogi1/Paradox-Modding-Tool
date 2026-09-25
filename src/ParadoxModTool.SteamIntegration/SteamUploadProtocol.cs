using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParadoxModTool.SteamIntegration;

/// <summary>Was das Programm dem Hilfsprozess für einen Upload über den Steam-Client mitgibt (als JSON-Datei; der Pfad steht in der Befehlszeile).</summary>
public sealed record SteamUploadRequest(string DllPath, string AppId, WorkshopUpload Upload, int PollMilliseconds, int TimeoutSeconds);

/// <summary>
/// Die Verständigung zwischen dem Programm und dem Hilfsprozess: eine JSON-Anfragedatei, Textzeilen auf der Standardausgabe des Helfers (<c>PROGRESS</c>, <c>RESULT</c>)
/// und die Zeile <c>CANCEL</c> auf seiner Standardeingabe.
/// </summary>
public static class SteamUploadProtocol
{
    /// <summary>Das erste Befehlszeilenargument, mit dem sich das Programm als Hilfsprozess startet (danach folgt der Pfad der Anfragedatei).</summary>
    public const string Argument = "--steam-upload";

    public const string CancelLine = "CANCEL";

    private const string ProgressPrefix = "PROGRESS\t";
    private const string ResultPrefix = "RESULT\t";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string SerializeRequest(SteamUploadRequest request) => JsonSerializer.Serialize(request, Options);

    public static SteamUploadRequest DeserializeRequest(string json) =>
        JsonSerializer.Deserialize<SteamUploadRequest>(json, Options) ?? throw new JsonException("Die Anfrage ist leer.");

    public static string ProgressLine(string text) => ProgressPrefix + text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');

    public static string ResultLine(WorkshopBackendResult result) => ResultPrefix + JsonSerializer.Serialize(result, Options);

    /// <summary>Liest eine Zeile des Helfers. <see langword="false"/> bei allem, was kein gültiges Protokoll ist (dann wird die Zeile ignoriert).</summary>
    public static bool TryParseLine(string line, out string? progress, out WorkshopBackendResult? result)
    {
        progress = null;
        result = null;
        if (line.StartsWith(ProgressPrefix, StringComparison.Ordinal))
        {
            progress = line[ProgressPrefix.Length..];
            return true;
        }

        if (line.StartsWith(ResultPrefix, StringComparison.Ordinal))
        {
            try
            {
                result = JsonSerializer.Deserialize<WorkshopBackendResult>(line[ResultPrefix.Length..], Options);
            }
            catch (JsonException)
            {
                return false;
            }

            return result is not null;
        }

        return false;
    }
}
