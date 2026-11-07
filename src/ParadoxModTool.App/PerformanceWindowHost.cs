using ParadoxModTool.GameProfiles;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.Session;

namespace ParadoxModTool.App;

/// <summary>
/// Was das Fenster "Performance-Checker" vom Hauptfenster braucht. Die Funktionen lesen den Zustand bei jedem Aufruf neu (aktive Mod, Spiel-Exe,
/// Playsets können sich ändern, solange das Fenster offen ist). Wer einen Wert auf einem Hintergrund-Thread braucht, liest ihn vorher auf dem
/// UI-Thread aus und gibt die Momentaufnahme weiter.
/// </summary>
/// <param name="GameId">Kennung des Spiels dieses PMT-Fensters.</param>
/// <param name="Profile">Spielprofil; <see cref="GameProfile.Performance"/> ist nutzbar, sonst öffnet das Fenster nicht.</param>
/// <param name="ActiveModRoot">Vorhandener Ordner der aktiven Mod; null ohne aktive Mod. Nur auf dem UI-Thread (liest die Mod-Liste des Hauptfensters).</param>
/// <param name="ActiveModId">Kennung der aktiven Mod; null ohne aktive Mod. Nur auf dem UI-Thread (liest die Mod-Liste des Hauptfensters).</param>
/// <param name="KnownMods">Bekannte Mods mit vorhandenem Ordner, nach Namen sortiert. Nur auf dem UI-Thread (liest die Mod-Liste des Hauptfensters).</param>
/// <param name="GameInstallRoot">
/// Vanilla-Dateien des Spiels (Ordner mit <c>common</c> usw.). Auf dem UI-Thread fragt es nach der Spiel-Exe, falls sie fehlt; null, wenn sie
/// danach (oder auf einem anderen Thread) nicht bekannt ist. Die Nachfrage gibt es nur auf dem UI-Thread.
/// </param>
/// <param name="LogFolder">Logs-Ordner des Spiels: der im Fehlerprotokoll gewählte, sonst der aus dem Spielprofil. Nur auf dem UI-Thread (liest die Wahl im Fehlerprotokoll).</param>
/// <param name="SaveFolder">Spielstand-Ordner; null, wenn das Profil keine Spielstände kennt.</param>
/// <param name="ContinueGamePath">
/// Pfad der Datei, die den letzten Spielstand nennt (<see cref="PerformanceSettings.ContinueGameFile"/> im Nutzerordner neben dem Logs-Ordner aus
/// <paramref name="LogFolder"/>). Nur auf dem UI-Thread (wie <paramref name="LogFolder"/>).
/// </param>
/// <param name="ExecutablePath">Spiel-Exe; null, solange sie nicht eingestellt ist.</param>
/// <param name="Playsets">Playsets der eingestellten Playset-Datei.</param>
/// <param name="StartGame">
/// Startet das Spiel mit dem Playset der Id (null = ohne Playset-Wechsel) und diesen Argumenten; true, wenn es gestartet wurde. Nur auf dem
/// UI-Thread (Rückfragen, Playset-Auswahl des Hauptfensters).
/// </param>
/// <param name="GameRunning">Das Spiel läuft.</param>
/// <param name="GameRunningInDebug">Das Spiel läuft und PMT hat es selbst im Debug-Modus gestartet.</param>
/// <param name="OpenFileAtLine">Öffnet die Datei im Editor des Hauptfensters an dieser Zeile. Nur auf dem UI-Thread.</param>
/// <param name="GameDirectory">PMT-Datenordner des Spiels; die Läufe liegen darunter.</param>
/// <param name="State">Sitzungszustand des Spiels (z. B. die Rauschschwellen).</param>
/// <param name="ImportRecipeFromAi">
/// Übergibt eine KI-Antwort mit Rezeptblock (ganzer Text) an die Patch-Rezepte dieser Mod (Ordner; null = die dort gewählte): öffnet das Fenster,
/// prüft und speichert das Rezept wie "Rezept mit KI erstellen" und gibt dessen Meldungen zurück. Nur auf dem UI-Thread.
/// </param>
internal sealed record PerformanceWindowHost(
    string GameId,
    GameProfile Profile,
    Func<string?> ActiveModRoot,
    Func<string?> ActiveModId,
    Func<IReadOnlyList<(string Name, string Path)>> KnownMods,
    Func<string?> GameInstallRoot,
    Func<string?> LogFolder,
    Func<string?> SaveFolder,
    Func<string?> ContinueGamePath,
    Func<string?> ExecutablePath,
    Func<IReadOnlyList<PlaysetInfo>> Playsets,
    Func<string?, string, bool> StartGame,
    Func<bool> GameRunning,
    Func<bool> GameRunningInDebug,
    Action<string, int> OpenFileAtLine,
    string GameDirectory,
    AppSessionState State,
    Func<string, string?, Task<IReadOnlyList<string>>> ImportRecipeFromAi);
