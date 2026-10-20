using System.Text;

namespace ParadoxModTool.GuiDesign.Index;

/// <summary>
/// Liest .gui- und .gfx-Dateien: streng als UTF-8 (ein BOM wird entfernt), sonst als Windows-1252. Klassische Dateien von EU IV sind teils
/// in Windows-1252 gespeichert (z.B. <c>end_of_combat.gui</c>); als UTF-8 gelesen würden Umlaute zu Ersatzzeichen.
/// </summary>
public static class GuiTextReader
{
    public const string Utf8 = "utf-8";
    public const string Utf8Bom = "utf-8-bom";
    public const string Windows1252 = "windows-1252";

    private static readonly UTF8Encoding StrictUtf8 = new(false, throwOnInvalidBytes: true);

    private static readonly Encoding Latin = Create1252();

    /// <returns>Der Text und der Name der erkannten Kodierung (<c>utf-8</c>, <c>utf-8-bom</c> oder <c>windows-1252</c>).</returns>
    public static (string Text, string EncodingName) Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var body = hasBom ? bytes.AsSpan(3) : bytes.AsSpan();
        try
        {
            return (StrictUtf8.GetString(body), hasBom ? Utf8Bom : Utf8);
        }
        catch (DecoderFallbackException)
        {
            // Ohne das BOM (sonst stünde "ï»¿" am Anfang). Gespeichert wird so eine Datei wegen des BOM wieder als UTF-8 mit BOM (ModFileEncoding.ForWrite).
            return (Latin.GetString(body), Windows1252);
        }
    }

    /// <summary>
    /// Text einer beliebigen Datei für Editor-Tabs und GUI-Index: <c>.gui</c> und <c>.gfx</c> per <see cref="Read"/> (Windows-1252-Rückfall), alles andere
    /// als UTF-8 (Architektur-Spezifikation, Abschnitt 5h). Eine Regel für Öffnen (<c>OpenDocument</c>) und Neuladen nach einem Schreiben.
    /// </summary>
    public static string ReadFile(string path) =>
        path.EndsWith(".gui", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".gfx", StringComparison.OrdinalIgnoreCase)
            ? Read(path).Text
            : File.ReadAllText(path, Encoding.UTF8);

    private static Encoding Create1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
