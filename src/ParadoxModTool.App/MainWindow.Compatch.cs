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
        new CompatchDefaults(_settings.CompatchCautiousWrite, _settings.CompatchSkipLocked, _compatchHiddenKinds ?? DefaultHiddenKinds, CompatchViews.Parse(_settings.CompatchDefaultView), _compatchCardsFolderPane, _compatchHideComments),
        kinds => _compatchHiddenKinds = kinds.ToList(),
        OnModRegistryChanged,
        OpenCompatFile,
        _compatchLastStart,
        memory => _compatchLastStart = memory,
        RememberCompatchView,
        on => _compatchCardsFolderPane = on,
        on => _compatchHideComments = on);

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
        var window = new CompatchWindow(host, state, store) { Owner = this };
        window.Show();
    }

    /// <summary>Einstellungsfenster und Umschalter im Merge-Fenster setzen dieselbe Standardansicht.</summary>
    private void RememberCompatchView(CompatchView view) => _settings.CompatchDefaultView = CompatchViews.ToSetting(view);
}
