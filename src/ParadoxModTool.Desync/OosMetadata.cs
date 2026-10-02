using System.Globalization;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Desync;

/// <summary>
/// Inhalt von <c>oos_metadata_&lt;m&gt;.txt</c> eines Out-of-Sync-Dumps (EU V Multiplayer).
/// Fehlende Zeilen ergeben <c>null</c>, 0, "" bzw. leere Listen - das Parsen wirft nie.
/// </summary>
/// <param name="FolderName">Wert von <c>oos_folder_name</c>, z. B. <c>11c0de175691b2bb_0</c>.</param>
/// <param name="OosId16">Der <c>oos_folder_name</c> ohne das Suffix <c>_&lt;n&gt;</c>, also die 16-stellige OOS-Id.</param>
/// <param name="LocalMachineId">Maschinen-Id des Rechners, der den Dump geschrieben hat.</param>
/// <param name="OosMachineId">Maschinen-Id, bei der die Abweichung erkannt wurde.</param>
/// <param name="TimesDesynchronized">Wert von <c>local_num_times_desynchronized</c>.</param>
/// <param name="TimeUtc">Zeitstempeltext von <c>time_utc</c> (getrimmt, unveränderte Schreibweise).</param>
/// <param name="AllChecksums">Alle Prüfsummen-Kategorien aus <c>all_checksums</c>.</param>
/// <param name="OosChecksums">Kategorien, die abwichen (die <c>&lt;i&gt;:&lt;KATEGORIE&gt;</c>-Zeilen unter <c>oos_checksums:</c>).</param>
public sealed record OosMetadata(
    string FolderName,
    string OosId16,
    int LocalMachineId,
    int OosMachineId,
    string? PlayerName,
    string? GameCommit,
    string? EngineCommit,
    string? BuildConfiguration,
    string? HotjoinVsPreparation,
    int TimesDesynchronized,
    string? TimeUtc,
    string? Language,
    IReadOnlyList<string> AllChecksums,
    IReadOnlyList<string> OosChecksums)
{
    // Zeilen unter "oos_checksums:" haben die Form "<index>:<KATEGORIE>".
    private static readonly Regex IndexedChecksum = new(@"^\d+:(?<name>.*)$", RegexOptions.Compiled);

    // Suffix "_<n>" des oos_folder_name.
    private static readonly Regex FolderSuffix = new(@"_\d+$", RegexOptions.Compiled);

    /// <summary>Liest die <c>key:value</c>-Zeilen einer Metadaten-Datei.</summary>
    public static OosMetadata Parse(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var allChecksums = new List<string>();
        var oosChecksums = new List<string>();
        var inOosChecksums = false;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            // Nach "oos_checksums:" folgen nummerierte Kategorien; ein anderer Schlüssel beendet den Abschnitt.
            if (inOosChecksums)
            {
                var indexed = IndexedChecksum.Match(line);
                if (indexed.Success)
                {
                    AddName(oosChecksums, indexed.Groups["name"].Value);
                    continue;
                }
                inOosChecksums = false;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;
            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();

            switch (key)
            {
                case "all_checksums":
                    foreach (var part in value.Split(','))
                        AddName(allChecksums, part);
                    break;
                case "oos_checksums":
                    inOosChecksums = true;
                    foreach (var part in value.Split(','))
                        AddName(oosChecksums, part);
                    break;
                default:
                    values.TryAdd(key, value);
                    break;
            }
        }

        var folderName = values.GetValueOrDefault("oos_folder_name") ?? "";
        return new OosMetadata(
            FolderName: folderName,
            OosId16: FolderSuffix.Replace(folderName, ""),
            LocalMachineId: ParseInt(values.GetValueOrDefault("local_machine_id")),
            OosMachineId: ParseInt(values.GetValueOrDefault("oos_machine_id")),
            PlayerName: values.GetValueOrDefault("oos_player_name"),
            GameCommit: values.GetValueOrDefault("game_commit"),
            EngineCommit: values.GetValueOrDefault("engine_commit"),
            BuildConfiguration: values.GetValueOrDefault("build_configuration"),
            HotjoinVsPreparation: values.GetValueOrDefault("hotjoin_vs_preparation"),
            TimesDesynchronized: ParseInt(values.GetValueOrDefault("local_num_times_desynchronized")),
            TimeUtc: values.GetValueOrDefault("time_utc"),
            Language: values.GetValueOrDefault("language"),
            AllChecksums: allChecksums,
            OosChecksums: oosChecksums);
    }

    private static void AddName(List<string> target, string name)
    {
        name = name.Trim();
        if (name.Length > 0)
            target.Add(name);
    }

    private static int ParseInt(string? text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
}
