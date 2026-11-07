using ParadoxModTool.FlagTracker;

namespace ParadoxModTool.ScriptCheck;

/// <summary>
/// Wertet einen <see cref="ScriptCheckIndex"/> aus: kaputte Verweise, tote Events, unbenutzte Blöcke, doppelte Event-Ids und Zyklen.
/// Funde gibt es nur für Definitionen und Verweise in Dateien der gewählten Mod; Vanilla und alle Playset-Mods zählen als Aufrufer und Definitionen.
/// Events werden überall über <see cref="EventIds.Key"/> verglichen, alle anderen Namen ordinal.
/// </summary>
public static class ScriptCheckAnalyzer
{
    private const string CycleSeparator = " → ";

    /// <summary>Eine Definition mit ihrer Datei und ihrem Ort (Zeile der Definition).</summary>
    private sealed record Located(CheckFile File, CheckDefinition Definition, SiteLocation Location);

    /// <param name="builtIns">Eingebaute Effekt- und Triggernamen (ordinal); leer = unbekannt, dann werden Effekt-/Trigger-Aufrufe nicht geprüft.</param>
    public static AnalyzerResult Analyze(ScriptCheckIndex index, IReadOnlySet<string> builtIns, ScriptCheckRules rules)
    {
        var blocksChecked = builtIns.Count > 0;
        if (index.ModRoot is not { } modRoot)
        {
            return new AnalyzerResult(Array.Empty<Finding>(), blocksChecked);
        }

        bool IsMod(SiteLocation location) => string.Equals(location.RootDirectory, modRoot.Directory, StringComparison.OrdinalIgnoreCase);

        var definitions = index.Files
            .SelectMany(f => f.Scan.Definitions.Select(d => new Located(f, d, f.File with { Line = d.Line })))
            .ToList();
        var modDefinitions = definitions.Where(d => IsMod(d.Location)).ToList();

        var findings = new List<Finding>();
        findings.AddRange(BrokenEvents(index, definitions, IsMod));
        if (blocksChecked)
        {
            findings.AddRange(BrokenBlocks(index, definitions, builtIns, rules));
        }

        // Mods kopieren Vanilla-Dateien: Was es auch außerhalb der Mod gibt (Vanilla, andere Playset-Mods), ist nie tot oder unbenutzt; die Engine oder Dritte können es nutzen.
        // Dazu zählen auch ersetzte Dateien mit gleichem Pfad, die selbst nicht im Index stehen.
        var foreign = definitions.Where(d => !IsMod(d.Location)).Select(d => d.Definition).Concat(index.ReplacedDefinitions).ToList();
        var foreignEvents = foreign.Where(d => d.Kind == CheckKind.Event).Select(d => EventIds.Key(d.Name)).ToHashSet(StringComparer.Ordinal);
        var foreignBlocks = foreign.Where(d => IsCheckedBlock(d.Kind)).Select(d => d.Name).ToHashSet(StringComparer.Ordinal);

        findings.AddRange(DeadEvents(index, modDefinitions, foreignEvents));
        findings.AddRange(UnusedBlocks(index, modDefinitions, foreignBlocks));
        findings.AddRange(DuplicateEvents(index.Sources, definitions, IsMod));
        findings.AddRange(EffectCycles(index, definitions, modDefinitions));
        findings.AddRange(EventCycles(index, definitions, modDefinitions));
        findings.AddRange(PerformanceAnalyzer.Analyze(index, rules));

        return new AnalyzerResult(Sort(findings), blocksChecked);
    }

    /// <summary>Anzeigereihenfolge der Funde: Schwere, verschachtelte teure Iteratoren zuerst, dann Datei und Zeile.</summary>
    internal static IReadOnlyList<Finding> Sort(IEnumerable<Finding> findings) =>
        findings
            .OrderBy(f => f.Severity)
            // Verschachtelte teure Iteratoren zuerst; andere Arten haben keine Leistungsangaben, ihre Reihenfolge bleibt.
            .ThenBy(f => f.Performance?.Inner is null)
            .ThenBy(f => f.Location.RelativePath, StringComparer.Ordinal)
            .ThenBy(f => f.Location.Line)
            .ToList();

    /// <summary>Event-Aufrufe der Mod, deren Schlüssel keine Event-Definition in irgendeiner Datei hat; ein Fund je Schlüssel am ersten Aufruf.</summary>
    private static IEnumerable<Finding> BrokenEvents(ScriptCheckIndex index, List<Located> definitions, Func<SiteLocation, bool> isMod)
    {
        var known = definitions.Where(d => d.Definition.Kind == CheckKind.Event).Select(d => EventIds.Key(d.Definition.Name)).ToHashSet(StringComparer.Ordinal);
        return index.EventCalls
            .Where(c => isMod(c.Location) && !c.Ref.Target.Contains('$') && !known.Contains(EventIds.Key(c.Ref.Target)))
            .GroupBy(c => EventIds.Key(c.Ref.Target), StringComparer.Ordinal)
            .Select(g => Broken(g.ToList(), CheckKind.Event));
    }

    /// <summary>
    /// Aufrufformen in der Mod, deren Name weder als Effekt noch als Trigger definiert noch eingebaut ist
    /// (<paramref name="builtIns"/> oder <see cref="ScriptCheckRules.ExtraBuiltins"/>) noch ein Schlüsselwort der Regeldaten
    /// (<see cref="ScriptCheckRules.TransparentKeys"/>, <see cref="ScriptCheckRules.ScriptContextKeys"/>, <see cref="ScriptCheckRules.CallContextKeys"/>, <see cref="ScriptCheckRules.EventCallKeys"/>) noch Parameter
    /// eines Skripteffekt-/Skripttrigger-Aufrufs ist (direkter Elternschlüssel ist ein definierter Effekt/Trigger); ein Fund je Name am ersten Aufruf.
    /// </summary>
    private static IEnumerable<Finding> BrokenBlocks(ScriptCheckIndex index, List<Located> definitions, IReadOnlySet<string> builtIns, ScriptCheckRules rules)
    {
        var scripted = BlockNames(definitions);
        var known = new HashSet<string>(scripted, StringComparer.Ordinal);
        known.UnionWith(definitions.Where(d => d.Definition.Kind == CheckKind.Named).Select(d => d.Definition.Name));
        known.UnionWith(rules.ExtraBuiltins);
        // Schlüsselwörter der Regeldaten (durchsichtig, Skript- und Aufrufkontext, Event-Aufruf) sind nie Skripteffekte.
        known.UnionWith(rules.TransparentKeys);
        known.UnionWith(rules.ScriptContextKeys);
        known.UnionWith(rules.CallContextKeys);
        known.UnionWith(rules.EventCallKeys);
        return index.ModBlockCalls
            .Where(c => !c.Ref.Target.Contains('$') && !known.Contains(c.Ref.Target) && !builtIns.Contains(c.Ref.Target))
            // Ein Schlüssel nur aus Großbuchstaben ist ein Parameter (AVOID_STORMS = no) oder ein Scope (HOI IV GER = { NOR_AAT = yes }), nie ein undefinierter Effekt;
            // ein definierter Effekt mit so einem Namen ist schon über known erlaubt.
            .Where(c => !ScriptCheckReader.IsParameterName(c.Ref.Target))
            // Kinder eines Aufrufs eines Skripteffekts/-triggers sind seine Parameter (auch klein geschrieben: pentarchy = yes).
            .Where(c => c.Ref.Parent is not { } parent || !scripted.Contains(parent))
            .GroupBy(c => c.Ref.Target, StringComparer.Ordinal)
            .Select(g => Broken(g.ToList(), CheckKind.Effect));
    }

    private static Finding Broken(List<LocatedRef> calls, CheckKind kind) =>
        Finding.Create(FindingKind.BrokenReference, FindingSeverity.Error, calls[0].Ref.Target, kind, calls[0].Location, calls.Select(Caller).ToList());

    /// <summary>
    /// Events der Mod, die sich nicht selbst auslösen und von keinem anderen Besitzer aufgerufen oder erwähnt werden; ein Fund je Schlüssel.
    /// Ein Aufruf mit Parameter (<c>ns.$X$</c>, irgendeine Datei) zählt als Aufruf jedes Events des Namespace.
    /// </summary>
    private static IEnumerable<Finding> DeadEvents(ScriptCheckIndex index, List<Located> modDefinitions, HashSet<string> foreignEvents)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var parameterised = index.EventCalls
            .Where(c => c.Ref.Target.Contains('$'))
            .Select(c => EventIds.Namespace(c.Ref.Target))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        foreach (var r in index.EventCalls.Concat(index.Mentions).Select(l => l.Ref).Where(r => r.TargetKind == CheckKind.Event && !r.Target.Contains('$')))
        {
            var key = EventIds.Key(r.Target);
            var self = r.OwnerKind == CheckKind.Event && r.Owner is not null && EventIds.Key(r.Owner) == key;
            if (!self)
            {
                used.Add(key);
            }
        }

        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in modDefinitions.Where(d => d.Definition.Kind == CheckKind.Event && !d.Definition.SelfFiring))
        {
            var key = EventIds.Key(d.Definition.Name);
            var namespaceUsed = EventIds.Namespace(d.Definition.Name) is { } ns && parameterised.Contains(ns);
            if (!used.Contains(key) && !namespaceUsed && !foreignEvents.Contains(key) && reported.Add(key))
            {
                yield return Finding.Create(FindingKind.DeadEvent, FindingSeverity.Warning, d.Definition.Name, CheckKind.Event, d.Location, Array.Empty<string>());
            }
        }
    }

    /// <summary>
    /// Effekte, Trigger und Skriptwerte der Mod ohne Erwähnung von einem anderen Besitzer; ein Fund je Art und Name.
    /// Eine Erwähnung zählt für jede Art des Namens (ein Name, der Effekt und Trigger ist, gilt für beide als benutzt).
    /// Passt eine Vorlage (<c>rvt_tmpl_$X$_effect</c>, irgendeine Datei, außerhalb der Definition selbst) auf den Namen, gilt er ebenfalls als benutzt.
    /// </summary>
    private static IEnumerable<Finding> UnusedBlocks(ScriptCheckIndex index, List<Located> modDefinitions, HashSet<string> foreignBlocks)
    {
        // Nur die sonst unbenutzten Kandidaten werden gegen die Vorlagen geprüft.
        var templates = new Lazy<TemplateMatcher>(() => new TemplateMatcher(index.Files.SelectMany(f => f.Scan.Templates)));
        var mentions = index.Mentions
            .Select(m => m.Ref)
            .Where(r => r.TargetKind != CheckKind.Event)
            .ToLookup(r => r.Target, StringComparer.Ordinal);
        var reported = new HashSet<(CheckKind, string)>();
        foreach (var d in modDefinitions.Where(d => IsCheckedBlock(d.Definition.Kind) && !foreignBlocks.Contains(d.Definition.Name)))
        {
            var (name, kind) = (d.Definition.Name, d.Definition.Kind);
            var used = mentions[name].Any(r => !(r.Owner == name && r.OwnerKind == kind));
            if (!used && !reported.Contains((kind, name)) && !templates.Value.Uses(name, kind))
            {
                reported.Add((kind, name));
                yield return Finding.Create(FindingKind.UnusedBlock, FindingSeverity.Warning, name, kind, d.Location, Array.Empty<string>());
            }
        }
    }

    /// <summary>
    /// Event-Definitionen mit gleichem Schlüssel: je Definition der Mod ein Fund, wenn eine andere Definition der Gruppe nicht aus Vanilla stammt
    /// (Mod gegen Vanilla ist ein gewolltes Überschreiben). Related = alle Orte der Gruppe, der nach <see cref="FlagSourceSet.Wins"/> gewinnende zuerst.
    /// </summary>
    private static IEnumerable<Finding> DuplicateEvents(FlagSourceSet sources, List<Located> definitions, Func<SiteLocation, bool> isMod)
    {
        var groups = definitions
            // Weiche Definitionen (inline_script-Parameter) sind oft nur Verweise (LOCALIZATION_ID, STAGE_1_EVENT_ID) und zählen nicht.
            .Where(d => d.Definition.Kind == CheckKind.Event && !d.Definition.Soft)
            .GroupBy(d => EventIds.Key(d.Definition.Name), StringComparer.Ordinal)
            .Where(g => g.Count() >= 2);
        foreach (var group in groups)
        {
            var members = group.ToList();
            List<string>? related = null;
            foreach (var d in members.Where(d => isMod(d.Location)))
            {
                if (!members.Any(o => !ReferenceEquals(o, d) && o.File.Origin != FlagOrigin.Vanilla))
                {
                    continue;
                }

                related ??= WinnerFirst(sources, members).Select(o => $"{o.Location.RelativePath}:{o.Location.Line} ({o.File.OriginName})").ToList();
                yield return Finding.Create(FindingKind.DuplicateEvent, FindingSeverity.Warning, d.Definition.Name, CheckKind.Event, d.Location, related);
            }
        }
    }

    /// <summary>Die Definition, die alle anderen schlägt, zuerst (Gleichstand: die erste); danach die übrigen in Dateireihenfolge.</summary>
    private static IEnumerable<Located> WinnerFirst(FlagSourceSet sources, List<Located> members)
    {
        var winner = members.FirstOrDefault(c => members.All(o => ReferenceEquals(o, c) || sources.Wins(c.Location, o.Location))) ?? members[0];
        return members.Where(m => !ReferenceEquals(m, winner)).Prepend(winner);
    }

    /// <summary>
    /// Zyklen zwischen Effekten und Triggern: Kanten aus den Aufrufformen der Mod und aus Erwähnungen in Aufrufform (Besitzer → Ziel, beide Effekt oder Trigger).
    /// Ohne Bedingung auf einem Kreis ein Fehler, sonst ein Hinweis; nur Komponenten mit einer Definition der Mod.
    /// </summary>
    private static IEnumerable<Finding> EffectCycles(ScriptCheckIndex index, List<Located> definitions, List<Located> modDefinitions)
    {
        var modBlocks = FirstByName(modDefinitions.Where(d => IsBlock(d.Definition.Kind)), d => d.Definition.Name);
        if (modBlocks.Count == 0)
        {
            yield break;
        }

        var defined = BlockNames(definitions);
        var edges = index.ModBlockCalls
            .Select(c => c.Ref)
            .Where(r => r.Owner is not null && IsBlock(r.OwnerKind) && defined.Contains(r.Target))
            .Concat(index.Mentions.Select(m => m.Ref).Where(r => r.CallForm && r.Owner is not null && IsBlock(r.OwnerKind) && IsBlock(r.TargetKind)))
            .Select(r => (r.Owner!, r.Target, r.Conditional))
            .ToList();

        foreach (var group in CycleFinder.Find(edges))
        {
            var first = group.Members.Select(m => modBlocks.GetValueOrDefault(m)).FirstOrDefault(d => d is not null);
            if (first is not null)
            {
                var severity = group.Unconditional ? FindingSeverity.Error : FindingSeverity.Hint;
                yield return Finding.Create(
                    FindingKind.EffectCycle, severity, string.Join(CycleSeparator, group.Members), first.Definition.Kind, first.Location, group.Members);
            }
        }
    }

    /// <summary>Zyklen zwischen Events über Event-Aufrufe aus Events (immer ein Hinweis); nur Komponenten mit einem Event der Mod.</summary>
    private static IEnumerable<Finding> EventCycles(ScriptCheckIndex index, List<Located> definitions, List<Located> modDefinitions)
    {
        var modEvents = FirstByName(modDefinitions.Where(d => d.Definition.Kind == CheckKind.Event), d => EventIds.Key(d.Definition.Name));
        if (modEvents.Count == 0)
        {
            yield break;
        }

        var edges = index.EventCalls
            .Select(c => c.Ref)
            .Where(r => r.OwnerKind == CheckKind.Event && r.Owner is not null && !r.Target.Contains('$'))
            .Select(r => (EventIds.Key(r.Owner!), EventIds.Key(r.Target), r.Conditional))
            .ToList();

        // Anzeigename je Schlüssel: der Name der ersten Definition der Mod, sonst der ersten Definition überhaupt, sonst der Schlüssel.
        var anyEvents = FirstByName(definitions.Where(d => d.Definition.Kind == CheckKind.Event), d => EventIds.Key(d.Definition.Name));
        string Display(string key) => (modEvents.GetValueOrDefault(key) ?? anyEvents.GetValueOrDefault(key))?.Definition.Name ?? key;

        foreach (var group in CycleFinder.Find(edges))
        {
            var members = group.Members.Select(Display).OrderBy(n => n, StringComparer.Ordinal).ToList();
            var first = members.Select(m => modEvents.GetValueOrDefault(EventIds.Key(m))).FirstOrDefault(d => d is not null);
            if (first is not null)
            {
                yield return Finding.Create(
                    FindingKind.EventCycle, FindingSeverity.Hint, string.Join(CycleSeparator, members), CheckKind.Event, first.Location, members);
            }
        }
    }

    private static bool IsBlock(CheckKind kind) => kind is CheckKind.Effect or CheckKind.Trigger;

    /// <summary>Arten, die als unbenutzt gemeldet werden können.</summary>
    private static bool IsCheckedBlock(CheckKind kind) => kind is CheckKind.Effect or CheckKind.Trigger or CheckKind.Value;

    /// <summary>Die erste Definition je Schlüssel (in Dateireihenfolge).</summary>
    private static Dictionary<string, Located> FirstByName(IEnumerable<Located> definitions, Func<Located, string> key)
    {
        var result = new Dictionary<string, Located>(StringComparer.Ordinal);
        foreach (var d in definitions)
        {
            result.TryAdd(key(d), d);
        }

        return result;
    }

    private static HashSet<string> BlockNames(List<Located> definitions) =>
        definitions.Where(d => IsBlock(d.Definition.Kind)).Select(d => d.Definition.Name).ToHashSet(StringComparer.Ordinal);

    private static string Caller(LocatedRef call) =>
        call.Ref.Owner is { } owner
            ? $"{owner} ({call.Location.RelativePath}:{call.Location.Line})"
            : $"{call.Location.RelativePath}:{call.Location.Line}";
}
