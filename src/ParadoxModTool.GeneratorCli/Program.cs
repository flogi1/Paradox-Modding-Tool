using ParadoxModTool.Generators.Ai;
using ParadoxModTool.Localization;

// Meldungen auf Englisch aus dem Programmordner (locales liegt neben ParadoxModTool.exe); ohne Ordner gelten die eingebauten Texte.
var locales = Path.Combine(AppContext.BaseDirectory, "locales");
if (Directory.Exists(locales))
{
    UiText.Load(locales, "en");
}

var result = GeneratorCli.Run(args);
foreach (var line in result.Lines)
{
    Console.WriteLine(line);
}

return result.ExitCode;
