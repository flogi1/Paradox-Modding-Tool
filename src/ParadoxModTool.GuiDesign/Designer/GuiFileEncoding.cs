using System.Text;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>
/// Kodierung beim Schreiben von Mod-Dateien (Spec "GuiOverridePlanner", 3b-Nacharbeit): neue Dateien nach dem Namen aus dem Spielprofil
/// (<c>utf-8</c> oder <c>utf-8-bom</c>; alles andere gilt als <c>utf-8-bom</c>), bestehende bleiben UTF-8 mit bzw. ohne BOM. <c>windows-1252</c> gibt
/// es hier bewusst nicht mehr (3c-Review): alle Leser des Designers erwarten UTF-8, Umlaute kämen beim Zurücklesen kaputt zurück und würden beim
/// nächsten Übernehmen als UTF-8 festgeschrieben.
/// </summary>
public static class GuiFileEncoding
{
    public static Encoding For(string profileEncoding, bool isNew, bool existingHasBom)
    {
        if (!isNew)
        {
            return new UTF8Encoding(existingHasBom);
        }

        return profileEncoding.Trim().ToLowerInvariant() == "utf-8" ? new UTF8Encoding(false) : new UTF8Encoding(true);
    }
}
