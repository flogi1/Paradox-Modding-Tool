using ParadoxModTool.Compatch;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.Mods;
using ParadoxModTool.Session;
using ParadoxModTool.VersionManager;

namespace ParadoxModTool.App;

/// <summary>Voreinstellungen für einen neuen Lauf (aus den Einstellungen).</summary>
/// <remarks>Ab <paramref name="MainAsBase"/>: Standard für neue Arbeitsstände aus dem Einstellungsfenster (jeder Arbeitsstand merkt sich danach eigene Werte).</remarks>
public sealed record CompatchDefaults(
    bool Cautious,
    bool SkipLocked,
    IReadOnlyList<string> HiddenKinds,
    CompatchView View,
    bool CardsFolderPane,
    bool HideComments,
    bool MainAsBase = false,
    bool FillMissing = false,
    bool HideFilled = false,
    bool HideReplaced = false,
    bool HideDecided = false,
    bool UncertainOnly = false,
    bool KindsSideBySide = false);

/// <summary>Optionen des Startdialogs, die "Als Voreinstellung merken" in den Standard für neue Arbeitsstände schreibt.</summary>
public sealed record CompatchStartOptions(bool Cautious, bool SkipLocked, bool MainAsBase, bool FillMissing);

/// <summary>Vorbelegung des Startdialogs: Ordner von A und B und das Ziel (Register-ID), jeweils optional.</summary>
public sealed record CompatchPreset(string? FolderA, string? FolderB, string? TargetModId);

/// <summary>Was die Kompatch-Fenster vom Hauptfenster brauchen (wie <see cref="ModCompatHost"/>).</summary>
public sealed record CompatchHost(
    ModRegistry Registry,
    string MetadataFile,
    string GameDirectory,
    string? VanillaRoot,
    string? SteamAppId,
    GameProfile? Profile,
    string? ModRoot,
    Func<StorageSettings> Storage,
    CompatchDefaults Defaults,
    Action<IReadOnlyList<string>> RememberHiddenKinds,
    Action RegistryChanged,
    Action<string> OpenInEditor,
    CompatchStartMemory? LastStart,
    Action<CompatchStartMemory> RememberStart,
    Action<CompatchView> RememberView,
    Action<bool> RememberCardsFolderPane,
    Action<bool> RememberHideComments,
    Action? OpenSettings = null,
    Action<CompatchStartOptions>? RememberStartOptions = null,
    Action<string>? OpenPatchRecipes = null,
    Action<string>? PatchFilesChanged = null)
{
    /// <summary>Einmal je Host angelegt, damit die Workshop-Ordner nur einmal gesucht werden.</summary>
    public CompatchPickContext Pick { get; } = new(Registry, MetadataFile, SteamAppId);

    /// <summary>Kodierung PMT-eigener Dateien je Endung aus dem Spielprofil (Ruling 13).</summary>
    public string EncodingFor(string extension) =>
        Profile?.FileTypes.FirstOrDefault(f => string.Equals(f.Extension, extension, StringComparison.OrdinalIgnoreCase))?.DefaultEncoding ?? "utf-8-bom";
}
