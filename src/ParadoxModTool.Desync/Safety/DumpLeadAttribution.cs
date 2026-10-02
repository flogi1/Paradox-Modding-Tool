using ParadoxModTool.ErrorLog;

namespace ParadoxModTool.Desync;

/// <summary>
/// Ordnet die Log-Hinweise eines OOS den Mods aus dem Dump zu (Modus B der MP-Prüfung, siehe Entwurf, Abschnitt "Mode B: ranking"),
/// nicht dem lokalen Playset: Die Namen der Dump-Mods werden den installierten Mods zugeordnet, ihre Reihenfolge ist die Ladereihenfolge
/// im Dump (später gewinnt). Spieldateien werden nicht gesucht.
/// </summary>
public static class DumpLeadAttribution
{
    /// <summary>
    /// Die Mod-Namen des Dumps ohne Doppelte (Schreibung egal): zuerst die Liste der Referenz-Maschine in ihrer Reihenfolge (die
    /// Ladereihenfolge), danach die Mods, die nur andere Maschinen hatten, nach Maschinennummer. Gibt es die Referenz-Maschine nicht, gilt
    /// die Reihenfolge der Maschinen in <paramref name="summary"/>.
    /// </summary>
    public static IReadOnlyList<string> ModOrder(OosSummary summary, int? referenceMachineId)
    {
        var machines = summary.Machines.Where(m => m.MachineId == referenceMachineId)
            .Concat(summary.Machines.Where(m => m.MachineId != referenceMachineId));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();
        foreach (var mod in machines.SelectMany(m => m.Mods))
        {
            if (seen.Add(mod.Name))
            {
                names.Add(mod.Name);
            }
        }

        return names;
    }

    /// <summary>Der installierte Mod zu einem Namen aus dem Dump (Schreibung egal); bei gleichem Namen geht ein Mod des Playsets vor. <c>null</c>: nicht installiert.</summary>
    public static ModSource? Installed(string name, IReadOnlyList<ModSource> installed) =>
        installed
            .Where(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.InPlayset)
            .FirstOrDefault();

    /// <summary>
    /// Zuordnung für <see cref="ReportLeads.From"/>: Ein Fehler mit Dateiangabe gehört zu dem Dump-Mod, der die Datei hat und im Dump am
    /// spätesten geladen wird (Phasenordner wie bei <see cref="SourceResolver"/>); Dump-Mods ohne installierten Mod entfallen, ob ein Mod im
    /// lokalen Playset ist, zählt nicht. Ohne Dateiangabe oder ohne passende Datei gibt es keine Zuordnung.
    /// <paramref name="fileExists"/> ersetzt <see cref="File.Exists(string)"/> in Tests.
    /// </summary>
    public static Func<ErrorEntry, LeadAttribution> For(
        IReadOnlyList<string> dumpModNames,
        IReadOnlyList<ModSource> installed,
        Func<string, bool>? fileExists = null)
    {
        var mods = new List<ModSource>();
        for (var i = 0; i < dumpModNames.Count; i++)
        {
            if (Installed(dumpModNames[i], installed) is { } mod)
            {
                // Die Stelle im Dump ist die Ladereihenfolge; damit zählt jeder zugeordnete Mod als "im Playset".
                mods.Add(mod with { LoadOrder = i });
            }
        }

        // Keine Spielordner; die Änderungszeit spielt keine Rolle (nie "nach dem Lauf geändert", ohne Zugriff auf die Platte).
        var resolver = new SourceResolver(mods, Array.Empty<string>(), DateTime.MaxValue, fileExists, _ => DateTime.MinValue);
        var none = new LeadAttribution(null, false, null);
        return error => error.FilePath is { } path && resolver.Resolve(path) is { Mod: { } mod } source
            ? new LeadAttribution(mod.Name, mod.IsOwn, source.FullPath)
            : none;
    }
}
