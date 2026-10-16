using ParadoxModTool.Localization;
using ParadoxModTool.Patches.Ai;

// Letzte Absicherung: auch ein Fehler außerhalb der Befehle (z. B. beim Laden der Texte oder bei der Ausgabe) gibt eine Zeile und Rückgabe 1 statt eines Absturzes.
try
{
    // Meldungen auf Englisch aus dem Programmordner (locales liegt neben ParadoxModTool.exe); ohne Ordner gelten die eingebauten Texte.
    var locales = Path.Combine(AppContext.BaseDirectory, "locales");
    if (Directory.Exists(locales))
    {
        UiText.Load(locales, "en");
    }

    var result = PatchCli.Run(args);
    foreach (var line in result.Lines)
    {
        Console.WriteLine(line);
    }

    return result.ExitCode;
}
catch (Exception ex)
{
    Console.WriteLine("error: " + ex.Message.ReplaceLineEndings(" "));
    return 1;
}
