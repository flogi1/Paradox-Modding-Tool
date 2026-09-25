namespace ParadoxModTool.Mods;

/// <summary>
/// Abhängigkeits-Mods für den GUI-Index (Spec "Einstellungen": je Mod einstellbar, vorbelegt aus den Addon-Beziehungen). Ihre Typen und
/// Templates stehen im Index zwischen Vanilla und der Mod selbst.
/// </summary>
public static class ModGuiDependencies
{
    /// <summary>
    /// Vorbelegung: bei einem Submod der Ordner seiner Hauptmod, bei einem Kompatch die Ordner seiner Ziele aus dem Register und angegebene
    /// Ordner (Workshop-Ziele ohne Register-Eintrag lassen sich hier nicht auflösen und bleiben außen vor).
    /// </summary>
    public static IReadOnlyList<string> Defaults(ModRegistry registry, ModInfo mod)
    {
        var result = new List<string>();
        if (mod.Role == ModRole.Submod
            && ModProjects.Root(registry, mod) is { } main
            && !ReferenceEquals(main, mod)
            && main.Paths.FirstOrDefault() is { } mainRoot)
        {
            result.Add(mainRoot);
        }

        if (mod.IsCompatch)
        {
            foreach (var target in mod.CompatchTargets)
            {
                if (target.ModId is { } id && registry.Get(id)?.Paths.FirstOrDefault() is { } path)
                {
                    result.Add(path);
                }
                else if (target.Folder is { } folder)
                {
                    result.Add(folder);
                }
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Eingestellte Ordner, sonst die Vorbelegung.</summary>
    public static IReadOnlyList<string> For(ModRegistry registry, ModInfo mod) => mod.GuiDependencies ?? Defaults(registry, mod);
}
