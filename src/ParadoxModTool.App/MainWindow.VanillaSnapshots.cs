using System.IO;
using System.Threading.Tasks;
using ParadoxModTool.VanillaDiff;
using ParadoxModTool.VersionManager;

namespace ParadoxModTool.App;

/// <summary>
/// Alte Vanilla-Fassungen automatisch einlesen (Spec 2026-09-30-vanilla-update-merge, Abschnitt 7.1): beim Start, beim Öffnen des Compare
/// und Update Tools und auf Knopfdruck. Läuft im Hintergrund und liest nur Felder, keine Oberfläche.
/// </summary>
public partial class MainWindow
{
    /// <summary>Legt die von lokalen Mods überschriebenen Vanilla-Dateien für die installierte Spielversion in der Historie ab.</summary>
    private SnapshotRecordResult RecordVanillaSnapshots(IProgress<(int Done, int Total)>? progress)
    {
        var profile = _activeProfile;
        var contentRoot = ComputeGameContentRoot(_gameExecutablePath);
        if (profile is null || contentRoot is null)
        {
            return new SnapshotRecordResult(0, 0, 0);
        }

        var storage = _storageSettings;
        var mods = VanillaSnapshotRecorder.LocalModFolders(ResolveDefaultModRoot(profile.DefaultModRootTemplate));
        return VanillaSnapshotRecorder.Record(
            mods,
            contentRoot,
            profile,
            GameVersionReader.TryReadVersion(contentRoot),
            mod => VanillaHistoryStore.FromHistoryRoot(StorageLocations.ResolveVanillaHistoryDirectory(mod, storage)),
            progress);
    }

    /// <summary>Beim Start: im Hintergrund einlesen; Fehler blockieren nie.</summary>
    private void RecordVanillaSnapshotsInBackground() =>
        _ = Task.Run(() =>
        {
            try
            {
                RecordVanillaSnapshots(null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                System.Diagnostics.Debug.WriteLine("Vanilla-Historie: " + ex.Message);
            }
        });
}
