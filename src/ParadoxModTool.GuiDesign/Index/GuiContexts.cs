namespace ParadoxModTool.GuiDesign.Index;

/// <summary>
/// Kontextordner von EU V: .gui-Dateien liegen in Spiel und Mods unter <c>in_game/gui</c>, <c>main_menu/gui</c> und <c>loading_screen/gui</c>.
/// Eine Mod-Datei in <c>gui/</c> lädt das Spiel nicht (gemessen 2026-09-24 an Vanilla und allen Mods dieses Rechners, Plan-Ruling 2).
/// </summary>
public static class GuiContexts
{
    public const string Default = "in_game";

    public static IReadOnlyList<string> All { get; } = new[] { "in_game", "main_menu", "loading_screen" };

    /// <summary>Kontext eines relativen Pfads wie <c>in_game/gui/x.gui</c>; null, wenn der Pfad nicht in einem Kontextordner beginnt.</summary>
    public static string? Of(string relativePath)
    {
        var first = relativePath.Replace('\\', '/').Split('/')[0];
        return All.FirstOrDefault(context => string.Equals(context, first, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Ordner für .gui-Dateien der Mod in diesem Kontext, z.B. <c>&lt;Mod&gt;\in_game\gui</c>.</summary>
    public static string GuiFolder(string modRoot, string context) => Path.Combine(modRoot, context, "gui");

    /// <summary>Liegt die Datei im Installationsordner des Spiels (<c>game</c>, <c>jomini</c> oder <c>clausewitz</c>)?</summary>
    public static bool IsVanillaPath(string installDirectory, string path)
    {
        var full = Path.GetFullPath(path);
        return new[] { "game", "jomini", "clausewitz" }.Any(folder =>
            full.StartsWith(Path.GetFullPath(Path.Combine(installDirectory, folder)).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }
}
