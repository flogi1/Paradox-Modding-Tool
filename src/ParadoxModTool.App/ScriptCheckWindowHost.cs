using ParadoxModTool.GameProfiles;
using ParadoxModTool.ScriptCheck;
using ParadoxModTool.ScriptDocs;

namespace ParadoxModTool.App;

/// <summary>
/// Was das Fenster "Skript-Prüfung" vom Hauptfenster braucht. Die Funktionen lesen den Zustand bei jedem Aufruf neu; das Fenster liest sie vor
/// jedem Aufbau auf dem UI-Thread und gibt die Momentaufnahme an den Hintergrund-Task weiter.
/// </summary>
/// <param name="GameId">Kennung des Spiels dieses PMT-Fensters.</param>
/// <param name="Profile">Spielprofil (Ladereihenfolge, <c>replace_path</c>).</param>
/// <param name="Rules">Regeln des Spiels aus <c>profiles/scriptcheck</c>.</param>
/// <param name="ActiveModDirectory">Vorhandener Ordner der aktiven Mod; null ohne aktive Mod.</param>
/// <param name="Mods">Mod-Auswahl wie im Fehlerprotokoll (eigene Mods, Playset, installierte Mods).</param>
/// <param name="InstallDirectory">Installationsordner des Spiels; null, solange die Spiel-Exe unbekannt ist.</param>
/// <param name="ContentRoot">Ordner mit den Vanilla-Spieldateien; null, solange die Spiel-Exe unbekannt ist.</param>
/// <param name="ScriptDocs">Die Script-Datenbank des Spiels, für die eingebauten Befehle.</param>
/// <param name="OpenFileAtLine">Öffnet die Datei im Editor des Hauptfensters an dieser Zeile (1-basiert).</param>
/// <param name="GameDirectory">PMT-Datenordner des Spiels; Zwischenspeicher und Ausblende-Listen liegen in dessen Unterordner <c>script-check</c>.</param>
/// <param name="SelectedPlaysetName">Name des in der PMT-Werkzeugleiste gewählten Playsets; null ohne Auswahl.</param>
internal sealed record ScriptCheckWindowHost(
    string GameId,
    GameProfile Profile,
    ScriptCheckRules Rules,
    Func<string?> ActiveModDirectory,
    ModSourceCollector Mods,
    Func<string?> InstallDirectory,
    Func<string?> ContentRoot,
    Func<ScriptDocDatabase> ScriptDocs,
    Action<string, int> OpenFileAtLine,
    string GameDirectory,
    Func<string?> SelectedPlaysetName);
