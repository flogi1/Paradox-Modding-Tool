using System.Globalization;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Desync;

/// <summary>Gemeinsame Darstellung der Berichte (Paradox-Formular und Mod-Autor): Einzeiler, Kürzen, Datum, Commits, Mods. Alle Texte sind Englisch.</summary>
internal static class ReportFormat
{
    internal const string Dash = "—";
    internal const string Ellipsis = "…";
    internal const string Unknown = "unknown";
    internal const string TopLevel = "top level";

    private static readonly Regex LineBreaks = new(@"\s*[\r\n]+\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Entfernt Leerraum an den Rändern und ersetzt Zeilenumbrüche (samt umgebendem Leerraum) durch ein Leerzeichen.</summary>
    internal static string OneLine(string text) => LineBreaks.Replace(text.Trim(), " ");

    /// <summary>Der Pfad einer Ursache auf einer Zeile; die oberste Ebene (leerer Pfad) heißt "top level".</summary>
    internal static string CausePath(string displayPath) =>
        string.IsNullOrWhiteSpace(displayPath) ? TopLevel : OneLine(displayPath);

    /// <summary>Die Maschinen eines Hinweises: "machine 2" bei einer, "machines 1, 2" bei mehreren.</summary>
    internal static string Machines(IReadOnlyList<int> ids) =>
        (ids.Count == 1 ? "machine " : "machines ") + string.Join(", ", ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Kürzt auf höchstens <paramref name="maxLength"/> Zeichen; ein gekürzter Text endet mit einer Ellipse.</summary>
    internal static string Truncate(string text, int maxLength) =>
        text.Length > maxLength ? text[..(maxLength - 1)] + Ellipsis : text;

    /// <summary>Ein Spieldatum; ein leerer Text zählt wie ein fehlendes Datum (<c>null</c>).</summary>
    internal static string? GameDate(string? date) => date is { Length: > 0 } ? date : null;

    /// <summary>Das Spieldatum des OOS; ein leerer Text zählt wie ein fehlendes Datum (<c>null</c>).</summary>
    internal static string? GameDate(OosSummary summary) => GameDate(summary.OosGameDate);

    /// <summary>Die ersten 10 Zeichen eines Commits; ohne Commit "unknown".</summary>
    internal static string Commit(string? commit) =>
        string.IsNullOrEmpty(commit) ? Unknown : commit[..Math.Min(10, commit.Length)];

    /// <summary>Ein Mod mit Herkunft, z. B. "Name (Workshop 123)" oder "Name (local folder X)".</summary>
    internal static string ModText(DumpMod mod) => mod.Kind switch
    {
        ModSourceKind.Workshop => $"{mod.Name} (Workshop{(string.IsNullOrEmpty(mod.WorkshopId) ? "" : " " + mod.WorkshopId)})",
        ModSourceKind.Local => $"{mod.Name} (local{(string.IsNullOrEmpty(mod.Folder) ? "" : " folder " + mod.Folder)})",
        _ => mod.Name,
    };

    /// <summary>Die Referenz-Maschine zuerst (der Host, sonst die niedrigste Nummer), danach die übrigen nach Nummer.</summary>
    internal static List<MachineSummary> ReferenceFirst(OosSummary summary)
    {
        var byId = summary.Machines.OrderBy(m => m.MachineId).ToList();
        var reference = byId.FirstOrDefault(m => string.Equals(m.Role, "Host", StringComparison.Ordinal));
        if (reference is not null)
        {
            byId.Remove(reference);
            byId.Insert(0, reference);
        }

        return byId;
    }

    internal static string ChangeWords(UnitChange change) => change switch
    {
        UnitChange.Changed => "changed",
        UnitChange.OwnScalarsChanged => "own values changed",
        UnitChange.SlotReused => "slot reused",
        UnitChange.OnlyReference => "only in reference",
        UnitChange.OnlyDesynced => "only in desynced",
        UnitChange.TooLarge => "too large to compare",
        _ => change.ToString(),
    };
}
