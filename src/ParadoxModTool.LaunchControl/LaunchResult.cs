namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Ergebnis eines Startversuchs über <see cref="GameLauncher.Launch"/>. Wie bei
/// <c>ParadoxModTool.Tools.ScriptFormatResult</c> bewusst als eigener Ergebnistyp statt einer
/// geworfenen Ausnahme, da ein Fehlschlag hier ein normaler, von der UI abzufangender Fall ist
/// (Pfad noch nicht eingestellt, Datei nicht mehr vorhanden, Spiel lässt sich aus sonstigen
/// Gründen nicht starten) und keine Ausnahmesituation.
/// </summary>
public sealed class LaunchResult
{
    public required bool Success { get; init; }

    /// <summary>Verständliche Fehlermeldung, sofern <see cref="Success"/> <see langword="false"/> ist - z. B. zur Anzeige in einer MessageBox.</summary>
    public string? ErrorMessage { get; init; }

    public static LaunchResult Ok() => new() { Success = true };

    public static LaunchResult Failure(string message) => new() { Success = false, ErrorMessage = message };
}
