namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Zugriff auf die echten EU-V-Dateien dieses Rechners. Fehlt das Spiel, liefern alle Helfer leere Ergebnisse und die Tests kehren sofort zurück.</summary>
internal static class VanillaGui
{
    public const string InstallDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\Europa Universalis V";

    public static bool Exists => Directory.Exists(InstallDirectory);

    public static IReadOnlyList<string> Files() =>
        Exists
            ? Directory.EnumerateFiles(InstallDirectory, "*.gui", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList()
            : Array.Empty<string>();
}
