using System.Text;

namespace ParadoxModTool.GameProfiles;

/// <summary>
/// Kodierung beim Schreiben von Mod-Dateien. Neue Dateien folgen <see cref="FileTypeMapping.DefaultEncoding"/> des Spielprofils (ohne Profil oder
/// Eintrag: UTF-8 mit BOM); bestehende behalten ihr BOM. Lokalisierung (<c>.yml</c>) bekommt immer ein BOM, sonst lesen die Spiele sie nicht.
/// Stellaris liest Skript-, .gui- und .gfx-Dateien mit BOM falsch ("Unexpected token: &lt;BOM&gt;", gemessen 2026-10-16), deshalb steht dort
/// <c>utf-8</c> im Profil.
/// </summary>
public static class ModFileEncoding
{
    public const string Utf8 = "utf-8";
    public const string Utf8Bom = "utf-8-bom";

    /// <summary>Kodierungsname aus dem Profil für neue Dateien mit dieser Endung (mit Punkt, Groß-/Kleinschreibung egal).</summary>
    public static string NewFileEncodingName(GameProfile? profile, string extension)
    {
        if (string.Equals(extension, ".yml", StringComparison.OrdinalIgnoreCase))
        {
            return Utf8Bom;
        }

        var name = profile?.FileTypes.FirstOrDefault(f => string.Equals(f.Extension, extension, StringComparison.OrdinalIgnoreCase))?.DefaultEncoding;
        return string.IsNullOrWhiteSpace(name) ? Utf8Bom : name.Trim().ToLowerInvariant();
    }

    /// <summary>Nur <c>utf-8</c> heißt ohne BOM; alles andere (auch Unbekanntes) bekommt eins, wie bisher.</summary>
    public static bool NewFileHasBom(GameProfile? profile, string extension) => NewFileEncodingName(profile, extension) != Utf8;

    public static Encoding NewFile(GameProfile? profile, string extension) => new UTF8Encoding(NewFileHasBom(profile, extension));

    /// <summary>
    /// Kodierung für das Schreiben nach <paramref name="path"/>: neue Dateien nach dem Profil, bestehende mit BOM wieder mit BOM, bestehende
    /// UTF-8-Dateien ohne BOM wieder ohne. Lokalisierung bekommt immer ein BOM. Bestehende <c>.gui</c>- und <c>.gfx</c>-Dateien, die kein gültiges UTF-8
    /// sind (Windows-1252 bei EU IV), bleiben Windows-1252: der Editor liest sie so (<c>GuiTextReader</c>), sonst würden Umlaute zerstört. Andere Dateien,
    /// die kein gültiges UTF-8 sind, werden wie neue behandelt; der Editor liest sie als UTF-8.
    /// </summary>
    public static Encoding ForWrite(GameProfile? profile, string path)
    {
        var extension = Path.GetExtension(path);
        if (string.Equals(extension, ".yml", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return NewFile(profile, extension);
        }

        var bytes = File.ReadAllBytes(path);
        if (HasUtf8Bom(bytes))
        {
            return new UTF8Encoding(true);
        }

        if (IsValidUtf8(bytes))
        {
            return new UTF8Encoding(false);
        }

        return IsGuiFile(extension) ? Windows1252 : NewFile(profile, extension);
    }

    private static bool IsGuiFile(string extension) =>
        string.Equals(extension, ".gui", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".gfx", StringComparison.OrdinalIgnoreCase);

    private static readonly Encoding Windows1252 = Create1252();

    private static Encoding Create1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    public static bool HasUtf8Bom(ReadOnlySpan<byte> bytes) => bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

    private static bool IsValidUtf8(byte[] bytes)
    {
        try
        {
            _ = new UTF8Encoding(false, throwOnInvalidBytes: true).GetCharCount(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
