using System.Text;

namespace ParadoxModTool.ScriptDocs;

/// <summary>Liest die Doku-Logs eines <c>docs</c>-Ordners in die Datenbank.</summary>
public static class ScriptDocImporter
{
    private static readonly ScriptDocKind[] AllKinds = Enum.GetValues<ScriptDocKind>();

    /// <summary>
    /// Fasst Einträge mit gleichem Namen zu einem zusammen (die Logs führen z. B. manche Event-Targets zweimal auf, einmal als globalen Verweis und
    /// einmal mit Eingabe-Scope): Scopes und Ziele werden vereinigt, bei Beschreibung und Angaben gilt der erste Eintrag.
    /// </summary>
    public static IReadOnlyList<ScriptDocEntry> MergeDuplicates(IEnumerable<ScriptDocEntry> entries)
    {
        var result = new List<ScriptDocEntry>();
        var byName = new Dictionary<(ScriptDocKind, string), ScriptDocEntry>();
        foreach (var entry in entries)
        {
            if (!byName.TryGetValue((entry.Kind, entry.Name), out var first))
            {
                byName[(entry.Kind, entry.Name)] = entry;
                result.Add(entry);
                continue;
            }

            foreach (var scope in entry.Scopes.Where(x => !first.Scopes.Contains(x)))
            {
                first.Scopes.Add(scope);
            }

            foreach (var target in entry.Targets.Where(x => !first.Targets.Contains(x)))
            {
                first.Targets.Add(target);
            }

            foreach (var (key, value) in entry.Extra)
            {
                first.Extra.TryAdd(key, value);
            }

            if (first.Description.Length == 0)
            {
                first.Description = entry.Description;
            }
        }

        return result;
    }

    /// <summary>Schreibzeit (UTC-Ticks) jeder vorhandenen Log-Datei; fehlende Dateien fehlen im Ergebnis.</summary>
    public static Dictionary<ScriptDocKind, long> ReadWriteTicks(string docsFolder)
    {
        var result = new Dictionary<ScriptDocKind, long>();
        foreach (var kind in AllKinds)
        {
            var path = Path.Combine(docsFolder, ScriptDocLogFiles.FileNameOf(kind));
            if (File.Exists(path))
            {
                result[kind] = File.GetLastWriteTimeUtc(path).Ticks;
            }
        }

        return result;
    }

    /// <summary>Ist ein Import nötig? Ohne jede Log-Datei nie.</summary>
    public static bool NeedsImport(ScriptDocDatabase database, string docsFolder)
    {
        var ticks = ReadWriteTicks(docsFolder);
        return ticks.Count > 0 && database.NeedsImport(ticks);
    }

    /// <summary>
    /// Liest alle vorhandenen Logs und übernimmt sie in <paramref name="database"/>. Eine Datei, aus der sich kein einziger Eintrag lesen lässt
    /// (leer oder in unbekanntem Format), zählt nicht als gelesen - sonst würden alle Einträge dieser Art fälschlich als veraltet gelten.
    /// Gibt <see langword="null"/> zurück, wenn nichts gelesen werden konnte.
    /// </summary>
    public static ImportRecord? Import(ScriptDocDatabase database, string docsFolder, DateTime nowUtc)
    {
        var parsed = new List<ScriptDocEntry>();
        var kinds = new List<ScriptDocKind>();
        var ticks = new Dictionary<string, long>();

        foreach (var kind in AllKinds)
        {
            var fileName = ScriptDocLogFiles.FileNameOf(kind);
            var path = Path.Combine(docsFolder, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            IReadOnlyList<ScriptDocEntry> entries;
            long writeTicks;
            try
            {
                writeTicks = File.GetLastWriteTimeUtc(path).Ticks;
                entries = ScriptDocLogParser.Parse(kind, File.ReadAllText(path, Encoding.UTF8));
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            if (entries.Count == 0)
            {
                continue;
            }

            parsed.AddRange(MergeDuplicates(entries));
            kinds.Add(kind);
            ticks[fileName] = writeTicks;
        }

        return kinds.Count == 0 ? null : database.ApplyImport(parsed, kinds, nowUtc, ticks);
    }
}
