using System.Text;
using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.App;

public partial class MainWindow
{
    /// <summary>Profil, nach dem neue Mod-Dateien kodiert werden (Stellaris: Skript, .gui und .gfx ohne BOM).</summary>
    private GameProfile? WriteProfile => GameContext.Profile ?? _activeProfile;

    /// <summary>Kodierung beim Schreiben nach <paramref name="path"/>: neue Dateien nach dem Spielprofil, bestehende behalten ihr BOM.</summary>
    private Encoding ModFileEncodingFor(string path) => ModFileEncoding.ForWrite(WriteProfile, path);
}
