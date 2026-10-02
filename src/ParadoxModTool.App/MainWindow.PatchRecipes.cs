using System.IO;
using System.Windows;
using ParadoxModTool.Editor;
using ParadoxModTool.Patches;
using ParadoxModTool.UndoRedo;
using ParadoxModTool.VersionManager;

namespace ParadoxModTool.App;

/// <summary>Fenster "Patch-Rezepte" (Spec 2026-09-30-patch-recipes): Öffnen und Anwenden eines Plans als ein Rückgängig-Schritt.</summary>
public partial class MainWindow
{
    private PatchRecipesWindow? _patchRecipesWindow;

    private void OpenPatchRecipes_Click(object sender, RoutedEventArgs e) => OpenPatchRecipes(null);

    /// <summary>Öffnet das Fenster (nur einmal); mit <paramref name="modRoot"/> ist diese Mod gewählt.</summary>
    internal void OpenPatchRecipes(string? modRoot)
    {
        if (_patchRecipesWindow is null)
        {
            _patchRecipesWindow = new PatchRecipesWindow(new PatchWindowHost(
                () => _mods?.ActiveMod?.Paths.FirstOrDefault(Directory.Exists),
                () => ComputeGameContentRoot(_gameExecutablePath),
                () => GameContext.GameId,
                () => _storageSettings,
                () => _activeHighlighting,
                ApplyPatchPlan,
                OpenPatchFileAtLine,
                OpenPatchFileInUpdateTool,
                PreparePatchOutdatedFiles,
                () => _activeProfile)) { Owner = this };
            _patchRecipesWindow.Closed += (_, _) => _patchRecipesWindow = null;
        }

        if (modRoot is not null)
        {
            _patchRecipesWindow.SelectMod(modRoot);
        }

        if (_patchRecipesWindow.IsVisible)
        {
            _patchRecipesWindow.Activate();
        }
        else
        {
            _patchRecipesWindow.Show();
        }
    }

    /// <summary>Wächter "Öffnen": die Datei im Editor an der Zeile, Hauptfenster nach vorn.</summary>
    private void OpenPatchFileAtLine(string path, int line)
    {
        JumpToBookmarkInFile(path, line);
        Activate();
    }

    /// <summary>Wächter "Im Update-Tool öffnen": das Compare und Update Tool mit dieser Datei im Update-Reiter.</summary>
    private void OpenPatchFileInUpdateTool(string modRoot, string relativePath)
    {
        OpenVanillaDiffWindow();
        _vanillaDiffWindow?.ShowUpdateFile(modRoot, relativePath);
    }

    /// <summary>
    /// Wächter "veraltet": die Mod-Kopien, die "Merge nach Spiel-Update" anbieten würde (dieselbe Entscheidung wie dort, mit dem Versionsbuch der Mod).
    /// Läuft auf dem UI-Thread und liest dort Profil, Spielordner, Version und Pfade; die zurückgegebene Funktion macht nur noch Dateiarbeit und läuft
    /// im Hintergrund. Fehler werden zur Warnung, der Scan läuft weiter. Ohne Profil, Spielordner oder Spielversion ist die Menge leer.
    /// </summary>
    private Func<OutdatedLookup> PreparePatchOutdatedFiles(string modRoot)
    {
        static OutdatedLookup Failed(Exception ex) => new(new HashSet<string>(), ParadoxModTool.Patches.PatchTexts.WatchOutdatedFailed(ex.Message));

        try
        {
            var profile = _activeProfile;
            var gameRoot = ComputeGameContentRoot(_gameExecutablePath);
            if (profile is null || gameRoot is null || ParadoxModTool.VanillaDiff.GameVersionReader.TryReadVersion(gameRoot) is not { } version)
            {
                return () => new OutdatedLookup(new HashSet<string>(), null);
            }

            var historyDirectory = StorageLocations.ResolveVanillaHistoryDirectory(modRoot, _storageSettings);
            var ledgerDirectory = VanillaUpdateModDirectory(modRoot);
            var supported = ParadoxModTool.Mods.ModMetadata.Read(modRoot, profile.MetadataFile)?.SupportedGameVersion;
            return () =>
            {
                try
                {
                    var scan = ParadoxModTool.Compatch.VanillaUpdateRun.Scan(
                        ParadoxModTool.VanillaDiff.VanillaFileTracker.FindOverriddenFiles(gameRoot, modRoot, profile),
                        Array.Empty<string>(),
                        gameRoot,
                        ParadoxModTool.VanillaDiff.VanillaHistoryStore.FromHistoryRoot(historyDirectory),
                        version,
                        ParadoxModTool.Compatch.VanillaVersionLedger.Load(ledgerDirectory),
                        supported,
                        null);
                    return new OutdatedLookup(scan.Changed.Select(f => f.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase), null);
                }
                catch (Exception ex)
                {
                    return Failed(ex);
                }
            };
        }
        catch (Exception ex)
        {
            var failed = Failed(ex);
            return () => failed;
        }
    }

    /// <summary>
    /// Schreibt einen Plan wie <see cref="ApplyModEdits"/>: nicht bei geänderten Dateien seit der Vorschau oder ungespeicherten Tabs,
    /// vorher eine Sicherung der Mod, bei Schreibfehlern alles zurück, danach ein Rückgängig-Schritt und neu geladene Tabs.
    /// </summary>
    private FixOutcome ApplyPatchPlan(PatchContext context, PatchPlan plan)
    {
        if (PatchPlanWriter.StaleFiles(context, plan).Count > 0)
        {
            return new FixOutcome(false, PatchRecipeTexts.Stale);
        }

        var files = plan.Files
            .Where(f => f.Status is PlannedFileStatus.New or PlannedFileStatus.Changed)
            .Select(f => Path.Combine(context.ModRoot, f.RelativePath.Replace('/', Path.DirectorySeparatorChar)))
            .ToList();
        var unsaved = AllPanes.SelectMany(p => p.Documents)
            .FirstOrDefault(d => d.IsModified && files.Any(f => EditorPane.PathsEqual(d.FilePath, f)));
        if (unsaved?.FilePath is { } unsavedPath)
        {
            return new FixOutcome(false, PatchRecipeTexts.Unsaved(Path.GetFileName(unsavedPath)));
        }

        try
        {
            new ModVersionManager(context.ModRoot, StorageLocations.ResolveSnapshotsDirectory(context.ModRoot, _storageSettings))
                .CreateSnapshot(PatchRecipeTexts.SnapshotNote(plan.RecipeName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return new FixOutcome(false, PatchRecipeTexts.SnapshotFailed(ex.Message));
        }

        var action = (CompositeUndoableAction)PatchPlanWriter.CreateAction(context, plan);
        var written = new List<IUndoableAction>();
        try
        {
            foreach (var write in action.Actions)
            {
                write.Do();
                written.Add(write);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            for (var i = written.Count - 1; i >= 0; i--)
            {
                try
                {
                    written[i].Undo();
                }
                catch (Exception undoError) when (undoError is IOException or UnauthorizedAccessException)
                {
                    // Weiter mit den übrigen; die Sicherung von eben enthält den alten Stand.
                }
            }

            ReloadOpenTabsAffectedBy(new CompositeUndoableAction(string.Empty, written));
            return new FixOutcome(false, PatchRecipeTexts.WriteFailed(ex.Message));
        }

        _history.Record(action);
        ReloadOpenTabsAffectedBy(action);
        try
        {
            PatchPlanWriter.RecordSnapshots(context, plan);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ohne Vanilla-Snapshot kennt der Update-Vergleich die Basis der neuen Kopie nicht; das Schreiben selbst ist erledigt.
        }

        return new FixOutcome(true, PatchRecipeTexts.Applied(files.Count));
    }
}
