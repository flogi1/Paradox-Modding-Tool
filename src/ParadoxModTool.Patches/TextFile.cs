using System.Text;

namespace ParadoxModTool.Patches;

/// <summary>Inhalt einer Textdatei samt BOM-Zustand, Zeilenende und Kodierung (<c>utf-8</c> oder <c>windows-1252</c>), damit geänderte Dateien genauso zurückgeschrieben werden.</summary>
public sealed record TextFileContent(string Text, bool HasBom, string NewLine, string EncodingName = TextFile.Utf8);

public static class TextFile
{
    public const string Utf8 = "utf-8";

    public const string Windows1252 = "windows-1252";

    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);

    private static readonly Encoding Cp1252 = CreateWindows1252();

    /// <summary>Gültiges UTF-8 (BOM wird entfernt), sonst Windows-1252 wie bei älteren Spielen (EU IV); so gehen keine Sonderzeichen verloren.</summary>
    public static TextFileContent Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var start = hasBom ? 3 : 0;
        try
        {
            var text = StrictUtf8.GetString(bytes, start, bytes.Length - start);
            return new TextFileContent(text, hasBom, NewLineOf(text), Utf8);
        }
        catch (DecoderFallbackException)
        {
            var text = Cp1252.GetString(bytes);
            return new TextFileContent(text, false, NewLineOf(text), Windows1252);
        }
    }

    /// <summary><c>"\r\n"</c>, wenn der Text es enthält, sonst <c>"\n"</c>.</summary>
    public static string NewLineOf(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    public static Encoding EncodingFor(bool hasBom, string encodingName = Utf8) =>
        encodingName == Windows1252 ? Cp1252 : new UTF8Encoding(hasBom);

    private static Encoding CreateWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
