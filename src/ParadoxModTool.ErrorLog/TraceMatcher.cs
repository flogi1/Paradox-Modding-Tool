using System.Globalization;

namespace ParadoxModTool.ErrorLog;

public enum TraceMatchKind
{
    /// <summary>Die Spur hat in debug.log noch nicht geschrieben.</summary>
    NoHits,

    /// <summary>Jeder gezählte Treffer gehört zu einem scheiternden Aufruf.</summary>
    Exact,

    /// <summary>Die Stelle lief öfter, als sie scheiterte: Die Scopes sind mögliche Auslöser.</summary>
    Candidates,
}

/// <summary>Ein Scope (mit Root) und wie oft er gezählt wurde; <see cref="Saved"/> vom ersten Treffer.</summary>
public sealed record TraceScopeCount(TraceScope Scope, TraceScope? Root, IReadOnlyList<(string Name, TraceScope Scope)> Saved, int Count);

public sealed record TraceResult(TraceMatchKind Kind, IReadOnlyList<TraceScopeCount> Scopes, int HitCount, int ErrorCount);

/// <summary>
/// Ordnet die Treffer einer Spur den Laufzeitfehlern an ihrer Stelle zu (Spec Scope-Verfolgung, Teil 3). Fehler vor dem ersten Treffer
/// stammen aus der Zeit vor dem Einfügen und zählen nicht. Mit Bedingung schreibt die Spur nur bei scheiternden Aufrufen: eindeutig. Ohne
/// Bedingung zählt je Sekunde nur die Leitmeldung (ein Aufruf erzeugt oft mehrere Meldungen); stimmen die Zahlen in jeder Sekunde mit
/// Fehlern, ist die Zuordnung eindeutig, sonst sind es Kandidaten.
/// </summary>
public static class TraceMatcher
{
    public static TraceResult Match(ActiveTrace trace, IReadOnlyList<ErrorEntry> errorsInRange, IReadOnlyList<TraceHit> hits)
    {
        var own = hits.Where(h => h.Id == trace.Id).ToList();
        if (own.Count == 0)
        {
            return new TraceResult(TraceMatchKind.NoHits, Array.Empty<TraceScopeCount>(), 0, 0);
        }

        var firstHit = own.Min(h => Seconds(h.Time));
        var errors = errorsInRange.Where(e => Seconds(e.Line.Time) >= firstHit).ToList();
        var lead = errors.GroupBy(e => e.Detail ?? string.Empty).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key;
        var leadErrors = lead is null ? new List<ErrorEntry>() : errors.Where(e => (e.Detail ?? string.Empty) == lead).ToList();
        if (leadErrors.Count == 0)
        {
            // Treffer, aber kein Fehler dieses Laufs danach: Die Treffer gehören zu einem anderen Lauf (debug.log gilt nur für den aktuellen).
            return new TraceResult(TraceMatchKind.NoHits, Array.Empty<TraceScopeCount>(), own.Count, 0);
        }

        if (trace.Guarded)
        {
            return new TraceResult(TraceMatchKind.Exact, Group(own), own.Count, leadErrors.Count);
        }

        var errorsPerSecond = leadErrors.GroupBy(e => Seconds(e.Line.Time)).ToDictionary(g => g.Key, g => g.Count());
        var hitsPerSecond = own.GroupBy(h => Seconds(h.Time)).ToDictionary(g => g.Key, g => g.Count());
        var exact = errorsPerSecond.Count > 0 && errorsPerSecond.All(p => hitsPerSecond.GetValueOrDefault(p.Key) == p.Value);
        var counted = own.Where(h => errorsPerSecond.ContainsKey(Seconds(h.Time))).ToList();
        return new TraceResult(exact ? TraceMatchKind.Exact : TraceMatchKind.Candidates, Group(counted), own.Count, leadErrors.Count);
    }

    private static IReadOnlyList<TraceScopeCount> Group(IEnumerable<TraceHit> hits) =>
        hits.GroupBy(h => (h.Scope, h.Root))
            .Select(g => new TraceScopeCount(g.Key.Scope, g.Key.Root, g.First().Saved, g.Count()))
            .OrderByDescending(s => s.Count)
            .ThenBy(s => s.Scope.Name, StringComparer.Ordinal)
            .ToList();

    private static int Seconds(string time) =>
        TimeSpan.TryParseExact(time, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var span) ? (int)span.TotalSeconds : -1;
}
