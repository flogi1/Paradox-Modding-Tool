namespace ParadoxModTool.Performance;

/// <summary>Eine Zeile eines Script-Profiler-Dumps. <paramref name="Key"/> ist der volle <c>File Location</c>-Text, Zeiten bleiben im Rohwert des Dumps.</summary>
/// <param name="Key">Voller <c>File Location</c>-Text inklusive Art in Klammern, z. B. <c>on_actions (on_actions) @ common/on_action/location_pulses.txt:38</c>.</param>
/// <param name="Kind">Text vor <c> @ </c>, z. B. <c>on_actions (effect)</c>.</param>
/// <param name="Path">Pfad mit <c>/</c>, ohne führenden Schrägstrich.</param>
/// <param name="Line">Zeilennummer aus dem abschließenden <c>:&lt;n&gt;</c>.</param>
/// <param name="Calls">Anzahl der Aufrufe.</param>
/// <param name="Self">Eigenzeit (Rohwert).</param>
/// <param name="Total">Gesamtzeit (Rohwert).</param>
public sealed record ProfilingRow(string Key, string Kind, string Path, int Line, long Calls, double Self, double Total)
{
    /// <summary>Gesamtzeit pro Aufruf; 0 bei null Aufrufen.</summary>
    public double AverageTotal => Calls == 0 ? 0 : Total / Calls;
}
