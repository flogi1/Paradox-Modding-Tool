namespace ParadoxModTool.Mods;

/// <summary>Ordnet Kompatch-Ziele Mods und Ordnern zu. Liest nur.</summary>
public static class CompatchTargets
{
    /// <summary>Die Workshop-ID, wenn der Ordner <c>…\workshop\content\&lt;app&gt;\&lt;ziffern&gt;</c> ist; sonst <see langword="null"/>.</summary>
    public static string? WorkshopIdOf(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var parts = folder.Replace('/', '\\').TrimEnd('\\').Split('\\');
        if (parts.Length < 4)
        {
            return null;
        }

        var id = parts[^1];
        return id.Length > 0 && id.All(char.IsAsciiDigit)
            && string.Equals(parts[^3], "content", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[^4], "workshop", StringComparison.OrdinalIgnoreCase)
            ? id
            : null;
    }

    public static bool IsWorkshopFolder(string? folder) => WorkshopIdOf(folder) is not null;

    /// <summary>Trifft das Ziel die Mod mit <paramref name="modId"/> oder den Ordner <paramref name="folder"/>?</summary>
    public static bool Matches(CompatchTarget target, string? modId, string? folder)
    {
        if (target.ModId is { Length: > 0 } id)
        {
            return id == modId;
        }

        if (target.WorkshopId is { Length: > 0 } workshop)
        {
            return WorkshopIdOf(folder) == workshop;
        }

        return target.Folder is { Length: > 0 } path && folder is not null
            && ModPaths.TryNormalize(folder, out var normalized)
            && ModPaths.AreEqual(path, normalized);
    }

    /// <summary>Ist <paramref name="compatch"/> ein Kompatch mit einem Ziel, das die Mod bzw. den Ordner trifft? Bei einer Register-Mod zählen alle ihre Ordner.</summary>
    public static bool Covers(ModRegistry registry, ModInfo compatch, string? modId, string? folder)
    {
        if (!compatch.IsCompatch)
        {
            return false;
        }

        var folders = new List<string?> { folder };
        if (modId is not null && registry.Get(modId) is { } mod)
        {
            folders.AddRange(mod.Paths);
        }

        return compatch.CompatchTargets.Any(t => folders.Any(f => Matches(t, modId, f)));
    }

    /// <summary>Alle Kompatchs im Register, die die Mod als Ziel haben, nach Name.</summary>
    public static IReadOnlyList<ModInfo> CompatchesFor(ModRegistry registry, ModInfo mod) =>
        registry.Mods
            .Where(m => m.Id != mod.Id && Covers(registry, m, mod.Id, null))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Der vorhandene Ordner des Ziels; <see langword="null"/>, wenn es fehlt. <paramref name="workshopFolder"/> findet Workshop-Ordner zur ID.</summary>
    public static string? Resolve(ModRegistry registry, CompatchTarget target, Func<string, string?>? workshopFolder)
    {
        if (target.ModId is { Length: > 0 } id)
        {
            return registry.Get(id) is { } mod ? BranchTransfer.WorkingFolder(mod) : null;
        }

        if (target.WorkshopId is { Length: > 0 } workshop)
        {
            return workshopFolder?.Invoke(workshop) is { } found && Directory.Exists(found) ? found : null;
        }

        return target.Folder is { Length: > 0 } folder && Directory.Exists(folder) ? folder : null;
    }

    /// <summary>"Kompatch für A + B" (Namen der Ziele in Listenreihenfolge).</summary>
    public static string Describe(ModInfo compatch) =>
        "Kompatch für " + string.Join(" + ", compatch.CompatchTargets.Select(t => t.DisplayName.Length > 0 ? t.DisplayName : t.ModId ?? t.WorkshopId ?? t.Folder ?? "?"));
}
