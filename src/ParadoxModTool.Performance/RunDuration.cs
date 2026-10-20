namespace ParadoxModTool.Performance;

/// <summary>Wie viele Monate ein Profiler-Lauf abdeckt, abgeleitet aus der einmal pro Monat gerufenen on_action-Zeile.</summary>
/// <param name="Months">Aufrufe dieser Zeile; <c>null</c>, wenn die Zeile fehlt oder kein Schlüssel angegeben wurde. Der Wert wird nicht korrigiert (ein Dump mit zwei Läufen zeigt z. B. 24).</param>
/// <param name="MonthlyPulseRow">Der übergebene Zeilenschlüssel; leer, wenn keiner angegeben wurde.</param>
/// <param name="RowFound">Ob die Zeile im Dump gefunden wurde.</param>
public sealed record RunDuration(int? Months, string MonthlyPulseRow, bool RowFound)
{
    public static RunDuration From(ProfilingDump dump, string? monthlyPulseRow)
    {
        if (monthlyPulseRow is null)
        {
            return new RunDuration(null, string.Empty, false);
        }

        var row = dump.Find(monthlyPulseRow);
        return row is null
            ? new RunDuration(null, monthlyPulseRow, false)
            : new RunDuration((int)row.Calls, monthlyPulseRow, true);
    }
}
