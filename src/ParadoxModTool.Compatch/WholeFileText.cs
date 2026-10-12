namespace ParadoxModTool.Compatch;

/// <summary>
/// Der Inhalt einer Seite einer ganzen Datei (<see cref="AddressKind.WholeFile"/> oder die Datei eines Schlüssels mit Form "Ganze Datei") für die Spaltenansicht:
/// Textdateien als Text (UTF-8, sonst Windows-1252), Dateien mit Null-Bytes (Bilder, Sounds) und sehr große Dateien nur als Zeile mit Größe und Änderungsdatum.
/// Nur lesend; das Ergebnis wird je Pfad, Größe und Änderungszeit gemerkt, weil die Ansicht nach jeder Entscheidung neu zeichnet.
/// </summary>
public static class WholeFileText
{
    /// <summary>Größere Dateien werden nicht als Text gezeigt (die Spaltenansicht baut je Zeile ein Element).</summary>
    public const long MaxBytes = 1_000_000;

    /// <summary>So viele Bytes am Anfang werden auf Null-Bytes geprüft.</summary>
    private const int ProbeBytes = 8192;

    private static readonly Dictionary<string, (long Length, DateTime WriteUtc, string Text)> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Der Text von <paramref name="side"/>; leer, wenn die Seite die Datei nicht hat.</summary>
    public static string Of(Effective side) =>
        side.Exists && !string.IsNullOrEmpty(side.FullPath) ? OfFile(side.FullPath) : string.Empty;

    /// <summary>Der Text der Datei <paramref name="fullPath"/>; leer, wenn sie fehlt.</summary>
    public static string OfFile(string fullPath)
    {
        try
        {
            var file = new FileInfo(fullPath);
            if (!file.Exists)
            {
                return string.Empty;
            }

            lock (Cache)
            {
                if (Cache.TryGetValue(file.FullName, out var known) && known.Length == file.Length && known.WriteUtc == file.LastWriteTimeUtc)
                {
                    return known.Text;
                }
            }

            var text = Read(file);
            lock (Cache)
            {
                Cache[file.FullName] = (file.Length, file.LastWriteTimeUtc, text);
            }

            return text;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return CompatchTexts.WholeFileUnreadable(ex.Message);
        }
    }

    private static string Read(FileInfo file)
    {
        if (file.Length > MaxBytes)
        {
            return Describe(file);
        }

        var bytes = File.ReadAllBytes(file.FullName);
        return Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, ProbeBytes)) >= 0 ? Describe(file) : TextEncodings.Decode(bytes);
    }

    private static string Describe(FileInfo file) => CompatchTexts.WholeFileNotText(Size(file.Length), file.LastWriteTime.ToString("g"));

    private static string Size(long bytes) => bytes < 1024 ? $"{bytes:N0} B" : bytes < 1024 * 1024 ? $"{bytes / 1024.0:N1} KB" : $"{bytes / (1024.0 * 1024):N1} MB";
}
