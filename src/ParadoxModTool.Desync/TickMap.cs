using System.Globalization;
using System.Text.RegularExpressions;
using ParadoxModTool.ErrorLog;

namespace ParadoxModTool.Desync;

/// <summary>
/// Ein Tick aus dem <c>dedicated_server</c>-Log. <c>WallSeconds</c> ist die Uhrzeit des Tages in Sekunden plus 86 400 je
/// Mitternachtsüberlauf in dieser Datei (siehe <see cref="WallClock"/>). Beispiel: <c>Date</c> <c>1425.2.4.8</c>, <c>Day</c> <c>1425.2.4</c>.
/// </summary>
public sealed record GameTick(long WallSeconds, string Date, string Day);

/// <summary>
/// Absolute Wanduhr-Zeit der Zeilen eines Logs. Alle Logs einer Maschine teilen dadurch einen Ursprung: den Tag, an dem die Datei beginnt.
/// </summary>
public static class WallClock
{
    private const long DaySeconds = 86_400;

    /// <summary>
    /// Sekunden des Tages je Zeile; liegt eine Zeit mehr als 12 h vor der vorigen, kommen ab dort weitere 86 400 s dazu (Mitternacht).
    /// Die Zeit der ersten Zeile wird nie abgezogen. Zeilen ohne Zeit erben den Wert der vorigen.
    /// </summary>
    public static IReadOnlyList<long> Absolute(IReadOnlyList<LogLine> lines)
    {
        var result = new long[lines.Count];
        long offset = 0;
        long? previous = null;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!TryParseTime(lines[i].Time, out var seconds))
            {
                result[i] = i == 0 ? 0 : result[i - 1];
                continue;
            }

            if (previous is { } last && seconds < last - DaySeconds / 2)
                offset += DaySeconds;
            previous = seconds;
            result[i] = offset + seconds;
        }

        return result;
    }

    private static bool TryParseTime(string time, out long seconds)
    {
        seconds = 0;
        if (time.Length != 8 || time[2] != ':' || time[5] != ':')
            return false;
        if (!int.TryParse(time.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var h)
            || !int.TryParse(time.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var m)
            || !int.TryParse(time.AsSpan(6, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var s))
            return false;
        seconds = h * 3600L + m * 60L + s;
        return true;
    }
}

/// <summary>
/// Zuordnung Wanduhr-Zeit zu Spieldatum für eine Maschine, gelesen aus den <c>Host_n Tick: …</c>- bzw. <c>Client_n Tick: …</c>-Zeilen
/// des <c>dedicated_server</c>-Logs.
/// </summary>
public sealed class TickMap
{
    private static readonly Regex TickPattern = new(
        @"^(?<role>Host|Client)_(?<m>\d+) Tick: (?<date>\d+\.\d+\.\d+(?:\.\d+)?)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const string OosMarker = "OOS Detected!";

    private readonly List<GameTick> _ticks = new();
    private readonly List<string?> _oosIds = new();
    private int _oosIndex = -1;

    private TickMap()
    {
    }

    /// <summary>"Host" oder "Client"; null, wenn das Log keine Tick-Zeile enthält.</summary>
    public string? Role { get; private set; }

    public int? MachineId { get; private set; }

    public IReadOnlyList<GameTick> Ticks => _ticks;

    /// <summary>
    /// Der letzte Tick vor der <c>OOS Detected</c>-Zeile des gewünschten OOS (siehe <see cref="Parse"/>); null, wenn die Zeile
    /// oder ein Tick davor fehlt.
    /// </summary>
    public GameTick? OosTick => _oosIndex >= 0 ? _ticks[_oosIndex] : null;

    /// <summary>
    /// Die vollen OOS-Ids aller <c>OOS Detected!</c>-Zeilen in Logreihenfolge (null bei einer Zeile ohne Id). Ein Log kann
    /// mehrere OOS enthalten, etwa wenn dieselbe Sitzung mehrfach abwich.
    /// </summary>
    public IReadOnlyList<string?> OosIds => _oosIds;

    /// <summary>
    /// Liest die Tick-Zeilen. Ein Log kann mehrere <c>OOS Detected!</c>-Zeilen enthalten: <see cref="OosTick"/> ist der letzte Tick vor
    /// der Zeile, deren Id mit <paramref name="oosId16"/> beginnt. Ohne <paramref name="oosId16"/> oder ohne passende Zeile ist es der
    /// letzte Tick vor der letzten OOS-Zeile (nie vor der ersten).
    /// </summary>
    /// <param name="oosId16">Die 16-stellige OOS-Id des gewünschten OOS (Anfang der vollen Id); null für das letzte OOS.</param>
    public static TickMap Parse(IReadOnlyList<LogLine> serverLines, string? oosId16 = null)
    {
        var map = new TickMap();
        var wall = WallClock.Absolute(serverLines);
        var lastOosIndex = -1;
        var matched = false;
        for (var i = 0; i < serverLines.Count; i++)
        {
            var message = serverLines[i].Message;
            if (message.StartsWith(OosMarker, StringComparison.Ordinal))
            {
                var id = OosDumpCatalog.OosDetected.Match(message) is { Success: true } idMatch ? idMatch.Groups["id"].Value : null;
                map._oosIds.Add(id);
                var tickIndex = map._ticks.Count - 1;
                lastOosIndex = tickIndex;
                if (!matched && oosId16 is not null && id is not null && id.StartsWith(oosId16, StringComparison.Ordinal))
                {
                    matched = true;
                    map._oosIndex = tickIndex;
                }
                continue;
            }

            // Folgezeilen gehören nicht zur Tick-Meldung.
            var firstLine = message;
            var newline = message.IndexOf('\n');
            if (newline >= 0)
                firstLine = message[..newline];

            var match = TickPattern.Match(firstLine);
            if (!match.Success)
                continue;

            var date = match.Groups["date"].Value;
            map.Role ??= match.Groups["role"].Value;
            // Eine Maschinennummer jenseits von int (kaputtes Log) lässt die Maschine offen, der Tick zählt trotzdem.
            if (map.MachineId is null && int.TryParse(match.Groups["m"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var machine))
                map.MachineId = machine;
            map._ticks.Add(new GameTick(wall[i], date, DayOf(date)));
        }

        if (!matched)
            map._oosIndex = lastOosIndex;
        return map;
    }

    /// <summary>Spieldatum des letzten Ticks, der zur Wanduhr-Zeit oder früher liegt; null davor.</summary>
    public string? DateAt(long wallSeconds)
    {
        int low = 0, high = _ticks.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (_ticks[mid].WallSeconds <= wallSeconds)
                low = mid + 1;
            else
                high = mid;
        }

        return low == 0 ? null : _ticks[low - 1].Date;
    }

    /// <summary>
    /// Wanduhr-Zeit, ab der das Fenster beginnt: vom OOS-Tick aus werden die Ticks rückwärts gelesen und verschiedene Spieltage gezählt,
    /// bis <c>gameDays + 1</c> Tage gesehen sind. Das Fenster beginnt beim ersten Tick des zuletzt erreichten Tages (ohne Kalenderrechnung).
    /// Null ohne OOS-Tick.
    /// </summary>
    public long? WindowStart(int gameDays)
    {
        if (_oosIndex < 0)
            return null;

        var wanted = Math.Max(gameDays, 0) + 1;
        var seen = 1;
        var start = _oosIndex;
        for (var i = _oosIndex - 1; i >= 0; i--)
        {
            if (_ticks[i].Day != _ticks[start].Day)
            {
                if (seen >= wanted)
                    break;
                seen++;
            }

            start = i;
        }

        return _ticks[start].WallSeconds;
    }

    private static string DayOf(string date)
    {
        var parts = date.Split('.');
        return parts.Length <= 3 ? date : string.Join('.', parts, 0, 3);
    }
}
