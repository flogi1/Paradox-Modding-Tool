namespace ParadoxModTool.Performance;

/// <summary>Ein gelesener Script-Profiler-Dump: alle Zeilen, übersprungene Zeilen als Warnungen und Suche über den vollen Schlüssel.</summary>
public sealed class ProfilingDump
{
    private readonly Dictionary<string, ProfilingRow> _byKey = new(StringComparer.Ordinal);

    public ProfilingDump(IReadOnlyList<ProfilingRow> rows, IReadOnlyList<string> warnings)
    {
        Rows = rows;
        Warnings = warnings;
        foreach (var row in rows)
        {
            _byKey.TryAdd(row.Key, row);
        }
    }

    public IReadOnlyList<ProfilingRow> Rows { get; }

    /// <summary>Übersprungene Zeilen als <c>line 123: &lt;Grund&gt;</c>.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Die Zeile mit genau diesem Schlüssel (bei doppelten Schlüsseln die erste), sonst <c>null</c>.</summary>
    public ProfilingRow? Find(string key) => _byKey.GetValueOrDefault(key);
}
