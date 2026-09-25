namespace ParadoxModTool.SteamIntegration;

/// <summary>Deutsche Texte für die <c>EResult</c>-Codes, die beim Workshop-Upload vorkommen.</summary>
public static class SteamResultText
{
    public static string Describe(int result) => result switch
    {
        2 => "Steam meldet einen allgemeinen Fehler (2).",
        3 => "Es besteht keine Verbindung zu Steam (3). Prüfe deine Internetverbindung.",
        8 => "Steam hat eine Angabe abgelehnt (8), zum Beispiel Titel, Beschreibung oder Vorschaubild.",
        9 => "Steam hat den Workshop-Eintrag oder eine Datei nicht gefunden (9).",
        10 => "Steam ist gerade beschäftigt (10). Versuche es später erneut.",
        15 => "Steam verweigert den Zugriff (15). Prüfe, ob dieses Konto den Workshop-Eintrag besitzt und das Spiel besitzt.",
        16 => "Steam antwortet nicht rechtzeitig (16). Versuche es später erneut.",
        25 => "Steam meldet ein Limit (25), etwa bei der Größe oder Anzahl. Prüfe die Größe der Mod.",
        _ => $"Steam meldet Fehler {result}.",
    };
}
