using System.IO;
using ParadoxModTool.Scopes;
using ParadoxModTool.ScriptDocs;

namespace ParadoxModTool.App;

/// <summary>Ergebnis eines Abgleichs der Scope-Regeln mit den Spieldateien.</summary>
public sealed record ScopeUpdateResult(bool Learned, ScopeRules? Rules, TimeSpan Duration, string Summary);

/// <summary>
/// Hält die gelernten Scope-Regeln des geöffneten Spiels (Ablage <c>games\&lt;id&gt;\scopes\rules.json</c>) und lernt sie im Hintergrund neu, wenn sich die
/// Grundlage geändert hat: die Script-Datenbank (neuer Import der Doku-Logs) oder die Vanilla-Dateien (Fingerabdruck aus Pfad, Größe und Änderungszeit,
/// etwa nach einem Spiel-Update). Danach bekommt der Editor die neue Scope-Verfolgung.
/// </summary>
public sealed class ScopeService
{
    private readonly ScopeRulesStore _store;
    private readonly Func<IReadOnlyList<string>> _getVanillaRoots;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ScopeRules? _rules;

    public ScopeService(string rulesFilePath, Func<IReadOnlyList<string>> getVanillaRoots)
    {
        _store = new ScopeRulesStore(rulesFilePath);
        _getVanillaRoots = getVanillaRoots;
    }

    /// <summary>
    /// Baut die Scope-Verfolgung aus der Datenbank und den Regeln auf und lernt die Regeln bei Bedarf neu (immer bei <paramref name="force"/>). Aufzurufen im
    /// Kontext der Oberfläche: das Ergebnis wird an den Editor übergeben (<see cref="ParadoxModTool.Editor.ScopeSettings"/>). Ein Abbruch wirft
    /// <see cref="OperationCanceledException"/>; Regeln und Scope-Verfolgung bleiben dann wie vorher.
    /// </summary>
    public async Task<ScopeUpdateResult> UpdateAsync(ScriptDocDatabase database, bool force, CancellationToken cancellation = default, IProgress<(int Done, int Total)>? progress = null)
    {
        await _gate.WaitAsync(cancellation);
        try
        {
            var db = ScopeDb.FromDatabase(database);
            var importId = database.Imports.Select(i => i.Id).DefaultIfEmpty(0).Max();
            var roots = _getVanillaRoots();
            var canLearn = roots.Count > 0 && database.Entries.Count > 0;

            _rules ??= await Task.Run(_store.TryLoad);

            var learn = canLearn && (force || _rules is null || _rules.DocsImportId != importId);
            if (canLearn && !learn && _rules is not null)
            {
                var current = await Task.Run(() => ScopeRuleLearner.ComputeFingerprint(ScopeRuleLearner.EnumerateFiles(roots)));
                learn = current != _rules.Fingerprint;
            }

            var duration = TimeSpan.Zero;
            var learned = false;
            if (learn)
            {
                var result = await Task.Run(() => ScopeRuleLearner.Learn(roots, db, importId, DateTime.UtcNow, cancellation, progress), cancellation);
                _rules = result.Rules;
                duration = result.Duration;
                learned = true;
                try
                {
                    await Task.Run(() => _store.Save(result.Rules));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Die Regeln gelten für diese Sitzung, werden aber nicht gemerkt; der nächste Start lernt neu.
                }
            }

            var summary = Summarize(_rules, canLearn);
            ParadoxModTool.Editor.ScopeSettings.SetEngine(new ScopeEngine(db, _rules), summary);
            return new ScopeUpdateResult(learned, _rules, duration, summary);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string Summarize(ScopeRules? rules, bool canLearn)
    {
        if (rules is null)
        {
            return canLearn
                ? "Scope-Regeln werden noch gelernt."
                : "Scope-Regeln fehlen: Es ist kein Vanilla-Ordner bekannt (Einstellungen › Spielpfad einstellen) oder die Script-Datenbank ist leer.";
        }

        return $"Scope-Regeln: {rules.ConfidentCount:N0} sichere, {rules.UnresolvedCount:N0} offene, {rules.Discriminators.Count:N0} mit Unterscheider; " +
               $"gelernt am {rules.LearnedUtc.ToLocalTime():g} aus {rules.FilesScanned:N0} Dateien.";
    }
}
