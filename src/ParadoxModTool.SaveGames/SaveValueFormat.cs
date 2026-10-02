using System.Globalization;

namespace ParadoxModTool.SaveGames;

/// <summary>
/// Lesbare Werte nach den Werttypen des Spielprofils (<c>fixedPoint</c>, <c>tickDate</c>, <c>objectName</c>, <c>yesNo</c>, <c>flagName</c>,
/// <c>raw</c>). Typen ohne Profil-Eintrag und nicht lesbare Rohwerte erscheinen unverändert.
/// </summary>
public sealed class SaveValueFormat
{
    private static readonly int[] MonthLengths = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

    private readonly SaveSnapshot _snapshot;

    public SaveValueFormat(SaveSnapshot snapshot) => _snapshot = snapshot;

    public string Format(SaveVariableEntry entry)
    {
        var raw = entry.RawValue;
        if (raw.Length == 0 || !_snapshot.Settings.ValueTypes.TryGetValue(entry.Type, out var type))
        {
            return raw;
        }

        switch (type.Format)
        {
            case "fixedPoint" when long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && type.Factor > 0:
                return FixedPoint(number, type.Factor);
            case "tickDate" when long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks):
                return TickDate(ticks, type.TicksPerDay, type.EpochYear);
            case "objectName" when type.ObjectPath is not null:
                return _snapshot.Objects.NameOf(type.ObjectPath, raw) ?? raw;
            case "yesNo":
                return raw switch
                {
                    "1" or "yes" => SaveExplorerTexts.Yes,
                    "0" or "no" => SaveExplorerTexts.No,
                    _ => raw,
                };
            default:
                return raw;
        }
    }

    /// <summary>Festkomma: Rohwert geteilt durch <paramref name="factor"/>, Punkt als Dezimaltrenner, ohne überflüssige Nullen.</summary>
    public static string FixedPoint(long raw, long factor) =>
        ((decimal)raw / factor).ToString("0.############################", CultureInfo.InvariantCulture);

    /// <summary>
    /// Datum aus Zeiteinheiten seit <paramref name="epochYear"/>.1.1 (365 Tage je Jahr, keine Schaltjahre). Bei 24 Einheiten je Tag (Stunden)
    /// hängt eine Stunde ungleich 0 als <c>.Stunde</c> an. Negative Werte oder eine ungültige Einheit ergeben die Zahl selbst.
    /// </summary>
    public static string TickDate(long ticks, int ticksPerDay, int epochYear)
    {
        if (ticks < 0 || ticksPerDay < 1)
        {
            return ticks.ToString(CultureInfo.InvariantCulture);
        }

        var days = ticks / ticksPerDay;
        var year = days / 365 + epochYear;
        var dayOfYear = (int)(days % 365);
        var month = 0;
        while (dayOfYear >= MonthLengths[month])
        {
            dayOfYear -= MonthLengths[month];
            month++;
        }

        var date = string.Create(CultureInfo.InvariantCulture, $"{year}.{month + 1}.{dayOfYear + 1}");
        var hour = ticksPerDay == 24 ? ticks % 24 : 0;
        return hour == 0 ? date : string.Create(CultureInfo.InvariantCulture, $"{date}.{hour}");
    }
}
