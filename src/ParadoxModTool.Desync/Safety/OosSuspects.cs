using System.Text.RegularExpressions;

namespace ParadoxModTool.Desync;

/// <summary>Woher ein Punkt der Verdachtsliste kommt.</summary>
public enum SuspectReasonKind
{
    /// <summary>Ein Hinweis aus dem Log verweist auf die Datei.</summary>
    LogLead,

    /// <summary>Ein Fund der MP-Prüfung steht in der Datei.</summary>
    Rule,

    /// <summary>Ein Begriff aus den Ursachen des Zustandsvergleichs kommt in der Datei vor.</summary>
    Term,
}

/// <summary>
/// Ein Grund, warum eine Datei verdächtig ist. <paramref name="Subject"/> ist der relative Pfad (Log-Hinweis), Regel-Id und Token des Funds
/// getrennt durch <c>": "</c> (Regel, nur zur Anzeige; die Teile stehen in <paramref name="RuleId"/> und <paramref name="Token"/>) bzw. der
/// Begriff (Begriff). <paramref name="Line"/> gibt es bei Log-Hinweisen und Funden.
/// <paramref name="Machines"/> sind die Maschinen eines Log-Hinweises (aufsteigend, ohne Doppelte; sonst leer).
/// <paramref name="FileCount"/> ist bei einem Begriff die Zahl der untersuchten Dateien, die ihn enthalten (sonst 0).
/// <paramref name="Severity"/> ist bei einer Regel die Strenge des Funds (nach Urteil), sonst <c>null</c>.
/// <paramref name="RuleId"/> und <paramref name="Token"/> sind bei einer Regel Regel-Id und Token des Funds, sonst <c>null</c>.
/// <paramref name="Points"/> sind die Punkte dieses Grundes allein; Regeln zählen je Datei zusammen höchstens <see cref="OosSuspects.MaxRulePoints"/>.
/// </summary>
public sealed record SuspectReason(
    SuspectReasonKind Kind,
    double Points,
    string Subject,
    int? Line,
    IReadOnlyList<int> Machines,
    int FileCount,
    SafetySeverity? Severity,
    string? RuleId = null,
    string? Token = null);

/// <summary>
/// Eine verdächtige Datei mit Punktzahl und Gründen. <paramref name="Line"/> ist die Zeile des ersten Log-Hinweises, sonst die des ersten
/// Regelfunds, sonst <c>null</c>.
/// </summary>
public sealed record SuspectFile(
    string ModName,
    bool IsOwnMod,
    string RelativePath,
    string FullPath,
    double Score,
    int? Line,
    IReadOnlyList<SuspectReason> Reasons);

/// <summary>
/// Reiht die Dateien der geprüften Mods nach Verdacht für einen OOS: Log-Hinweise, Funde der MP-Prüfung und Begriffe aus den Ursachen
/// des Zustandsvergleichs (siehe Entwurf, Abschnitt "Mode B: ranking").
/// </summary>
public static class OosSuspects
{
    /// <summary>Höchstzahl der Dateien in der Liste.</summary>
    public const int MaxFiles = 50;

    /// <summary>Punkte je Log-Hinweis (Datei und Zeile; Maschinen werden zusammengefasst).</summary>
    public const double LeadPoints = 100;

    /// <summary>Punkte je Fund mit der Strenge <see cref="SafetySeverity.Risk"/>.</summary>
    public const double RiskPoints = 50;

    /// <summary>Punkte je Fund mit der Strenge <see cref="SafetySeverity.Check"/>.</summary>
    public const double CheckPoints = 15;

    /// <summary>Höchstzahl der Punkte, die Funde der MP-Prüfung zusammen einer Datei geben; so wiegt ein Log-Hinweis mehr als beliebig viele Funde.</summary>
    public const double MaxRulePoints = 50;

    /// <summary>Grundpunkte je Begriff, vor Faktor und Seltenheit.</summary>
    public const double TermPoints = 20;

    /// <summary>Faktor der Begriffe aus Folgen (Ursachen haben 1).</summary>
    public const double ConsequenceFactor = 0.3;

    /// <summary>Kürzeste Länge eines Begriffs.</summary>
    public const int MinTermLength = 4;

    private static readonly Regex Identifier = new(@"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.CultureInvariant);
    private static readonly Regex Word = new(@"[A-Za-z0-9_]+", RegexOptions.CultureInvariant);

    /// <summary>
    /// Begriffe (klein) aus den Ursachen und Folgen des Zustandsvergleichs mit dem stärksten Faktor: Bezeichner aus dem Anzeigepfad (Faktor des Gewichts der Einheit)
    /// sowie aus Feldpfaden und beiden Feldwerten (Faktor des Gewichts des Feldes). Rauschen liefert nichts; Stoppbegriffe, kurze und reine Zahlenbegriffe entfallen.
    /// </summary>
    public static IReadOnlyDictionary<string, double> TermsOf(IReadOnlyList<RankedDiff> causes, SafetyRules rules)
    {
        var terms = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var diff in causes)
        {
            if (FactorOf(diff.Weight) is not { } diffFactor)
            {
                continue;
            }

            Collect(diff.DisplayPath, diffFactor);
            foreach (var field in diff.Fields)
            {
                if (FactorOf(field.Weight) is not { } fieldFactor)
                {
                    continue;
                }

                Collect(field.Field.Path, fieldFactor);
                Collect(field.Field.ReferenceValue, fieldFactor);
                Collect(field.Field.DesyncedValue, fieldFactor);
            }
        }

        return terms;

        void Collect(string? text, double factor)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            foreach (Match match in Identifier.Matches(text))
            {
                if (match.Length < MinTermLength || match.Value.All(c => c == '_'))
                {
                    continue;
                }

                var term = match.Value.ToLowerInvariant();
                if (rules.StopTerms.Contains(term))
                {
                    continue;
                }

                terms[term] = Math.Max(terms.GetValueOrDefault(term), factor);
            }
        }
    }

    /// <summary>
    /// Für jede lesbare Datei die Begriffe, die in ihrem Text als ganze Wörter vorkommen (Schreibung egal; Schlüssel ist der volle Pfad, Groß- und Kleinschreibung egal).
    /// Nicht lesbare Dateien (<paramref name="readText"/> liefert <c>null</c>) fehlen. Ohne Begriffe wird nichts gelesen.
    /// <paramref name="progress"/> erhält nach jeder Datei den Anteil (0 bis 1); ohne Begriffe sofort 1.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> TermHits(
        IReadOnlyList<ScannedFile> files,
        IReadOnlyCollection<string> terms,
        Func<string, string?> readText,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        var hits = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
        if (terms.Count == 0)
        {
            progress?.Report(1.0);
            return hits;
        }

        var wanted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var term in terms)
        {
            wanted.TryAdd(term, term);
        }

        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];
            if (!hits.ContainsKey(file.FullPath) && readText(file.FullPath) is { } text)
            {
                var found = new HashSet<string>(StringComparer.Ordinal);
                foreach (Match word in Word.Matches(text))
                {
                    if (word.Length >= MinTermLength && wanted.TryGetValue(word.Value, out var term))
                    {
                        found.Add(term);
                    }
                }

                if (found.Count > 0)
                {
                    hits[file.FullPath] = found;
                }
            }

            progress?.Report((double)(i + 1) / files.Count);
        }

        if (files.Count == 0)
        {
            progress?.Report(1.0);
        }

        return hits;
    }

    /// <summary>
    /// Die verdächtigsten Dateien (höchstens <see cref="MaxFiles"/>, nur mit Punkten über 0): nach Punkten absteigend, dann Modname, dann Pfad.
    /// Hinweise ohne vollen Pfad oder mit einer Datei außerhalb von <see cref="SafetyScanResult.Files"/> zählen nicht. Funde geben einer Datei
    /// zusammen höchstens <see cref="MaxRulePoints"/> Punkte; jeder Fund bleibt als Grund mit seinen eigenen Punkten stehen.
    /// </summary>
    public static IReadOnlyList<SuspectFile> Rank(
        IReadOnlyList<ReportLead> leads,
        SafetyScanResult scan,
        IReadOnlyDictionary<string, double> terms,
        IReadOnlyDictionary<string, IReadOnlySet<string>> termHits)
    {
        var files = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in scan.Files)
        {
            if (Normalize(file.FullPath) is { } key)
            {
                files.TryAdd(key, new Candidate(file));
            }
        }

        AddLeads(leads, files);

        foreach (var finding in scan.Findings)
        {
            var points = finding.Severity switch
            {
                SafetySeverity.Risk => RiskPoints,
                SafetySeverity.Check => CheckPoints,
                _ => 0,
            };
            if (points > 0 && Normalize(finding.FullPath) is { } key && files.TryGetValue(key, out var candidate))
            {
                candidate.Reasons.Add(new SuspectReason(
                    SuspectReasonKind.Rule,
                    points,
                    finding.RuleId + ": " + finding.Token,
                    finding.Line,
                    Array.Empty<int>(),
                    0,
                    finding.Severity,
                    finding.RuleId,
                    finding.Token));
            }
        }

        AddTerms(scan, files, terms, termHits);

        return files.Values
            .Select(candidate => candidate.ToSuspect())
            .Where(suspect => suspect.Score > 0)
            .OrderByDescending(suspect => suspect.Score)
            .ThenBy(suspect => suspect.ModName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(suspect => suspect.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(MaxFiles)
            .ToList();
    }

    private static void AddLeads(IReadOnlyList<ReportLead> leads, Dictionary<string, Candidate> files)
    {
        var merged = new Dictionary<(string Path, int? Line), SortedSet<int>>();
        var order = new List<(string Path, int? Line)>();
        foreach (var lead in leads)
        {
            if (lead.FullPath is null || Normalize(lead.FullPath) is not { } key || !files.ContainsKey(key))
            {
                continue;
            }

            if (!merged.TryGetValue((key, lead.Line), out var machines))
            {
                machines = new SortedSet<int>();
                merged[(key, lead.Line)] = machines;
                order.Add((key, lead.Line));
            }

            machines.UnionWith(lead.MachineIds);
        }

        foreach (var entry in order)
        {
            var candidate = files[entry.Path];
            candidate.Reasons.Add(new SuspectReason(
                SuspectReasonKind.LogLead, LeadPoints, candidate.File.RelativePath, entry.Line, merged[entry].ToList(), 0, null));
        }
    }

    private static void AddTerms(
        SafetyScanResult scan,
        Dictionary<string, Candidate> files,
        IReadOnlyDictionary<string, double> terms,
        IReadOnlyDictionary<string, IReadOnlySet<string>> termHits)
    {
        if (terms.Count == 0 || termHits.Count == 0)
        {
            return;
        }

        var total = files.Count;
        var hitsByFile = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, hits) in termHits)
        {
            if (Normalize(path) is { } key && files.ContainsKey(key))
            {
                hitsByFile[key] = hits;
            }
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var hits in hitsByFile.Values)
        {
            foreach (var term in hits.Where(terms.ContainsKey))
            {
                counts[term] = counts.GetValueOrDefault(term) + 1;
            }
        }

        foreach (var (key, hits) in hitsByFile)
        {
            foreach (var term in hits.Where(terms.ContainsKey).OrderBy(term => term, StringComparer.Ordinal))
            {
                var count = counts[term];
                var points = TermPoints * terms[term] * Math.Log((double)total / count);
                if (points > 0)
                {
                    files[key].Reasons.Add(new SuspectReason(SuspectReasonKind.Term, points, term, null, Array.Empty<int>(), count, null));
                }
            }
        }
    }

    private static double? FactorOf(DiffWeight weight) => weight switch
    {
        DiffWeight.Cause => 1.0,
        DiffWeight.Consequence => ConsequenceFactor,
        _ => null,
    };

    private static string? Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private sealed class Candidate(ScannedFile file)
    {
        public ScannedFile File { get; } = file;

        public List<SuspectReason> Reasons { get; } = new();

        public SuspectFile ToSuspect()
        {
            var line = Reasons.FirstOrDefault(r => r.Kind == SuspectReasonKind.LogLead && r.Line is not null)?.Line
                ?? Reasons.FirstOrDefault(r => r.Kind == SuspectReasonKind.Rule)?.Line;
            var rules = Math.Min(Reasons.Where(r => r.Kind == SuspectReasonKind.Rule).Sum(r => r.Points), MaxRulePoints);
            var score = Reasons.Where(r => r.Kind != SuspectReasonKind.Rule).Sum(r => r.Points) + rules;
            return new SuspectFile(File.ModName, File.IsOwnMod, File.RelativePath, File.FullPath, score, line, Reasons);
        }
    }
}
