using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ParadoxModTool.ErrorLog;

namespace ParadoxModTool.Desync;

/// <summary>
/// Kopfdaten eines OOS-Spielstands. Fehlende oder nicht lesbare Werte sind <c>null</c> bzw. eine leere Liste.
/// <see cref="RandomCount"/> ist der rohe Zählerstand: gleiche Zähler beider Maschinen beweisen nie, dass der Zufall nicht abweicht.
/// </summary>
/// <param name="Date">Wert von <c>date=</c> im <c>metadata</c>-Block, z. B. <c>1425.2.4.8</c>.</param>
/// <param name="Version">Wert von <c>version="…"</c> im <c>metadata</c>-Block, z. B. <c>1.0.3</c>.</param>
/// <param name="RandomSeed">Wert von <c>random_seed=</c> (oberste Ebene).</param>
/// <param name="RandomCount">Wert von <c>random_count=</c> (oberste Ebene).</param>
/// <param name="EnabledDlcs">Die Namen aus <c>enabled_dlcs={ … }</c>.</param>
public sealed record SaveHeader(
    string? Date,
    string? Version,
    long? RandomSeed,
    long? RandomCount,
    IReadOnlyList<string> EnabledDlcs);

/// <summary>Liest den Kopf eines OOS-Spielstands aus den ersten Bytes; der Rest der Datei (hunderte MB) wird nie gelesen.</summary>
public static class SaveHeaderReader
{
    // Direkte Kinder des metadata-Blocks sind mit genau einem Tab eingerückt.
    private static readonly Regex DateLine = new(@"^\tdate=(?<v>[^\s]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex VersionLine = new(@"^\tversion=""(?<v>[^""]*)""", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DlcsLine = new(@"^\tenabled_dlcs=\{(?<body>.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SeedLine = new(@"^random_seed=(?<v>-?\d+)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CountLine = new(@"^random_count=(?<v>-?\d+)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Quoted = new(@"""(?<n>[^""]*)""", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Liest höchstens <paramref name="maxBytes"/> Bytes vom Anfang des Streams. Eine durch die Grenze abgeschnittene
    /// letzte Zeile wird verworfen, damit keine halbe Zahl gelesen wird.
    /// </summary>
    public static SaveHeader Read(Stream stream, int maxBytes = 2 * 1024 * 1024)
    {
        var buffer = new byte[maxBytes];
        var count = 0;
        while (count < maxBytes)
        {
            var read = stream.Read(buffer, count, maxBytes - count);
            if (read == 0)
                break;
            count += read;
        }

        var text = Encoding.UTF8.GetString(buffer, 0, count);
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        if (count == maxBytes && lines.Count > 0)
            lines.RemoveAt(lines.Count - 1);

        string? date = null, version = null;
        long? seed = null, randomCount = null;
        List<string>? dlcs = null;
        var inString = false;
        var collectingDlcs = false;
        var dlcText = new StringBuilder();

        foreach (var line in lines)
        {
            // Zeilen im Text einer Zeichenkette (etwa der mehrzeilige flag="…") sind keine Schlüssel.
            var wasInString = inString;
            if (OddUnescapedQuotes(line))
                inString = !inString;
            if (wasInString)
                continue;

            if (collectingDlcs)
            {
                dlcText.Append(' ').Append(line);
                if (line.Contains('}'))
                {
                    dlcs = DlcNames(dlcText.ToString());
                    collectingDlcs = false;
                }
                continue;
            }

            Match match;
            if (date is null && (match = DateLine.Match(line)).Success)
                date = match.Groups["v"].Value;
            else if (version is null && (match = VersionLine.Match(line)).Success)
                version = match.Groups["v"].Value;
            else if (dlcs is null && (match = DlcsLine.Match(line)).Success)
            {
                dlcText.Append(match.Groups["body"].Value);
                if (line.Contains('}'))
                    dlcs = DlcNames(dlcText.ToString());
                else
                    collectingDlcs = true;
            }
            else if (seed is null && (match = SeedLine.Match(line)).Success)
                seed = ParseLong(match.Groups["v"].Value);
            else if (randomCount is null && (match = CountLine.Match(line)).Success)
                randomCount = ParseLong(match.Groups["v"].Value);
        }

        return new SaveHeader(date, version, seed, randomCount, dlcs ?? []);
    }

    /// <summary>Zahl aus dem Spielstand; eine Zahl jenseits von long ergibt null statt einer Ausnahme.</summary>
    internal static long? ParseLong(string text) =>
        long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>Ob die Zeile eine ungerade Zahl nicht mit Backslash maskierter Anführungszeichen enthält.</summary>
    private static bool OddUnescapedQuotes(string line)
    {
        var quotes = 0;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '\\')
                i++;
            else if (line[i] == '"')
                quotes++;
        }
        return quotes % 2 == 1;
    }

    private static List<string> DlcNames(string body)
    {
        var end = body.IndexOf('}');
        if (end >= 0)
            body = body[..end];
        return Quoted.Matches(body).Select(m => m.Groups["n"].Value).ToList();
    }
}

/// <summary>Woher ein Mod einer Maschine kommt.</summary>
public enum ModSourceKind
{
    /// <summary>Weder der Mod-Block noch ein Einhängepunkt nennt den Ordner.</summary>
    Unknown,

    /// <summary>Abonniert über den Steam-Workshop (<c>workshop/content/&lt;app&gt;/&lt;id&gt;</c>).</summary>
    Workshop,

    /// <summary>Lokaler Mod-Ordner.</summary>
    Local,
}

/// <summary>Ein Mod, den eine Maschine aktiviert hatte (aus <c>debug_&lt;m&gt;.log</c>).</summary>
/// <param name="Name">Name des Mods; die Maschinen werden nur über ihn verglichen.</param>
/// <param name="Kind">Herkunft: Workshop, lokaler Ordner oder unbekannt. Gleicher Name mit anderer Herkunft ist keine Abweichung.</param>
/// <param name="WorkshopId">Steam-Workshop-Id; nur bei <see cref="ModSourceKind.Workshop"/>.</param>
/// <param name="Folder">Ordnername; nur bei <see cref="ModSourceKind.Local"/>.</param>
/// <param name="Root">Wurzelordner des Mods mit "/" und ohne "/" am Ende (je Maschine anders); <c>null</c> ohne bekannte Herkunft.</param>
public sealed record DumpMod(string Name, ModSourceKind Kind, string? WorkshopId, string? Folder, string? Root);

/// <summary>Die Angaben einer Maschine zum OOS.</summary>
/// <param name="Role">"Host" oder "Client" aus den Tick-Zeilen des Server-Logs; <c>null</c> ohne Tick-Zeile.</param>
/// <param name="HasRandomLog">Ob <c>randomlog_&lt;m&gt;.log</c> echten Inhalt hat.</param>
/// <param name="HasCallstacks">Ob <c>callstacks_&lt;m&gt;.log</c> echten Inhalt hat.</param>
public sealed record MachineSummary(
    int MachineId,
    string? Role,
    string? PlayerName,
    string? GameCommit,
    string? EngineCommit,
    SaveHeader Save,
    IReadOnlyList<DumpMod> Mods,
    bool HasRandomLog,
    bool HasCallstacks);

/// <summary>Art einer Auffälligkeit zwischen den Maschinen.</summary>
public enum SummaryWarningKind
{
    DifferentGameCommit,
    DifferentEngineCommit,
    DifferentDlcs,
    DifferentMods,
    MissingMachine,
}

/// <summary>Eine Auffälligkeit; <paramref name="Detail"/> sind die rohen Daten (kein Anzeigetext).</summary>
/// <param name="Detail">
/// Commits: <c>1=…, 2=…</c>. DLCs und Mods: die Namen, die nicht auf allen Maschinen vorkommen. Fehlende Maschine: ihre Nummer.
/// </param>
public sealed record SummaryWarning(SummaryWarningKind Kind, string Detail);

/// <summary>Zusammenfassung eines OOS-Paares für die Übersichtskarte.</summary>
/// <param name="OosIdFull">Die 32-stellige Id aus <c>OOS Detected!</c>, deren Anfang die <paramref name="OosId16"/> ist.</param>
/// <param name="OosGameDate">Spieldatum des letzten Ticks vor <c>OOS Detected</c>.</param>
/// <param name="StartFrame">Wert von <c>StartFrame:</c> im Debug-Log derselben Maschine wie <paramref name="OosFrame"/>.</param>
/// <param name="OosFrame">Wert von <c>OOSFrame:</c> im Debug-Log.</param>
/// <param name="ChecksumCategories">Die Prüfsummen-Kategorien, die abwichen.</param>
/// <param name="SessionKind">Wert von <c>hotjoin_vs_preparation</c> aus den Metadaten (<c>preparation</c> oder <c>hotjoin</c>); <c>null</c> ohne Angabe.</param>
public sealed record OosSummary(
    string OosId16,
    string? OosIdFull,
    string? OosGameDate,
    long? StartFrame,
    long? OosFrame,
    IReadOnlyList<string> ChecksumCategories,
    IReadOnlyList<MachineSummary> Machines,
    IReadOnlyList<SummaryWarning> Warnings,
    string? SessionKind = null);

/// <summary>Baut die <see cref="OosSummary"/> aus den geöffneten Dumps eines Paares.</summary>
public static class OosSummaryBuilder
{
    /// <summary>Kopf eines DLC/Mod-Blocks (<c>dlc_reloadable.cpp:616]: DLC:</c>); die Zeilennummer der Quelle kann sich mit dem Spiel ändern.</summary>
    private static readonly Regex DlcBlockHeader = new(@"dlc_reloadable\.cpp:\d+\]: DLC:\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Ordner eines abonnierten Mods (ohne "/" am Ende).</summary>
    private static readonly Regex WorkshopFolder = new(@"/workshop/content/\d+/(?<ws>\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Wurzel eines Mod-Ordners in einer <c>Mounted Data:</c>-Zeile; danach folgt genau ein Einhängeordner (z. B. <c>in_game</c>).</summary>
    private static readonly Regex MountedMod = new(
        @"Mounted Data: (?<root>.*/(?:workshop/content/\d+/(?<ws>\d+)|mod/(?<local>[^/]+)))/[^/]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MatchedMod = new(
        @"\bMod (?<name>.+?) \([^)]*\) version .+ successfully matched",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OosFrameLine = new(@"OOSFrame: (?<n>\d+)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex StartFrameLine = new(@"StartFrame: (?<n>\d+)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Fasst das Paar zusammen. <paramref name="dumps"/> sind die geöffneten Dumps der Maschinen; jeder wird nacheinander
    /// (nie gleichzeitig) gelesen. Nicht lesbare Dateien ergeben leere Angaben, nie eine Ausnahme.
    /// </summary>
    public static OosSummary Build(OosPair pair, IReadOnlyList<OosDump> dumps)
    {
        var reads = dumps.OrderBy(d => d.MachineId).Select(d => Read(d, pair.OosId16)).ToList();

        var machines = reads.Select(r => r.Machine).ToList();
        var referenceId = pair.Reference?.MachineId;
        // Die Referenz-Maschine zuerst: ihre Logs liefern Datum, Frames und Id bevorzugt.
        var preferred = reads.Where(r => r.Machine.MachineId == referenceId)
            .Concat(reads.Where(r => r.Machine.MachineId != referenceId)).ToList();

        var checksums = preferred.Select(r => r.Dump.Metadata.OosChecksums).FirstOrDefault(c => c.Count > 0) ?? [];
        // Start- und OOS-Frame immer von derselben Maschine: jede zählt ihre Frames selbst.
        var frames = preferred.Select(r => r.Frames).FirstOrDefault(f => f.Start is not null || f.Oos is not null);

        return new OosSummary(
            pair.OosId16,
            preferred.Select(r => FullIdFor(r.ServerIds, pair.OosId16)).FirstOrDefault(id => id is not null),
            preferred.Select(r => r.OosGameDate).FirstOrDefault(d => d is not null),
            frames.Start,
            frames.Oos,
            checksums,
            machines,
            WarningsFor(pair, reads),
            // Die Referenz-Maschine zuerst, sonst die abgewichene.
            preferred.Select(r => r.Dump.Metadata.HotjoinVsPreparation).FirstOrDefault(kind => kind is not null));
    }

    /// <summary>
    /// Die aktivierten Mods einer Maschine aus den Zeilen von <c>debug_&lt;m&gt;.log</c>: die Einträge <c>name|pfad|Enabled</c> des letzten
    /// nicht leeren <c>Mod:</c>-Blocks (nach einer Kopfzeile <c>dlc_reloadable.cpp:…]: DLC:</c> bis zur Leerzeile). Gibt es keinen solchen
    /// Block, die Namen der <c>successfully matched</c>-Zeilen nach dem letzten <c>Switching to playset</c>. Beim Wechsel des Playsets hängt das
    /// Spiel jeden installierten Mod ein; die <c>Mounted Data:</c>-Zeilen dienen deshalb nur dazu, die Herkunft eines Mods ohne Pfad zu finden.
    /// </summary>
    public static IReadOnlyList<DumpMod> ModsFromDebugLog(IEnumerable<string> lines)
    {
        var scan = new DebugScan();
        foreach (var line in lines)
            scan.Add(line);
        return scan.Mods();
    }

    private sealed record MachineRead(OosDump Dump, MachineSummary Machine, bool HasSave, bool HasDebug,
        IReadOnlyList<string?> ServerIds, (long? Start, long? Oos) Frames, string? OosGameDate);

    private static MachineRead Read(OosDump dump, string oosId16)
    {
        var (role, gameDate, serverIds) = ReadServerLog(dump, oosId16);
        var scan = ReadDebugLog(dump);
        var save = ReadSaveHeader(dump);
        var metadata = dump.Metadata;

        var machine = new MachineSummary(
            dump.MachineId,
            role,
            metadata.PlayerName,
            metadata.GameCommit,
            metadata.EngineCommit,
            save ?? new SaveHeader(null, null, null, null, []),
            scan?.Mods() ?? [],
            Guarded(() => dump.HasContent(DumpFileKind.RandomLog)),
            Guarded(() => dump.HasContent(DumpFileKind.Callstacks)));

        return new MachineRead(dump, machine, save is not null, scan is not null, serverIds,
            scan?.FramesFor(serverIds, oosId16) ?? (null, null), gameDate);
    }

    /// <summary>Rolle, Spieldatum des OOS mit der Id <paramref name="oosId16"/> und die Ids aller OOS-Zeilen des Server-Logs (in Reihenfolge, null für eine Zeile ohne Id).</summary>
    private static (string? Role, string? OosGameDate, IReadOnlyList<string?> Ids) ReadServerLog(OosDump dump, string oosId16)
    {
        if (!dump.Has(DumpFileKind.DedicatedServer))
            return (null, null, []);
        try
        {
            var ticks = TickMap.Parse(LogReader.Parse(dump.ReadLines(DumpFileKind.DedicatedServer)), oosId16);
            return (ticks.Role, ticks.OosTick?.Date, ticks.OosIds);
        }
        catch (Exception ex) when (ex is OosDumpException or IOException or InvalidDataException)
        {
            return (null, null, []);
        }
    }

    private static DebugScan? ReadDebugLog(OosDump dump)
    {
        if (!dump.Has(DumpFileKind.Debug))
            return null;
        try
        {
            var scan = new DebugScan();
            using var reader = new StreamReader(dump.OpenRead(DumpFileKind.Debug), Encoding.UTF8);
            string? line;
            while ((line = reader.ReadLine()) != null)
                scan.Add(line);
            return scan;
        }
        catch (Exception ex) when (ex is OosDumpException or IOException or InvalidDataException)
        {
            return null;
        }
    }

    private static SaveHeader? ReadSaveHeader(OosDump dump)
    {
        if (!dump.Has(DumpFileKind.OosSave))
            return null;
        try
        {
            using var stream = dump.OpenRead(DumpFileKind.OosSave);
            return SaveHeaderReader.Read(stream);
        }
        catch (Exception ex) when (ex is OosDumpException or IOException or InvalidDataException)
        {
            return null;
        }
    }

    private static bool Guarded(Func<bool> check)
    {
        try
        {
            return check();
        }
        catch (Exception ex) when (ex is OosDumpException or IOException or InvalidDataException)
        {
            return false;
        }
    }

    private static string? FullIdFor(IReadOnlyList<string?> ids, string oosId16) =>
        ids.FirstOrDefault(id => id is not null && id.StartsWith(oosId16, StringComparison.Ordinal));

    private static List<SummaryWarning> WarningsFor(OosPair pair, IReadOnlyList<MachineRead> reads)
    {
        var warnings = new List<SummaryWarning>();
        var machines = reads.Select(r => r.Machine).ToList();

        AddCommitWarning(warnings, SummaryWarningKind.DifferentGameCommit, machines, m => m.GameCommit);
        AddCommitWarning(warnings, SummaryWarningKind.DifferentEngineCommit, machines, m => m.EngineCommit);

        // Nur Maschinen mit Spielstand bzw. Debug-Log vergleichen; eine fehlende Datei ist keine Abweichung.
        var dlcSets = reads.Where(r => r.HasSave)
            .Select(r => r.Machine.Save.EnabledDlcs.Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(n => n, n => n, StringComparer.OrdinalIgnoreCase)).ToList();
        var dlcDiff = NotOnAll(dlcSets);
        if (dlcDiff.Count > 0)
            warnings.Add(new SummaryWarning(SummaryWarningKind.DifferentDlcs, string.Join(", ", dlcDiff)));

        // Mods werden über den Namen verglichen: Pfad und Herkunft (Workshop oder lokaler Ordner) sind je Maschine verschieden.
        var modSets = reads.Where(r => r.HasDebug)
            .Select(r => r.Machine.Mods.GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase)).ToList();
        var modDiff = NotOnAll(modSets);
        if (modDiff.Count > 0)
            warnings.Add(new SummaryWarning(SummaryWarningKind.DifferentMods, string.Join(", ", modDiff)));

        foreach (var missing in pair.MissingMachineIds.Order())
            warnings.Add(new SummaryWarning(SummaryWarningKind.MissingMachine, missing.ToString(CultureInfo.InvariantCulture)));

        return warnings;
    }

    private static void AddCommitWarning(List<SummaryWarning> warnings, SummaryWarningKind kind,
        IReadOnlyList<MachineSummary> machines, Func<MachineSummary, string?> commitOf)
    {
        var known = machines.Where(m => commitOf(m) is not null).ToList();
        if (known.Select(commitOf).Distinct(StringComparer.Ordinal).Count() > 1)
        {
            warnings.Add(new SummaryWarning(kind,
                string.Join(", ", known.Select(m => $"{m.MachineId}={commitOf(m)}"))));
        }
    }

    /// <summary>Anzeigenamen der Einträge, die nicht in jeder Menge vorkommen (je Schlüssel einmal, in Reihenfolge des Auftretens).</summary>
    private static List<string> NotOnAll(IReadOnlyList<Dictionary<string, string>> sets)
    {
        if (sets.Count < 2)
            return [];
        return sets.SelectMany(s => s)
            .Where(kv => sets.Any(s => !s.ContainsKey(kv.Key)))
            .GroupBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First().Value)
            .ToList();
    }

    /// <summary>Sammelt Zeile für Zeile Mod-Blöcke, Mod-Namen, Einhängepunkte und Frames des Debug-Logs, damit das Log nur einmal gelesen wird.</summary>
    private sealed class DebugScan
    {
        // Einhängepunkte und Namen seit dem letzten Wechsel des Playsets.
        private readonly List<(string Root, string? WorkshopId, string Folder)> _roots = new();
        private readonly HashSet<string> _seenRoots = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _names = new();

        // Laufender DLC/Mod-Block: ob er offen ist, ob die Mod-Zeilen schon begonnen haben, Zahl seiner Mod-Zeilen und die aktivierten.
        private bool _inBlock;
        private bool _inModSection;
        private int _blockModLines;
        private List<(string Name, string Path)> _blockEnabled = new();

        // Aktivierte Mods des letzten Blocks mit mindestens einer Mod-Zeile; null ohne solchen Block.
        private List<(string Name, string Path)>? _lastBlock;

        // Je OOS schreibt das Spiel "OOSFrame:" gefolgt von "StartFrame:"; die Paare stehen in Logreihenfolge.
        private readonly List<(long? Oos, long? Start)> _frames = new();

        public void Add(string line)
        {
            if (_inBlock)
            {
                // Der Block endet an der Leerzeile; eine neue Zeitstempel-Zeile beendet ihn ebenfalls und wird normal gelesen.
                // Mod-Namen dürfen mit "[" beginnen (z. B. "[EU5] Better Borders"), daher zählt nur ein echter Zeitstempel "[hh:mm:ss]".
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    EndBlock();
                    return;
                }

                if (!OosDumpCatalog.LineTime.IsMatch(trimmed))
                {
                    AddBlockLine(trimmed);
                    return;
                }

                EndBlock();
            }

            if (line.Contains("DLC:", StringComparison.Ordinal) && DlcBlockHeader.IsMatch(line))
            {
                _inBlock = true;
                _inModSection = false;
                _blockModLines = 0;
                _blockEnabled = new List<(string Name, string Path)>();
            }
            else if (line.Contains("Switching to playset", StringComparison.Ordinal))
            {
                _names.Clear();
                _roots.Clear();
                _seenRoots.Clear();
            }
            else if (line.Contains("Mounted Data:", StringComparison.Ordinal))
            {
                var mount = MountedMod.Match(line.TrimEnd());
                if (mount.Success && _seenRoots.Add(mount.Groups["root"].Value))
                {
                    var workshop = mount.Groups["ws"].Success ? mount.Groups["ws"].Value : null;
                    var folder = workshop ?? mount.Groups["local"].Value;
                    _roots.Add((mount.Groups["root"].Value, workshop, folder));
                }
            }
            else if (line.Contains("successfully matched", StringComparison.Ordinal))
            {
                var matched = MatchedMod.Match(line);
                if (matched.Success)
                    _names.Add(matched.Groups["name"].Value);
            }
            else if (line.Contains("Frame: ", StringComparison.Ordinal))
            {
                var oos = OosFrameLine.Match(line);
                if (oos.Success)
                    _frames.Add((SaveHeaderReader.ParseLong(oos.Groups["n"].Value), null));
                var start = StartFrameLine.Match(line);
                if (start.Success)
                {
                    var value = SaveHeaderReader.ParseLong(start.Groups["n"].Value);
                    if (_frames.Count > 0 && _frames[^1].Start is null)
                        _frames[^1] = (_frames[^1].Oos, value);
                    else
                        _frames.Add((null, value));
                }
            }
        }

        /// <summary>
        /// Start- und OOS-Frame des OOS mit der Id <paramref name="oosId16"/>: das Frame-Paar mit derselben Ordnungszahl wie diese Id unter den
        /// OOS-Zeilen <paramref name="serverIds"/>. Stimmt die Anzahl der Paare nicht mit der der OOS-Zeilen überein (oder fehlt die Id),
        /// zählt das letzte Paar. Beide Werte stammen immer aus demselben Paar.
        /// </summary>
        public (long? Start, long? Oos) FramesFor(IReadOnlyList<string?> serverIds, string oosId16)
        {
            if (_frames.Count == 0)
                return (null, null);

            var ordinal = -1;
            if (serverIds.Count == _frames.Count)
                ordinal = serverIds.ToList().FindIndex(id => id is not null && id.StartsWith(oosId16, StringComparison.Ordinal));
            var (oos, start) = _frames[ordinal >= 0 ? ordinal : _frames.Count - 1];
            return (start, oos);
        }

        /// <summary>Zeile im Block: bis <c>Mod:</c> DLCs (die stehen auch im Spielstand), danach <c>name|pfad|status</c>.</summary>
        private void AddBlockLine(string line)
        {
            if (!_inModSection)
            {
                _inModSection = line == "Mod:";
                return;
            }

            var parts = line.Split('|');
            if (parts.Length < 3)
                return;

            _blockModLines++;
            if (parts[^1].Trim() == "Enabled")
                _blockEnabled.Add((string.Join('|', parts[..^2]).Trim(), parts[^2].Trim()));
        }

        private void EndBlock()
        {
            if (_blockModLines > 0)
                _lastBlock = _blockEnabled;
            _inBlock = false;
        }

        public IReadOnlyList<DumpMod> Mods()
        {
            // Ein Block, der bis zum Ende des Logs offen blieb, zählt wie ein beendeter.
            var block = _inBlock && _blockModLines > 0 ? _blockEnabled : _lastBlock;
            if (block is not null)
            {
                return block.DistinctBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(e => FromPath(e.Name, e.Path)).ToList();
            }

            return FromMatchedNames();
        }

        /// <summary>
        /// Namen ohne Pfad: zuerst ein lokaler Einhängeordner, der wie der Mod heißt; die übrigen Namen erhalten die übrigen Einhängepunkte in
        /// Reihenfolge (das Spiel hängt die Mods in der Reihenfolge der <c>successfully matched</c>-Zeilen ein). Ohne Einhängepunkt bleibt die
        /// Herkunft unbekannt.
        /// </summary>
        private List<DumpMod> FromMatchedNames()
        {
            var names = _names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var claimed = new bool[_roots.Count];
            var rootOf = new int[names.Count];
            for (var i = 0; i < names.Count; i++)
            {
                var name = names[i];
                rootOf[i] = _roots.FindIndex(r => r.WorkshopId is null && string.Equals(r.Folder, name, StringComparison.OrdinalIgnoreCase));
                if (rootOf[i] >= 0 && claimed[rootOf[i]])
                    rootOf[i] = -1;
                else if (rootOf[i] >= 0)
                    claimed[rootOf[i]] = true;
            }

            for (var i = 0; i < names.Count; i++)
            {
                if (rootOf[i] >= 0)
                    continue;
                rootOf[i] = Array.IndexOf(claimed, false);
                if (rootOf[i] >= 0)
                    claimed[rootOf[i]] = true;
            }

            return names.Select((name, i) => rootOf[i] < 0
                    ? new DumpMod(name, ModSourceKind.Unknown, null, null, null)
                    : FromRoot(name, _roots[rootOf[i]]))
                .ToList();
        }

        private static DumpMod FromRoot(string name, (string Root, string? WorkshopId, string Folder) root) =>
            root.WorkshopId is { } workshopId
                ? new DumpMod(name, ModSourceKind.Workshop, workshopId, null, root.Root)
                : new DumpMod(name, ModSourceKind.Local, null, root.Folder, root.Root);

        /// <summary>Herkunft aus dem Pfad des Mod-Blocks: unter <c>workshop/content/&lt;app&gt;/&lt;id&gt;</c> Workshop, sonst der lokale Ordner.</summary>
        private static DumpMod FromPath(string name, string path)
        {
            var root = path.Replace('\\', '/').TrimEnd('/');
            var workshop = WorkshopFolder.Match(root);
            if (workshop.Success)
                return new DumpMod(name, ModSourceKind.Workshop, workshop.Groups["ws"].Value, null, root);

            var folder = root[(root.LastIndexOf('/') + 1)..];
            return folder.Length == 0
                ? new DumpMod(name, ModSourceKind.Unknown, null, null, null)
                : new DumpMod(name, ModSourceKind.Local, null, folder, root);
        }
    }
}
