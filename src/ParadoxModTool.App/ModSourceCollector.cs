using System.IO;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Mods;
using ParadoxModTool.SteamIntegration;

namespace ParadoxModTool.App;

/// <summary>
/// Gleiche Mod-Auswahl wie im Fehlerprotokoll (Spec "Festlegungen" 1): eigene Mods aus dem Register, installierte Mods aus Mod-Ordner und
/// Workshop und die Einträge eines Playsets. Vom <see cref="ErrorLogService"/> und vom Flag-Tracker gemeinsam genutzt.
/// </summary>
internal sealed class ModSourceCollector
{
    private readonly Func<ModRegistry?> _registry;
    private readonly string _metadataFile;
    private readonly string? _steamAppId;
    private readonly string? _modRootTemplate;
    private readonly Func<string?> _playsetsFile;

    public ModSourceCollector(Func<ModRegistry?> registry, string metadataFile, string? steamAppId, string? modRootTemplate, Func<string?> playsetsFile)
    {
        _registry = registry;
        _metadataFile = metadataFile;
        _steamAppId = steamAppId;
        _modRootTemplate = modRootTemplate;
        _playsetsFile = playsetsFile;
    }

    /// <summary>Ordner der eigenen Mods (im UI-Thread gelesen, das Register ist nicht threadsicher).</summary>
    public List<OwnModFolder> OwnMods() =>
        _registry() is not { } registry
            ? new List<OwnModFolder>()
            : registry.Mods.SelectMany(m => m.Paths.Where(Directory.Exists).Select(p => new OwnModFolder(m.Name, p))).ToList();

    /// <summary>Einträge des Playsets <paramref name="playsetName"/> (im UI-Thread); <see langword="null"/>, wenn kein Playset gewählt ist.</summary>
    public List<PlaysetEntry>? PlaysetEntries(string? playsetName) =>
        playsetName is { } name
            ? PlaysetStore.ListPlaysetMods(_playsetsFile(), name).Select(m => new PlaysetEntry(m.Path, m.IsEnabled)).ToList()
            : null;

    /// <summary>Name des aktiven Playsets (das erste mit <c>IsActive</c>), sonst das erste; <see langword="null"/>, wenn es keins gibt.</summary>
    public string? DefaultPlaysetName() => DefaultPlaysetName(PlaysetStore.ListPlaysets(_playsetsFile()));

    /// <summary>Regel von <see cref="DefaultPlaysetName()"/> für eine schon gelesene Playset-Liste (das erste aktive, sonst das erste).</summary>
    public static string? DefaultPlaysetName(IReadOnlyList<PlaysetInfo> playsets) =>
        playsets.FirstOrDefault(p => p.IsActive)?.Name ?? playsets.Select(p => p.Name).FirstOrDefault();

    /// <summary>Installierte Mods aus dem lokalen Mod-Ordner und dem Workshop, mit Namen aus den Metadaten (im Hintergrund).</summary>
    public List<InstalledModFolder> InstalledMods()
    {
        var result = new List<InstalledModFolder>();
        if (ModRoot() is { } modRoot)
        {
            foreach (var folder in Directory.EnumerateDirectories(modRoot).Where(f => !Path.GetFileName(f).StartsWith('.') && !ParadoxModTool.GfxAudit.GfxArchive.IsArchiveFolder(Path.GetFileName(f))))
            {
                result.Add(new InstalledModFolder(NameOf(folder, CompatOrigin.Folder), folder));
            }
        }

        if (_steamAppId is { Length: > 0 } appId && SteamFolders.Find() is { } steam)
        {
            foreach (var folder in CompatMembers.FindWorkshopFolders(appId, SteamFolders.LibraryFolders(steam)))
            {
                result.Add(new InstalledModFolder(NameOf(folder, CompatOrigin.Workshop), folder));
            }
        }

        return result;
    }

    private string? ModRoot()
    {
        if (string.IsNullOrWhiteSpace(_modRootTemplate))
        {
            return null;
        }

        var path = Environment.ExpandEnvironmentVariables(_modRootTemplate);
        return Directory.Exists(path) ? Path.GetFullPath(path) : null;
    }

    private string NameOf(string folder, CompatOrigin origin)
    {
        try
        {
            return CompatMembers.FromFolder(folder, origin, null, _metadataFile).Name;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return Path.GetFileName(folder);
        }
    }
}
