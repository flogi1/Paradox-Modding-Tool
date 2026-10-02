using System.Globalization;

namespace ParadoxModTool.Desync;

/// <summary>
/// Ein Hinweis auf die Ursache aus der Zeitleiste, wie ihn der Bericht auflistet. Gleichheit vergleicht <paramref name="MachineIds"/>
/// nach Inhalt (nicht nach Listen-Referenz), damit neu gebaute Hinweise gleich bleiben.
/// </summary>
/// <param name="MachineIds">Die Maschinen mit diesem Hinweis, aufsteigend; aus der Zeitleiste genau eine.</param>
/// <param name="GameDate">Spieldatum der Zeile; <c>null</c> ohne Ticks.</param>
/// <param name="Message">Der Text der Meldung auf einer Zeile (siehe <see cref="ReportLeads.From"/>).</param>
/// <param name="File">Der angezeigte Pfad (relativ im Mod oder im Spiel, mit "/"); <c>null</c> ohne Dateiverweis.</param>
/// <param name="FullPath">Der volle Pfad, nur wenn die Datei auf der Platte gefunden wurde.</param>
/// <param name="ModName">Name des Mods, zu dem die Datei gehört; <c>null</c> bei Spieldateien.</param>
/// <param name="IsOwnMod">Ob die Datei zu einem Mod des Nutzers gehört.</param>
public sealed record ReportLead(
    IReadOnlyList<int> MachineIds,
    string? GameDate,
    string Message,
    string? File,
    string? FullPath,
    int? Line,
    string? ModName,
    bool IsOwnMod,
    LeadKind Kind)
{
    public bool Equals(ReportLead? other) =>
        other is not null
        && MachineIds.SequenceEqual(other.MachineIds)
        && string.Equals(GameDate, other.GameDate, StringComparison.Ordinal)
        && string.Equals(Message, other.Message, StringComparison.Ordinal)
        && string.Equals(File, other.File, StringComparison.Ordinal)
        && string.Equals(FullPath, other.FullPath, StringComparison.Ordinal)
        && Line == other.Line
        && string.Equals(ModName, other.ModName, StringComparison.Ordinal)
        && IsOwnMod == other.IsOwnMod
        && Kind == other.Kind;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var id in MachineIds)
        {
            hash.Add(id);
        }

        hash.Add(GameDate);
        hash.Add(Message);
        hash.Add(File);
        hash.Add(FullPath);
        hash.Add(Line);
        hash.Add(ModName);
        hash.Add(IsOwnMod);
        hash.Add(Kind);
        return hash.ToHashCode();
    }
}

/// <summary>Ein Dump im Bericht; eine fehlende Maschine hat keinen Pfad und die Größe 0.</summary>
/// <param name="Path">Zip-Datei oder Ordner; <c>null</c> bei einer fehlenden Maschine.</param>
/// <param name="SizeBytes">Länge der Zip-Datei bzw. Summe der Dateien des Ordners (ein Pfad kann mehrere Maschinen enthalten).</param>
public sealed record ReportDump(int MachineId, string? Path, bool IsFolder, long SizeBytes, bool Missing);

/// <summary>
/// Eine mögliche Ursache: die abweichende Einheit mit höchstens <see cref="ReportFacts.MaxFieldsPerCause"/> Feldern, nach Gewicht geordnet
/// (Ursache, Folge, Rauschen; innerhalb eines Gewichts in der bestehenden Reihenfolge).
/// </summary>
public sealed record ReportCause(string DisplayPath, UnitChange Change, IReadOnlyList<FieldDiff> Fields);

/// <summary>
/// Die Tatsachen eines Desync-Berichts, einmal aus Zusammenfassung, Hinweisen und Spielstand-Vergleich gesammelt.
/// Alle Texte des Berichts sind Englisch und werden nie übersetzt.
/// </summary>
/// <param name="DiffAvailable">Ob ein Spielstand-Vergleich vorlag; ohne ihn ist <paramref name="Causes"/> leer.</param>
/// <param name="ChecksumExplanations">Je abweichender Prüfsummen-Kategorie ein englischer Erklärtext.</param>
public sealed record ReportFacts(
    OosSummary Summary,
    IReadOnlyList<ReportLead> Leads,
    IReadOnlyList<ReportCause> Causes,
    bool DiffAvailable,
    IReadOnlyList<ReportDump> Dumps,
    IReadOnlyList<string> ChecksumExplanations)
{
    public const int MaxLeads = 10;
    public const int MaxCauses = 10;
    public const int MaxFieldsPerCause = 5;

    /// <summary>
    /// Sammelt die Tatsachen. Hinweise: Einträge ohne Hinweisart entfallen; gleiche Hinweise (Datei, Zeile, Meldung) werden zu einem
    /// zusammengefasst (neuestes Spieldatum, Maschinen aller Einträge), dann neueste zuerst (Spieldatum zahlenweise wie in der Zeitleiste)
    /// und erst danach gekappt. Ursachen: die ersten Einheiten mit Gewicht <see cref="DiffWeight.Cause"/> in der bestehenden Reihenfolge.
    /// </summary>
    public static ReportFacts Build(OosSummary summary, IReadOnlyList<ReportLead> leads, IReadOnlyList<RankedDiff>? ranked,
        OosPair pair, DesyncProfile profile)
    {
        var newestFirst = leads
            .Where(l => l.Kind != LeadKind.None)
            .GroupBy(l => (l.File, l.Line, l.Message))
            .Select(Merge)
            .OrderByDescending(l => l.GameDate, OosTimeline.DateComparer.Instance)
            .Take(MaxLeads)
            .ToList();

        var causes = (ranked ?? [])
            .Where(r => r.Weight == DiffWeight.Cause)
            .Take(MaxCauses)
            .Select(r => new ReportCause(r.DisplayPath, r.Unit.Change, r.Fields.OrderBy(f => Rank(f.Weight)).Select(f => f.Field).Take(MaxFieldsPerCause).ToList()))
            .ToList();

        var dumps = pair.Dumps.Select(DumpOf)
            .Concat(pair.MissingMachineIds.Select(id => new ReportDump(id, null, false, 0, true)))
            .OrderBy(d => d.MachineId)
            .ToList();

        var explanations = summary.ChecksumCategories.Select(category => ExplainEnglish(profile, category)).ToList();

        return new ReportFacts(summary, newestFirst, causes, ranked is not null, dumps, explanations);
    }

    /// <summary>Rang eines Feldes in einer Ursache: Ursache vor Folge vor Rauschen.</summary>
    private static int Rank(DiffWeight weight) => weight switch
    {
        DiffWeight.Cause => 0,
        DiffWeight.Consequence => 1,
        _ => 2,
    };

    /// <summary>Ein Hinweis für eine Gruppe gleicher Hinweise: der neueste Eintrag (bei Gleichstand der erste) mit den Maschinen aller Einträge.</summary>
    private static ReportLead Merge(IEnumerable<ReportLead> group)
    {
        var all = group.ToList();
        var newest = all.Aggregate((best, lead) => OosTimeline.DateComparer.Instance.Compare(lead.GameDate, best.GameDate) > 0 ? lead : best);
        return newest with { MachineIds = all.SelectMany(l => l.MachineIds).Distinct().Order().ToList() };
    }

    private static string ExplainEnglish(DesyncProfile profile, string category) =>
        profile.Describe(category)?.Explain.GetValueOrDefault("en") is { Length: > 0 } text
            ? text
            : string.Create(CultureInfo.InvariantCulture, $"Checksum category {category} differed.");

    private static ReportDump DumpOf(OosDumpInfo info)
    {
        var isFolder = Directory.Exists(info.Path);
        return new ReportDump(info.MachineId, info.Path, isFolder, SizeOf(info.Path, isFolder), false);
    }

    private static long SizeOf(string path, bool isFolder)
    {
        try
        {
            return isFolder
                ? new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)
                : new FileInfo(path).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
