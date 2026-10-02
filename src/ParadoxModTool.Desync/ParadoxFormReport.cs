using System.Globalization;
using static ParadoxModTool.Desync.ReportFormat;

namespace ParadoxModTool.Desync;

/// <summary>Ein ausgefülltes Feld des Paradox-Formulars; <paramref name="Attachments"/> nur bei <see cref="FormFieldKind.Attachments"/>.</summary>
public sealed record ParadoxFormEntry(string Label, FormFieldKind Kind, string Value, IReadOnlyList<ReportDump> Attachments);

/// <summary>
/// Baut den Bericht für das Paradox-Fehlerformular aus den Tatsachen. Welche Felder es gibt und in welcher Reihenfolge,
/// bestimmt das Profil; dieser Code liefert nur den Inhalt je <see cref="FormSource"/>. Alle Texte sind Englisch und werden nie übersetzt.
/// </summary>
public static class ParadoxFormReport
{
    /// <summary>Längste Zusammenfassung; längere wird auf 149 Zeichen gekürzt und mit einer Ellipse beendet.</summary>
    public const int MaxSummaryLength = 150;

    private const string EnDash = "–";

    /// <summary>Die Felder des Formulars in Profilreihenfolge; einzeilige Werte haben die Zeilenumbrüche durch Leerzeichen ersetzt.</summary>
    public static IReadOnlyList<ParadoxFormEntry> Build(ReportFacts facts, IReadOnlyList<ParadoxFormField> form) =>
        form.Select(field =>
        {
            var value = ValueOf(facts, field);
            if (field.Kind != FormFieldKind.Multiline)
                value = OneLine(value);
            var attachments = field.Source == FormSource.Dumps ? facts.Dumps : [];
            return new ParadoxFormEntry(field.Label, field.Kind, value, attachments);
        }).ToList();

    /// <summary>
    /// Die einzeilige Zusammenfassung, höchstens <see cref="MaxSummaryLength"/> Zeichen. Sie nennt die erste Ursache mit Pfad;
    /// die oberste Ebene (leerer Pfad) sagt dort nichts und wird übersprungen.
    /// </summary>
    public static string SummaryLine(ReportFacts facts)
    {
        var summary = facts.Summary;
        var categories = summary.ChecksumCategories.Count > 0 ? $" ({string.Join(", ", summary.ChecksumCategories)})" : "";
        var date = GameDate(summary) is { } known ? known : "an unknown date";
        var line = $"Out of Sync{categories} at {date} in multiplayer";
        if (facts.Causes.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.DisplayPath)) is { } cause)
            line += $" {Dash} {cause.DisplayPath} diverges";

        return Truncate(OneLine(line), MaxSummaryLength);
    }

    private static string ValueOf(ReportFacts facts, ParadoxFormField field) => field.Source switch
    {
        FormSource.VerifiedFiles =>
            "Verify the game files in Steam (Properties > Installed Files > Verify integrity) on every machine before posting, then select Yes.",
        // Ohne den Dump einer Maschine ist nicht belegt, dass sie ohne Mods lief.
        FormSource.ModsDisabled => facts.Summary.Machines.Any(m => m.Mods.Count > 0) || facts.Dumps.Any(d => d.Missing) ? "No" : "Yes",
        FormSource.Summary => SummaryLine(facts),
        FormSource.Description => Description(facts),
        FormSource.IssueType => "Pick the type closest to the affected feature; there is no dedicated type for desyncs.",
        FormSource.Steps => Steps(facts),
        FormSource.GameVersion => GameVersion(facts),
        FormSource.Fixed => field.Value ?? "",
        FormSource.Dumps => "Attach both OOS dump folders as zip files.",
        FormSource.Screenshot => "Optional: a screenshot of the Out of Sync message.",
        _ => "",
    };

    private static string Number(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? Unknown;

    private static string GameVersion(ReportFacts facts)
    {
        // Je verschiedener Version die erste Maschine (Referenz zuerst); Maschinen ohne Version zählen nicht.
        var versions = ReferenceFirst(facts.Summary)
            .Where(m => !string.IsNullOrEmpty(m.Save.Version))
            .GroupBy(m => m.Save.Version!, StringComparer.Ordinal)
            .Select(g => (Version: g.Key, MachineId: g.First().MachineId))
            .ToList();

        return versions.Count switch
        {
            0 => Unknown,
            1 => versions[0].Version,
            _ => string.Join(" / ", versions.Select(v => $"{v.Version} (machine {v.MachineId})")) + $" {Dash} versions differ",
        };
    }

    private static string Steps(ReportFacts facts)
    {
        var how = string.Equals(facts.Summary.SessionKind, "hotjoin", StringComparison.Ordinal)
            ? "player joined during the game (hotjoin)"
            : "players joined in the lobby";
        return string.Join("\n",
            $"1. Host a multiplayer game with {facts.Summary.Machines.Count} players ({how}).",
            "2. Play at speed <game speed> until <what happened right before the desync>.",
            $"3. Out of Sync is detected at game date {GameDate(facts.Summary) ?? Unknown}.",
            "4. <Does it happen again after rehosting or reloading? yes/no>");
    }

    private static string Description(ReportFacts facts)
    {
        var summary = facts.Summary;
        var sections = new List<List<string>>();

        var head = new List<string>
        {
            $"Out of Sync in multiplayer (session start: {summary.SessionKind ?? Unknown}).",
            $"OOS id: {summary.OosIdFull ?? summary.OosId16}. Detected at game date {GameDate(summary) ?? Unknown}, " +
            $"frames {Number(summary.StartFrame)}{EnDash}{Number(summary.OosFrame)}.",
        };
        if (summary.ChecksumCategories.Count > 0)
            head.Add($"Checksum categories that differed: {string.Join(", ", summary.ChecksumCategories)}.");
        head.AddRange(facts.ChecksumExplanations);
        sections.Add(head);

        var environment = EnvironmentLines(summary);
        if (environment.Count > 0)
            sections.Add(["Environment:", .. environment]);

        sections.Add(!facts.DiffAvailable ? ["State comparison not run."]
            : facts.Causes.Count == 0 ? ["State comparison found no cause candidates."]
            : ["State comparison (top cause candidates, reference vs desynced):", .. facts.Causes.Select(CauseLine)]);

        sections.Add(facts.Leads.Count > 0
            ? ["Leads in the 7 game days before the OOS:", .. facts.Leads.Select(LeadLine)]
            : ["No leads in the 7 game days before the OOS."]);

        sections.Add([DumpsLine(facts.Dumps)]);

        return string.Join("\n\n", sections.Select(lines => string.Join("\n", lines)));
    }

    private static List<string> EnvironmentLines(OosSummary summary)
    {
        var lines = new List<string>();
        foreach (var machine in summary.Machines.OrderBy(m => m.MachineId))
        {
            var who = (machine.Role ?? "unknown role") + (string.IsNullOrEmpty(machine.PlayerName) ? "" : ", " + machine.PlayerName);
            lines.Add($"- Machine {machine.MachineId} ({who}): version {machine.Save.Version ?? Unknown}, " +
                      $"game {Commit(machine.GameCommit)}, engine {Commit(machine.EngineCommit)}");
            lines.Add("  DLCs: " + (machine.Save.EnabledDlcs.Count > 0 ? string.Join(", ", machine.Save.EnabledDlcs) : "none"));
            lines.Add("  Mods: " + (machine.Mods.Count > 0 ? string.Join(", ", machine.Mods.Select(ModText)) : "none"));
        }

        lines.AddRange(summary.Warnings.Select(w => $"- Difference: {WarningWords(w.Kind)}: {OneLine(w.Detail)}"));
        return lines;
    }

    private static string WarningWords(SummaryWarningKind kind) => kind switch
    {
        SummaryWarningKind.DifferentGameCommit => "game build",
        SummaryWarningKind.DifferentEngineCommit => "engine build",
        SummaryWarningKind.DifferentDlcs => "DLCs",
        SummaryWarningKind.DifferentMods => "mods",
        SummaryWarningKind.MissingMachine => "missing machine",
        _ => kind.ToString(),
    };

    private static string CauseLine(ReportCause cause)
    {
        var line = $"- {CausePath(cause.DisplayPath)} ({ChangeWords(cause.Change)})";
        if (cause.Fields.Count == 0)
            return line;

        var fields = cause.Fields.Select(f =>
            $"{(f.Path.Length > 0 ? f.Path : "(unit)")} = {OneLine(f.ReferenceValue ?? "(absent)")} vs {OneLine(f.DesyncedValue ?? "(absent)")}");
        return line + ": " + string.Join("; ", fields);
    }

    private static string LeadLine(ReportLead lead)
    {
        var location = lead.File is null
            ? ""
            : $" ({lead.File}{(lead.Line is { } line ? ":" + line.ToString(CultureInfo.InvariantCulture) : "")})";
        var mod = lead.ModName is null ? "" : $" [{lead.ModName}]";
        return $"- {lead.GameDate ?? "unknown date"} {Machines(lead.MachineIds)}: {OneLine(lead.Message)}{location}{mod}";
    }

    private static string DumpsLine(IReadOnlyList<ReportDump> dumps)
    {
        var available = dumps.Where(d => !d.Missing).Select(d => d.MachineId).Distinct().OrderBy(id => id).ToList();
        var missing = dumps.Where(d => d.Missing).Select(d => d.MachineId).Distinct().OrderBy(id => id).ToList();
        if (available.Count == 0)
            return "No OOS dump is available.";
        if (missing.Count == 0)
            return "Both OOS dumps (all machines) are attached.";
        return $"Only the dump of machine {string.Join(", ", available)} is available; the dump of machine {string.Join(", ", missing)} is missing.";
    }
}
