using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;

namespace ParadoxModTool.ReferenceTree;

/// <summary>Schlüssel einer Flag-Tracker-Sitzung (Pfad normalisiert, null = kein Mod).</summary>
public sealed record FlagSessionKey(string? ActiveModDirectory, string? PlaysetName, bool IsPlayset);

/// <summary>Schlüssel einer Skript-Prüfungs-Sitzung (Pfad normalisiert, "" = kein Mod).</summary>
public sealed record CheckSessionKey(string ChosenModDirectory, string? PlaysetName);

/// <summary>
/// Ein Register je Hauptfenster (Spiel): gleicher Schlüssel liefert dieselbe Sitzung, damit Baum und
/// Fenster die Indizes im Speicher teilen. Wird nur vom UI-Thread benutzt, daher ohne Sperren.
/// </summary>
/// <remarks>
/// Jeder Nutzer (Fenster, Baum) hält je Art höchstens eine Sitzung: Holt er eine zu einem anderen Schlüssel, gibt er die alte frei.
/// Eine Sitzung ohne Nutzer fällt aus dem Register (ein noch laufender Aufbau endet dann ohne Abnehmer); der nächste Abruf legt eine neue an.
/// </remarks>
public sealed class SharedSessions
{
    private readonly Registry<FlagSessionKey, FlagTrackerSession> _flags = new(() => new FlagTrackerSession());
    private readonly Registry<CheckSessionKey, ScriptCheckSession> _checks = new(() => new ScriptCheckSession());

    /// <summary>Liefert die Flag-Sitzung zum Schlüssel (dieselbe Instanz, solange sie Nutzer hat) und meldet <paramref name="user"/> als Nutzer an.</summary>
    public FlagTrackerSession Flags(FlagSessionKey key, object user) => _flags.Acquire(key, user);

    /// <summary>Liefert die Prüfungs-Sitzung zum Schlüssel (dieselbe Instanz, solange sie Nutzer hat) und meldet <paramref name="user"/> als Nutzer an.</summary>
    public ScriptCheckSession Checks(CheckSessionKey key, object user) => _checks.Acquire(key, user);

    /// <summary>Gibt beide Sitzungen von <paramref name="user"/> frei (Fenster geschlossen, Baum zurückgesetzt).</summary>
    public void Release(object user)
    {
        _flags.Release(user);
        _checks.Release(user);
    }

    /// <summary>Baut einen normalisierten Flag-Schlüssel (leerer Mod-Pfad zählt als kein Mod).</summary>
    public static FlagSessionKey FlagKey(string? activeModDirectory, string? playsetName, bool isPlayset)
        => new(NormalizePath(activeModDirectory), playsetName, isPlayset);

    /// <summary>Baut einen normalisierten Prüfungs-Schlüssel (kein Mod wird zu "").</summary>
    public static CheckSessionKey CheckKey(string? chosenModDirectory, string? playsetName)
        => new(NormalizePath(chosenModDirectory) ?? "", playsetName);

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? "";
        if (full.Length > root.Length)
            full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return full.ToLowerInvariant();
    }

    /// <summary>Sitzungen einer Art mit ihren Nutzern (Vergleich der Nutzer über die Identität).</summary>
    private sealed class Registry<TKey, TSession>(Func<TSession> create) where TKey : notnull
    {
        private readonly Dictionary<TKey, (TSession Session, HashSet<object> Users)> _entries = new();
        private readonly Dictionary<object, TKey> _keyOfUser = new(ReferenceEqualityComparer.Instance);

        public TSession Acquire(TKey key, object user)
        {
            if (_keyOfUser.TryGetValue(user, out var held) && !EqualityComparer<TKey>.Default.Equals(held, key))
            {
                Release(user);
            }

            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = (create(), new HashSet<object>(ReferenceEqualityComparer.Instance));
                _entries[key] = entry;
            }

            entry.Users.Add(user);
            _keyOfUser[user] = key;
            return entry.Session;
        }

        public void Release(object user)
        {
            if (!_keyOfUser.Remove(user, out var key) || !_entries.TryGetValue(key, out var entry))
            {
                return;
            }

            entry.Users.Remove(user);
            if (entry.Users.Count == 0)
            {
                _entries.Remove(key);
            }
        }
    }
}
