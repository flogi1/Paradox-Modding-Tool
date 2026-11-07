using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;

namespace ParadoxModTool.GfxAudit;

/// <summary>Eine Zeile der Befundliste.</summary>
/// <param name="Place"><c>relativer/pfad:zeile</c>, bei Zeile 0 (Bilder) nur der Pfad.</param>
/// <param name="Related">Wer benutzt oder worauf zeigt der Fund, gekürzt; bei ungenutzten Bildern und Sprites ohne Eintrag <see cref="GfxAuditTexts.NobodyUses"/>.</param>
/// <param name="IsImage">Ein ungenutztes Bild (keine Textstelle, Doppelklick zeigt die Datei im Explorer).</param>
public sealed record GfxFindingRow(GfxFinding Finding, string KindText, string SeverityText, string Name, string Place, string Related, bool IsHidden, bool IsImage);

/// <summary>Anzeige-Logik des Fensters "GFX-Prüfung", ohne Oberfläche.</summary>
public static class GfxAuditView
{
    /// <summary>So viele Einträge der Spalte "Benutzt von / zeigt auf" werden gezeigt, der Rest nur gezählt.</summary>
    private const int MaxRelated = 5;

    /// <summary>Höchstens so viele Pfade in der Rückfrage vor dem Auslagern.</summary>
    private const int MaxPathsInConfirm = 30;

    /// <summary>Höchstens so viele nicht verschobene Dateien im Tooltip einer Statuszeile.</summary>
    private const int MaxFailuresInTip = 40;

    /// <summary>Die Pfadliste der Rückfrage vor dem Auslagern: höchstens <see cref="MaxPathsInConfirm"/> Pfade je Zeile, dann <c>… (+N)</c>.</summary>
    public static string ConfirmList(IReadOnlyList<string> paths)
    {
        var lines = paths.Take(MaxPathsInConfirm).ToList();
        if (paths.Count > MaxPathsInConfirm)
        {
            lines.Add($"… (+{paths.Count - MaxPathsInConfirm})");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Tooltip einer Statuszeile nach Auslagern oder Zurückholen: je nicht verschobener Datei <c>Pfad: Grund</c>, bei
    /// <see cref="GfxArchiveFailureReason.IoError"/> mit der Meldung in Klammern; höchstens <see cref="MaxFailuresInTip"/> Zeilen, null ohne Fehler.
    /// </summary>
    public static string? FailureTip(IReadOnlyList<GfxArchiveFailure> failed) =>
        failed.Count == 0
            ? null
            : string.Join(
                Environment.NewLine,
                failed.Take(MaxFailuresInTip).Select(f => f.Relative + ": " + GfxAuditTexts.Failure(f.Reason) + (f.Message is { Length: > 0 } message ? " (" + message + ")" : string.Empty)));

    /// <summary>
    /// Zeilen in der Reihenfolge der Funde. Ausgeblendete (nach <see cref="GfxFinding.Key"/>, also alle Stellen eines Namens) fallen weg, außer bei
    /// <paramref name="showHidden"/>; <paramref name="kind"/> beschränkt auf eine Art; <paramref name="query"/> sucht ohne Rücksicht auf Groß/Klein
    /// in Name, Ort und Verwendung.
    /// </summary>
    public static IReadOnlyList<GfxFindingRow> Rows(IReadOnlyList<GfxFinding> findings, ScriptCheckHideList? hide, bool showHidden, GfxFindingKind? kind, string query)
    {
        var rows = new List<GfxFindingRow>();
        var text = query.Trim();
        foreach (var finding in findings)
        {
            var hidden = hide?.IsHidden(finding.Key) ?? false;
            if ((hidden && !showHidden) || (kind is not null && finding.Kind != kind))
            {
                continue;
            }

            var place = finding.Location.Line == 0 ? finding.Location.RelativePath : finding.Location.RelativePath + ":" + finding.Location.Line;
            var related = RelatedText(finding);
            if (text.Length > 0
                && !finding.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                && !place.Contains(text, StringComparison.OrdinalIgnoreCase)
                && !related.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            rows.Add(new GfxFindingRow(
                finding,
                GfxAuditTexts.Kind(finding.Kind),
                GfxAuditTexts.Severity(finding.Severity),
                finding.Name,
                place,
                related,
                hidden,
                finding.Kind == GfxFindingKind.UnusedImage));
        }

        return rows;
    }

    /// <summary>
    /// Ob die Quellen einen Vanilla-Ordner enthalten. Ohne ihn wären alle Vanilla-Sprites und -Texturen unbekannt: Die Funde wären falsch
    /// (benutzte Vanilla-Bilder als "fehlend", viele Bilder als "ungenutzt"), das Fenster zeigt dann keine Liste.
    /// </summary>
    public static bool HasVanilla(GfxAuditIndex? index) => index is not null && index.Sources.Roots.Any(r => r.Origin == FlagOrigin.Vanilla);

    /// <summary>
    /// Der Grund, warum "Auslagern" gerade gesperrt ist, oder null, wenn es erlaubt ist. Reihenfolge: Aufbau läuft, Mod nicht lokal, kein Vanilla-Ordner,
    /// nicht lesbare Dateien, Liste gehört nicht zur gewählten Mod. Ungenutzt ist nur dann sicher, wenn alles gelesen wurde.
    /// </summary>
    public static string? MoveOutBlock(bool building, bool isLocalMod, GfxAuditIndex? index, bool resultForChosenMod)
    {
        if (building)
        {
            return GfxAuditTexts.MoveBlockedBuilding;
        }

        if (!isLocalMod)
        {
            return GfxAuditTexts.MoveBlockedNotLocal;
        }

        if (!HasVanilla(index))
        {
            return GfxAuditTexts.MoveBlockedNoVanilla;
        }

        if (index!.UnreadFiles.Count > 0)
        {
            return GfxAuditTexts.MoveBlockedUnread(index.UnreadFiles.Count);
        }

        return resultForChosenMod ? null : GfxAuditTexts.MoveBlockedStale;
    }

    /// <summary>Filter für die Knöpfe: zuerst "Alle", dann jede Art mit mindestens einem sichtbaren Fund, in der Reihenfolge von <see cref="GfxFindingKind"/>.</summary>
    public static IReadOnlyList<(GfxFindingKind? Kind, string Text, int Count)> KindFilters(IReadOnlyList<GfxFinding> findings, ScriptCheckHideList? hide, bool showHidden)
    {
        var visible = findings.Where(f => showHidden || hide is null || !hide.IsHidden(f.Key)).ToList();
        var filters = new List<(GfxFindingKind? Kind, string Text, int Count)> { (null, GfxAuditTexts.FilterAll, visible.Count) };
        foreach (var kind in Enum.GetValues<GfxFindingKind>())
        {
            var count = visible.Count(f => f.Kind == kind);
            if (count > 0)
            {
                filters.Add((kind, GfxAuditTexts.Kind(kind), count));
            }
        }

        return filters;
    }

    private static string RelatedText(GfxFinding finding)
    {
        if (finding.Related.Count == 0)
        {
            return finding.Kind is GfxFindingKind.UnusedImage or GfxFindingKind.UnusedSprite ? GfxAuditTexts.NobodyUses : string.Empty;
        }

        var shown = string.Join(", ", finding.Related.Take(MaxRelated));
        return finding.Related.Count > MaxRelated ? shown + ", … (+" + (finding.Related.Count - MaxRelated) + ")" : shown;
    }
}
