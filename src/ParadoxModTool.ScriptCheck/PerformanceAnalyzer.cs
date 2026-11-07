using ParadoxModTool.FlagTracker;

namespace ParadoxModTool.ScriptCheck;

/// <summary>Wie oft Code läuft; die Reihenfolge ist die Geschwindigkeit (schnellster zuerst). <see cref="Checked"/> = ständig geprüfter Block oder abgefragter Event-Block.</summary>
public enum PerformanceRate
{
    Daily,
    Monthly,
    Checked,
    Yearly,
    Rare,
}

/// <summary>Angaben eines Fundes <see cref="FindingKind.ExpensiveIterator"/>.</summary>
/// <param name="Rate">Schnellster Takt der Stellen des Fundes; Ort, Weg und Zeilen gehören zur ersten Stelle mit diesem Takt.</param>
/// <param name="Path">Anzeigenamen vom Start bis zum Besitzer: Start = Puls-Name oder <c>besitzer (block)</c> bei geprüften Blöcken; Events mit dem Namen der Definition.</param>
/// <param name="Inner">Erster globaler Iterator innerhalb dieses (verschachtelt); null = keiner.</param>
/// <param name="Narrower">Engere Iteratoren laut Regel; leer = keine bekannt.</param>
/// <param name="Lines">Alle Zeilen dieses Schlüssels in der Datei des Fundes (Datei der ersten schnellsten Stelle), aufsteigend.</param>
public sealed record PerformanceInfo(PerformanceRate Rate, IReadOnlyList<string> Path, string? Inner, IReadOnlyList<string> Narrower, IReadOnlyList<int> Lines);

/// <summary>
/// Findet teure globale Iteratoren (<see cref="ScriptCheckPerformanceRules.GlobalIterators"/>) in häufig laufendem Code der gewählten Mod.
/// Läuft den Verweisgraphen (Event-Aufrufe, On-Action-Aufrufe, Erwähnungen in Aufrufform, Aufrufformen der Mod, Aufrufformen in geprüften Blöcken) von den Pulsen und den ständig
/// geprüften Blöcken ab; jeder Knoten erbt den schnellsten Takt. Eine Stelle ohne bekannten Takt ergibt keinen Fund.
/// </summary>
public static class PerformanceAnalyzer
{
    private static readonly PerformanceRate[] SpeedOrder = { PerformanceRate.Daily, PerformanceRate.Monthly, PerformanceRate.Checked, PerformanceRate.Yearly, PerformanceRate.Rare };

    /// <summary>Start des Laufs: ein Knoten mit Takt; <paramref name="Label"/> = Anzeigename des Starts vor dem Knoten (geprüfter Block), null bei Pulsen.</summary>
    private sealed record Start(PerformanceRate Rate, string Node, string? Label);

    /// <summary>Eine bewertete Iterator-Stelle.</summary>
    private sealed record RatedSite(CheckFile File, IteratorSite Site, PerformanceRate Rate, IReadOnlyList<string> Path);

    /// <summary>
    /// Funde der gewählten Mod; leer ohne <see cref="ScriptCheckRules.Performance"/>, ohne <see cref="ScriptCheckPerformanceRules.GlobalIterators"/>,
    /// ohne <see cref="ScriptCheckPerformanceRules.PulseOnActions"/> oder ohne <see cref="ScriptCheckIndex.ModRoot"/>.
    /// </summary>
    public static IReadOnlyList<Finding> Analyze(ScriptCheckIndex index, ScriptCheckRules rules)
    {
        if (rules.Performance is not { } performance || performance.GlobalIterators.Count == 0 || performance.PulseOnActions.Count == 0
            || index.ModRoot is not { } modRoot)
        {
            return Array.Empty<Finding>();
        }

        var checkedFolders = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        bool InCheckedFolder(string relativePath)
        {
            if (!checkedFolders.TryGetValue(relativePath, out var result))
            {
                result = performance.AlwaysCheckedBlocks.Any(r => rules.Matches(r, relativePath));
                checkedFolders[relativePath] = result;
            }

            return result;
        }

        // Abgefragte Events je Datei (Wurzel + relativer Pfad), über EventIds.Key.
        var polled = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in index.Files)
        {
            foreach (var d in file.Scan.Definitions.Where(d => d.Kind == CheckKind.Event && d.Polled))
            {
                var key = FileKey(file.File);
                if (!polled.TryGetValue(key, out var set))
                {
                    polled[key] = set = new HashSet<string>(StringComparer.Ordinal);
                }

                set.Add(EventIds.Key(d.Name));
            }
        }

        // Der Block zählt in einem Ordner mit ständig geprüften Blöcken, in Event-Dateien nur bei abgefragten Events.
        bool IsChecked(SiteLocation location, string? checkedBlock, string? owner, CheckKind ownerKind) =>
            checkedBlock is not null
            && (InCheckedFolder(location.RelativePath)
                || (ownerKind == CheckKind.Event && owner is not null && polled.TryGetValue(FileKey(location), out var events) && events.Contains(EventIds.Key(owner))));

        var display = new Dictionary<string, string>(StringComparer.Ordinal);
        // Definierte Arten je Name (Effekt, Trigger, Skriptwert): Verweise tragen die Art nicht sicher (siehe TargetNodes).
        var kinds = new Dictionary<string, HashSet<CheckKind>>(StringComparer.Ordinal);
        foreach (var file in index.Files)
        {
            foreach (var d in file.Scan.Definitions)
            {
                if (d.Kind == CheckKind.Event)
                {
                    display.TryAdd("E:" + EventIds.Key(d.Name), d.Name);
                }
                else if (d.Kind is CheckKind.Effect or CheckKind.Trigger or CheckKind.Value)
                {
                    if (!kinds.TryGetValue(d.Name, out var set))
                    {
                        kinds[d.Name] = set = new HashSet<CheckKind>();
                    }

                    set.Add(d.Kind);
                }
            }
        }

        // Knoten eines Verweisziels. Aufrufformen der Mod tragen immer Effekt (Effekt oder Trigger), Erwähnungen die zuerst gesehene Art des Namens;
        // darum zählt die definierte Art: eine Aufrufform trifft Effekt und Trigger des Namens, aus einem Trigger oder einem geprüften Block
        // (Bedingungskontext, siehe CheckedBlockRule) nur den Trigger, falls es ihn gibt; eine Erwähnung ohne Aufrufform den Skriptwert.
        // Ohne passende Definition gilt die Art des Verweises.
        IEnumerable<string> TargetNodes(CheckRef r)
        {
            if (r.TargetKind is not (CheckKind.Effect or CheckKind.Trigger or CheckKind.Value))
            {
                return Node(r.Target, r.TargetKind) is { } single ? new[] { single } : Array.Empty<string>();
            }

            var defined = kinds.GetValueOrDefault(r.Target) ?? new HashSet<CheckKind>();
            List<CheckKind> matched;
            if (!r.CallForm)
            {
                matched = defined.Contains(CheckKind.Value) ? new List<CheckKind> { CheckKind.Value } : new List<CheckKind>();
            }
            else if ((r.OwnerKind == CheckKind.Trigger || r.CheckedBlock is not null) && defined.Contains(CheckKind.Trigger))
            {
                matched = new List<CheckKind> { CheckKind.Trigger };
            }
            else
            {
                matched = new[] { CheckKind.Effect, CheckKind.Trigger }.Where(defined.Contains).ToList();
            }

            if (matched.Count == 0)
            {
                matched.Add(r.TargetKind);
            }

            return matched.Select(k => Node(r.Target, k)!);
        }

        var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var checkedStarts = new List<Start>();
        void Add(SiteLocation location, CheckRef r)
        {
            var source = r.Owner is { } owner ? Node(owner, r.OwnerKind) : null;
            if (source is not null)
            {
                display.TryAdd(source, r.Owner!);
            }

            var isChecked = IsChecked(location, r.CheckedBlock, r.Owner, r.OwnerKind);
            foreach (var target in TargetNodes(r))
            {
                display.TryAdd(target, r.Target);
                if (source is not null)
                {
                    if (!edges.TryGetValue(source, out var list))
                    {
                        edges[source] = list = new List<string>();
                    }

                    list.Add(target);
                }

                if (isChecked)
                {
                    checkedStarts.Add(new Start(PerformanceRate.Checked, target, CheckedLabel(r.Owner ?? location.RelativePath, r.CheckedBlock!)));
                }
            }
        }

        foreach (var r in index.EventCalls)
        {
            Add(r.Location, r.Ref);
        }

        foreach (var file in index.Files)
        {
            foreach (var r in file.Scan.OnActionCalls)
            {
                Add(file.File with { Line = r.Line }, r);
            }

            // Aufrufformen in Gewichtungsblöcken: nur Namen mit Trigger-Definition (Gewichtungsblöcke sind Trigger; eingebaute Namen und reine Effekte fallen so weg).
            foreach (var r in file.Scan.CheckedCalls.Where(c => kinds.TryGetValue(c.Target, out var defined) && defined.Contains(CheckKind.Trigger)))
            {
                Add(file.File with { Line = r.Line }, r);
            }
        }

        foreach (var r in index.Mentions.Where(m => m.Ref.TargetKind == CheckKind.Value || (m.Ref.TargetKind is CheckKind.Effect or CheckKind.Trigger && m.Ref.CallForm)))
        {
            Add(r.Location, r.Ref);
        }

        foreach (var r in index.ModBlockCalls)
        {
            Add(r.Location, r.Ref);
        }

        var starts = new List<Start>();
        foreach (var (name, pulseRate) in performance.PulseOnActions.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var node = "O:" + name;
            display.TryAdd(node, name);
            starts.Add(new Start(Map(pulseRate), node, null));
        }

        starts.AddRange(checkedStarts);

        // Takte der Reihe nach (schnellster zuerst), je Takt ein Breitenlauf von allen Starts dieses Takts zugleich (Pulse nach Namen, dann
        // geprüfte Blöcke in Indexreihenfolge): der erste Besuch legt Takt und Vorgänger fest, so ist der Weg der kürzeste des Takts; Zirkel enden so.
        var rates = new Dictionary<string, PerformanceRate>(StringComparer.Ordinal);
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rate in SpeedOrder)
        {
            var queue = new Queue<string>();
            foreach (var start in starts.Where(s => s.Rate == rate))
            {
                if (!rates.TryAdd(start.Node, rate))
                {
                    continue;
                }

                if (start.Label is not null)
                {
                    labels[start.Node] = start.Label;
                }

                queue.Enqueue(start.Node);
            }

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (!edges.TryGetValue(node, out var targets))
                {
                    continue;
                }

                foreach (var target in targets)
                {
                    if (rates.TryAdd(target, rate))
                    {
                        parents[target] = node;
                        queue.Enqueue(target);
                    }
                }
            }
        }

        IReadOnlyList<string> PathTo(string node)
        {
            var path = new List<string>();
            for (string? n = node; n is not null; n = parents.GetValueOrDefault(n))
            {
                path.Add(display.GetValueOrDefault(n, n[2..]));
                if (labels.TryGetValue(n, out var label))
                {
                    path.Add(label);
                }
            }

            path.Reverse();
            return path;
        }

        bool IsMod(SiteLocation location) => string.Equals(location.RootDirectory, modRoot.Directory, StringComparison.OrdinalIgnoreCase);

        var groups = new Dictionary<string, List<RatedSite>>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var file in index.Files.Where(f => IsMod(f.File)))
        {
            foreach (var site in file.Scan.Iterators.Where(s => !s.UnderGlobal))
            {
                if (Rate(file, site) is not { } rated)
                {
                    continue;
                }

                var key = FindingKind.ExpensiveIterator + "|" + (site.Owner ?? file.File.RelativePath) + "|" + site.Iterator;
                if (!groups.TryGetValue(key, out var list))
                {
                    groups[key] = list = new List<RatedSite>();
                    order.Add(key);
                }

                list.Add(rated);
            }
        }

        RatedSite? Rate(CheckFile file, IteratorSite site)
        {
            PerformanceRate? nodeRate = null;
            string? node = site.Owner is { } owner ? Node(owner, site.OwnerKind) : null;
            if (node is not null && rates.TryGetValue(node, out var r))
            {
                nodeRate = r;
            }

            if (nodeRate is { } fast && fast < PerformanceRate.Checked)
            {
                return new RatedSite(file, site, fast, PathTo(node!));
            }

            var location = file.File with { Line = site.Line };
            if (IsChecked(location, site.CheckedBlock, site.Owner, site.OwnerKind))
            {
                return new RatedSite(file, site, PerformanceRate.Checked, new[] { CheckedLabel(site.Owner ?? file.File.RelativePath, site.CheckedBlock!) });
            }

            return nodeRate is { } slow ? new RatedSite(file, site, slow, PathTo(node!)) : null;
        }

        var narrower = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var rule in performance.GlobalIterators)
        {
            narrower.TryAdd(rule.Name, rule.Narrower ?? Array.Empty<string>());
        }

        var findings = new List<Finding>();
        foreach (var key in order)
        {
            var sites = groups[key];
            // Anker: die erste Stelle mit dem schnellsten Takt (OrderBy ist stabil); Ort, Weg und Zeilen kommen von ihr.
            var anchor = sites.OrderBy(s => s.Rate).First();
            var inner = sites.Select(s => s.Site.Inner).FirstOrDefault(i => i is not null);
            var severity = inner is not null || anchor.Rate is PerformanceRate.Daily or PerformanceRate.Monthly or PerformanceRate.Checked
                ? FindingSeverity.Warning
                : FindingSeverity.Hint;
            var lines = sites
                .Where(s => string.Equals(FileKey(s.File.File), FileKey(anchor.File.File), StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Site.Line)
                .Distinct()
                .Order()
                .ToList();
            var info = new PerformanceInfo(anchor.Rate, anchor.Path, inner, narrower.GetValueOrDefault(anchor.Site.Iterator, Array.Empty<string>()), lines);
            var nameKind = anchor.Site.Owner is null ? CheckKind.Other : anchor.Site.OwnerKind;
            findings.Add(Finding.Create(FindingKind.ExpensiveIterator, severity, anchor.Site.Iterator, nameKind, anchor.File.File with { Line = anchor.Site.Line }, anchor.Path)
                with { Key = key, Performance = info });
        }

        return findings;
    }

    /// <summary>
    /// Knoten des Graphen je Art: Events über <see cref="EventIds.Key"/>, On-Actions, Effekte, Trigger und Skriptwerte getrennt
    /// (gleichnamiger Effekt und Trigger sind zwei Knoten); andere Arten sind keine Knoten. Die Präfixe sind zwei Zeichen lang (Anzeige in PathTo).
    /// </summary>
    private static string? Node(string name, CheckKind kind) => kind switch
    {
        CheckKind.Event => "E:" + EventIds.Key(name),
        CheckKind.OnAction => "O:" + name,
        CheckKind.Effect => "F:" + name,
        CheckKind.Trigger => "T:" + name,
        CheckKind.Value => "V:" + name,
        _ => null,
    };

    private static string CheckedLabel(string owner, string block) => owner + " (" + block + ")";

    private static string FileKey(SiteLocation location) => location.RootDirectory + "|" + location.RelativePath;

    private static PerformanceRate Map(PulseRate rate) => rate switch
    {
        PulseRate.Daily => PerformanceRate.Daily,
        PulseRate.Monthly => PerformanceRate.Monthly,
        PulseRate.Yearly => PerformanceRate.Yearly,
        _ => PerformanceRate.Rare,
    };
}
