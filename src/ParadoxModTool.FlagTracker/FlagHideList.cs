using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ParadoxModTool.FlagTracker;

/// <summary>
/// Ausblendregeln einer aktiven Mod (Spec-Nachtrag „Einträge ausblenden“). Eine Regel ist ein Name oder ein Muster mit <c>*</c>; verglichen wird
/// wie bei der Suche (ganzer Name, Groß/Klein nach den Regeln des Spiels). Gespeichert als JSON-Liste von Zeichenketten in
/// <c>&lt;Ordner&gt;/&lt;Schlüssel&gt;.json</c>, Schlüssel aus <see cref="KeyFor"/>. Regeln ohne <c>*</c> stehen in einer Hash-Menge (auch
/// tausende Regeln kosten je Name nur ein Nachschlagen), nur Muster werden zu regulären Ausdrücken. Nicht threadsicher; nur vom UI-Thread benutzen.
/// </summary>
public sealed class FlagHideList
{
    private readonly string _directory;

    /// <summary>Zieldatei; null, wenn sich aus dem Mod-Ordner kein Schlüssel bilden ließ (dann scheitert <see cref="Save"/>).</summary>
    private readonly string? _path;

    private readonly List<string> _rules = new();
    private bool _ignoreCase;
    private HashSet<string> _exact;
    private List<(string Rule, Regex Regex)> _patterns = new();

    private FlagHideList(string directory, string? path, bool ignoreCase)
    {
        _directory = directory;
        _path = path;
        _ignoreCase = ignoreCase;
        _exact = new HashSet<string>(Comparer);
    }

    /// <summary>Zählt jede Änderung der Regeln oder ihres Vergleichs (für Zwischenspeicher in <see cref="FlagTrackerSession"/>).</summary>
    public int Version { get; private set; }

    /// <summary>Die Regeln in der Reihenfolge, in der sie angelegt wurden.</summary>
    public IReadOnlyList<string> Rules => _rules;

    /// <summary>Groß/Klein ignorieren (Regel des Spiels, HOI IV und EU IV).</summary>
    public bool IgnoreCase => _ignoreCase;

    private StringComparer Comparer => _ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Schlüssel des Mod-Ordners: die ersten 16 Hex-Zeichen (klein) des SHA-256 über den normalisierten Pfad (voller Pfad, ohne
    /// abschließende Trennzeichen, klein geschrieben).
    /// </summary>
    public static string KeyFor(string activeModDirectory)
    {
        var full = Path.GetFullPath(activeModDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(full));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    /// <summary>
    /// Liest die Regeln der Mod; wirft nie. Eine fehlende Datei ergibt eine leere Liste. Eine kaputte Datei (kein JSON oder keine Liste)
    /// wird nach Möglichkeit in <c>&lt;Schlüssel&gt;.json.bad</c> umbenannt, damit das nächste Speichern sie nicht stillschweigend überschreibt;
    /// die Liste ist dann leer. Einträge, die keine Zeichenketten oder leer sind, werden übergangen.
    /// </summary>
    public static FlagHideList Load(string directory, string activeModDirectory, bool ignoreCase)
    {
        string path;
        try
        {
            path = Path.Combine(directory, KeyFor(activeModDirectory) + ".json");
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return new FlagHideList(directory, null, ignoreCase);
        }

        var list = new FlagHideList(directory, path, ignoreCase);
        try
        {
            if (!File.Exists(path))
            {
                return list;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                MoveAside(path);
                return list;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    list.Add(item.GetString() ?? "");
                }
            }
        }
        catch (JsonException)
        {
            list.Clear();
            MoveAside(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            list.Clear();
        }

        list.Version = 0;
        return list;
    }

    /// <summary>Schreibt die Regeln (Zwischendatei im selben Ordner, dann Umbenennen). Wirft bei Schreibfehlern (IOException u. a.).</summary>
    public void Save()
    {
        if (_path is null)
        {
            throw new IOException("Kein gültiger Ordner der aktiven Mod.");
        }

        Directory.CreateDirectory(_directory);
        var temp = Path.Combine(_directory, Path.GetFileName(_path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(_rules, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    /// <summary>
    /// Wechselt den Vergleich (Groß/Klein) und baut die Vergleicher neu; die Regeln, auch ungespeicherte, bleiben. Doppelte Regeln unter dem
    /// neuen Vergleich bleiben in <see cref="Rules"/> stehen (harmlos).
    /// </summary>
    public void SetIgnoreCase(bool ignoreCase)
    {
        if (ignoreCase == _ignoreCase)
        {
            return;
        }

        _ignoreCase = ignoreCase;
        _exact = new HashSet<string>(Comparer);
        _patterns = new List<(string, Regex)>();
        foreach (var rule in _rules)
        {
            AddMatcher(rule);
        }

        Version++;
    }

    /// <summary>Legt eine Regel an (getrimmt); false, wenn sie leer ist oder schon besteht.</summary>
    public bool Add(string rule)
    {
        rule = (rule ?? string.Empty).Trim();
        if (rule.Length == 0 || _rules.Contains(rule, Comparer))
        {
            return false;
        }

        _rules.Add(rule);
        AddMatcher(rule);
        Version++;
        return true;
    }

    /// <summary>Entfernt genau diese Regel; false, wenn es sie nicht gibt.</summary>
    public bool Remove(string rule)
    {
        var index = _rules.FindIndex(r => Comparer.Equals(r, rule));
        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        Version++;
        return true;
    }

    /// <summary>Entfernt jede Regel, die auf den Namen passt („Einblenden“); liefert ihre Anzahl.</summary>
    public int RemoveMatching(string name)
    {
        var removed = 0;
        for (var i = _rules.Count - 1; i >= 0; i--)
        {
            if (Matches(_rules[i], name))
            {
                RemoveAt(i);
                removed++;
            }
        }

        if (removed > 0)
        {
            Version++;
        }

        return removed;
    }

    /// <summary>Passt eine Regel auf den ganzen Namen? Erst die Namen ohne <c>*</c> (Hash), dann die Muster.</summary>
    public bool IsHidden(string name)
    {
        if (_exact.Contains(name))
        {
            return true;
        }

        foreach (var (_, regex) in _patterns)
        {
            if (regex.IsMatch(name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Vorschlag für „Ähnliche ausblenden“ (Spec-Festlegung 3): Der Name wird an <c>_</c> und <c>.</c> in Abschnitte geteilt; ein Abschnitt
    /// nur aus Ziffern wird zu <c>*</c>, Ziffern in einem Wort bleiben (<c>iv2</c>, <c>adm2x</c>). Mehrere <c>*</c> hintereinander werden zu einem.
    /// <c>iv2_nat_idea_select_adm_105_1</c> ergibt <c>iv2_nat_idea_select_adm_*_*</c>.
    /// </summary>
    public static string SuggestPattern(string name)
    {
        var pattern = Regex.Replace(name ?? string.Empty, @"(?<=^|[_.])[0-9]+(?=$|[_.])", "*");
        return Regex.Replace(pattern, @"\*{2,}", "*");
    }

    private static bool IsPattern(string rule) => rule.Contains('*');

    private void AddMatcher(string rule)
    {
        if (IsPattern(rule))
        {
            _patterns.Add((rule, FlagIndex.ToRegex(rule, false, _ignoreCase)));
        }
        else
        {
            _exact.Add(rule);
        }
    }

    private bool Matches(string rule, string name) =>
        IsPattern(rule) ? _patterns.First(p => ReferenceEquals(p.Rule, rule)).Regex.IsMatch(name) : Comparer.Equals(rule, name);

    private void RemoveAt(int index)
    {
        var rule = _rules[index];
        _rules.RemoveAt(index);
        if (IsPattern(rule))
        {
            _patterns.RemoveAll(p => ReferenceEquals(p.Rule, rule));
        }
        else
        {
            _exact.Remove(rule);

            // Nach einem Wechsel auf Groß/Klein ignorieren können zwei Regeln denselben Eintrag der Menge teilen: die andere bleibt wirksam.
            foreach (var other in _rules.Where(r => !IsPattern(r) && Comparer.Equals(r, rule)))
            {
                _exact.Add(other);
            }
        }
    }

    private void Clear()
    {
        _rules.Clear();
        _exact.Clear();
        _patterns.Clear();
    }

    /// <summary>Kaputte Datei beiseitelegen (<c>.bad</c>); Fehler dabei werden verschluckt.</summary>
    private static void MoveAside(string path)
    {
        try
        {
            File.Move(path, path + ".bad", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
