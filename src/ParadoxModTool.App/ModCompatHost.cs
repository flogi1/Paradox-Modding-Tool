using ParadoxModTool.GameProfiles;
using ParadoxModTool.Mods;

namespace ParadoxModTool.App;

/// <summary>Was das Fenster "Mod-Kompatibilität" vom Hauptfenster braucht (wie <c>BranchHooks</c>): Register, Ordner des Spiels und Rückrufe.</summary>
public sealed record ModCompatHost(
    ModRegistry Registry,
    string MetadataFile,
    string GameDirectory,
    string? VanillaRoot,
    string? SteamAppId,
    Func<string?> PlaysetsFile,
    GameProfile? Profile,
    Action<string> OpenInEditor,
    Action<string, string, string, string> CompareFiles,
    Action<string, string> OpenCompatch);
