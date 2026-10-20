using ParadoxModTool.GuiDesign.Imaging;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>
/// Findet Texturpfade wie <c>gfx/interface/x.dds</c> in Mod, Abhängigkeits-Mods, DLCs und Vanilla und merkt sich ihren Kopf (Größe, Format). Suche,
/// Merkliste und Köpfe siehe <see cref="TextureLocator"/>; hier ohne <c>.tga</c>→<c>.dds</c>-Ersatz.
/// </summary>
public sealed class GuiTextureLocator : TextureLocator
{
    private static readonly string[] Contexts = { "in_game", "main_menu", "loading_screen" };

    public GuiTextureLocator(IEnumerable<string> contextRoots)
        : base(contextRoots.ToList(), tgaFallsBackToDds: false)
    {
    }

    /// <summary>
    /// Suchreihenfolge: aktuelle Mod, Abhängigkeits-Mods, jedes <c>game/dlc/&lt;name&gt;</c>, dann game, jomini, clausewitz; darin je in_game, main_menu,
    /// loading_screen.
    /// </summary>
    public static IReadOnlyList<string> ContextRootsFor(string installDirectory, IEnumerable<string> dependencyMods, string? modRoot) =>
        ContextRootsFor(installDirectory, dependencyMods, modRoot, Contexts);

    /// <summary>
    /// Wie oben mit den Kontextordnern des Spiels; ohne Kontextordner (flaches Layout, Crusader Kings III) sind die Wurzeln selbst die Suchordner.
    /// </summary>
    public static IReadOnlyList<string> ContextRootsFor(string installDirectory, IEnumerable<string> dependencyMods, string? modRoot, IReadOnlyList<string> contexts)
    {
        var bases = new List<string>();
        if (!string.IsNullOrEmpty(modRoot))
        {
            bases.Add(modRoot);
        }

        bases.AddRange(dependencyMods);

        // Annahme: Ein DLC schlägt game, und ein späteres DLC (nach Namen) schlägt ein früheres, wie in JominiLocalisation. Es zählt nur bei den
        // wenigen Victoria-3-Pfaden, die in game und in einem DLC (oder in zwei DLCs) liegen.
        bases.AddRange(DlcFolders(installDirectory));
        bases.AddRange(new[] { "game", "jomini", "clausewitz" }.Select(folder => Path.Combine(installDirectory, folder)));
        return (contexts.Count == 0 ? bases : bases.SelectMany(b => contexts.Select(c => Path.Combine(b, c)))).Where(Directory.Exists).ToList();
    }

    /// <summary>Die Ordner <c>game/dlc/&lt;name&gt;</c> nach Namen absteigend (das späteste DLC zuerst); leer ohne <c>dlc</c>-Ordner.</summary>
    private static IEnumerable<string> DlcFolders(string installDirectory)
    {
        var dlc = Path.Combine(installDirectory, "game", "dlc");
        if (!Directory.Exists(dlc))
        {
            return Array.Empty<string>();
        }

        try
        {
            var directories = Directory.GetDirectories(dlc);
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
            Array.Reverse(directories);
            return directories;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }
}
