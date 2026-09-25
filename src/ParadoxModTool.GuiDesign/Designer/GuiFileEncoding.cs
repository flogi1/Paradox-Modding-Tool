using System.Text;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>
/// Kodierung beim Schreiben von Mod-Dateien (Spec "GuiOverridePlanner", 3b-Nacharbeit): neue Dateien nach dem Namen aus dem Spielprofil
/// (<c>utf-8</c>, <c>utf-8-bom</c>, <c>windows-1252</c>; Unbekanntes gilt als <c>utf-8-bom</c>), bestehende bleiben UTF-8 mit bzw. ohne BOM.
/// </summary>
public static class GuiFileEncoding
{
    public static Encoding For(string profileEncoding, bool isNew, bool existingHasBom)
    {
        if (!isNew)
        {
            return new UTF8Encoding(existingHasBom);
        }

        switch (profileEncoding.Trim().ToLowerInvariant())
        {
            case "utf-8":
                return new UTF8Encoding(false);
            case "windows-1252":
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding(1252);
            default:
                return new UTF8Encoding(true);
        }
    }
}
