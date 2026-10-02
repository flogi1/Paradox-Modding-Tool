namespace ParadoxModTool.Mods;

/// <summary>Wie zwei Mods desselben Registers zueinander stehen (Grundlage für die Kompatibilitätsprüfung, Teilprojekt B).</summary>
public enum ModRelationKind
{
    /// <summary>Verschiedene Projekte.</summary>
    Unrelated,

    /// <summary>Hauptmod und Dev-Branch oder zwei Dev-Branches desselben Projekts: sie ersetzen sich und laufen nicht zusammen.</summary>
    Alternative,

    /// <summary>Eine Submod und ein anderes Mitglied desselben Projekts: gewollte Abhängigkeit.</summary>
    Addon,
}

/// <summary>Projekte aus Hauptmod, Dev-Branches und Submods (siehe <see cref="ModInfo.ParentModId"/>). Liest nur.</summary>
public static class ModProjects
{
    /// <summary>Der Hauptmod des Projekts; bei einer Mod ohne (gültigen) Elternteil sie selbst.</summary>
    public static ModInfo Root(ModRegistry registry, ModInfo mod) =>
        mod.ParentModId is { } parentId && registry.Get(parentId) is { } parent ? parent : mod;

    /// <summary>Hauptmod zuerst, danach seine Kinder nach Name. Eine Mod ohne Projekt liefert nur sich selbst.</summary>
    public static IReadOnlyList<ModInfo> Members(ModRegistry registry, ModInfo mod)
    {
        var root = Root(registry, mod);
        var members = registry.Mods
            .Where(m => m.ParentModId == root.Id)
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        members.Insert(0, root);
        return members;
    }

    /// <summary>
    /// Alle Mods für Listen: Hauptmods (auch eigenständige) nach Name, direkt darunter ihre Dev-Branches, dann ihre Submods, jeweils nach Name, mit Tiefe 1.
    /// Ein Kind, dessen Hauptmod fehlt, erscheint als Hauptmod.
    /// </summary>
    public static IReadOnlyList<(ModInfo Mod, int Depth)> Ordered(ModRegistry registry)
    {
        var ids = registry.Mods.Select(m => m.Id).ToHashSet();
        var result = new List<(ModInfo, int)>();
        foreach (var root in registry.Mods.Where(m => m.ParentModId is null || !ids.Contains(m.ParentModId)).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
        {
            result.Add((root, 0));
            var children = registry.Mods
                .Where(m => m.ParentModId == root.Id)
                .OrderBy(m => m.Role == ModRole.Submod ? 1 : 0)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase);
            result.AddRange(children.Select(c => (c, 1)));
        }

        return result;
    }

    public static bool IsProject(ModRegistry registry, ModInfo mod) => Members(registry, mod).Count >= 2;

    public static ModRelationKind Relation(ModRegistry registry, ModInfo a, ModInfo b)
    {
        if (a.Id == b.Id)
        {
            throw new ArgumentException("Eine Mod steht in keiner Beziehung zu sich selbst.");
        }

        if (CompatchTargets.Covers(registry, a, b.Id, null) || CompatchTargets.Covers(registry, b, a.Id, null))
        {
            return ModRelationKind.Addon;
        }

        if (Root(registry, a).Id != Root(registry, b).Id)
        {
            return ModRelationKind.Unrelated;
        }

        return a.Role == ModRole.Submod || b.Role == ModRole.Submod ? ModRelationKind.Addon : ModRelationKind.Alternative;
    }

    /// <summary>Kurzer Text zur Rolle für Listen und Statusleiste; <see langword="null"/>, wenn die Mod zu keinem Projekt gehört.</summary>
    public static string? RoleText(ModRegistry registry, ModInfo mod)
    {
        if (mod.IsCompatch)
        {
            return CompatchTargets.Describe(mod);
        }

        if (!IsProject(registry, mod))
        {
            return null;
        }

        var root = Root(registry, mod);
        return mod.Role switch
        {
            ModRole.DevBranch => ModsTexts.RoleDevBranch(root.Name),
            ModRole.Submod => ModsTexts.RoleSubmod(root.Name),
            _ => ModsTexts.RoleMain(Members(registry, mod).Count),
        };
    }
}
