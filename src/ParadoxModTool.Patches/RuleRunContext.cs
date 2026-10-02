namespace ParadoxModTool.Patches;

/// <summary>Eine für eine Regel übersprungene Zeile; <see cref="AlreadyDefined"/>: der Schlüssel steht schon anderswo in der Mod (übernehmbar).</summary>
public sealed record SkippedRow(string RuleId, string RowKey, string Reason, bool AlreadyDefined = false);

/// <summary>Gemeinsamer Zustand eines Rezeptlaufs, den alle Regel-Anwender teilen.</summary>
public sealed class RuleRunContext
{
    public RuleRunContext(PatchRecipe recipe, PatchWorkspace workspace, ResolvedTable table, PatchMemory oldMemory)
    {
        Recipe = recipe;
        Workspace = workspace;
        Table = table;
        OldMemory = oldMemory;
        NewMemory = oldMemory.Clone();
    }

    public PatchRecipe Recipe { get; }

    public PatchWorkspace Workspace { get; }

    public ResolvedTable Table { get; }

    public PatchMemory OldMemory { get; }

    /// <summary>Startet als Kopie von <see cref="OldMemory"/>; die Anwender tragen das neu Erzeugte ein.</summary>
    public PatchMemory NewMemory { get; }

    public List<SkippedRow> Skipped { get; } = new();

    public List<string> Warnings { get; } = new();

    private readonly HashSet<(string RuleId, string RowKey)> _quietSkips = new();

    /// <summary>Zeilen, die diese Regel übersprungen hat (gemeldet oder still bei der Einzel-Vorschau); ihre Bausteine und Einträge bleiben stehen.</summary>
    public IReadOnlySet<string> SkippedRowKeys(string ruleId) =>
        Skipped.Where(s => s.RuleId == ruleId).Select(s => s.RowKey)
            .Concat(_quietSkips.Where(q => q.RuleId == ruleId).Select(q => q.RowKey))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// true, wenn ein in den Vorlagen benutzter Platzhalter für diese Zeile leer ist; die Zeile wird dann für die Regel übersprungen
    /// (sonst träfe z. B. <c>special_building_slot = {key}</c> mit leerem Wert jede Slot-Zeile).
    /// </summary>
    public bool SkipForEmptyPlaceholders(PatchRule rule, ResolvedRow row, params string[] templates)
    {
        var empty = templates.SelectMany(t => TemplateRenderer.EmptyPlaceholders(t, row.Values)).Distinct(StringComparer.Ordinal).ToList();
        if (empty.Count == 0)
        {
            return false;
        }

        Skipped.Add(new SkippedRow(rule.Id, row.Key, PatchTexts.EmptyPlaceholder(string.Join(", ", empty))));
        return true;
    }

    /// <summary>Bausteine mit Konflikt samt ihrer drei Fassungen.</summary>
    public List<ConflictUnit> Conflicts { get; } = new();

    /// <summary>Blöcke, die "Bestehende Blöcke übernehmen" aus ihrer Datei entfernt hat.</summary>
    public List<AdoptedBlock> Adopted { get; } = new();

    /// <summary>Welche Bausteine (Gedächtnis-Ids) in welcher Datei stehen; der Plan-Schreiber setzt sie bei nicht geschriebenen Konflikt-Dateien zurück.</summary>
    public Dictionary<string, List<string>> UnitsByFile { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void RecordUnit(string file, string unitId)
    {
        if (!UnitsByFile.TryGetValue(file, out var list))
        {
            list = new List<string>();
            UnitsByFile[file] = list;
        }

        list.Add(unitId);
    }

    /// <summary>Die fehlerfreien Zeilen der Regel (bei <see cref="RuleScope.Global"/> eine Zeile mit leerem <c>key</c>); fehlerhafte landen in <see cref="Skipped"/>.</summary>
    /// <param name="record">false: fehlerhafte Zeilen nur weglassen, nicht als übersprungen melden (für Hilfsaufrufe außerhalb der eigenen Regel).</param>
    public IReadOnlyList<ResolvedRow> RowsFor(PatchRule rule, bool record = true)
    {
        if (rule.Scope == RuleScope.Global)
        {
            return new[] { new ResolvedRow { Key = "global", Values = new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = string.Empty } } };
        }

        var rows = new List<ResolvedRow>();
        foreach (var row in Table.Rows)
        {
            if (Recipe.OnlyRowKey is { } only && row.Key != only)
            {
                // Einzel-Vorschau: die übrigen Zeilen gelten als übersprungen (Bausteine und Einträge bleiben), ohne Meldung.
                _quietSkips.Add((rule.Id, row.Key));
                continue;
            }

            if (row.HasErrors && record)
            {
                Skipped.Add(new SkippedRow(rule.Id, row.Key, string.Join("; ", row.Errors)));
            }
            else if (!row.HasErrors && ForcedSkips.TryGetValue(row.Key, out var forced))
            {
                // "Zeilen ganz oder gar nicht": eine andere Regel hat die Zeile übersprungen; die auslösende Regel behält ihren eigenen Grund.
                if (record)
                {
                    Skipped.Add(forced.RuleId == rule.Id ? forced : new SkippedRow(rule.Id, row.Key, PatchTexts.SkippedWithRule(RuleLabel(forced.RuleId), forced.Reason)));
                }
            }
            else if (!row.HasErrors)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    /// <summary>Zeilen, die alle Regeln auslassen (aus einem früheren Durchgang mit "Zeilen ganz oder gar nicht"), mit dem ursprünglichen Grund.</summary>
    public IReadOnlyDictionary<string, SkippedRow> ForcedSkips { get; init; } = new Dictionary<string, SkippedRow>(StringComparer.Ordinal);

    private string RuleLabel(string ruleId) =>
        Recipe.Rules.FirstOrDefault(r => r.Id == ruleId) is { Name.Length: > 0 } rule ? $"{rule.Name} ({ruleId})" : ruleId;
}
