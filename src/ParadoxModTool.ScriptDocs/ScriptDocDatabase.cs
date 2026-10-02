namespace ParadoxModTool.ScriptDocs;

/// <summary>Ein Einlesevorgang der Doku-Logs: welche Arten er abdeckte und welchen Stand (Schreibzeit) die Dateien hatten.</summary>
public sealed class ImportRecord
{
    public int Id { get; set; }

    public DateTime ImportedUtc { get; set; }

    /// <summary>Die Eintragsarten, deren Log in diesem Import gelesen wurde. Nur für diese gelten "veraltet" und "neu".</summary>
    public List<ScriptDocKind> Kinds { get; set; } = new();

    /// <summary>Letzte Schreibzeit (UTC-Ticks) jeder gelesenen Log-Datei, Schlüssel ist der Dateiname.</summary>
    public Dictionary<string, long> SourceWriteTicks { get; set; } = new();

    /// <summary>Mit welchem Einleseformat (<see cref="ScriptDocDatabase.CurrentFormatVersion"/>) dieser Import lief; ältere Datenbanken haben 0.</summary>
    public int FormatVersion { get; set; }
}

/// <summary>
/// Alle je gesehenen Einträge der Script-Datenbank eines Spiels samt der Liste der Importe. Neue Importe ergänzen und aktualisieren die
/// Einträge, löschen aber keine: was im letzten Import fehlt, gilt als <see cref="IsOutdated">veraltet</see> und bleibt nachschlagbar
/// (z. B. der Eintrag eines Mods, der im aktuellen Playset nicht aktiv ist).
/// </summary>
public sealed class ScriptDocDatabase
{
    /// <summary>
    /// Stand des Einleseformats, gespeichert je Import (<see cref="ImportRecord.FormatVersion"/>). 1: Event-Targets tragen <c>Plain Inputs</c>/<c>Data Inputs</c>
    /// (siehe <see cref="ScriptDocImporter.MergeDuplicates"/>); Event-Targets aus einem älteren Import werden darum einmal neu eingelesen.
    /// </summary>
    public const int CurrentFormatVersion = 1;

    public List<ImportRecord> Imports { get; set; } = new();

    public List<ScriptDocEntry> Entries { get; set; } = new();

    /// <summary>Kennung des jüngsten Imports, der Einträge dieser Art gelesen hat; 0, wenn es keinen gibt.</summary>
    public int LatestImportCovering(ScriptDocKind kind) =>
        Imports.Where(i => i.Kinds.Contains(kind)).Select(i => i.Id).DefaultIfEmpty(0).Max();

    /// <summary>Veraltet: Der Eintrag stand im jüngsten Import seiner Art nicht mehr.</summary>
    public bool IsOutdated(ScriptDocEntry entry)
    {
        var latest = LatestImportCovering(entry.Kind);
        return latest > 0 && entry.LastSeenImport < latest;
    }

    /// <summary>Neu: Der Eintrag kam erst mit dem jüngsten Import seiner Art dazu, und es gab davor schon einen Import dieser Art.</summary>
    public bool IsNew(ScriptDocEntry entry)
    {
        var latest = LatestImportCovering(entry.Kind);
        return latest > 0
            && entry.FirstSeenImport == latest
            && Imports.Count(i => i.Kinds.Contains(entry.Kind)) >= 2;
    }

    /// <summary>
    /// Übernimmt die gelesenen Einträge: bekannte werden aktualisiert (Beschreibung, Scopes usw. vom Spiel), unbekannte neu angelegt.
    /// Gibt den neuen Import zurück. <paramref name="parsed"/> muss nur Arten enthalten, die in <paramref name="kinds"/> stehen.
    /// </summary>
    public ImportRecord ApplyImport(
        IReadOnlyList<ScriptDocEntry> parsed,
        IReadOnlyCollection<ScriptDocKind> kinds,
        DateTime nowUtc,
        IReadOnlyDictionary<string, long> sourceWriteTicks)
    {
        var record = new ImportRecord
        {
            Id = Imports.Select(i => i.Id).DefaultIfEmpty(0).Max() + 1,
            ImportedUtc = nowUtc,
            Kinds = kinds.ToList(),
            SourceWriteTicks = new Dictionary<string, long>(sourceWriteTicks),
            FormatVersion = CurrentFormatVersion,
        };

        var index = new Dictionary<(ScriptDocKind, string), ScriptDocEntry>();
        foreach (var existing in Entries)
        {
            index[(existing.Kind, existing.Name)] = existing;
        }

        foreach (var entry in parsed)
        {
            if (index.TryGetValue((entry.Kind, entry.Name), out var known))
            {
                known.Description = entry.Description;
                known.Scopes = entry.Scopes;
                known.Targets = entry.Targets;
                known.Extra = entry.Extra;
                known.LastSeenImport = record.Id;
            }
            else
            {
                entry.FirstSeenImport = record.Id;
                entry.LastSeenImport = record.Id;
                Entries.Add(entry);
                index[(entry.Kind, entry.Name)] = entry;
            }
        }

        Imports.Add(record);
        return record;
    }

    /// <summary>
    /// Ist seit dem letzten Import ein Log neu oder geändert (andere Schreibzeit), oder wurde eine Art noch nie gelesen?
    /// <paramref name="currentWriteTicks"/> enthält die Schreibzeit jeder aktuell vorhandenen Log-Datei.
    /// </summary>
    public bool NeedsImport(IReadOnlyDictionary<ScriptDocKind, long> currentWriteTicks)
    {
        if (currentWriteTicks.ContainsKey(ScriptDocKind.EventTarget)
            && Imports.Where(i => i.Kinds.Contains(ScriptDocKind.EventTarget)).MaxBy(i => i.Id) is { } eventTargets
            && eventTargets.FormatVersion < CurrentFormatVersion)
        {
            return true;
        }

        foreach (var (kind, ticks) in currentWriteTicks)
        {
            var lastImport = Imports.Where(i => i.Kinds.Contains(kind)).OrderByDescending(i => i.Id).FirstOrDefault();
            if (lastImport is null
                || !lastImport.SourceWriteTicks.TryGetValue(ScriptDocLogFiles.FileNameOf(kind), out var known)
                || known != ticks)
            {
                return true;
            }
        }

        return false;
    }
}
