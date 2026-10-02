using System.IO;
using ParadoxModTool.Templates;

namespace ParadoxModTool.App;

/// <summary>Ergebnis eines Abgleichs der Lokalisierungs-Profile mit den Spieldateien.</summary>
public sealed record LocalizationProfileUpdate(bool Learned, LocalizationProfiles? Profiles, TimeSpan Duration, string Summary);

/// <summary>
/// Hält die gelernten Lokalisierungs-Profile des geöffneten Spiels (Ablage <c>games\&lt;id&gt;\localization\profiles.json</c>) und lernt sie im Hintergrund neu,
/// wenn der Fingerabdruck der Skript- und Loc-Dateien (Pfad, Größe, Änderungszeit) von dem der gespeicherten Profile abweicht, etwa nach einem Spiel-Update.
/// Ohne bekannten Vanilla-Ordner wird nichts gelernt; das Template-Fenster nimmt dann das Rückfallprofil.
/// </summary>
public sealed class LocalizationProfileService
{
    /// <summary>Aus den Dateien dieser Sprache wird gelernt; die Schlüssel sind in allen Sprachen gleich.</summary>
    public const string Language = "english";

    private readonly LocalizationProfileStore _store;
    private readonly Func<IReadOnlyList<string>> _getVanillaRoots;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LocalizationProfiles? _profiles;
    private bool _loaded;
    private string _status = LearningStatusTexts.LocProfilesNotLearned;

    public LocalizationProfileService(string profilesFilePath, Func<IReadOnlyList<string>> getVanillaRoots)
    {
        _store = new LocalizationProfileStore(profilesFilePath);
        _getVanillaRoots = getVanillaRoots;
    }

    /// <summary>Die gelernten Profile; <see langword="null"/>, solange nichts gelernt oder geladen ist.</summary>
    public LocalizationProfiles? Profiles => _profiles;

    /// <summary>Ein Satz für die Statuszeile: Stand der Profile oder der Grund, warum es keine gibt.</summary>
    public string Status => _status;

    /// <summary>Lädt die gespeicherten Profile, vergleicht den Fingerabdruck und lernt im Hintergrund neu, falls nötig (immer bei <paramref name="force"/>).</summary>
    public async Task<LocalizationProfileUpdate> UpdateAsync(bool force, CancellationToken cancellation = default, IProgress<(int Done, int Total)>? progress = null)
    {
        await _gate.WaitAsync(cancellation);
        try
        {
            var roots = _getVanillaRoots();
            if (!_loaded)
            {
                _profiles = await Task.Run(_store.TryLoad, cancellation);
                _loaded = true;
            }

            var canLearn = roots.Count > 0;
            var learn = canLearn && (force || _profiles is null);
            if (canLearn && !learn && _profiles is not null)
            {
                var current = await Task.Run(() => LocalizationProfileLearner.ComputeFingerprint(roots, Language), cancellation);
                learn = current != _profiles.Fingerprint;
            }

            var duration = TimeSpan.Zero;
            var learned = false;
            if (learn)
            {
                _status = LearningStatusTexts.LocProfilesLearning;
                try
                {
                    var result = await Task.Run(() => LocalizationProfileLearner.Learn(roots, Language, DateTime.UtcNow, cancellation, progress), cancellation);
                    _profiles = result.Profiles;
                    duration = result.Duration;
                    learned = true;
                    try
                    {
                        await Task.Run(() => _store.Save(result.Profiles), cancellation);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // Die Profile gelten für diese Sitzung, werden aber nicht gemerkt; der nächste Start lernt neu.
                    }
                }
                catch (OperationCanceledException)
                {
                    _status = Summarize(_profiles, canLearn);
                    throw;
                }
            }

            _status = Summarize(_profiles, canLearn);
            return new LocalizationProfileUpdate(learned, _profiles, duration, _status);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string Summarize(LocalizationProfiles? profiles, bool canLearn)
    {
        if (profiles is null)
        {
            return canLearn
                ? LearningStatusTexts.LocProfilesNotLearned
                : LearningStatusTexts.LocProfilesNoVanilla;
        }

        return LearningStatusTexts.LocProfilesLearned(profiles.LearnedUtc.ToLocalTime(), profiles.Profiles.Count);
    }
}
