using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.App;

/// <summary>
/// Was das Fenster "Flag-Tracker" vom Hauptfenster braucht. Die Funktionen lesen den Zustand bei jedem Aufruf neu (aktive Mod, Spiel-Exe und
/// ScriptDocs können sich ändern, solange das Fenster offen ist). Das Fenster liest sie vor jedem Aufbau auf dem UI-Thread und gibt die
/// Momentaufnahme an den Hintergrund-Task weiter.
/// </summary>
/// <param name="GameId">Kennung des Spiels dieses PMT-Fensters ("eu5", "ck3").</param>
/// <param name="Profile">Spielprofil (Ladereihenfolge, <c>replace_path</c>).</param>
/// <param name="ActiveModDirectory">Vorhandener Ordner der aktiven Mod; null ohne aktive Mod. Nur auf dem UI-Thread.</param>
/// <param name="Mods">Mod-Auswahl wie im Fehlerprotokoll (eigene Mods, Playset, installierte Mods).</param>
/// <param name="InstallDirectory">Installationsordner des Spiels; null, solange die Spiel-Exe unbekannt ist. Nur auf dem UI-Thread.</param>
/// <param name="ContentRoot">Ordner mit den Vanilla-Spieldateien; null, solange die Spiel-Exe unbekannt ist. Nur auf dem UI-Thread.</param>
/// <param name="ScriptDocNames">Namen der nicht veralteten Effekte und Trigger aus den ScriptDocs; leer ohne Import. Nur auf dem UI-Thread.</param>
/// <param name="OpenFileAtLine">Öffnet die Datei im Editor des Hauptfensters an dieser Zeile (1-basiert). Nur auf dem UI-Thread.</param>
/// <param name="GameDirectory">PMT-Datenordner des Spiels; das Verzeichnis liegt in dessen Unterordner <c>flag-tracker</c>.</param>
/// <param name="SelectedPlaysetName">Name des in der PMT-Werkzeugleiste gewählten Playsets; null ohne Auswahl. Nur auf dem UI-Thread.</param>
internal sealed record FlagTrackerWindowHost(
    string GameId,
    GameProfile Profile,
    Func<string?> ActiveModDirectory,
    ModSourceCollector Mods,
    Func<string?> InstallDirectory,
    Func<string?> ContentRoot,
    Func<IReadOnlyList<string>> ScriptDocNames,
    Action<string, int> OpenFileAtLine,
    string GameDirectory,
    Func<string?> SelectedPlaysetName);
