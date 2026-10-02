using System.IO;
using System.Windows;
using ParadoxModTool.Compatch;
using ParadoxModTool.Mods;
using ParadoxModTool.Session;

namespace ParadoxModTool.App;

/// <summary>Kompatch-Merge im Hauptfenster: Einstellungen, Menüpunkt, Fenster öffnen.</summary>
public partial class MainWindow
{
    private static readonly IReadOnlyList<string> DefaultHiddenKinds = CompatchFileFilter.DefaultHiddenExtensions.Values.Distinct().ToList();

    private List<string>? _compatchHiddenKinds;
    private CompatchStartMemory? _compatchLastStart;
    private bool _compatchCardsFolderPane;
    private bool _compatchHideComments = true;

    private void LoadCompatchSettings(AppSessionState state)
    {
        _compatchHiddenKinds = state.CompatchHiddenKinds?.ToList();
        _compatchLastStart = state.CompatchLastStart;
        _compatchCardsFolderPane = state.CompatchCardsFolderPane;
        _compatchHideComments = state.CompatchHideComments;
    }

    private void SaveCompatchSettings(AppSessionState state)
    {
        state.CompatchHiddenKinds = _compatchHiddenKinds?.ToList();
        state.CompatchLastStart = _compatchLastStart;
        state.CompatchCardsFolderPane = _compatchCardsFolderPane;
        state.CompatchHideComments = _compatchHideComments;
    }

    private CompatchHost? CompatchHostFor() => _mods is null ? null : new CompatchHost(
        _mods.Registry,
        _mods.MetadataFile,
        GameContext.Repository.GameDirectory(GameContext.GameId),
        ComputeGameContentRoot(_gameExecutablePath),
        GameContext.Profile?.SteamAppId,
        GameContext.Profile ?? _activeProfile,
        ResolveDefaultModRoot(_activeProfile?.DefaultModRootTemplate),
        () => _storageSettings,
        new CompatchDefaults(
            _settings.CompatchCautiousWrite,
            _settings.CompatchSkipLocked,
            _compatchHiddenKinds ?? DefaultHiddenKinds,
            CompatchViews.Parse(_settings.CompatchDefaultView),
            _compatchCardsFolderPane,
            _compatchHideComments,
            _settings.CompatchMainAsBase,
            _settings.CompatchFillMissing,
            _settings.CompatchHideFilled,
            _settings.CompatchHideReplaced,
            _settings.CompatchHideDecided,
            _settings.CompatchUncertainOnly,
            _settings.CompatchKindsSideBySide),
        kinds => _compatchHiddenKinds = kinds.ToList(),
        OnModRegistryChanged,
        OpenCompatFile,
        _compatchLastStart,
        memory => _compatchLastStart = memory,
        RememberCompatchView,
        on => _compatchCardsFolderPane = on,
        on => _compatchHideComments = on,
        () => OpenSettingsAt(ParadoxModTool.Settings.SettingsCategory.Compatch),
        options =>
        {
            _settings.CompatchCautiousWrite = options.Cautious;
            _settings.CompatchSkipLocked = options.SkipLocked;
            _settings.CompatchMainAsBase = options.MainAsBase;
            _settings.CompatchFillMissing = options.FillMissing;
        }) with { OpenPatchRecipes = OpenPatchRecipes, PatchFilesChanged = folder => _patchRecipesWindow?.FilesChangedOutside(folder) };

    private void OpenCompatch_Click(object sender, RoutedEventArgs e) => OpenCompatchStart(new CompatchPreset(null, null, null));

    /// <summary>Startdialog, danach das Merge-Fenster.</summary>
    private void OpenCompatchStart(CompatchPreset preset)
    {
        if (CompatchHostFor() is not { } host)
        {
            return;
        }

        if (CompatchStartWindow.Ask(this, host, preset) is { } started)
        {
            OpenCompatchWindow(host, started.State, started.Store);
        }
    }

    /// <summary>Öffnet den Kompatch-Merge für einen Kompatch aus dem Mods-Fenster.</summary>
    private void OpenCompatchForMod(ModInfo mod)
    {
        if (CompatchHostFor() is not { } host)
        {
            return;
        }

        var folders = mod.CompatchTargets.Select(t => CompatchTargets.Resolve(host.Registry, t, id => CompatchModPicker.WorkshopFolder(host.Pick, id))).ToList();
        OpenCompatchStart(new CompatchPreset(folders.ElementAtOrDefault(0), folders.ElementAtOrDefault(1), mod.Id));
    }

    /// <summary>Das Merge-Fenster (nicht modal).</summary>
    private void OpenCompatchWindow(CompatchHost host, CompatchState state, CompatchStateStore store)
    {
        if (state.Target == PatchTarget.UpdateMod)
        {
            ResumeVanillaUpdate(host, state, store);
            return;
        }

        var window = new CompatchWindow(host, state, store) { Owner = this };
        window.Show();
    }

    /// <summary>Gespeicherten Vanilla-Update-Lauf fortsetzen: Ordner old/ und new/ aus den gespeicherten Versionen neu anlegen (Spec, Abschnitt 4).</summary>
    private void ResumeVanillaUpdate(CompatchHost host, CompatchState state, CompatchStateStore store)
    {
        if (host.VanillaRoot is not { } vanillaRoot || state.UpdateNewVersion is not { } version)
        {
            return;
        }

        var files = state.UpdatePaths
            .Where(p => File.Exists(Path.Combine(vanillaRoot, p.Replace('/', Path.DirectorySeparatorChar))))
            .Select(p => new UpdateFile(
                p,
                Path.Combine(vanillaRoot, p.Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(state.TargetFolder, p.Replace('/', Path.DirectorySeparatorChar)),
                state.UpdateOldVersions.TryGetValue(p, out var old) ? old : null,
                true))
            .ToList();
        var history = ParadoxModTool.VanillaDiff.VanillaHistoryStore.FromHistoryRoot(
            ParadoxModTool.VersionManager.StorageLocations.ResolveVanillaHistoryDirectory(state.TargetFolder, host.Storage()));
        try
        {
            var (input, _) = VanillaUpdateRun.Prepare(
                store.Directory, files, history, state.TargetFolder, state.ModBName, state.ModBId, version, UpdateRules(host), host.Profile?.KeyOperators ?? new List<string>());
            new CompatchWindow(host, state, store, input, () => _vanillaDiffWindow?.ReloadUpdateTab()) { Owner = this }.Show();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show(this, ex.Message, VanillaUpdateTexts.StartTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static ParadoxModTool.ModCompat.OverrideRules UpdateRules(CompatchHost host)
    {
        var gameRules = ParadoxModTool.ModCompat.OverrideRulesStore.Load(Path.Combine(host.GameDirectory, "compat", "rules.json"));
        return host.Profile is { } profile
            ? ParadoxModTool.ModCompat.OverrideRules.FromProfile(profile, gameRules)
            : new ParadoxModTool.ModCompat.OverrideRules(Array.Empty<ParadoxModTool.ModCompat.OverrideRule>(), gameRules);
    }

    /// <summary>Schlüssel der Arbeitsstände einer Mod: ihre Id im Register, sonst "local-" und der Ordnername.</summary>
    private string VanillaUpdateModKey(string modFolder) =>
        _mods?.Registry.Resolve(modFolder)?.Id ?? "local-" + ParadoxModTool.VersionManager.StorageLocations.GetModName(modFolder);

    /// <summary>Ordner der Arbeitsstände einer Mod (dort liegt auch <c>vanilla-versions.json</c>).</summary>
    private string VanillaUpdateModDirectory(string modFolder) =>
        CompatchStateStore.DirectoryFor(GameContext.Repository.GameDirectory(GameContext.GameId), VanillaUpdateModKey(modFolder));

    /// <summary>
    /// "Merge nach Update" aus dem Compare und Update Tool: Lauf vorbereiten und den Kompatch-Merge im Update-Modus öffnen (Spec
    /// 2026-09-30-vanilla-update-merge). Entscheidungen eines gespeicherten Laufs für dieselbe Spielversion bleiben erhalten.
    /// </summary>
    private void OpenVanillaUpdateMerge(VanillaUpdateLaunch launch)
    {
        if (CompatchHostFor() is not { } host)
        {
            return;
        }

        var store = new CompatchStateStore(CompatchStateStore.DirectoryForUpdate(host.GameDirectory, VanillaUpdateModKey(launch.ModFolder)));
        var rules = UpdateRules(host);
        try
        {
            var (input, state) = VanillaUpdateRun.Prepare(
                store.Directory,
                launch.Files,
                launch.Store,
                launch.ModFolder,
                launch.ModName,
                host.Registry.Resolve(launch.ModFolder)?.Id,
                launch.CurrentVersion,
                rules,
                host.Profile?.KeyOperators ?? new List<string>());
            if (store.Load() is { Target: PatchTarget.UpdateMod } previous && !CompatchStateStore.IsOutdatedUpdate(previous, launch.CurrentVersion))
            {
                state.Decisions = previous.Decisions;
                state.Forms = previous.Forms;
                state.Seen = previous.Seen;
                state.BlockTexts = previous.BlockTexts;
                state.HideDecided = previous.HideDecided;
                state.UncertainOnly = previous.UncertainOnly;
                state.Cautious = previous.Cautious;
            }
            else if (File.Exists(Path.Combine(store.Directory, "base.json")))
            {
                // Vergleichsbasis eines Laufs für eine andere Spielversion passt nicht mehr.
                File.Delete(Path.Combine(store.Directory, "base.json"));
            }

            store.Save(state);
            new CompatchWindow(host, state, store, input, launch.AfterWrite) { Owner = this }.Show();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show(this, ex.Message, VanillaUpdateTexts.StartTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Einstellungsfenster und Umschalter im Merge-Fenster setzen dieselbe Standardansicht.</summary>
    private void RememberCompatchView(CompatchView view) => _settings.CompatchDefaultView = CompatchViews.ToSetting(view);
}
