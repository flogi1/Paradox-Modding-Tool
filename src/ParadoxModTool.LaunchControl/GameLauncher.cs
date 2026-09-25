using System.ComponentModel;
using System.Diagnostics;

namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Startet die Spiel-.exe direkt aus dem Programm heraus (Konzept, Abschnitt 8: "Es wäre super,
/// wenn man das Spiel direkt aus dem Programm starten könnte ... das Spiel direkt im Debug Mode
/// zu starten ... die Option das Spiel weiter mit Mods zu starten, wenn es bei Loading Resources
/// crasht"). Die konkreten Kommandozeilen-Argumente für diese drei Fälle liefert bereits das
/// jeweilige Spielprofil als <c>ParadoxModTool.GameProfiles.GameProfile.LaunchOptions</c> (aus der
/// EU5-Wiki-Recherche, siehe <c>profiles/eu5.json</c>: "Normal" / "-debug_mode" /
/// "--ignore-disable-mods-on-crash") - dieses Projekt bleibt daher bewusst UNABHÄNGIG von
/// <c>ParadoxModTool.GameProfiles</c> (wie schon <c>ParadoxModTool.UndoRedo</c>/<c>.Session</c>)
/// und nimmt Pfad und Argumente als einfache Zeichenketten entgegen; das Zuordnen der gewählten
/// Startoption zum Spielprofil übernimmt das <c>App</c>-Modul.
/// </summary>
public static class GameLauncher
{
    /// <summary>
    /// Baut die <see cref="ProcessStartInfo"/> für den Spielstart. Als eigene, reine (keine
    /// Seiteneffekte) Methode ausgelagert, damit sie ohne tatsächlichen Prozessstart getestet
    /// werden kann. <see cref="ProcessStartInfo.UseShellExecute"/> ist bewusst
    /// <see langword="true"/> (wie bei einem Doppelklick im Explorer) statt des .NET-Standards
    /// <see langword="false"/> - Paradox-Spiele werden i. d. R. über den Steam-Client verifiziert/
    /// gestartet, und <see langword="true"/> vermeidet unnötige Probleme mit Arbeitsverzeichnis-
    /// bzw. DLL-Auflösung, die bei direktem Prozessstart ohne Shell gelegentlich auftreten.
    /// </summary>
    public static ProcessStartInfo BuildStartInfo(string executablePath, string arguments) => new()
    {
        FileName = executablePath,
        Arguments = arguments,
        UseShellExecute = true,
        WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executablePath)) ?? string.Empty,
    };

    /// <summary>
    /// Startet das Spiel. Prüft <paramref name="executablePath"/> selbst auf Leere/Existenz statt
    /// das direkt dem Betriebssystem zu überlassen, damit eine fehlende Konfiguration und eine
    /// nicht mehr vorhandene Datei unterschiedliche, verständliche Meldungen liefern.
    /// <paramref name="starter"/> erlaubt es Tests, den eigentlichen Prozessstart durch ein Fake zu
    /// ersetzen (Standard: <see cref="Process.Start(ProcessStartInfo)"/>) - ein echter Spielstart
    /// lässt sich in dieser Umgebung ohnehin nicht automatisiert testen.
    /// </summary>
    public static LaunchResult Launch(string? executablePath, string arguments, Func<ProcessStartInfo, Process?>? starter = null)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return LaunchResult.Failure("Es ist noch kein Pfad zur Spiel-.exe eingestellt.");
        }

        if (!File.Exists(executablePath))
        {
            return LaunchResult.Failure($"Die Datei \"{executablePath}\" wurde nicht gefunden.");
        }

        try
        {
            var startInfo = BuildStartInfo(executablePath, arguments);
            (starter ?? Process.Start)(startInfo);
            return LaunchResult.Ok();
        }
        catch (Win32Exception ex)
        {
            return LaunchResult.Failure($"Das Spiel konnte nicht gestartet werden: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return LaunchResult.Failure($"Das Spiel konnte nicht gestartet werden: {ex.Message}");
        }
    }
}
