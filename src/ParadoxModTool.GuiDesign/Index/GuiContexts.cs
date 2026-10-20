namespace ParadoxModTool.GuiDesign.Index;

/// <summary>
/// Kontextordner von EU V: .gui-Dateien liegen in Spiel und Mods unter <c>in_game/gui</c>, <c>main_menu/gui</c> und <c>loading_screen/gui</c>.
/// Eine Mod-Datei in <c>gui/</c> lädt das Spiel nicht (gemessen 2026-09-24 an Vanilla und allen Mods dieses Rechners, Plan-Ruling 2).
/// <see cref="All"/>, <see cref="Default"/> und <see cref="Of(string)"/> meinen EU V; spielabhängig rechnen die Überladungen mit <see cref="GuiLayout"/>.
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

    /// <summary>
    /// Kontext eines relativen Pfads im Layout des Spiels: im flachen Layout (CK III) immer "", sonst der Kontextordner am Anfang des Pfads oder null.
    /// </summary>
    public static string? Of(string relativePath, GuiLayout layout)
    {
        if (layout.IsFlat)
        {
            return string.Empty;
        }

        var first = relativePath.Replace('\\', '/').Split('/')[0];
        return layout.Contexts.FirstOrDefault(context => string.Equals(context, first, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Kontext für neue Dateien: der erste des Layouts, im flachen Layout "".</summary>
    public static string DefaultOf(GuiLayout layout) => layout.IsFlat ? string.Empty : layout.Contexts[0];

    /// <summary>Ordner für .gui-Dateien der Mod in diesem Kontext, z.B. <c>&lt;Mod&gt;\in_game\gui</c>; mit Kontext "" (flach) <c>&lt;Mod&gt;\gui</c>.</summary>
    public static string GuiFolder(string modRoot, string context) => Path.Combine(modRoot, context, "gui");

    /// <summary>Liegt die Datei im Installationsordner des Spiels (<c>game</c>, <c>jomini</c> oder <c>clausewitz</c>)?</summary>
    public static bool IsVanillaPath(string installDirectory, string path) => IsVanillaPath(installDirectory, path, GuiLayout.Default);

    /// <summary>
    /// Klassisch (HOI IV, EU IV): alles unter dem Installationsordner ist Vanilla (auch <c>interface/</c> direkt darin und DLC-Ordner); Jomini wie
    /// bisher <c>game</c>, <c>jomini</c> und <c>clausewitz</c>.
    /// </summary>
    public static bool IsVanillaPath(string installDirectory, string path, GuiLayout layout)
    {
        if (layout.IsClassic)
        {
            return Path.GetFullPath(path).StartsWith(
                Path.GetFullPath(installDirectory).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        var full = Path.GetFullPath(path);
        return new[] { "game", "jomini", "clausewitz" }.Any(folder =>
            full.StartsWith(Path.GetFullPath(Path.Combine(installDirectory, folder)).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }
}
