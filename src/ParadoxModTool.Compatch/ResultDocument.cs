using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

public enum DocLineKind
{
    /// <summary>Kopf einer Adresse (Schlüssel, "Rest der Datei", "ganze Datei").</summary>
    Header,

    /// <summary>Eingebettete Auswahlzeile einer Aktion, bei der A und B verschieden sind (Ruling 2).</summary>
    Choice,

    /// <summary>Eine Zeile des Ergebnisses.</summary>
    Code,

    /// <summary>Ganze Datei: gewählte Seite.</summary>
    WholeFile,

    /// <summary>Die Adresse fehlt im Ergebnis.</summary>
    Removed,
}

/// <summary>Eine Zeile des Ergebnisdokuments. <see cref="ActionId"/>: bei <see cref="DocLineKind.Choice"/> die Aktion der Zeile, sonst die erste Aktion der Adresse.</summary>
public sealed record DocLine(DocLineKind Kind, string Text, RowOrigin Origin, EntryAddress Address, string ActionId);

/// <summary>Ansicht B: das Ergebnis einer Datei (Plan 5, Ruling 4) als Zeilen mit Herkunft. Baut immer nur ein Dokument.</summary>
public static class ResultDocument
{
    public static IReadOnlyList<string> Files(IEnumerable<MergeAction> actions) => actions
        .Select(TreePath.FileOf)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(f => f, TreePath.Comparer)
        .ToList();

    /// <param name="hideComments">Kommentarzeilen ausblenden (nur Anzeige, siehe <see cref="ActionText.StripCommentLines"/>).</param>
    public static IReadOnlyList<DocLine> Build(CompatchRun run, string file, bool hideComments = false)
    {
        var lines = new List<DocLine>();
        var groups = run.Actions
            .Where(a => string.Equals(TreePath.FileOf(a), file, StringComparison.OrdinalIgnoreCase))
            .GroupBy(a => a.Address)
            .OrderBy(g => g.Key.Kind)
            .ThenBy(g => g.Key.Key, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var address = group.Key;
            var actions = group.OrderBy(a => a.DisplayPath, StringComparer.Ordinal).ToList();
            var first = actions[0].Id;
            var header = address.Kind switch
            {
                AddressKind.Key => address.Key,
                AddressKind.FileRest => CompatchTexts.DocFileRest,
                _ => CompatchTexts.DocWholeFile,
            };
            lines.Add(new DocLine(DocLineKind.Header, header, RowOrigin.Same, address, first));
            foreach (var action in actions.Where(a => a.Origin is null))
            {
                lines.Add(new DocLine(DocLineKind.Choice, action.DisplayPath, RowOrigin.Other, address, action.Id));
            }

            run.Applied.Desired.TryGetValue(address, out var desired);
            if (address.Kind == AddressKind.WholeFile)
            {
                var (kind, origin) = desired?.FileChoice switch
                {
                    MergeSide.A => (DecisionKind.TakeA, RowOrigin.FromA),
                    MergeSide.B => (DecisionKind.TakeB, RowOrigin.FromB),
                    _ => (DecisionKind.TakeVanilla, RowOrigin.Other),
                };
                lines.Add(new DocLine(DocLineKind.WholeFile, CompatchTexts.WholeFileChoice(ActionText.SideName(kind)), origin, address, first));
                continue;
            }

            var entries = desired?.Entries ?? Array.Empty<ScriptEntry>();
            if (entries.Count == 0)
            {
                lines.Add(new DocLine(DocLineKind.Removed, CompatchTexts.DocRemoved, RowOrigin.Other, address, first));
                continue;
            }

            var info = run.Resolved.Addresses[address];
            foreach (var row in ColumnAligner.Align(ActionText.Render(info.A.Entries, hideComments), ActionText.Render(entries, hideComments), ActionText.Render(info.B.Entries, hideComments), null))
            {
                if (row.Result is not null)
                {
                    lines.Add(new DocLine(DocLineKind.Code, row.Result, row.Origin, address, first));
                }
            }
        }

        return lines;
    }

    /// <summary>Zeile, zu der ◀ ▶ springt: die Auswahlzeile der Aktion, sonst der Kopf ihrer Adresse; -1, wenn die Aktion nicht im Dokument steht.</summary>
    public static int LineOf(IReadOnlyList<DocLine> lines, MergeAction action)
    {
        var header = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Kind == DocLineKind.Choice && lines[i].ActionId == action.Id)
            {
                return i;
            }

            if (header < 0 && lines[i].Kind == DocLineKind.Header && lines[i].Address == action.Address)
            {
                header = i;
            }
        }

        return header;
    }
}
