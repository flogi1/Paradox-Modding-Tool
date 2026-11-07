using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;

namespace ParadoxModTool.ScriptCheck;

/// <summary>Eine Zeile der Befundliste.</summary>
/// <param name="Place"><c>relativer/pfad:zeile</c>.</param>
/// <param name="Suggestion">Nur bei teuren Iteratoren: die Vorschläge, durch Zeilenumbruch getrennt (Tooltip).</param>
/// <param name="Related">Wer ruft oder wird gerufen, gekürzt; bei toten Events und ungenutzten Bausteinen <see cref="ScriptCheckTexts.NobodyCalls"/>.</param>
public sealed record FindingRow(Finding Finding, string KindText, string SeverityText, string Name, string Place, string Related, bool IsHidden, string? Suggestion = null);

/// <summary>Aufrufer und Gerufene eines Namens.</summary>
/// <param name="OnlyEventCallers">Der Name ist keine Definition der gewählten Mod: Von seinen Aufrufern sind nur die Event-Aufrufe bekannt.</param>
public sealed record CallsView(IReadOnlyList<CallRow> Callers, IReadOnlyList<CallRow> Callees, bool OnlyEventCallers);

/// <summary>Ob das Ergebnis einer Prüfung zur gewählten Mod gezeigt werden darf.</summary>
public enum ScriptCheckResultState
{
    /// <summary>Noch kein Ergebnis.</summary>
    None,

    /// <summary>Ergebnis der gewählten Mod, mit Vanilla.</summary>
    Usable,

    /// <summary>Das Ergebnis gehört zu einer anderen Mod (Wechsel, der neue Aufbau läuft oder wurde abgebrochen).</summary>
    OtherMod,

    /// <summary>Ohne Spielordner gilt jeder Aufruf nach Vanilla als "ins Leere": lauter falsche Fehler.</summary>
    NoVanilla,
}

/// <summary>Anzeige-Logik des Fensters "Skript-Prüfung", ohne Oberfläche.</summary>
public static class ScriptCheckView
{
    /// <summary>So viele Einträge der Spalte "Wer ruft" werden gezeigt, der Rest nur gezählt.</summary>
    private const int MaxRelated = 5;

    /// <summary>
    /// Zeilen in der Reihenfolge der Funde. Ausgeblendete fallen weg, außer bei <paramref name="showHidden"/>; <paramref name="kind"/> beschränkt
    /// auf eine Art; <paramref name="query"/> sucht ohne Rücksicht auf Groß/Klein in Name und Ort.
    /// </summary>
    public static IReadOnlyList<FindingRow> Rows(IReadOnlyList<Finding> findings, ScriptCheckHideList? hide, bool showHidden, FindingKind? kind, string query)
    {
        var rows = new List<FindingRow>();
        var text = query.Trim();
        foreach (var finding in findings)
        {
            var hidden = hide?.IsHidden(finding) ?? false;
            if ((hidden && !showHidden) || (kind is not null && finding.Kind != kind))
            {
                continue;
            }

            var place = finding.Location.RelativePath + ":" + finding.Location.Line;
            if (text.Length > 0
                && !finding.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                && !place.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var kindText = ScriptCheckTexts.Kind(finding.Kind);
            if (finding is { Kind: FindingKind.ExpensiveIterator, Performance: { } perf })
            {
                var nested = perf.Inner is null ? kindText : kindText + " · " + ScriptCheckTexts.PerfNested;
                var suggestion = string.Join("\n", PerformanceTexts.Suggestions(finding));
                rows.Add(new FindingRow(finding, nested, ScriptCheckTexts.Severity(finding.Severity), finding.Name, place, PerformanceTexts.Path(perf), hidden, suggestion));
                continue;
            }

            rows.Add(new FindingRow(finding, kindText, ScriptCheckTexts.Severity(finding.Severity), finding.Name, place, RelatedText(finding), hidden));
        }

        return rows;
    }

    /// <summary>Filter für die Knöpfe: zuerst "Alle", dann jede Art mit mindestens einem sichtbaren Fund, in der Reihenfolge von <see cref="FindingKind"/>.</summary>
    public static IReadOnlyList<(FindingKind? Kind, string Text, int Count)> KindFilters(IReadOnlyList<Finding> findings, ScriptCheckHideList? hide, bool showHidden)
    {
        var visible = findings.Where(f => showHidden || hide is null || !hide.IsHidden(f)).ToList();
        var filters = new List<(FindingKind? Kind, string Text, int Count)> { (null, ScriptCheckTexts.FilterAll, visible.Count) };
        foreach (var kind in Enum.GetValues<FindingKind>())
        {
            var count = visible.Count(f => f.Kind == kind);
            if (count > 0)
            {
                filters.Add((kind, ScriptCheckTexts.Kind(kind), count));
            }
        }

        return filters;
    }

    /// <summary>Darf <paramref name="index"/> für <paramref name="chosenMod"/> gezeigt werden?</summary>
    public static ScriptCheckResultState StateOf(ScriptCheckIndex? index, string? chosenMod)
    {
        if (index is null)
        {
            return ScriptCheckResultState.None;
        }

        if (index.ModRoot is not { } mod || string.IsNullOrWhiteSpace(chosenMod)
            || !string.Equals(ErrorLogSources.Key(mod.Directory), ErrorLogSources.Key(chosenMod), StringComparison.OrdinalIgnoreCase))
        {
            return ScriptCheckResultState.OtherMod;
        }

        return index.Sources.Roots.Any(r => r.Origin == FlagOrigin.Vanilla) ? ScriptCheckResultState.Usable : ScriptCheckResultState.NoVanilla;
    }

    /// <summary>Name für "Wer ruft wen": bei Zirkeln das erste Mitglied, denn der Name eines Zirkels ist die ganze Kette.</summary>
    public static string CallsName(Finding finding) =>
        finding.Kind is FindingKind.EffectCycle or FindingKind.EventCycle && finding.Related.Count > 0 ? finding.Related[0] : finding.Name;

    /// <summary>Aufrufer und Gerufene von <paramref name="name"/>.</summary>
    public static CallsView Calls(ScriptCheckIndex index, string name)
    {
        var inMod = index.ModRoot is { } mod
            && index.DefinitionsOf(name).Any(d => string.Equals(d.File.File.RootDirectory, mod.Directory, StringComparison.OrdinalIgnoreCase));
        return new CallsView(index.CallersOf(name), index.CalleesOf(name), !inMod);
    }

    private static string RelatedText(Finding finding)
    {
        if (finding.Related.Count == 0)
        {
            return finding.Kind is FindingKind.DeadEvent or FindingKind.UnusedBlock ? ScriptCheckTexts.NobodyCalls : string.Empty;
        }

        var shown = string.Join(", ", finding.Related.Take(MaxRelated));
        return finding.Related.Count > MaxRelated ? shown + ", … (+" + (finding.Related.Count - MaxRelated) + ")" : shown;
    }
}
