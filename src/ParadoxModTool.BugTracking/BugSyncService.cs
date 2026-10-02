using System.IO;
using ParadoxModTool.Mods;
using ParadoxModTool.Notes;

namespace ParadoxModTool.BugTracking;

public sealed record BugSourceOutcome(BugSourceEntry Source, BugSyncResult? Result, string? Error);

public sealed record BugRefreshSummary(IReadOnlyList<BugSourceOutcome> Outcomes)
{
    public int Added => Outcomes.Sum(o => o.Result?.Added ?? 0);

    public int Changed => Outcomes.Sum(o => o.Result?.Changed ?? 0);

    public int Removed => Outcomes.Sum(o => o.Result?.Removed ?? 0);

    public IReadOnlyList<BugSourceOutcome> Errors => Outcomes.Where(o => o.Error is not null).ToList();
}

/// <summary>
/// Abruf und Status aller Quellen eines Projekts. Schreibt nur über <see cref="BugReportStore.Update"/> (unter Sperre frisch lesen), damit
/// gleichzeitige Änderungen am Speicher (Bezüge, andere Fenster) erhalten bleiben.
/// </summary>
public sealed class BugSyncService
{
    private readonly Dictionary<string, IBugSource> _sources;
    private readonly string _bugsPath;

    public BugSyncService(IEnumerable<IBugSource> sources, string bugsPath)
    {
        _sources = sources.ToDictionary(source => source.Kind, StringComparer.Ordinal);
        _bugsPath = bugsPath;
    }

    public static BugSource SourceOf(string kind) => kind == BugSourceKinds.Discord ? BugSource.Discord : BugSource.GitHub;

    public static string KindOf(BugSource source) => source switch
    {
        BugSource.GitHub => BugSourceKinds.GitHub,
        BugSource.Discord => BugSourceKinds.Discord,
        _ => string.Empty,
    };

    /// <summary>Ruft alle eingeschalteten Quellen nacheinander ab; ein Fehler betrifft nur seine Quelle. Neue Meldungen gehören <paramref name="projectId"/>.</summary>
    public async Task<BugRefreshSummary> RefreshAsync(string projectId, IReadOnlyList<BugSourceEntry> entries, IProgress<(int Done, int Total)>? progress, CancellationToken cancellation)
    {
        var active = entries.Where(entry => entry.Enabled && _sources.ContainsKey(entry.Kind)).ToList();
        var outcomes = new List<BugSourceOutcome>();
        for (var i = 0; i < active.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            progress?.Report((i, active.Count));
            var entry = active[i];
            var source = _sources[entry.Kind];
            try
            {
                var fetched = await source.FetchAsync(entry, cancellation);
                BugSyncResult? result = null;
                BugReportStore.Update(_bugsPath, data => result = BugSync.Apply(data, source.Source, entry.Key, projectId, fetched));
                outcomes.Add(new BugSourceOutcome(entry, result, null));
            }
            catch (BugSourceException ex)
            {
                outcomes.Add(new BugSourceOutcome(entry, null, ex.Message));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                outcomes.Add(new BugSourceOutcome(entry, null, ex.Message));
            }
        }

        progress?.Report((active.Count, active.Count));
        return new BugRefreshSummary(outcomes);
    }

    /// <summary>Lokale Meldungen: sofort. Externe: erst beim Dienst, lokal nur nach Erfolg. Wirft <see cref="BugSourceException"/> mit lesbarem Grund.</summary>
    public async Task SetStatusAsync(BugReport report, BugStatus status, IReadOnlyList<BugSourceEntry> entries, CancellationToken cancellation)
    {
        if (report.Source != BugSource.Local)
        {
            var (source, entry) = Resolve(report, entries);
            await source.SetStatusAsync(entry, report, status, cancellation);
        }

        BugReportStore.Update(_bugsPath, data => BugEditing.SetStatus(data, report.Id, status));
    }

    public async Task<IReadOnlyList<BugComment>> LoadDiscussionAsync(BugReport report, IReadOnlyList<BugSourceEntry> entries, CancellationToken cancellation)
    {
        if (report.Source == BugSource.Local)
        {
            return Array.Empty<BugComment>();
        }

        var (source, entry) = Resolve(report, entries);
        return await source.LoadDiscussionAsync(entry, report, cancellation);
    }

    private (IBugSource Source, BugSourceEntry Entry) Resolve(BugReport report, IReadOnlyList<BugSourceEntry> entries)
    {
        var kind = KindOf(report.Source);
        var entry = entries.FirstOrDefault(e => e.Kind == kind && e.Key.Equals(report.SourceKey, StringComparison.OrdinalIgnoreCase));
        return entry is not null && _sources.TryGetValue(kind, out var source)
            ? (source, entry)
            : throw new BugSourceException(BugTexts.SourceUnknown);
    }
}
