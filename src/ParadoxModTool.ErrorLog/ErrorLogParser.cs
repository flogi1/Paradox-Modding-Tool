using System.Text.RegularExpressions;

namespace ParadoxModTool.ErrorLog;

/// <summary>
/// Parst EU5-Error-Logs. Kein einzelner Regex genügt — die realen Logs enthalten
/// mindestens fünf unterschiedliche Zeilenmuster (siehe Architektur-Spezifikation,
/// Abschnitt 9), inklusive eines mehrzeiligen Blockformats. Dieser Parser ist daher
/// ein kleiner zustandsbehafteter Zeilen-Interpreter statt einer einzelnen
/// Regex-Ersetzung.
/// </summary>
public static class ErrorLogParser
{
    // Beispiel: [13:53:23][pdx_persistent_reader.cpp:289]: Error: "Unexpected token: text, near line: 6" in file: "input_profile/z_cmf.profile" near line: 6
    private static readonly Regex ParsingErrorPattern = new(
        @"^\[(?<time>\d{2}:\d{2}:\d{2})\]\[(?<source>[^\]]+)\]:\s*Error:\s*""(?<msg>.*?)""\s*in file:\s*""(?<file>[^""]+)""\s*near line:\s*(?<line>\d+)\s*$",
        RegexOptions.Compiled);

    // Beispiel: [12:43:18][pdx_gui_widget_desc.h:18]: gui/Pfad/datei.gui:145 - Duplicate property 'enabled'(773)
    private static readonly Regex GuiWidgetDescPattern = new(
        @"^\[(?<time>\d{2}:\d{2}:\d{2})\]\[pdx_gui_widget_desc\.h:\d+\]:\s*(?<file>.+\.gui):(?<line>\d+)\s*-\s*(?<msg>.+)$",
        RegexOptions.Compiled);

    // Beispiel: [12:43:18][pdx_gui_factory.cpp:290]: failed reading property, at gui/Pfad/datei.gui:145
    private static readonly Regex GuiFactoryPattern = new(
        @"^\[(?<time>\d{2}:\d{2}:\d{2})\]\[pdx_gui_factory\.cpp:\d+\]:\s*(?<msg>.+?),\s*at\s*(?<file>.+\.gui):(?<line>\d+)\s*$",
        RegexOptions.Compiled);

    // Kopfzeile eines mehrzeiligen Blocks: [12:45:54][jomini_script_system.cpp:256]: Script system error!
    private static readonly Regex ScriptErrorHeaderPattern = new(
        @"^\[(?<time>\d{2}:\d{2}:\d{2})\]\[jomini_script_system\.cpp:\d+\]:\s*Script system error!\s*$",
        RegexOptions.Compiled);

    //   Error: Event target link 'government_type' returned an unset scope
    private static readonly Regex ScriptErrorMessagePattern = new(
        @"^\s+Error:\s*(?<msg>.+)$",
        RegexOptions.Compiled);

    //   Script location: common/building_types/01_IV2_idea_buildings.txt:839
    private static readonly Regex ScriptErrorLocationPattern = new(
        @"^\s+Script location:\s*(?<file>.+):(?<line>\d+)\s*$",
        RegexOptions.Compiled);

    // Beispiel: [12:43:24][jomini_effect.cpp:1207]:  Possible match: 'iv2_nat_FLO_2'
    private static readonly Regex SuggestionPattern = new(
        @"^\[(?<time>\d{2}:\d{2}:\d{2})\]\[(?<source>[^\]]+)\]:\s+Possible match:\s*'(?<candidate>[^']+)'\s*$",
        RegexOptions.Compiled);

    // Fallback: irgendeine Zeile mit Zeitstempel + Quelle, aber ohne Dateibezug.
    // Beispiel: [12:43:21][message_handler.cpp:434]: Failed to find message type: WE_PERFORM_x_ACTION.
    private static readonly Regex SymbolicKeyPattern = new(
        @"^\[(?<time>\d{2}:\d{2}:\d{2})\]\[(?<source>[^\]]+)\]:\s*(?<msg>.+)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parst eine vollständige Logdatei (als Zeilenfolge) in eine Liste von Einträgen.
    /// Vorschlagszeilen werden an den jeweils vorherigen Eintrag angehängt, doppelt
    /// gemeldete GUI-Fehler (aus zwei Quellsystemen für dieselbe Datei/Zeile) werden
    /// zusammengeführt.
    /// </summary>
    public static IReadOnlyList<LogEntry> Parse(IEnumerable<string> lines)
    {
        var lineArray = lines as IList<string> ?? new List<string>(lines);
        var raw = new List<LogEntry>();

        for (var i = 0; i < lineArray.Count; i++)
        {
            var line = lineArray[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            Match m;

            if ((m = ParsingErrorPattern.Match(line)).Success)
            {
                raw.Add(new LogEntry(
                    LogEntryKind.ParsingError,
                    m.Groups["time"].Value,
                    m.Groups["source"].Value,
                    m.Groups["msg"].Value,
                    m.Groups["file"].Value,
                    int.Parse(m.Groups["line"].Value),
                    Array.Empty<string>()));
                continue;
            }

            if ((m = GuiWidgetDescPattern.Match(line)).Success)
            {
                raw.Add(new LogEntry(
                    LogEntryKind.GuiError,
                    m.Groups["time"].Value,
                    "pdx_gui_widget_desc.h",
                    m.Groups["msg"].Value,
                    m.Groups["file"].Value.Trim(),
                    int.Parse(m.Groups["line"].Value),
                    Array.Empty<string>()));
                continue;
            }

            if ((m = GuiFactoryPattern.Match(line)).Success)
            {
                raw.Add(new LogEntry(
                    LogEntryKind.GuiError,
                    m.Groups["time"].Value,
                    "pdx_gui_factory.cpp",
                    m.Groups["msg"].Value,
                    m.Groups["file"].Value.Trim(),
                    int.Parse(m.Groups["line"].Value),
                    Array.Empty<string>()));
                continue;
            }

            if (ScriptErrorHeaderPattern.Match(line) is { Success: true } header)
            {
                var time = header.Groups["time"].Value;
                var msg = string.Empty;
                string? file = null;
                int? errLine = null;

                if (i + 1 < lineArray.Count &&
                    ScriptErrorMessagePattern.Match(lineArray[i + 1]) is { Success: true } msgMatch)
                {
                    msg = msgMatch.Groups["msg"].Value;
                    i++;
                }

                if (i + 1 < lineArray.Count &&
                    ScriptErrorLocationPattern.Match(lineArray[i + 1]) is { Success: true } locMatch)
                {
                    file = locMatch.Groups["file"].Value.Trim();
                    errLine = int.Parse(locMatch.Groups["line"].Value);
                    i++;
                }

                raw.Add(new LogEntry(
                    LogEntryKind.ScriptRuntimeError,
                    time,
                    "jomini_script_system.cpp",
                    msg,
                    file,
                    errLine,
                    Array.Empty<string>()));
                continue;
            }

            if ((m = SuggestionPattern.Match(line)).Success)
            {
                raw.Add(new LogEntry(
                    LogEntryKind.SuggestionMatch,
                    m.Groups["time"].Value,
                    m.Groups["source"].Value,
                    m.Groups["candidate"].Value,
                    null,
                    null,
                    Array.Empty<string>()));
                continue;
            }

            if ((m = SymbolicKeyPattern.Match(line)).Success)
            {
                raw.Add(new LogEntry(
                    LogEntryKind.SymbolicError,
                    m.Groups["time"].Value,
                    m.Groups["source"].Value,
                    m.Groups["msg"].Value,
                    null,
                    null,
                    Array.Empty<string>()));
                continue;
            }

            raw.Add(new LogEntry(LogEntryKind.Unclassified, string.Empty, string.Empty, line, null, null, Array.Empty<string>()));
        }

        return DeduplicateGuiErrors(AttachSuggestions(raw));
    }

    /// <summary>
    /// Faltet aufeinanderfolgende "Possible match"-Zeilen in die Suggestions-Liste
    /// des jeweils vorangehenden Eintrags.
    /// </summary>
    private static List<LogEntry> AttachSuggestions(List<LogEntry> entries)
    {
        var result = new List<LogEntry>();
        LogEntry? pendingTarget = null;
        var pendingSuggestions = new List<string>();

        void Flush()
        {
            if (pendingTarget is { } target)
            {
                result.Add(pendingSuggestions.Count == 0
                    ? target
                    : target with { Suggestions = pendingSuggestions.ToArray() });
            }

            pendingTarget = null;
            pendingSuggestions = new List<string>();
        }

        foreach (var entry in entries)
        {
            if (entry.Kind == LogEntryKind.SuggestionMatch)
            {
                pendingSuggestions.Add(entry.Message);
                continue;
            }

            Flush();
            pendingTarget = entry;
        }

        Flush();
        return result;
    }

    /// <summary>
    /// Führt GUI-Fehler zusammen, die von zwei Quellsystemen (pdx_gui_widget_desc.h
    /// und pdx_gui_factory.cpp) für dieselbe Datei/Zeile gemeldet werden — im UI soll
    /// das als ein Eintrag erscheinen, nicht als zwei.
    /// </summary>
    private static List<LogEntry> DeduplicateGuiErrors(List<LogEntry> entries)
    {
        var indexByKey = new Dictionary<(string File, int Line), int>();
        var result = new List<LogEntry>();

        foreach (var entry in entries)
        {
            if (entry.Kind == LogEntryKind.GuiError && entry.FilePath is not null && entry.LineNumber is not null)
            {
                var key = (entry.FilePath, entry.LineNumber.Value);
                if (indexByKey.TryGetValue(key, out var existingIndex))
                {
                    var existing = result[existingIndex];
                    if (!existing.Message.Contains(entry.Message, StringComparison.Ordinal))
                    {
                        result[existingIndex] = existing with { Message = existing.Message + " / " + entry.Message };
                    }

                    continue;
                }

                indexByKey[key] = result.Count;
            }

            result.Add(entry);
        }

        return result;
    }
}
