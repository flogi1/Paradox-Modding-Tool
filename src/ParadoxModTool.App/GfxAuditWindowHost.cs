using ParadoxModTool.GameProfiles;
using ParadoxModTool.GfxAudit;

namespace ParadoxModTool.App;

/// <summary>
/// Was das Fenster "GFX-Prüfung" vom Hauptfenster braucht. Die Funktionen lesen den Zustand bei jedem Aufruf neu; das Fenster liest sie vor
/// jedem Aufbau auf dem UI-Thread und gibt die Momentaufnahme an den Hintergrund-Task weiter.
/// </summary>
/// <param name="GameId">Kennung des Spiels dieses PMT-Fensters.</param>
/// <param name="Profile">Spielprofil (Ladereihenfolge, <c>replace_path</c>).</param>
/// <param name="Rules">Regeln des Spiels aus <c>profiles/gfxaudit</c>.</param>
/// <param name="ActiveModDirectory">Vorhandener Ordner der aktiven Mod; null ohne aktive Mod.</param>
/// <param name="Mods">Mod-Auswahl wie im Fehlerprotokoll (eigene Mods, Playset, installierte Mods).</param>
/// <param name="InstallDirectory">Installationsordner des Spiels; null, solange die Spiel-Exe unbekannt ist.</param>
/// <param name="ContentRoot">Ordner mit den Vanilla-Spieldateien; null, solange die Spiel-Exe unbekannt ist.</param>
/// <param name="OpenFileAtLine">Öffnet die Datei im Editor des Hauptfensters an dieser Zeile (1-basiert).</param>
/// <param name="GameDirectory">PMT-Datenordner des Spiels; Zwischenspeicher und Ausblende-Listen liegen in dessen Unterordner <c>gfx-audit</c>.</param>
/// <param name="SelectedPlaysetName">Name des in der PMT-Werkzeugleiste gewählten Playsets; null ohne Auswahl.</param>
/// <param name="ModRoot">Mod-Ordner des Spiels (Umgebungsvariablen aufgelöst); null, wenn das Profil keinen kennt. Nur Mods darunter können Bilder auslagern.</param>
internal sealed record GfxAuditWindowHost(
    string GameId,
    GameProfile Profile,
    GfxAuditRules Rules,
    Func<string?> ActiveModDirectory,
    ModSourceCollector Mods,
    Func<string?> InstallDirectory,
    Func<string?> ContentRoot,
    Action<string, int> OpenFileAtLine,
    string GameDirectory,
    Func<string?> SelectedPlaysetName,
    string? ModRoot);
