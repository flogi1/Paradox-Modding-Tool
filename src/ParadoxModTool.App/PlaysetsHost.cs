using ParadoxModTool.LaunchControl;

namespace ParadoxModTool.App;

/// <summary>Was das Playset-Fenster vom Hauptfenster braucht: die Verwaltung des Spiels (neu gebaut bei jedem Aufruf, damit Einstellungen und Spielwechsel gelten) und Rückrufe.</summary>
/// <param name="Manager">Verwaltung für die eingestellte Playset-Datei; null, wenn keine gefunden wurde.</param>
/// <param name="SelectedPlaysetName">Name des Playsets, das in der Werkzeugleiste gewählt ist.</param>
/// <param name="SelectPlayset">Wählt in der Werkzeugleiste das Playset mit diesem Namen.</param>
/// <param name="CanStart">Ob das Spiel aus dem Fenster gestartet werden darf (nicht bei Launcher-Spielen ohne Lade-Datei).</param>
/// <param name="StartGame">Startet das Spiel mit dem Playset der Id; false, wenn es nicht gestartet wurde (die Meldung hat dann schon das Hauptfenster gezeigt).</param>
/// <param name="EnsureGameExecutable">Stellt vor jedem Schreiben sicher, dass die Spiel-.exe bekannt ist (sonst kann PMT nicht sehen, ob das Spiel läuft); fragt bei Bedarf danach, false = abgebrochen.</param>
/// <param name="Changed">Nach jeder Änderung an den Playsets, damit die Werkzeugleiste neu liest.</param>
public sealed record PlaysetsHost(
    Func<PlaysetManager?> Manager,
    Func<string?> SelectedPlaysetName,
    Action<string> SelectPlayset,
    Func<bool> CanStart,
    Func<string, bool> StartGame,
    Func<bool> EnsureGameExecutable,
    Action Changed);
