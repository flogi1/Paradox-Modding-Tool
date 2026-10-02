using ParadoxModTool.BugTracking;
using ParadoxModTool.Notes;

namespace ParadoxModTool.ModOverview;

/// <summary>
/// Bugreports für die Übersicht "Meine Mods": ruft die Bug-Quellen aller Projekte aller Spiele ab (je Spiel ein <see cref="BugSyncService"/>, weil
/// jedes Spiel seinen eigenen gh-Pfad hat) und markiert Meldungen als gesehen. Geschrieben wird nur über <see cref="BugReportStore.Update"/>.
/// </summary>
public static class OverviewBugRefresher
{
    /// <summary>
    /// Ruft jedes Projekt (Hauptmod mit mindestens einer aktiven Quelle) ab und sammelt die Fehler je Quelle; eine ausfallende Quelle hält die anderen
    /// nicht auf. Der Fortschritt zählt Projekte.
    /// </summary>
    public static async Task<IReadOnlyList<OverviewBugSourceError>> RefreshAllAsync(
        OverviewSnapshot snapshot,
        Func<string, BugSyncService> serviceForGame,
        IProgress<(int Done, int Total)>? progress,
        CancellationToken ct)
    {
        var projects = snapshot.Mods
            .Where(m => ReferenceEquals(m.ProjectRoot, m) && m.Mod.BugSources.Any(s => s.Enabled))
            .ToList();
        var services = new Dictionary<string, BugSyncService>(StringComparer.Ordinal);
        var errors = new List<OverviewBugSourceError>();
        for (var i = 0; i < projects.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report((i, projects.Count));
            var root = projects[i];
            if (!services.TryGetValue(root.GameId, out var service))
            {
                service = serviceForGame(root.GameId);
                services[root.GameId] = service;
            }

            try
            {
                var summary = await service.RefreshAsync(root.Mod.Id, root.Mod.BugSources, null, ct).ConfigureAwait(false);
                errors.AddRange(summary.Errors.Select(o => new OverviewBugSourceError(root, o.Source.Key, o.Error!)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.AddRange(root.Mod.BugSources.Where(s => s.Enabled).Select(s => new OverviewBugSourceError(root, s.Key, ex.Message)));
            }
        }

        progress?.Report((projects.Count, projects.Count));
        return errors;
    }

    /// <summary>Markiert die Meldungen in einem gesperrten Schritt als gesehen; gibt zurück, wie viele vorher ungesehen waren.</summary>
    public static int MarkSeen(string bugsPath, IEnumerable<string> reportIds)
    {
        var ids = reportIds.ToList();
        var changed = 0;
        BugReportStore.Update(bugsPath, data => changed = ids.Count(id => BugEditing.MarkSeen(data, id)));
        return changed;
    }
}
