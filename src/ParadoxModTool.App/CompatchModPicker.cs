using System.IO;
using System.Windows;
using ParadoxModTool.Compatch;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Mods;
using ParadoxModTool.SteamIntegration;

namespace ParadoxModTool.App;

/// <summary>Register, Metadatenformat und Steam-App des Spiels. Die Workshop-Ordner werden einmal je Kontext gesucht (Steam-Bibliotheken durchsuchen ist teuer).</summary>
public sealed class CompatchPickContext
{
    private readonly Lazy<IReadOnlyList<string>> _workshopFolders;

    public CompatchPickContext(ModRegistry registry, string metadataFile, string? steamAppId)
    {
        Registry = registry;
        MetadataFile = metadataFile;
        SteamAppId = steamAppId;
        _workshopFolders = new Lazy<IReadOnlyList<string>>(() =>
            steamAppId is { } appId && SteamFolders.Find() is { } steam
                ? CompatMembers.FindWorkshopFolders(appId, SteamFolders.LibraryFolders(steam))
                : Array.Empty<string>());
    }

    public ModRegistry Registry { get; }

    public string MetadataFile { get; }

    public string? SteamAppId { get; }

    public IReadOnlyList<string> WorkshopFolders => _workshopFolders.Value;
}

/// <summary>Eine gewählte Mod: Ordner, Anzeigename und, falls bekannt, Register-ID oder Workshop-ID.</summary>
public sealed record PickedMod(string Folder, string Name, string? ModId, string? WorkshopId)
{
    public CompatchTarget ToTarget() => ModId is not null
        ? new CompatchTarget { ModId = ModId, DisplayName = Name }
        : WorkshopId is not null
            ? new CompatchTarget { WorkshopId = WorkshopId, DisplayName = Name }
            : new CompatchTarget { Folder = Folder, DisplayName = Name };
}

/// <summary>Mod wählen aus Register, Workshop oder Ordner (wie im Fenster Mod-Kompatibilität).</summary>
public static class CompatchModPicker
{
    public static PickedMod? FromRegister(Window owner, CompatchPickContext context, Func<ModInfo, bool>? filter = null)
    {
        var mods = ModProjects.Ordered(context.Registry).Select(o => o.Mod)
            .Where(m => BranchTransfer.WorkingFolder(m) is not null && (filter?.Invoke(m) ?? true))
            .ToList();
        var labels = mods.Select(m => ModProjects.RoleText(context.Registry, m) is { } role ? $"{m.Name} ({role})" : m.Name).ToList();
        return PickListDialog.Ask(owner, CompatchTexts.FromRegister, CompatchTexts.FromRegister, labels, multi: false) is [var index]
            ? Describe(context, BranchTransfer.WorkingFolder(mods[index])!)
            : null;
    }

    public static PickedMod? FromWorkshop(Window owner, CompatchPickContext context)
    {
        var folders = WorkshopFolders(context);
        var labels = folders.Select(f => $"{CompatMembers.FromFolder(f, CompatOrigin.Workshop, null, context.MetadataFile).Name}  [{Path.GetFileName(f)}]").ToList();
        return labels.Count > 0 && PickListDialog.Ask(owner, CompatchTexts.FromWorkshop, CompatchTexts.FromWorkshop, labels, multi: false) is [var index]
            ? Describe(context, folders[index])
            : null;
    }

    public static PickedMod? FromFolder(Window owner, CompatchPickContext context)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = CompatchTexts.FromFolder, UseDescriptionForTitle = true };
        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("compatch.modFolder");
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? Describe(context, dialog.SelectedPath) : null;
    }

    /// <summary>Name und Kennungen zu einem Ordner: Register-Mod, sonst Workshop-Ordner, sonst freier Ordner.</summary>
    public static PickedMod Describe(CompatchPickContext context, string folder)
    {
        var normalized = CompatSet.NormalizeFolder(folder);
        if (context.Registry.Resolve(normalized) is { } mod && mod.Paths.Any(p => ModPaths.AreEqual(ModPaths.Normalize(p), normalized)))
        {
            return new PickedMod(normalized, mod.Name, mod.Id, null);
        }

        var workshopId = CompatchTargets.WorkshopIdOf(normalized);
        var origin = workshopId is not null ? CompatOrigin.Workshop : CompatOrigin.Folder;
        return new PickedMod(normalized, CompatMembers.FromFolder(normalized, origin, null, context.MetadataFile).Name, null, workshopId);
    }

    public static string? WorkshopFolder(CompatchPickContext context, string workshopId) =>
        context.WorkshopFolders.FirstOrDefault(f => Path.GetFileName(f) == workshopId);

    private static IReadOnlyList<string> WorkshopFolders(CompatchPickContext context) => context.WorkshopFolders;
}
