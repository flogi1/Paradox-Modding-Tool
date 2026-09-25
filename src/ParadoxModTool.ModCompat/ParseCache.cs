using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.ModCompat;

/// <summary>Ergebnis des Parsens einer Skriptdatei: entweder der Baum oder eine Fehlermeldung.</summary>
public sealed class ParsedScript
{
    public string Text { get; init; } = string.Empty;

    public ScriptBlock? Root { get; init; }

    public string? Error { get; init; }

    /// <summary>Kodierung, mit der <see cref="Text"/> gelesen wurde: "utf-8" oder "windows-1252" (ältere Spiele wie EU IV).</summary>
    public string EncodingName { get; init; } = "utf-8";

    /// <summary>Liest eine Textdatei: gültiges UTF-8 (BOM wird entfernt), sonst Windows-1252. So bleiben Sonderzeichen älterer Spiele erhalten.</summary>
    public static (string Text, string EncodingName) Decode(byte[] bytes)
    {
        var start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        try
        {
            return (StrictUtf8.GetString(bytes, start, bytes.Length - start), "utf-8");
        }
        catch (System.Text.DecoderFallbackException)
        {
            return (Windows1252.GetString(bytes), "windows-1252");
        }
    }

    private static readonly System.Text.Encoding StrictUtf8 = new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true);

    private static readonly System.Text.Encoding Windows1252 = CreateWindows1252();

    private static System.Text.Encoding CreateWindows1252()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        return System.Text.Encoding.GetEncoding(1252);
    }
}

/// <summary>Merkt geparste Dateien nach Größe und Änderungsdatum, damit eine wiederholte Prüfung nur Geändertes neu liest. Nicht threadsicher.</summary>
public sealed class ParseCache
{
    private readonly Dictionary<string, (long Size, DateTime WriteUtc, ParsedScript Parsed)> _items = new(StringComparer.OrdinalIgnoreCase);

    public ParsedScript Get(string fullPath)
    {
        try
        {
            var info = new FileInfo(fullPath);
            if (_items.TryGetValue(fullPath, out var hit) && hit.Size == info.Length && hit.WriteUtc == info.LastWriteTimeUtc)
            {
                return hit.Parsed;
            }

            var (text, encoding) = ParsedScript.Decode(File.ReadAllBytes(fullPath));
            ParsedScript parsed;
            try
            {
                parsed = new ParsedScript { Text = text, Root = ScriptDocument.Parse(text), EncodingName = encoding };
            }
            catch (ScriptParseException ex)
            {
                parsed = new ParsedScript { Text = text, Error = ex.Message, EncodingName = encoding };
            }

            _items[fullPath] = (info.Length, info.LastWriteTimeUtc, parsed);
            return parsed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ParsedScript { Error = ex.Message };
        }
    }
}
